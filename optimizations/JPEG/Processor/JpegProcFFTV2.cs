using JPEG.Algorithmes;
using JPEG.Images;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System;
using System.Linq;
using System.Numerics;
using JPEG.Utilities;
using PixelFormat = JPEG.Images.PixelFormat;
using System.Runtime.InteropServices;

namespace JPEG.Processor;

public class JpegProcFFTV2 : IJpegProcessor
{
    public static readonly JpegProcFFTV2 Init = new();
    public const int PercentToSave = 99;
    public const int Quality = 30;
    public const int FrameSize = 512;

    public Complex[,] maps = new Complex[3, 0];

    private FFT2DNew proc = new FFT2DNew(FrameSize);
    private double normolizeCoef = 0;
    private const int DCTSize = 2;

    public void Compress(string imagePath,
        string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        //var ff = FFT.Padding(bmp);
        var imageMatrix = (Matrix)bmp;
        

        //Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
        var compressionResult = Compress(imageMatrix, Quality);
        compressionResult.Save(compressedImagePath);
    }

    public void Uncompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);
        var uncompressedImage = Uncompress(compressedImage);
        var resultBmp = (Bitmap)uncompressedImage;
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private CompressedImage Compress(Matrix matrix, int quality = 50)
    {
        var width = matrix.Width;
        var height = matrix.Height;

        var allQuantizedBytes = new List<byte>();
        var selectorFuncs = new Func<Pixel, double>[] { p => p.Y, p => p.Cb, p => p.Cr };
        var selectorRGBFuncs = new Func<Pixel, double>[] { p => p.R, p => p.G, p => p.B };
        var subMatrix = new Matrix(DCTSize, DCTSize);
        var channelFreqs = new double[DCTSize, DCTSize];
        var quantizationMatrix = GetQuantizationMatrix(Quality);
        var quantizedFreqs = new byte[DCTSize, DCTSize];
        var quantizedBytes = new byte[DCTSize * DCTSize];
        var flag = false;
                    Console.WriteLine("Before : ");
        foreach (var selector in selectorRGBFuncs)
        {
            for (int y = 0; y < height; y += DCTSize)
            {
                for (int x = 0; x < width; x += DCTSize)
                {
                    subMatrix = GetSubMatrix(matrix, y, DCTSize, x, DCTSize, subMatrix);
                    var complexes = proc.Forward(subMatrix, selector);
                    //complexes = Threshold(complexes, PercentToSave);
                    var extracted = ExtractRealPart(complexes);

                    
                        if (!flag)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                for (int j = 0; j < 2; j++)
                                {
                                    Console.Write((int)extracted[i, j] + " ");
                                }
                            }

                            flag = true;
                        }


                    //JpegProcBuffAndParallel.ShiftMatrixValues(subMatrix, -128);
                    quantizedFreqs = Quantize(extracted, quantizationMatrix, quantizedFreqs);

                    quantizedBytes = (byte[])ZigZagScan(quantizedFreqs, quantizedBytes);
                    allQuantizedBytes.AddRange(quantizedBytes);


                }
            }
            //var realPart = GetBytesForPart(complexes, new Func<Complex, double>(x => x.Real));
            //var imaginaryPart = GetBytesForPart(complexes, new Func<Complex, double>(x => x.Imaginary));

            //allQuantizedBytes.AddRange(realPart);
            //allQuantizedBytes.AddRange(imaginaryPart);
        }

                    Console.WriteLine("\n");
        
        long bitsCount;
        Dictionary<BitsWithLength, byte> decodeTable;
        var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out decodeTable, out bitsCount);

        return new CompressedImage
        {
            Quality = quality,
            CompressedBytes = compressedBytes,
            BitsCount = bitsCount,
            DecodeTable = decodeTable,
            Height = matrix.Height,
            Width = matrix.Width
        };
    }
    public static byte[,] Quantize(double[,] channelFreqs, int[,] quantizationMatrix, byte[,] quantizedFreqs)
    {
        for (int y = 0; y < channelFreqs.GetLength(0); y++)
        {
            for (int x = 0; x < channelFreqs.GetLength(1); x++)
            {
                var coef = channelFreqs[y, x] / quantizationMatrix[y, x];
                if (coef < -128 || coef > 128) throw new ArgumentException();
                quantizedFreqs[y, x] = (byte)coef;
            }
        }

        return quantizedFreqs;
    }
    private double[,] ExtractRealPart(Complex[][] complexes)
    {
        var extracted= new double[complexes.GetLength(0), complexes.GetLength(0)];

        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes.GetLength(0); x++)
            {
                extracted[y,x] = complexes[y][x].Real;
            }
        }

        return extracted;
    } 


    private byte[] GetBytesForPart(Complex[][] complexes, Func<Complex, double> selector)
    {
        var vectors = new List<byte>(complexes.Length);

        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes.GetLength(0); x++)
            {
                var bt = (byte)(selector(complexes[y][x]) / normolizeCoef);
                vectors.Add(bt);
                //var floatNum = (float)selector(complexes[y][x]);
                //var f = BitConverter.GetBytes(floatNum);

                //for (int i = 0; i < f.Length; i++)
                //{
                //    vectors.Add(f[i]);
                //}
            }
        }

        vectors = vectors.OrderByDescending(x => x).ToList();
        return vectors.ToArray();
    }
    private Complex[][] Threshold(Complex[][] complexes, int percent)
    {
        if (percent < 1 || percent > 100) throw new ArgumentException("Invalid percent value");
        var allValues = new List<Complex>(complexes.Length);

        foreach (var complexLine in complexes)
        {
            foreach (var complex in complexLine)
            {
                allValues.Add(complex);
            }
        }

        var sortedValues = allValues.OrderByDescending(x => x.Real).ToArray();
        var threshIndex = (int)((double)percent / 100 * sortedValues.Length);
        var threshValue = sortedValues[threshIndex].Real;
        
        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes[0].GetLength(0); x++)
            {
                if (complexes[y][x].Real <= threshValue)
                {
                    complexes[y][x] = Complex.Zero;
                }
            }
        }

        return complexes;
    }
    private sbyte[] BytesThreshold(sbyte[] coefs, int percent)
    {
        if (percent < 1 || percent > 100) throw new ArgumentException("Invalid percent value");
        
        var sortedValues = coefs.OrderByDescending(x => x).ToArray();
        var threshIndex = (int)((double)percent / 100 * sortedValues.Length);
        var threshValue = sortedValues[threshIndex];

        for (int y = 0; y < coefs.Length; y++)
        {
            if (coefs[y] <= threshValue)
            {
                coefs[y] = 0;
            }
        }

        return coefs;
    }


    private Matrix Uncompress(CompressedImage image)
    {
        var result = new Matrix(image.Height, image.Width);
        var resultArray = new double[image.Height, image.Width];
        using (var allQuantizedBytes =
               new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
        {
            var width = result.Width;
            var height = result.Height;

            var r = new double[width, height];
            var g = new double[width, height];
            var b = new double[width, height];
            
            //var quantizedBytes = new byte[4 * FrameSize * FrameSize];

            var quantizationMatrix = GetQuantizationMatrix(image.Quality);
            var quantizedBytes = new byte[DCTSize * DCTSize];
            var quantizedFreqs = new byte[DCTSize, DCTSize];
            var channelFreqs = new double[quantizedFreqs.GetLength(0), quantizedFreqs.GetLength(1)];
            var flag = false;
                        Console.WriteLine("Before : ");
            foreach (var channel in new[] { r, g, b })
            {
                for (int y = 0; y < height; y += DCTSize)
                {
                    for (int x = 0; x < width; x += DCTSize)
                    {
                        allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();

                        quantizedFreqs = ZigZagUnScan(quantizedBytes, quantizedFreqs);
                        channelFreqs = DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);

                        if (!flag)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                for (int j = 0; j < 2; j++)
                                {
                                    Console.Write((int)channelFreqs[i, j] + " ");
                                    
                                }
                            }

                            flag = true;
                        }
                        
                        
                        var matrix = ExtractComplexes(channelFreqs);
                        var map = proc.Inverse(matrix);

                        SetArray(channel, map, y, x);
                    }
                }
                
                //var coefs = SetBytesForRealPart(quantizedBytes);

                //allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
                //coefs = SetBytesForImaginaryPart(coefs, quantizedBytes);

                //var map = proc.Inverse(coefs);
                //var layer = InsertValues(channel, map);
            }
            
                        Console.WriteLine("\n");
            SetPixels(result, r, g, b, PixelFormat.RGB, 0, 0);
        }
        //ToPicture(resultArray, "temp.bmp");
        return result;
    }
    public static Matrix GetSubMatrix(Matrix matrix, int yOffset, int yLength, int xOffset, int xLength, Matrix subMatrix)
    {
        for (var j = 0; j < yLength; j++)
        for (var i = 0; i < xLength; i++)
            subMatrix.Pixels[j, i] = matrix.Pixels[yOffset + j, xOffset + i];
        return subMatrix;
    }

    private Complex[][] ExtractComplexes(double[,] matrix)
    {
        var extracted = new Complex[matrix.GetLength(1)][];

        for (int y = 0; y < matrix.GetLength(0); y++)
        {
            var line = new Complex[matrix.GetLength(0)];
            extracted[y] = line;

            for (int x = 0; x < matrix.GetLength(0); x++)
            {
                extracted[y][x] = new Complex(matrix[y,x], 0);
            }
        }

        return extracted;
    }
    private double[,] InsertValues(double[,] receiving, double[,] values)
    {
        for (int y = 0; y < receiving.GetLength(1); y++)
        {
            for (int x = 0; x < receiving.GetLength(0); x++)
            {
                receiving[y, x] = values[x, y];
            }
        }

        return receiving;
    }
    private Complex[][] SetBytesForRealPart(byte[] coeffs)
    {
        var indexer = 0;
        var complexes = new Complex[FrameSize][];
        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            var line = new Complex[FrameSize];
            complexes[y] = line;

            for (int x = 0; x < complexes.GetLength(0); x++)
            {
                var value = coeffs[indexer] * normolizeCoef;
                complexes[y][x] += value;
                indexer++;
                //var myFloat = BitConverter.ToSingle(coeffs, indexer);
                //complexes[y][x] += myFloat;
                //indexer += 4;
            }
        }

        return complexes;
    }
    private Complex[][] SetBytesForImaginaryPart(Complex[][] complexes, byte[] coeffs)
    {
        var indexer = 0;

        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes.GetLength(0); x++)
            {
                var value = coeffs[indexer] * normolizeCoef;
                var newComp = new Complex(0, value);
                complexes[y][x] += newComp;
                indexer++;
                //var myFloat = System.BitConverter.ToSingle(coeffs, indexer);
                //var newComp = new Complex(0, myFloat);
                //complexes[y][x] += newComp;
                //indexer += 4;
            }
        }

        return complexes;
    }

    private static void SetArray(double[,] matrix, double[,] a, int yOffset, int xOffset)
    {
        var height = a.GetLength(0);
        var width = a.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            matrix[yOffset + y, xOffset + x] = a[y, x];
    }
    private static void SetPixels(Matrix matrix, double[,] a, double[,] b, double[,] c, PixelFormat format,
        int yOffset, int xOffset)
    {
        var height = a.GetLength(0);
        var width = a.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            matrix.Pixels[yOffset + y, xOffset + x] = new Pixel(a[y, x], b[y, x], c[y, x], format);
    }
    public static double[,] DeQuantize(byte[,] quantizedBytes, int[,] quantizationMatrix, double[,] channelFreqs)
    {
        for (int y = 0; y < quantizedBytes.GetLength(0); y++)
        {
            for (int x = 0; x < quantizedBytes.GetLength(1); x++)
            {
                channelFreqs[y, x] =
                    ((sbyte)quantizedBytes[y, x]) *
                    quantizationMatrix[y, x]; //NOTE cast to sbyte not to loose negative numbers
            }
        }

        return channelFreqs;
    }
    public static int[,] GetQuantizationMatrix(int quality)
    {
        if (quality < 1 || quality > 99)
            throw new ArgumentException("quality must be in [1,99] interval");
        
        var result = new[,]
        {
            { 8, 4 },
            { 4, 4 }
        };

        //var result = new[,]
        //{
        //    { 32, 22, 20, 32 },
        //    { 28, 32, 80, 138 },
        //    { 36, 74, 156, 206 },
        //    { 78, 156, 206, 240 }
        //};

        //var result = new[,]
        //{
        //    { 8, 16, 20, 24 },
        //    { 16, 16, 20, 24 },
        //    { 20, 20, 24, 24 },
        //    { 24, 24, 24, 32 },

        //};

        //var result = new[,]
        //{
        //    { 16, 11, 10, 16 },
        //    { 12, 12, 14, 19 },
        //    { 14, 13, 16, 24 },
        //    { 14, 17, 22, 29 },
        //};

        //var result = new[,]
        //{
        //    { 16, 11, 10, 16, 24, 40, 51, 61 },
        //    { 12, 12, 14, 19, 26, 58, 60, 55 },
        //    { 14, 13, 16, 24, 40, 57, 69, 56 },
        //    { 14, 17, 22, 29, 51, 87, 80, 62 },
        //    { 18, 22, 37, 56, 68, 109, 103, 77 },
        //    { 24, 35, 55, 64, 81, 104, 113, 92 },
        //    { 49, 64, 78, 87, 103, 121, 120, 101 },
        //    { 72, 92, 95, 98, 112, 100, 103, 99 }
        //};

        for (int y = 0; y < result.GetLength(0); y++)
        {
            for (int x = 0; x < result.GetLength(1); x++)
            {
                //result[y, x] = (multiplier * result[y, x] + 50) / 100;
            }
        }

        return result;
    }

    public static IEnumerable<byte> ZigZagScan(byte[,] channelFreqs, byte[] quantizedFreqs)
    {
        quantizedFreqs[0] = channelFreqs[0, 0];
        quantizedFreqs[1] = channelFreqs[0, 1];
        quantizedFreqs[2] = channelFreqs[1, 0];
        quantizedFreqs[3] = channelFreqs[1, 1];

        //quantizedFreqs[0] = channelFreqs[0, 0];
        //quantizedFreqs[1] = channelFreqs[0, 1];
        //quantizedFreqs[2] = channelFreqs[1, 0];
        //quantizedFreqs[3] = channelFreqs[2, 0];
        //quantizedFreqs[4] = channelFreqs[1, 1];
        //quantizedFreqs[5] = channelFreqs[0, 2];
        //quantizedFreqs[6] = channelFreqs[0, 3];
        //quantizedFreqs[7] = channelFreqs[1, 2];
        //quantizedFreqs[8] = channelFreqs[2, 1];
        //quantizedFreqs[9] = channelFreqs[3, 0];
        //quantizedFreqs[10] = channelFreqs[3, 1];
        //quantizedFreqs[12] = channelFreqs[2, 2];
        //quantizedFreqs[13] = channelFreqs[1, 3];
        //quantizedFreqs[14] = channelFreqs[2, 3];
        //quantizedFreqs[15] = channelFreqs[3, 2];
        //quantizedFreqs[11] = channelFreqs[3, 3];

        return quantizedFreqs;
    }

    public static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes, byte[,] outputFreq)
    {
        outputFreq[0, 0] = quantizedBytes[0];
        outputFreq[0, 1] = quantizedBytes[1];
        outputFreq[1, 0] = quantizedBytes[2];
        outputFreq[1, 1] = quantizedBytes[3];

        //outputFreq[0, 0] = quantizedBytes[0];
        //outputFreq[0, 1] = quantizedBytes[1];
        //outputFreq[0, 2] = quantizedBytes[5];
        //outputFreq[0, 3] = quantizedBytes[6];

        //outputFreq[1, 0] = quantizedBytes[2];
        //outputFreq[1, 1] = quantizedBytes[4];
        //outputFreq[1, 2] = quantizedBytes[7];
        //outputFreq[1, 3] = quantizedBytes[12];

        //outputFreq[2, 0] = quantizedBytes[3];
        //outputFreq[2, 1] = quantizedBytes[8];
        //outputFreq[2, 2] = quantizedBytes[11];
        //outputFreq[2, 3] = quantizedBytes[13];

        //outputFreq[3, 0] = quantizedBytes[9];
        //outputFreq[3, 1] = quantizedBytes[10];
        //outputFreq[3, 2] = quantizedBytes[14];
        //outputFreq[3, 3] = quantizedBytes[15];

        return outputFreq;
    }

}
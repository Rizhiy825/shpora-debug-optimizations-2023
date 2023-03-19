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

public class JpegProcFFTCooleyTukey : IJpegProcessor
{
    public static readonly JpegProcFFTCooleyTukey Init = new();
    public const int PercentToSave = 5;
    public const int FrameSize = 512;

    public Complex[,] maps = new Complex[3, 0];

    private FFT2DNew proc = new FFT2DNew(FrameSize);
    private double normolizeCoef = 0;

    public void Compress(string imagePath,
        string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        //var ff = FFT.Padding(bmp);
        var imageMatrix = (Matrix)bmp;
        

        //Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
        var compressionResult = Compress(imageMatrix, PercentToSave);
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
        

        foreach (var selector in selectorRGBFuncs)
        {
            var complexes = proc.Forward(matrix, selector);

            complexes = Threshold(complexes, PercentToSave);

            var realPart = GetBytesForPart(complexes, new Func<Complex, double>(x => x.Real));
            var imaginaryPart = GetBytesForPart(complexes, new Func<Complex, double>(x => x.Imaginary));

            allQuantizedBytes.AddRange(realPart);
            allQuantizedBytes.AddRange(imaginaryPart);
        }

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
        if (percent < 1 || percent > 99) throw new ArgumentException("Invalid percent value");
        var allValues = new List<Complex>(complexes.Length);

        foreach (var complexLine in complexes)
        {
            foreach (var complex in complexLine)
            {
                allValues.Add(complex);
            }
        }

        var sortedValues = allValues.OrderByDescending(x => x.Magnitude).ToArray();
        var threshIndex = (int)((double)percent / 100 * sortedValues.Length);
        var maxValue = sortedValues[0];
        var threshValue = sortedValues[threshIndex];
        var threshMagnitude = sortedValues[threshIndex].Magnitude;

        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes[0].GetLength(0); x++)
            {
                if (complexes[y][x].Magnitude < threshMagnitude)
                {
                    complexes[y][x] = Complex.Zero;
                }
            }
        }

        sortedValues = sortedValues.Take(threshIndex).ToArray();

        sortedValues = sortedValues.OrderByDescending(x => x.Real).ToArray();
        var rMax = sortedValues.First().Real;
        var rMin = sortedValues.Last().Real;

        sortedValues = sortedValues.OrderByDescending(x => x.Imaginary).ToArray();
        var iMax = sortedValues.First().Imaginary;
        var iMin = sortedValues.Last().Imaginary;

        var maxVal = Math.Max(rMax, iMax);
        maxVal = Math.Max(maxVal, Math.Abs(rMin));
        maxVal = Math.Max(maxVal, Math.Abs(iMin));

        normolizeCoef = maxVal / 128;

        return complexes;
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
            
            var quantizedBytes = new byte[4 * FrameSize * FrameSize];
            
            foreach (var channel in new[] { r, g, b })
            {
                allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();

                var coefs = SetBytesForRealPart(quantizedBytes);

                allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
                coefs = SetBytesForImaginaryPart(coefs, quantizedBytes);

                var map = proc.Inverse(coefs);
                var layer = InsertValues(channel, map);
            }
            
            SetPixels(result, r, g, b, PixelFormat.RGB, 0, 0);
        }
        //ToPicture(resultArray, "temp.bmp");
        return result;
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

    public static double[,] GetSubMatrix(double[,] matrix, int yOffset, int yLength, int xOffset, int xLength)
    {
        var result = new double[yLength, xLength];
        for (var j = 0; j < yLength; j++)
        for (var i = 0; i < xLength; i++)
            result[j, i] = matrix[yOffset + j, xOffset + i];
        return result;
    }

    private static byte[,] Quantize(double[,] channelFreqs)
    {
        var result = new byte[channelFreqs.GetLength(0), channelFreqs.GetLength(1)];
        
        for (int y = 0; y < channelFreqs.GetLength(0); y++)
        {
            for (int x = 0; x < channelFreqs.GetLength(1); x++)
            {
                result[y, x] = (byte)channelFreqs[y, x];
            }
        }

        return result;
    }

    private static double[,] DeQuantize(byte[,] quantizedBytes)
    {
        var result = new double[quantizedBytes.GetLength(0), quantizedBytes.GetLength(1)];

        for (int y = 0; y < quantizedBytes.GetLength(0); y++)
        {
            for (int x = 0; x < quantizedBytes.GetLength(1); x++)
            {
                result[y, x] =
                    (byte)quantizedBytes[y, x]; //NOTE cast to sbyte not to loose negative numbers
            }
        }

        return result;
    }
}
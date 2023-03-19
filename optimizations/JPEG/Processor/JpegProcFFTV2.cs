using JPEG.Algorithmes;
using JPEG.Images;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System;
using System.Numerics;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcFFTV2 : IJpegProcessor
{
    public static readonly JpegProcFFTV2 Init = new();
    public const int Quality = 30;
    
    private FFT2DNew proc = new ();
    private double normolizeCoef = 0;
    private const int DCTSize = 2;

    public void Compress(string imagePath,
        string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
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
        var selectorRGBFuncs = new Func<Pixel, double>[] { p => p.R, p => p.G, p => p.B };

        var subMatrix = new Matrix(DCTSize, DCTSize);
        var quantizationMatrix = GetQuantizationMatrix(Quality);
        var quantizedFreqs = new byte[DCTSize, DCTSize];
        var quantizedBytes = new byte[DCTSize * DCTSize];

        foreach (var selector in selectorRGBFuncs)
        {
            for (int y = 0; y < height; y += DCTSize)
            {
                for (int x = 0; x < width; x += DCTSize)
                {
                    subMatrix = GetSubMatrix(matrix, y, DCTSize, x, DCTSize, subMatrix);
                    var complexes = proc.Forward(subMatrix, selector);

                    var extracted = ExtractRealPart(complexes);
                    
                    quantizedFreqs = Quantize(extracted, quantizationMatrix, quantizedFreqs);

                    quantizedBytes = (byte[])ZigZagScan(quantizedFreqs, quantizedBytes);
                    allQuantizedBytes.AddRange(quantizedBytes);
                }
            }
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
    
    private Matrix Uncompress(CompressedImage image)
    {
        var result = new Matrix(image.Height, image.Width);
        var resultArray = new double[image.Height, image.Width];
        using (var allQuantizedBytes =
               new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
        {
            var width = result.Width;
            var height = result.Height;

            var r = new double[height, width];
            var g = new double[height, width];
            var b = new double[height, width];

            var quantizationMatrix = GetQuantizationMatrix(Quality);
            var quantizedBytes = new byte[DCTSize * DCTSize];
            var quantizedFreqs = new byte[DCTSize, DCTSize];
            var channelFreqs = new double[quantizedFreqs.GetLength(0), quantizedFreqs.GetLength(1)];
            var flag = false;
            foreach (var channel in new[] { r, g, b })
            {
                for (int y = 0; y < height; y += DCTSize)
                {
                    for (int x = 0; x < width; x += DCTSize)
                    {
                        allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();

                        quantizedFreqs = ZigZagUnScan(quantizedBytes, quantizedFreqs);
                        channelFreqs = DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
                        
                        var matrix = ExtractComplexes(channelFreqs);
                        var map = proc.Inverse(matrix);

                        SetArray(channel, map, y, x);
                    }
                }
            }
            
            SetPixels(result, r, g, b, PixelFormat.RGB, 0, 0);
        }
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

    public static int[,] GetQuantizationMatrix(int quality)
    {
        if (quality < 1 || quality > 99)
            throw new ArgumentException("quality must be in [1,99] interval");

        var multiplier = 10000 / quality;

        var result = new[,]
        {
            { 8, 4 },
            { 4, 4 }
        };

        for (int y = 0; y < result.GetLength(0); y++)
        {
            for (int x = 0; x < result.GetLength(1); x++)
            {
                result[y, x] = (multiplier * result[y, x] + 50) / 100;
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
        return quantizedFreqs;
    }

    public static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes, byte[,] outputFreq)
    {
        outputFreq[0, 0] = quantizedBytes[0];
        outputFreq[0, 1] = quantizedBytes[1];
        outputFreq[1, 0] = quantizedBytes[2];
        outputFreq[1, 1] = quantizedBytes[3];
        return outputFreq;
    }
}
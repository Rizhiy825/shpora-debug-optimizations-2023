using JPEG.Algorithmes;
using JPEG.Images;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Numerics;
using System;
using PixelFormat = JPEG.Images.PixelFormat;
using Buffer = JPEG.Utilities.Buffer;

namespace JPEG.Processor;

public class JpegProcFFTV3 : IJpegProcessor
{
    public static readonly JpegProcFFTV3 Init = new();
    public const int Quality = 30;

    private FFT2DV3 proc = new();
    public const int DCTSize = 2;
    
    public void Compress(string imagePath,
        string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);


        //Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
        var compressionResult = Compress(bmp, Quality);
        compressionResult.Save(compressedImagePath);
    }

    public void Uncompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);
        var uncompressedImage = Uncompress(compressedImage);
        uncompressedImage.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private CompressedImage Compress(Bitmap image, int quality = 50)
    {
        var width = image.Width;
        var height = image.Height;
        
        var allQuantizedBytes = new List<byte>();
        var selectorRGBFuncs = new Func<Color, double>[] { p => p.R, p => p.G, p => p.B };
        
        var quantizationMatrix = GetQuantizationMatrix(Quality);
        var extractedComplexes = new double[DCTSize, DCTSize];
        
        foreach (var selector in selectorRGBFuncs)
        {
            for (int y = 0; y < height; y += DCTSize)
            {
                for (int x = 0; x < width; x += DCTSize)
                {
                    Buffer.complexes = proc.Forward(image, y, x, selector,
                        Buffer.complexes,
                        Buffer.bufferP,
                        Buffer.bufferF,
                        Buffer.bufferT);
                    
                    extractedComplexes = ExtractRealPart(Buffer.complexes, extractedComplexes);

                    Buffer.quantizedFreqs = Quantize(extractedComplexes, quantizationMatrix, Buffer.quantizedFreqs);
                    Buffer.quantizedBytes = (byte[])ZigZagScan(Buffer.quantizedFreqs, Buffer.quantizedBytes);
                    allQuantizedBytes.AddRange(Buffer.quantizedBytes);
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
            Height = image.Height,
            Width = image.Width
        };
    }

    private double[,] ExtractRealPart(Complex[][] complexes, double[,] extracted)
    {
        for (int y = 0; y < complexes.GetLength(0); y++)
        {
            for (int x = 0; x < complexes.GetLength(0); x++)
            {
                extracted[y, x] = complexes[y][x].Real;
            }
        }

        return extracted;
    }

    private Bitmap Uncompress(CompressedImage image)
    {
        var result = new Bitmap(image.Width, image.Height);
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
            var channelFreqs = new double[Buffer.quantizedFreqs.GetLength(0), Buffer.quantizedFreqs.GetLength(1)];
            var map = new double[DCTSize, DCTSize];

            foreach (var channel in new[] { r, g, b })
            {
                for (int y = 0; y < height; y += DCTSize)
                {
                    for (int x = 0; x < width; x += DCTSize)
                    {
                        allQuantizedBytes.ReadAsync(Buffer.quantizedBytes, 0, Buffer.quantizedBytes.Length).Wait();

                        Buffer.quantizedFreqs = ZigZagUnScan(Buffer.quantizedBytes, Buffer.quantizedFreqs);
                        channelFreqs = DeQuantize(Buffer.quantizedFreqs, quantizationMatrix, channelFreqs);

                        Buffer.complexes = ExtractComplexes(channelFreqs, Buffer.complexes);
                        map = proc.Inverse(Buffer.complexes, 
                            Buffer.bufferP, 
                            Buffer.bufferF, 
                            Buffer.bufferT, 
                            map);

                        SetArray(channel, map, y, x);
                    }
                }
            }

            SetPixels(result, r, g, b, 0, 0);
        }
        return result;
    }

    private Complex[][] ExtractComplexes(double[,] matrix, Complex[][] complexes)
    {
        for (int y = 0; y < matrix.GetLength(0); y++)
        {
            for (int x = 0; x < matrix.GetLength(0); x++)
            {
                complexes[y][x] = new Complex(matrix[y, x], 0);
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
    private static void SetPixels(Bitmap matrix, double[,] a, double[,] b, double[,] c, int yOffset, int xOffset)
    {
        var height = a.GetLength(0);
        var width = a.GetLength(1);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                matrix.SetPixel(xOffset + x, yOffset + y, Color.FromArgb((int)a[y, x], (int)b[y, x], (int)c[y, x]));
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
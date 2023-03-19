using System.Drawing;
using System;
using System.IO;
using JPEG.Images;
using System.Collections.Generic;
using System.Drawing.Imaging;
using JPEG.Algorithmes;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcBuffAndParallel : IJpegProcessor
{
    public static readonly JpegProcBuffAndParallel Init = new();
    public const int CompressionQuality = JpegProcessor.CompressionQuality;
    public const int DCTSize = JpegProcessor.DCTSize;

    public void Compress(string imagePath,
        string compressedImagePath)
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        var imageMatrix = (Matrix)bmp;
        //Console.WriteLine($"{bmp.Width}x{bmp.Height} - {fileStream.Length / (1024.0 * 1024):F2} MB");
        var compressionResult = Compress(imageMatrix, JpegProcessor.CompressionQuality);
        compressionResult.Save(compressedImagePath);
    }
    public void Uncompress(string compressedImagePath, string uncompressedImagePath)
    {
        var compressedImage = CompressedImage.Load(compressedImagePath);
        var uncompressedImage = Uncompress(compressedImage);
        var resultBmp = (Bitmap)uncompressedImage;
        resultBmp.Save(uncompressedImagePath, ImageFormat.Bmp);
    }

    private static CompressedImage Compress(Matrix matrix, int quality = 50)
    {
        var allQuantizedBytes = new List<byte>();
        var channelFreqs = new double[DCTSize, DCTSize];
        var quantizedFreqs = new byte[channelFreqs.GetLength(0), channelFreqs.GetLength(1)];
        var quantizationMatrix = GetQuantizationMatrix(quality);
        var subMatrix = new double[DCTSize, DCTSize];
        var quantizedBytes = new byte[quantizedFreqs.GetLength(0) * quantizedFreqs.GetLength(1)];
        var selectorFuncs = new Func<Pixel, double>[] { p => p.Y, p => p.Cb, p => p.Cr };

        for (var y = 0; y < matrix.Height; y += DCTSize)
        {
            for (var x = 0; x < matrix.Width; x += DCTSize)
            {
                foreach (var selector in selectorFuncs)
                {
                    subMatrix = GetSubMatrix(matrix, y, DCTSize, x, DCTSize, selector, subMatrix);
                    ShiftMatrixValues(subMatrix, -128);
                    DCTParallel.DCT2D(subMatrix, channelFreqs);
                    quantizedFreqs = Quantize(channelFreqs, quantizationMatrix, quantizedFreqs);
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

    private static Matrix Uncompress(CompressedImage image)
    {
        var result = new Matrix(image.Height, image.Width);
        using (var allQuantizedBytes =
               new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
        {
            var _y = new double[DCTSize, DCTSize];
            var cb = new double[DCTSize, DCTSize];
            var cr = new double[DCTSize, DCTSize];
            var channels = new[] { _y, cb, cr };

            var quantizationMatrix = GetQuantizationMatrix(image.Quality);
            var quantizedBytes = new byte[DCTSize * DCTSize];
            var quantizedFreqs = new byte[DCTSize, DCTSize];
            var channelFreqs = new double[quantizedFreqs.GetLength(0), quantizedFreqs.GetLength(1)];

            for (var y = 0; y < image.Height; y += DCTSize)
            {
                for (var x = 0; x < image.Width; x += DCTSize)
                {
                    //_y cb and cr
                    foreach (var channel in channels)
                    {
                        //quantized
                        allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
                        quantizedFreqs = ZigZagUnScan(quantizedBytes, quantizedFreqs);
                        channelFreqs = DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
                        DCTParallel.IDCT2D(channelFreqs, channel);
                        ShiftMatrixValues(channel, 128);
                    }

                    SetPixels(result, _y, cb, cr, PixelFormat.YCbCr, y, x);
                }
            }
        }

        return result;
    }

    public static double[,] GetSubMatrix(Matrix matrix, int yOffset, int yLength, int xOffset, int xLength,
        Func<Pixel, double> componentSelector, double[,] subMatrix)
    {
        for (var j = 0; j < yLength; j++)
        for (var i = 0; i < xLength; i++)
            subMatrix[j, i] = componentSelector(matrix.Pixels[yOffset + j, xOffset + i]);
        return subMatrix;
    }

    public static IEnumerable<byte> ZigZagScan(byte[,] channelFreqs, byte[] quantizedFreqs)
    {
        quantizedFreqs[0] = channelFreqs[0, 0];
        quantizedFreqs[1] = channelFreqs[0, 1];
        quantizedFreqs[2] = channelFreqs[1, 0];
        quantizedFreqs[3] = channelFreqs[2, 0];
        quantizedFreqs[4] = channelFreqs[1, 1];
        quantizedFreqs[5] = channelFreqs[0, 2];
        quantizedFreqs[6] = channelFreqs[0, 3];
        quantizedFreqs[7] = channelFreqs[1, 2];
        quantizedFreqs[8] = channelFreqs[2, 1];
        quantizedFreqs[9] = channelFreqs[3, 0];
        quantizedFreqs[10] = channelFreqs[4, 0];
        quantizedFreqs[11] = channelFreqs[3, 1];
        quantizedFreqs[12] = channelFreqs[2, 2];
        quantizedFreqs[13] = channelFreqs[1, 3];
        quantizedFreqs[14] = channelFreqs[0, 4];
        quantizedFreqs[15] = channelFreqs[0, 5];

        quantizedFreqs[16] = channelFreqs[1, 4];
        quantizedFreqs[17] = channelFreqs[2, 3];
        quantizedFreqs[18] = channelFreqs[3, 2];
        quantizedFreqs[19] = channelFreqs[4, 1];
        quantizedFreqs[20] = channelFreqs[5, 0];
        quantizedFreqs[21] = channelFreqs[6, 0];
        quantizedFreqs[22] = channelFreqs[5, 1];
        quantizedFreqs[23] = channelFreqs[4, 2];
        quantizedFreqs[24] = channelFreqs[3, 3];
        quantizedFreqs[25] = channelFreqs[2, 4];
        quantizedFreqs[26] = channelFreqs[1, 5];
        quantizedFreqs[27] = channelFreqs[0, 6];
        quantizedFreqs[28] = channelFreqs[0, 7];
        quantizedFreqs[29] = channelFreqs[1, 6];
        quantizedFreqs[30] = channelFreqs[2, 5];
        quantizedFreqs[31] = channelFreqs[3, 4];

        quantizedFreqs[32] = channelFreqs[4, 3];
        quantizedFreqs[33] = channelFreqs[5, 2];
        quantizedFreqs[34] = channelFreqs[6, 1];
        quantizedFreqs[35] = channelFreqs[7, 0];
        quantizedFreqs[36] = channelFreqs[7, 1];
        quantizedFreqs[37] = channelFreqs[6, 2];
        quantizedFreqs[38] = channelFreqs[5, 3];
        quantizedFreqs[39] = channelFreqs[4, 4];
        quantizedFreqs[40] = channelFreqs[3, 5];
        quantizedFreqs[41] = channelFreqs[2, 6];
        quantizedFreqs[42] = channelFreqs[1, 7];
        quantizedFreqs[43] = channelFreqs[2, 7];
        quantizedFreqs[44] = channelFreqs[3, 6];
        quantizedFreqs[45] = channelFreqs[4, 5];
        quantizedFreqs[46] = channelFreqs[5, 4];
        quantizedFreqs[47] = channelFreqs[6, 3];

        quantizedFreqs[48] = channelFreqs[7, 2];
        quantizedFreqs[49] = channelFreqs[7, 3];
        quantizedFreqs[50] = channelFreqs[6, 4];
        quantizedFreqs[51] = channelFreqs[5, 5];
        quantizedFreqs[52] = channelFreqs[4, 6];
        quantizedFreqs[53] = channelFreqs[3, 7];
        quantizedFreqs[54] = channelFreqs[4, 7];
        quantizedFreqs[55] = channelFreqs[5, 6];
        quantizedFreqs[56] = channelFreqs[6, 5];
        quantizedFreqs[57] = channelFreqs[7, 4];
        quantizedFreqs[58] = channelFreqs[7, 5];
        quantizedFreqs[59] = channelFreqs[6, 6];
        quantizedFreqs[60] = channelFreqs[5, 7];
        quantizedFreqs[61] = channelFreqs[6, 7];
        quantizedFreqs[62] = channelFreqs[7, 6];
        quantizedFreqs[63] = channelFreqs[7, 7];

        return quantizedFreqs;
    }

    public static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes, byte[,] outputFreq)
    {
        outputFreq[0, 0] = quantizedBytes[0];
        outputFreq[0, 1] = quantizedBytes[1];
        outputFreq[0, 2] = quantizedBytes[5];
        outputFreq[0, 3] = quantizedBytes[6];
        outputFreq[0, 4] = quantizedBytes[14];
        outputFreq[0, 5] = quantizedBytes[15];
        outputFreq[0, 6] = quantizedBytes[27];
        outputFreq[0, 7] = quantizedBytes[28];

        outputFreq[1, 0] = quantizedBytes[2];
        outputFreq[1, 1] = quantizedBytes[4];
        outputFreq[1, 2] = quantizedBytes[7];
        outputFreq[1, 3] = quantizedBytes[13];
        outputFreq[1, 4] = quantizedBytes[16];
        outputFreq[1, 5] = quantizedBytes[26];
        outputFreq[1, 6] = quantizedBytes[29];
        outputFreq[1, 7] = quantizedBytes[42];

        outputFreq[2, 0] = quantizedBytes[3];
        outputFreq[2, 1] = quantizedBytes[8];
        outputFreq[2, 2] = quantizedBytes[12];
        outputFreq[2, 3] = quantizedBytes[17];
        outputFreq[2, 4] = quantizedBytes[25];
        outputFreq[2, 5] = quantizedBytes[30];
        outputFreq[2, 6] = quantizedBytes[41];
        outputFreq[2, 7] = quantizedBytes[43];

        outputFreq[3, 0] = quantizedBytes[9];
        outputFreq[3, 1] = quantizedBytes[11];
        outputFreq[3, 2] = quantizedBytes[18];
        outputFreq[3, 3] = quantizedBytes[24];
        outputFreq[3, 4] = quantizedBytes[31];
        outputFreq[3, 5] = quantizedBytes[40];
        outputFreq[3, 6] = quantizedBytes[44];
        outputFreq[3, 7] = quantizedBytes[53];

        outputFreq[4, 0] = quantizedBytes[10];
        outputFreq[4, 1] = quantizedBytes[19];
        outputFreq[4, 2] = quantizedBytes[23];
        outputFreq[4, 3] = quantizedBytes[32];
        outputFreq[4, 4] = quantizedBytes[39];
        outputFreq[4, 5] = quantizedBytes[45];
        outputFreq[4, 6] = quantizedBytes[52];
        outputFreq[4, 7] = quantizedBytes[54];

        outputFreq[5, 0] = quantizedBytes[20];
        outputFreq[5, 1] = quantizedBytes[22];
        outputFreq[5, 2] = quantizedBytes[33];
        outputFreq[5, 3] = quantizedBytes[38];
        outputFreq[5, 4] = quantizedBytes[46];
        outputFreq[5, 5] = quantizedBytes[51];
        outputFreq[5, 6] = quantizedBytes[55];
        outputFreq[5, 7] = quantizedBytes[60];

        outputFreq[6, 0] = quantizedBytes[21];
        outputFreq[6, 1] = quantizedBytes[34];
        outputFreq[6, 2] = quantizedBytes[37];
        outputFreq[6, 3] = quantizedBytes[47];
        outputFreq[6, 4] = quantizedBytes[50];
        outputFreq[6, 5] = quantizedBytes[56];
        outputFreq[6, 6] = quantizedBytes[59];
        outputFreq[6, 7] = quantizedBytes[61];

        outputFreq[7, 0] = quantizedBytes[35];
        outputFreq[7, 1] = quantizedBytes[36];
        outputFreq[7, 2] = quantizedBytes[48];
        outputFreq[7, 3] = quantizedBytes[49];
        outputFreq[7, 4] = quantizedBytes[57];
        outputFreq[7, 5] = quantizedBytes[58];
        outputFreq[7, 6] = quantizedBytes[62];
        outputFreq[7, 7] = quantizedBytes[63];

        return outputFreq;
    }

    public static byte[,] Quantize(double[,] channelFreqs, int[,] quantizationMatrix, byte[,] quantizedFreqs)
    {
        for (int y = 0; y < channelFreqs.GetLength(0); y++)
        {
            for (int x = 0; x < channelFreqs.GetLength(1); x++)
            {
                var coef = channelFreqs[y, x] / quantizationMatrix[y, x];
                quantizedFreqs[y, x] = (byte)coef;
            }
        }

        return quantizedFreqs;
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

        var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

        //var result = new[,]
        //{
        //    { 8, 16, 20, 24, 32, 40, 48, 64 },
        //    { 16, 16, 20, 24, 32, 40, 48, 64 },
        //    { 20, 20, 24, 24, 32, 40, 48, 64 },
        //    { 24, 24, 24, 32, 32, 40, 48, 64 },
        //    { 32, 32, 32, 32, 40, 40, 48, 64 },
        //    { 40, 40, 40, 40, 40, 48, 48, 64 },
        //    { 48, 48, 48, 48, 48, 48, 64, 64 },
        //    { 64, 64, 64, 64, 64, 64, 64, 128 }
        //};

        var result = new[,]
        {
            { 16, 11, 10, 16, 24, 40, 51, 61 },
            { 12, 12, 14, 19, 26, 58, 60, 55 },
            { 14, 13, 16, 24, 40, 57, 69, 56 },
            { 14, 17, 22, 29, 51, 87, 80, 62 },
            { 18, 22, 37, 56, 68, 109, 103, 77 },
            { 24, 35, 55, 64, 81, 104, 113, 92 },
            { 49, 64, 78, 87, 103, 121, 120, 101 },
            { 72, 92, 95, 98, 112, 100, 103, 99 }
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

    public static void ShiftMatrixValues(double[,] subMatrix, int shiftValue)
    {
        var height = subMatrix.GetLength(0);
        var width = subMatrix.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            subMatrix[y, x] = subMatrix[y, x] + shiftValue;
    }

    public static void SetPixels(Matrix matrix, double[,] a, double[,] b, double[,] c, PixelFormat format,
        int yOffset, int xOffset)
    {
        var height = a.GetLength(0);
        var width = a.GetLength(1);

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            matrix.Pixels[yOffset + y, xOffset + x] = new Pixel(a[y, x], b[y, x], c[y, x], format);
    }
}
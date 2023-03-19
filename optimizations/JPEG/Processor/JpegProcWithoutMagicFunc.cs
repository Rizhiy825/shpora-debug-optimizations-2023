using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using JPEG.Images;
using JPEG.Algorithmes;
using System.Collections.Generic;
using System;
using PixelFormat = JPEG.Images.PixelFormat;

namespace JPEG.Processor;

public class JpegProcWithoutMagicFunc : IJpegProcessor
{
    public static readonly JpegProcWithoutMagicFunc Init = new();
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
        var quantizationMatrix = JpegProcBuffAndParallel.GetQuantizationMatrix(quality);
        var subMatrix = new double[DCTSize, DCTSize];
        var quantizedBytes = new byte[quantizedFreqs.GetLength(0) * quantizedFreqs.GetLength(1)];
        var selectorFuncs = new Func<Pixel, double>[] { p => p.Y, p => p.Cb, p => p.Cr };

        for (var y = 0; y < matrix.Height; y += DCTSize)
        {
            for (var x = 0; x < matrix.Width; x += DCTSize)
            {
                foreach (var selector in selectorFuncs)
                {
                    subMatrix = JpegProcBuffAndParallel.GetSubMatrix(matrix, y, DCTSize, x, DCTSize, selector, subMatrix);
                    JpegProcBuffAndParallel.ShiftMatrixValues(subMatrix, -128);
                    DCTWithoutMagicFunc.DCT2D(subMatrix, channelFreqs);
                    quantizedFreqs = JpegProcBuffAndParallel.Quantize(channelFreqs, quantizationMatrix, quantizedFreqs);
                    quantizedBytes = (byte[])JpegProcBuffAndParallel.ZigZagScan(quantizedFreqs, quantizedBytes);
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

            var quantizationMatrix = JpegProcBuffAndParallel.GetQuantizationMatrix(image.Quality);
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
                        quantizedFreqs = JpegProcBuffAndParallel.ZigZagUnScan(quantizedBytes, quantizedFreqs);
                        channelFreqs = JpegProcBuffAndParallel.DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
                        DCTWithoutMagicFunc.IDCT2D(channelFreqs, channel);
                        JpegProcBuffAndParallel.ShiftMatrixValues(channel, 128);
                    }

                    JpegProcBuffAndParallel.SetPixels(result, _y, cb, cr, PixelFormat.YCbCr, y, x);
                }
            }
        }

        return result;
    }
}
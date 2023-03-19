using JPEG.Algorithmes;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Numerics;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Buffer = JPEG.Utilities.Buffer;

namespace JPEG.Processor;

public class JpegProcFFTParallel : IJpegProcessor
{
    public static readonly JpegProcFFTParallel Init = new();
    public const int Quality = 30;

    private FFT2DV4 proc = new();
    public const int DCTSize = 2;
    public static int stride = 0;
     
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
        BitmapData btmDt = image.LockBits(
            new Rectangle(0, 0, image.Width, image.Height),
            ImageLockMode.ReadWrite, image.PixelFormat
        );
        IntPtr pointer = btmDt.Scan0;
        int size = Math.Abs(btmDt.Stride) * image.Height;
        stride = btmDt.Stride;
        byte[] pixels = new byte[size];
        Marshal.Copy(pointer, pixels, 0, size);
        
        

        var width = image.Width;
        var height = image.Height;

        var allQuantizedBytes = new List<byte>();

        var quantizationMatrix = GetQuantizationMatrix(Quality);
        var extractedComplexes = new double[DCTSize, DCTSize];

        var phase = (double)Buffer.Omega * -4.0;
        var polarComplex = Complex.FromPolarCoordinates(1, phase);

        for (int selector = 0; selector < 3; selector++)
        {
            for (int y = 0; y < height; y += DCTSize)
            {
                for (int x = 0; x < width; x += DCTSize)
                {
                    Buffer.complexes = proc.Forward(pixels, y, x, selector,
                        Buffer.complexes,
                        Buffer.bufferP,
                        Buffer.bufferF,
                        Buffer.bufferT, polarComplex);

                    extractedComplexes = ExtractRealPart(Buffer.complexes, extractedComplexes);

                    Buffer.quantizedFreqs = Quantize(extractedComplexes, quantizationMatrix, Buffer.quantizedFreqs);
                    Buffer.quantizedBytes = ZigZagScan(Buffer.quantizedFreqs, Buffer.quantizedBytes);
                    allQuantizedBytes.AddRange(Buffer.quantizedBytes);
                }
            }
        }

        Marshal.Copy(pixels, 0, pointer, size);
        image.UnlockBits(btmDt);

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
        for (int y = 0; y < DCTSize; y++)
        {
            for (int x = 0; x < DCTSize; x++)
            {
                extracted[y, x] = complexes[y][x].Real;
            }
        }

        return extracted;
    }

    private Bitmap Uncompress(CompressedImage image)
    {
        var result = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb);

        BitmapData btmDt = result.LockBits(
            new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.ReadWrite, result.PixelFormat
        );

        IntPtr pointer = btmDt.Scan0;
        int size = Math.Abs(btmDt.Stride) * result.Height;
        byte[] pixels = new byte[size];
        Marshal.Copy(pointer, pixels, 0, size);
        
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
            var channelFreqs = new double[DCTSize, DCTSize];
            var map = new double[DCTSize, DCTSize];
            var phase = Convert.ToInt32(Buffer.Omega * -1);
            var polarComplex = Complex.FromPolarCoordinates(1, 0);

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
                            map,
                            polarComplex);

                        SetArray(channel, map, y, x);
                    }
                }
            }

            SetPixels(pixels, r, g, b);
        }

        Marshal.Copy(pixels, 0, pointer, size);
        result.UnlockBits(btmDt);


        return result;
    }

    private Complex[][] ExtractComplexes(double[,] matrix, Complex[][] complexes)
    {
        for (int y = 0; y < DCTSize; y++)
        {
            for (int x = 0; x < DCTSize; x++)
            {
                complexes[y][x] = new Complex(matrix[y, x], 0);
            }
        }

        return complexes;
    }

    private static void SetArray(double[,] matrix, double[,] a, int yOffset, int xOffset)
    {
        for (var y = 0; y < DCTSize; y++)
            for (var x = 0; x < DCTSize; x++)
                matrix[yOffset + y, xOffset + x] = a[y, x];
    }
    private static void SetPixels(byte[] image, double[,] red, double[,] green, double[,] blue)
    {
        int indexer = 0;

        foreach (var pixel in red)
        {
            image[indexer] = (byte)pixel;
            indexer += 3;
        }

        indexer = 1;

        foreach (var pixel in green)
        {
            image[indexer] = (byte)pixel;
            indexer += 3;
        }

        indexer = 2;

        foreach (var pixel in blue)
        {
            image[indexer] = (byte)pixel;
            indexer += 3;
        }
    }
    public static double[,] DeQuantize(byte[,] quantizedBytes, int[,] quantizationMatrix, double[,] channelFreqs)
    {
        for (int y = 0; y < DCTSize; y++)
        {
            for (int x = 0; x < DCTSize; x++)
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
        for (int y = 0; y < DCTSize; y++)
        {
            for (int x = 0; x < DCTSize; x++)
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

        for (int y = 0; y < DCTSize; y++)
        {
            for (int x = 0; x < DCTSize; x++)
            {
                result[y, x] = (multiplier * result[y, x] + 50) / 100;
            }
        }

        return result;
    }

    public static byte[] ZigZagScan(byte[,] channelFreqs, byte[] quantizedFreqs)
    {
        quantizedFreqs[0] = channelFreqs[0, 0];
        quantizedFreqs[1] = channelFreqs[0, 1];
        quantizedFreqs[2] = channelFreqs[1, 0];
        quantizedFreqs[3] = channelFreqs[1, 1];
        return quantizedFreqs;
    }

    public static byte[,] ZigZagUnScan(byte[] quantizedBytes, byte[,] outputFreq)
    {
        outputFreq[0, 0] = quantizedBytes[0];
        outputFreq[0, 1] = quantizedBytes[1];
        outputFreq[1, 0] = quantizedBytes[2];
        outputFreq[1, 1] = quantizedBytes[3];
        return outputFreq;
    }
}
using System.Drawing;
using BenchmarkDotNet.Attributes;
using JPEG.Images;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class GetSubMatrixBenchmark
{
    private static readonly string imagePath = @"sample.bmp";
    private static readonly string compressedImagePath = imagePath + ".compressed." + JpegProcessor.CompressionQuality;
    private static readonly string uncompressedImagePath =
        imagePath + ".uncompressed." + JpegProcessor.CompressionQuality + ".bmp";

    private static Matrix imageMatrix;
    
    public const int CompressionQuality = 70;
    private const int DCTSize = 8;

    [GlobalSetup]
    public void SetUp()
    {
        using var fileStream = File.OpenRead(imagePath);
        using var bmp = (Bitmap)Image.FromStream(fileStream, false, false);
        imageMatrix = (Matrix)bmp;
    }

    [Benchmark]
    public void GetSubMatrix()
    {
        var selectorFuncs = new Func<Pixel, double>[] {p => p.Y, p => p.Cb, p => p.Cr};
        var subMatrix = new double[DCTSize, DCTSize];
        
        for (var y = 0; y < imageMatrix.Height; y += DCTSize)
        {
            for (var x = 0; x < imageMatrix.Width; x += DCTSize)
            {
                foreach (var selector in selectorFuncs)
                {
                    //subMatrix = JpegProcessor.GetSubMatrix(imageMatrix, y, DCTSize, x, DCTSize, selector, subMatrix);
                }
            }
        }
    }
}
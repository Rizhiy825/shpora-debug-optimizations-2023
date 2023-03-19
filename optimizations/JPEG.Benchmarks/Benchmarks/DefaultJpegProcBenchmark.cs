using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class DefaultJpegProcBenchmark
{
	private IJpegProcessor jpegProcessor;
	public const string imagePath = @"C:\Users\Artemii\source\repos\Optimization\shpora-debug-optimizations-2023\optimizations\JPEG\bin\Debug\net7.0\sample.bmp";
    public static readonly string compressedImagePath = @"C:\Users\Artemii\source\repos\Optimization\shpora-debug-optimizations-2023\optimizations\JPEG\bin\Debug\net7.0\sample.bmp.compressed.70.bmp";
    public static readonly string uncompressedImagePath = @"C:\Users\Artemii\source\repos\Optimization\shpora-debug-optimizations-2023\optimizations\JPEG\bin\Debug\net7.0\sample.bmp.uncompressed.70.bmp";

    [GlobalSetup]
	public void SetUp()
	{
        jpegProcessor = JpegProcessor.Init;
	}

	[Benchmark]
	public void CompressDefault()
	{
        jpegProcessor.Compress(imagePath, compressedImagePath);
	}

    [Benchmark]
    public void UncompressDefault()
    {
        jpegProcessor.Uncompress(compressedImagePath, uncompressedImagePath);
    }
}
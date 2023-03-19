using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class DefaultJpegProcBenchmark
{
	private IJpegProcessor jpegProcessor;
	public const string imagePath = @"sample_short";
    public static readonly string compressedImagePath = imagePath + ".compressed." + JpegProcessor.CompressionQuality;
    public static readonly string uncompressedImagePath =
		imagePath + ".uncompressed." + JpegProcessor.CompressionQuality + ".bmp";
	
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
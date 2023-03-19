using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class WithoutMagicJpegProcessBenchmark
{
    private JpegProcWithoutMagicFunc jpegProcessor;
    public string imagePath = DefaultJpegProcBenchmark.imagePath;
    public string compressedImagePath = DefaultJpegProcBenchmark.compressedImagePath;
    public string uncompressedImagePath = DefaultJpegProcBenchmark.uncompressedImagePath;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcWithoutMagicFunc.Init;
    }

    [Benchmark]
    public void CompressWithoutMagicFunc()
    {
        jpegProcessor.Compress(imagePath, compressedImagePath);
    }

    [Benchmark]
    public void UncompressWithoutMagicFunc()
    {
        jpegProcessor.Uncompress(compressedImagePath, uncompressedImagePath);
    }
}
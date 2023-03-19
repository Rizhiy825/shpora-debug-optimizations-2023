using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class FFTPfrallelBenchmark
{
    private JpegProcFFTParallel jpegProcessor;
    public string imagePath = DefaultJpegProcBenchmark.imagePath;
    public string compressedImagePath = DefaultJpegProcBenchmark.compressedImagePath;
    public string uncompressedImagePath = DefaultJpegProcBenchmark.uncompressedImagePath;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcFFTParallel.Init;
    }

    [Benchmark]
    public void CompressFFTVFastest()
    {
        jpegProcessor.Compress(imagePath, compressedImagePath);
    }

    [Benchmark]
    public void UncompressFFTV3Fastest()
    {
        jpegProcessor.Uncompress(compressedImagePath, uncompressedImagePath);
    }
}
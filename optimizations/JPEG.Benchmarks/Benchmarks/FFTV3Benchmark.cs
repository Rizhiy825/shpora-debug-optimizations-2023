using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class FFTV3Benchmark
{
    private JpegProcFFTV3 jpegProcessor;
    public string imagePath = DefaultJpegProcBenchmark.imagePath;
    public string compressedImagePath = DefaultJpegProcBenchmark.compressedImagePath;
    public string uncompressedImagePath = DefaultJpegProcBenchmark.uncompressedImagePath;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcFFTV3.Init;
    }

    [Benchmark]
    public void CompressFFTV3()
    {
        jpegProcessor.Compress(imagePath, compressedImagePath);
    }

    [Benchmark]
    public void UncompressFFTV3()
    {
        jpegProcessor.Uncompress(compressedImagePath, uncompressedImagePath);
    }
}
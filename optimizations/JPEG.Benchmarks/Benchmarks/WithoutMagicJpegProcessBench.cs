using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class WithoutMagicJpegProcessBench
{
    private IJpegProcessor jpegProcessor;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcWithoutMagicFunc.Init;
    }

    [Benchmark]
    public void CompressWithoutMagicFunc()
    {
        jpegProcessor.Compress(DefaultJpegProcBenchmark.imagePath, 
            DefaultJpegProcBenchmark.compressedImagePath);
    }

    [Benchmark]
    public void UncompressWithoutMagicFunc()
    {
        jpegProcessor.Uncompress(DefaultJpegProcBenchmark.compressedImagePath, 
            DefaultJpegProcBenchmark.uncompressedImagePath);
    }
}
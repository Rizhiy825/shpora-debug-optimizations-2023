using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class ParallelJpegBenchmark
{
    private IJpegProcessor jpegProcessor;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcBuffAndParallel.Init;
    }

    [Benchmark]
    public void CompressParallel()
    {
        jpegProcessor.Compress(DefaultJpegProcBenchmark.imagePath,
            DefaultJpegProcBenchmark.compressedImagePath);
    }

    [Benchmark]
    public void UncompressParallel()
    {
        jpegProcessor.Uncompress(DefaultJpegProcBenchmark.compressedImagePath, 
            DefaultJpegProcBenchmark.uncompressedImagePath);
    }
}
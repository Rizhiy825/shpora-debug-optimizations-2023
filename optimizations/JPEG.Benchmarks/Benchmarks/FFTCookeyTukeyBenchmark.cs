using System.Drawing;
using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class FFTCookeyTukeyBenchmark
{
    private JpegProcFFTCooleyTukey jpegProcessor;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcFFTCooleyTukey.Init;
    }

    [Benchmark]
    public void CompressFFT()
    {
        jpegProcessor.Compress(DefaultJpegProcBenchmark.imagePath,
            DefaultJpegProcBenchmark.compressedImagePath);
    }

    //[Benchmark]
    //public void UncompressFFT()
    //{
    //    jpegProcessor.Uncompress(DefaultJpegProcBenchmark.compressedImagePath,
    //        DefaultJpegProcBenchmark.uncompressedImagePath);
    //}
}
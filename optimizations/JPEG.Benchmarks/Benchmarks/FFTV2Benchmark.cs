﻿using BenchmarkDotNet.Attributes;
using JPEG.Processor;

namespace JPEG.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class FFTV2Benchmark
{
    private JpegProcFFTV2 jpegProcessor;
    public string imagePath = DefaultJpegProcBenchmark.imagePath;
    public string compressedImagePath = DefaultJpegProcBenchmark.compressedImagePath;
    public string uncompressedImagePath = DefaultJpegProcBenchmark.uncompressedImagePath;

    [GlobalSetup]
    public void SetUp()
    {
        jpegProcessor = JpegProcFFTV2.Init;
    }

    [Benchmark]
    public void CompressFFTV2()
    {
        jpegProcessor.Compress(imagePath, compressedImagePath);
    }

    [Benchmark]
    public void UncompressFFTV2()
    {
        jpegProcessor.Uncompress(compressedImagePath, uncompressedImagePath);
    }
}
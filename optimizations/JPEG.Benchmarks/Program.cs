using BenchmarkDotNet.Running;
using JPEG.Benchmarks.Benchmarks;

namespace JPEG.Benchmarks;

internal class Program
{
	public static void Main(string[] args)
	{
		BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        //BenchmarkRunner.Run<DefaultJpegProcBenchmark>();
        //BenchmarkRunner.Run<ParallelJpegBenchmark>();
        //BenchmarkRunner.Run<WithoutMagicJpegProcessBench>();
        //BenchmarkRunner.Run<FFTV2Benchmark>();
        //BenchmarkRunner.Run<FFTV3Benchmark>();
    }
}
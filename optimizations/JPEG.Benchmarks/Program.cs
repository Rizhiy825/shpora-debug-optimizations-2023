using BenchmarkDotNet.Running;
using JPEG.Benchmarks.Benchmarks;

namespace JPEG.Benchmarks;

internal class Program
{
	public static void Main(string[] args)
	{
		//BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        BenchmarkRunner.Run<JpegProcessorBenchmark>();
        //BenchmarkRunner.Run<GetSubMatrixBenchmark>();
    }
}
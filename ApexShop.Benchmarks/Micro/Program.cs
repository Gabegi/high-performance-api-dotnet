using BenchmarkDotNet.Running;

namespace ApexShop.Benchmarks.Micro;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<DatabaseBenchmarks>();
    }
}

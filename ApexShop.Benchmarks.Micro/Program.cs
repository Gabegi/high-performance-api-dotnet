using ApexShop.Benchmarks.Micro;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance
    .AddExporter(CsvExporter.Default)
    .AddExporter(HtmlExporter.Default);

BenchmarkRunner.Run<ApiEndpointBenchmarks>(config);

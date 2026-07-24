using BenchmarkDotNet.Running;
using Topos.Hypergraph.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(SparseSetVsDictionaryBenchmarks).Assembly).Run(args);

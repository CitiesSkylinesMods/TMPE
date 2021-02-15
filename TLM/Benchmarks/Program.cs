using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmarks {
    class Program {
        static void Main(string[] args) {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
                .AddJob(
                    Job.ShortRun
                        .WithRuntime(
                            new MonoRuntime("Mono x64", @"c:\program files (x86)\steam\steamapps\common\cities_skylines\mono\bin\mono.exe")
                        )
                        .WithToolchain(InProcessEmitToolchain.Instance)
                )
                .AddDiagnoser(MemoryDiagnoser.Default);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args, config);
        }
    }
}

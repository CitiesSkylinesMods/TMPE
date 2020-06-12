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
                                new MonoRuntime("Mono x86", @"C:\Program Files\Unity\Hub\Editor\5.6.7f1\Editor\Data\Mono\bin\mono.exe")
                            )
                            .WithToolchain(InProcessEmitToolchain.Instance)
                    )
                    .AddDiagnoser(MemoryDiagnoser.Default);

            var spiralLoopPerfTestsSummary = BenchmarkRunner.Run<SpiralLoopPerfTests>(
                config
            );
        }
    }
}

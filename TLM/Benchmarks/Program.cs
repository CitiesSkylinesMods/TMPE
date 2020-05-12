using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Benchmarks {
    class Program {
        static void Main(string[] args) {
            var summary = BenchmarkRunner.Run<SpiralLoopPerfTests>(
                ManualConfig.Create(DefaultConfig.Instance)
                    .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
                    .AddJob(
                        Job.ShortRun
                            .WithRuntime(
                                new MonoRuntime("Mono x86", @"C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Mono\bin\mono.exe")
                            )
                            .WithToolchain(InProcessEmitToolchain.Instance)
                            
                    )
                    .AddDiagnoser(MemoryDiagnoser.Default)
            );
        }
    }
}

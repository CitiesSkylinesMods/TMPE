// <copyright file="RealTimeBenchmark.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace TrafficManager.Benchmark {
#if BENCHMARK
    using ColossalFramework.IO;
    using SkyTools.Benchmarks;
    using SkyTools.Tools;

    /// <summary>
    /// A special class that handles the performance benchmarking.
    /// </summary>
    internal static class BenchmarkManager {
        /// <summary>
        /// Initializes the benchmarking.
        /// </summary>
        public static void Setup() {
            var benchmarkSimulationManager = new BenchmarkSimulationManager();
            SimulationManager.RegisterSimulationManager(benchmarkSimulationManager);
            LoadingManager.instance.m_levelUnloaded += benchmarkSimulationManager.Stop;
        }

        private sealed class BenchmarkSimulationManager : ISimulationManager {
            private readonly Benchmark simulationManagerSimulationStepBenchmark;
            private bool isRunning;

            public BenchmarkSimulationManager() {
                simulationManagerSimulationStepBenchmark = Benchmark.Create(0x1000 * 0x100);
                SetupMethods();
            }

            public void Stop() {
                simulationManagerSimulationStepBenchmark.Stop();
                simulationManagerSimulationStepBenchmark.Dump();
                isRunning = false;
                Log.Info("Benchmarking stopped.");
            }

            public void EarlyUpdateData() {
            }

            public void GetData(FastList<IDataContainer> data) {
            }

            public string GetName() => nameof(BenchmarkSimulationManager);

            public ThreadProfiler GetSimulationProfiler() => null;

            public void LateUpdateData(SimulationManager.UpdateMode mode) {
            }

            public void SimulationStep(int subStep) {
                if (subStep == 1000 || subStep == 0) {
                    // This is the 'late update data' phase or the simulation is paused
                    return;
                } else if (!isRunning) {
                    isRunning = true;

                    try {
                        // On failure, don't try to activate benchmark on each step
                        simulationManagerSimulationStepBenchmark.Start();
                        Log.Info("Benchmarking started.");
                    }
                    catch {
                    }
                }

                if ((SimulationManager.instance.m_currentFrameIndex & 0xFFF) == 0xFFF) {
                    simulationManagerSimulationStepBenchmark.MakeSnapshot();
                }
            }

            public void UpdateData(SimulationManager.UpdateMode mode) {
            }

            private void SetupMethods() {
                try {
                    simulationManagerSimulationStepBenchmark.BenchmarkMethod(typeof(SimulationManager), "SimulationStep");
                }
                catch {
                }
            }
        }
    }
#endif
}

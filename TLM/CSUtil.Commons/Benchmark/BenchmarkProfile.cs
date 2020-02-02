namespace CSUtil.Commons.Benchmark {
    using System;
    using System.Diagnostics;

    public class BenchmarkProfile {
        private string Id { get; }

        private Stopwatch timer;

        public int NumBenchmarks { get; private set; }

        public BenchmarkProfile(string id) {
            Id = id;
            timer = new Stopwatch();
        }

        public void Start() {
            timer.Start();
        }

        public void Stop() {
            if (timer.IsRunning) {
                timer.Stop();
                ++NumBenchmarks;
            }
        }

        public TimeSpan GetElapsedTime() {
            return timer.Elapsed;
        }
    }
}
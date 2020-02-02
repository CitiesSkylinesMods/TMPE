namespace CSUtil.Commons.Benchmark {
    using System;
    using System.Diagnostics;
    using System.Reflection;

    public class Benchmark : IDisposable {
        private BenchmarkProfile profile;

#if !BENCHMARK
        /// <summary>
        /// Does nothing when #define BENCHMARK is not set
        /// </summary>
        public class NullBenchmark : IDisposable {
            public void Dispose() { }
        }

        private static NullBenchmark reusableNullBenchmark_ = new NullBenchmark();
#endif

        /// <summary>
        /// Creates Benchmark object if #define BENCHMARK is set, otherwise creates a NullBenchmark
        /// </summary>
        public static IDisposable MaybeCreateBenchmark(string id = null, string postfix = null) {
#if BENCHMARK
            return new Benchmark(id, postfix);
#else
            return reusableNullBenchmark_;
#endif
        }

        private Benchmark(string id = null, string postfix = null) {
            if (id == null) {
                StackFrame frame = new StackFrame(1);
                MethodBase method = frame.GetMethod();
                id = method.DeclaringType.Name + "#" + method.Name;
            }

            if (postfix != null) {
                id += "#" + postfix;
            }

            profile = BenchmarkProfileProvider.Instance.GetProfile(id);
            profile.Start();
        }

        public void Dispose() {
            profile.Stop();
        }
    }
}
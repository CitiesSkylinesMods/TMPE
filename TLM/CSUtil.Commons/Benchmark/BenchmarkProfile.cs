using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CSUtil.Commons.Benchmark {
	public class BenchmarkProfile {
		public string Id { get; private set; }
		private Stopwatch timer;
		public int NumBenchmarks { get; private set; } = 0;

		public BenchmarkProfile(string id) {
			Id = id;
			timer = new Stopwatch();
		}

		public void Start() {
			timer.Start();
		}

		public void Stop() {
			timer.Stop();
			++NumBenchmarks;
		}

		public TimeSpan GetElapsedTime() {
			return timer.Elapsed;
		}
	}
}

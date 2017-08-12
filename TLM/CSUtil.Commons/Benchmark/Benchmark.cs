using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CSUtil.Commons.Benchmark {
	public class Benchmark : IDisposable {
		private BenchmarkProfile profile;

		public Benchmark(string id = null, string postfix = null) {
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

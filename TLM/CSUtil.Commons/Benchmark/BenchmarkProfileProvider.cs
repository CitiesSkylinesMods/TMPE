using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSUtil.Commons.Benchmark {
	public class BenchmarkProfileProvider {
		public static readonly BenchmarkProfileProvider Instance = new BenchmarkProfileProvider();

		private IDictionary<string, BenchmarkProfile> Profiles = new Dictionary<string, BenchmarkProfile>();

		public BenchmarkProfile GetProfile(string id) {
			BenchmarkProfile profile = null;
			Profiles.TryGetValue(id, out profile);
			if (profile == null) {
				profile = new BenchmarkProfile(id);
				Profiles.Add(id, profile);
			}
			return profile;
		}

		public void ClearProfiles() {
			Profiles.Clear();
		}

		public string CreateReport() {
			string ret = "=== BENCHMARK REPORT ===\n";

			ret += "=== ORDERED BY TOTAL TIME ===\n";
			List<string> orderedKeys = new List<string>(Profiles.Keys);
			orderedKeys.Sort(delegate (string x, string y) {
				long xTicks = Profiles[x].GetElapsedTime().Ticks;
				long yTicks = Profiles[y].GetElapsedTime().Ticks;
				return yTicks.CompareTo(xTicks);
			});
			ret = CreateReport(ret, orderedKeys);

			ret += "\n=== ORDERED BY AVG. TIME ===\n";
			orderedKeys = new List<string>(Profiles.Keys);
			orderedKeys.Sort(delegate (string x, string y) {
				BenchmarkProfile xProfile = Profiles[x];
				BenchmarkProfile yProfile = Profiles[y];
				if (xProfile.NumBenchmarks <= 0 && yProfile.NumBenchmarks <= 0) {
					return 0;
				} else if (xProfile.NumBenchmarks > 0 && yProfile.NumBenchmarks <= 0) {
					return -1;
				} else if (xProfile.NumBenchmarks <= 0 && yProfile.NumBenchmarks > 0) {
					return 1;
				} else {
					float xAvg = (float)xProfile.GetElapsedTime().TotalMilliseconds / (float)xProfile.NumBenchmarks;
					float yAvg = (float)yProfile.GetElapsedTime().TotalMilliseconds / (float)yProfile.NumBenchmarks;
					return yAvg.CompareTo(xAvg);
				}
			});

			ret = CreateReport(ret, orderedKeys);
			return ret;
		}

		private string CreateReport(string ret, List<string> orderedKeys) {
			foreach (string key in orderedKeys) {
				BenchmarkProfile profile = Profiles[key];
				ret += $"\t{key}: {profile.GetElapsedTime()} ({profile.NumBenchmarks} benchmarks, avg. {(profile.NumBenchmarks <= 0 ? 0f : (float)profile.GetElapsedTime().TotalMilliseconds / (float)profile.NumBenchmarks)})\n";
			}

			return ret;
		}
	}
}

namespace CSUtil.Commons.Benchmark {
    using System.Collections.Generic;
    using System.Text;

    public class BenchmarkProfileProvider {
        public static readonly BenchmarkProfileProvider Instance = new BenchmarkProfileProvider();

        private IDictionary<string, BenchmarkProfile> Profiles =
            new Dictionary<string, BenchmarkProfile>();

        public BenchmarkProfile GetProfile(string id) {
            BenchmarkProfile profile;
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
            var reportSb = new StringBuilder();
            reportSb.Append("=== BENCHMARK REPORT ===\n");
            reportSb.Append("=== ORDERED BY TOTAL TIME ===\n");

            var orderedKeys = new List<string>(Profiles.Keys);
            orderedKeys.Sort(
                (x, y) => {
                    long xTicks = Profiles[x].GetElapsedTime().Ticks;
                    long yTicks = Profiles[y].GetElapsedTime().Ticks;
                    return yTicks.CompareTo(xTicks);
                });

            CreateReport(reportSb, orderedKeys);
            reportSb.Append("\n=== ORDERED BY AVG. TIME ===\n");

            orderedKeys = new List<string>(Profiles.Keys);
            orderedKeys.Sort(
                (x, y) => {
                    BenchmarkProfile xProfile = Profiles[x];
                    BenchmarkProfile yProfile = Profiles[y];
                    if (xProfile.NumBenchmarks <= 0 && yProfile.NumBenchmarks <= 0) {
                        return 0;
                    }

                    if (xProfile.NumBenchmarks > 0 && yProfile.NumBenchmarks <= 0) {
                        return -1;
                    }

                    if (xProfile.NumBenchmarks <= 0 && yProfile.NumBenchmarks > 0) {
                        return 1;
                    }

                    float xAvg = (float)xProfile.GetElapsedTime().TotalMilliseconds /
                                 xProfile.NumBenchmarks;
                    float yAvg = (float)yProfile.GetElapsedTime().TotalMilliseconds /
                                 yProfile.NumBenchmarks;
                    return yAvg.CompareTo(xAvg);
                });

            CreateReport(reportSb, orderedKeys);
            return reportSb.ToString();
        }

        /// <summary>
        /// Adds ordered report lines to the stringbuilder
        /// </summary>
        /// <param name="reportSb">StringBuilder where report lines are added</param>
        /// <param name="orderedKeys">The data</param>
        private void CreateReport(StringBuilder reportSb, List<string> orderedKeys) {
            foreach (string key in orderedKeys) {
                BenchmarkProfile profile = Profiles[key];
                reportSb.AppendFormat(
                        "\t{0}: {1} ({2} benchmarks, avg. {3})\n",
                        key,
                        profile.GetElapsedTime(),
                        profile.NumBenchmarks,
                        profile.NumBenchmarks <= 0
                            ? 0f
                            : (float)profile.GetElapsedTime().TotalMilliseconds /
                              (float)profile.NumBenchmarks);
            }
        }
    }
}
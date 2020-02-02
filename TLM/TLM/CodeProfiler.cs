namespace TrafficManager {
#if TRACE
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using ColossalFramework;
    using CSUtil.Commons;

    public class CodeProfiler
        : Singleton<CodeProfiler>
    {
        private readonly Dictionary<string, Stopwatch> watches_ = new Dictionary<string, Stopwatch>();
        private readonly Dictionary<string, ulong> intervals_ = new Dictionary<string, ulong>();

        internal void Start(string name1) {
            Validate(name);
            try {
                Monitor.Enter(watches_);
                watches_[name].Start();
            } finally {
                Monitor.Exit(watches_);
            }
        }

        internal void Stop(string name) {
            Validate(name);
            try {
                Monitor.Enter(watches_);
                watches_[name].Stop();
                ++intervals_[name];
            } finally {
                Monitor.Exit(watches_);
            }
        }

        internal void Reset(string name) {
            Validate(name);
            try {
                Monitor.Enter(watches_);
                watches_[name].Reset();
            } finally {
                Monitor.Exit(watches_);
            }
        }

        internal ulong ElapsedNano(string name) {
            Validate(name);
            try {
                Monitor.Enter(watches_);
                return (ulong)(watches_[name].Elapsed.TotalMilliseconds * 1000d * 1000d);
            } finally {
                Monitor.Exit(watches_);
            }
        }

        private void Validate(string name) {
            try {
                Monitor.Enter(watches_);
                if (!watches_.ContainsKey(name)) {
                    watches_[name] = new Stopwatch();
                    intervals_[name] = 0;
                }
            } finally {
                Monitor.Exit(watches_);
            }
        }

        internal void OnLevelUnloading() {
            try {
                Monitor.Enter(watches_);

                foreach (KeyValuePair<string, Stopwatch> we in watches_) {
                    Log._DebugFormat(
                        "Stopwatch {0}: Total elapsed ns: {1} ms: {2} s: {3} min: {4} h: {5} " +
                        "intervals: {6} avg. ns: {7}",
                        we.Key,
                        ElapsedNano(we.Key),
                        ElapsedNano(we.Key) / 1000u / 1000u,
                        ElapsedNano(we.Key) / 1000u / 1000u / 1000u,
                        ElapsedNano(we.Key) / 1000u / 1000u / 1000u / 60u,
                        ElapsedNano(we.Key) / 1000u / 1000u / 1000u / 60u / 60u,
                        intervals_[we.Key],
                        intervals_[we.Key] > 0
                            ? (string.Empty + (ElapsedNano(we.Key) / intervals_[we.Key]))
                            : "n/a");
                }

                watches_.Clear();
                intervals_.Clear();
            } finally {
                Monitor.Exit(watches_);
            }
        }
    }
#endif
}
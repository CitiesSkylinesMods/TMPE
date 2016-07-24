using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace TrafficManager {
#if TRACE
	public class CodeProfiler : Singleton<CodeProfiler> {
		private Dictionary<string, Stopwatch> watches = new Dictionary<string, Stopwatch>();
		private Dictionary<string, ulong> intervals = new Dictionary<string, ulong>();

		internal void Start(string name) {
			Validate(name);
			try {
				Monitor.Enter(watches);
				watches[name].Start();
			} finally {
				Monitor.Exit(watches);
			}
		}

		internal void Stop(string name) {
			Validate(name);
			try {
				Monitor.Enter(watches);
				watches[name].Stop();
				++intervals[name];
			} finally {
				Monitor.Exit(watches);
			}
		}

		internal void Reset(string name) {
			Validate(name);
			try {
				Monitor.Enter(watches);
				watches[name].Reset();
			} finally {
				Monitor.Exit(watches);
			}
		}

		internal ulong ElapsedNano(string name) {
			Validate(name);
			try {
				Monitor.Enter(watches);
				return (ulong)(watches[name].Elapsed.TotalMilliseconds * 1000d * 1000d);
			} finally {
				Monitor.Exit(watches);
			}
		}

		private void Validate(string name) {
			try {
				Monitor.Enter(watches);
				if (!watches.ContainsKey(name)) {
					watches[name] = new Stopwatch();
					intervals[name] = 0;
				}
			} finally {
				Monitor.Exit(watches);
			}
		}

		internal void OnLevelUnloading() {
			try {
				Monitor.Enter(watches);

				foreach (KeyValuePair<string, Stopwatch> we in watches) {
					Log._Debug($"Stopwatch {we.Key}: Total elapsed ns: {ElapsedNano(we.Key)} ms: {ElapsedNano(we.Key) / 1000u / 1000u} s: {ElapsedNano(we.Key) / 1000u / 1000u / 1000u} min: {ElapsedNano(we.Key) / 1000u / 1000u / 1000u / 60u} h: {ElapsedNano(we.Key) / 1000u / 1000u / 1000u / 60u / 60u} intervals: {intervals[we.Key]} avg. ns: {(intervals[we.Key] > 0 ? ("" + (ElapsedNano(we.Key) / intervals[we.Key])) : "n/a")}");
                }

				watches.Clear();
				intervals.Clear();
			} finally {
				Monitor.Exit(watches);
			}
		}
	}
#endif
}

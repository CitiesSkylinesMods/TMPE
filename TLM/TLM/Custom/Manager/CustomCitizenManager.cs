using ColossalFramework.Math;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using UnityEngine;

namespace TrafficManager.Custom.Manager {
	public class CustomCitizenManager : CitizenManager {

		public void CustomReleaseCitizenInstance(ushort instanceId) {
#if BENCHMARK
			using (var bm = new Benchmark(null, "OnReleaseInstance")) {
#endif
				ExtCitizenInstanceManager.Instance.OnReleaseInstance(instanceId);
#if BENCHMARK
			}
#endif
			this.ReleaseCitizenInstanceImplementation(instanceId, ref this.m_instances.m_buffer[(int)instanceId]);
		}

		public void CustomReleaseCitizen(uint citizenId) {
#if BENCHMARK
			using (var bm = new Benchmark(null, "OnReleaseCitizen")) {
#endif
				ExtCitizenManager.Instance.OnReleaseCitizen(citizenId);
#if BENCHMARK
			}
#endif
			this.ReleaseCitizenImplementation(citizenId, ref this.m_citizens.m_buffer[citizenId]);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ReleaseCitizenInstanceImplementation(ushort instanceId, ref CitizenInstance instanceData) {
			Log.Error("CustomCitizenManager.ReleaseCitizenInstanceImplementation called.");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ReleaseCitizenImplementation(uint citizenId, ref Citizen citizen) {
			Log.Error("CustomCitizenManager.ReleaseCitizenImplementation called.");
		}
	}
}

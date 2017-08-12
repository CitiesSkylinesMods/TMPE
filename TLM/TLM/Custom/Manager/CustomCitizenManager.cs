using ColossalFramework.Math;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
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

		private void ReleaseCitizenInstanceImplementation(ushort instanceId, ref CitizenInstance instanceData) {
			Log.Error("CustomCitizenManager.ReleaseCitizenInstanceImplementation called.");
		}
	}
}

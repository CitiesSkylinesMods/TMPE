#define USEPATHWAITCOUNTERx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager {
	public class ExtCitizenInstanceManager : AbstractCustomManager {
		public static ExtCitizenInstanceManager Instance { get; private set; } = null;

		internal void OnReleaseInstance(ushort instanceId) {
			GetExtInstance(instanceId).Reset();
		}

		static ExtCitizenInstanceManager() {
			Instance = new ExtCitizenInstanceManager();
		}

		/// <summary>
		/// All additional data for citizen instance
		/// </summary>
		private ExtCitizenInstance[] ExtInstances = null;

		private ExtCitizenInstanceManager() {
			ExtInstances = new ExtCitizenInstance[CitizenManager.MAX_INSTANCE_COUNT];
			for (uint i = 0; i < CitizenManager.MAX_INSTANCE_COUNT; ++i) {
				ExtInstances[i] = new ExtCitizenInstance((ushort)i);
			}
		}

		/// <summary>
		/// Retrieves the additional citizen instance data for the given instance id.
		/// </summary>
		/// <param name="instanceId"></param>
		/// <returns>the additional citizen instance data</returns>
		public ExtCitizenInstance GetExtInstance(ushort instanceId) {
			return ExtInstances[instanceId];
		}
		
		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			Reset();
		}

		internal void Reset() {
			for (int i = 0; i < ExtInstances.Length; ++i)
				ExtInstances[i].Reset();
		}
	}
}

using ColossalFramework.Math;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using UnityEngine;

namespace TrafficManager.Custom.Manager {
	public class CustomCitizenManager : CitizenManager {

		public void CustomReleaseCitizenInstance(ushort instanceId) {
			ExtCitizenInstanceManager.Instance.OnReleaseInstance(instanceId);
			this.ReleaseCitizenInstanceImplementation(instanceId, ref this.m_instances.m_buffer[(int)instanceId]);
		}

		private void ReleaseCitizenInstanceImplementation(ushort instanceId, ref CitizenInstance instanceData) {
			Log.Error("CustomCitizenManager.ReleaseCitizenInstanceImplementation called.");
		}
	}
}

using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using UnityEngine;

namespace TrafficManager.Patch._CitizenManager {
	[HarmonyPatch(typeof(CitizenManager), "ReleaseCitizenInstance")]
	public static class ReleaseCitizenInstancePatch {
		/// <summary>
		/// Notifies the extended citizen instance manager about a released citizen instance.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(CitizenManager __instance, ushort instance) {
			Constants.ManagerFactory.ExtCitizenInstanceManager.OnReleaseInstance(instance);
		}
	}
}

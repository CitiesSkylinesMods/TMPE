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
	[HarmonyPatch(typeof(CitizenManager), "ReleaseCitizen")]
	public static class ReleaseCitizenPatch {
		/// <summary>
		/// Notifies the extended citizen manager about a released citizen.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(CitizenManager __instance, uint citizen) {
			Constants.ManagerFactory.ExtCitizenManager.OnReleaseCitizen(citizen);
		}
	}
}

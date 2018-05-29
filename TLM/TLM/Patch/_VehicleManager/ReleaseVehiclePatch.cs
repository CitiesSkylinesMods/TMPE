using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Patch._VehicleManager {
	[HarmonyPatch(typeof(VehicleManager), "ReleaseVehicle")]
	public static class ReleaseVehiclePatch {
		/// <summary>
		/// Notifies the vehicle state manager about a released vehicle.
		/// </summary>
		[HarmonyPrefix]
		public static void Prefix(VehicleManager __instance, ushort vehicle) {
			Constants.ManagerFactory.VehicleStateManager.OnReleaseVehicle(vehicle, ref __instance.m_vehicles.m_buffer[vehicle]);
		}
	}
}

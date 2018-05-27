using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Patched._VehicleManager {
	[HarmonyPatch(typeof(VehicleManager), "ReleaseVehicle")]
	public static class ReleaseVehiclePatch {
		[HarmonyPrefix]
		public static void Prefix(VehicleManager __instance, ushort vehicle) {
			Log._Debug($"ReleaseVehiclePatch.Prefix({vehicle}) called.");
			Constants.ManagerFactory.VehicleStateManager.OnReleaseVehicle(vehicle, ref __instance.m_vehicles.m_buffer[vehicle]);
		}
	}
}

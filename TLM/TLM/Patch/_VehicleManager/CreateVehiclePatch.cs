using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Patch._VehicleManager {
	[HarmonyPatch(typeof(VehicleManager), "CreateVehicle")]
	public static class CreateVehiclePatch {
		/// <summary>
		/// Restricts the set of vehicle types allowed to spawn before hitting the vehicle limit.
		/// </summary>
		[HarmonyPrefix]
		public static bool Prefix(VehicleManager __instance, ref ushort vehicle, VehicleInfo info) {
			if (__instance.m_vehicleCount > Constants.ServiceFactory.VehicleService.MaxVehicleCount - 5) {
				// prioritize service vehicles and public transport when hitting the vehicle limit
				ItemClass.Service service = info.GetService();
				if (service == ItemClass.Service.Residential || service == ItemClass.Service.Industrial || service == ItemClass.Service.Commercial || service == ItemClass.Service.Office) {
					vehicle = 0;
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Notifies the vehicle state manager about a created vehicle.
		/// </summary>
		[HarmonyPostfix]
		public static void Postfix(VehicleManager __instance, bool __result, ref ushort vehicle) {
			if (__result) {
				Constants.ManagerFactory.ExtVehicleManager.OnCreateVehicle(vehicle, ref __instance.m_vehicles.m_buffer[vehicle]); // NON-STOCK CODE
			}
		}
	}
}

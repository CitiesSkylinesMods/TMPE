using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Patch._Vehicle {
    // TODO [issue #864] replace custom AI with harmony patch when possible.

    //[HarmonyPatch(typeof(Vehicle), "Spawn")]
    //public static class SpawnPatch {
    //	/// <summary>
    //	/// Notifies the vehicle state manager about a spawned vehicle.
    //	/// </summary>
    //	[HarmonyPostfix]
    //	public static void Postfix(ushort vehicleID) {
    //		Constants.ManagerFactory.VehicleStateManager.OnSpawnVehicle(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]);
    //	}
    //}
}
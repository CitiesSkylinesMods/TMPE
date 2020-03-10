using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Patch._Vehicle {
    // TODO this does currently not work with Harmony v1.1

    //[HarmonyPatch(typeof(Vehicle), "Unspawn")]
    //public static class UnspawnPatch {
    //	/// <summary>
    //	/// Notifies the vehicle state manager about a despawned vehicle.
    //	/// </summary>
    //	//[HarmonyPrefix]
    //	public static void Prefix(ushort vehicleID) {
    //		Constants.ManagerFactory.VehicleStateManager.OnDespawnVehicle(vehicleID, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleID]);
    //	}
    //}
}
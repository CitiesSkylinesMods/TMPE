using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Enums;
using UnityEngine;

namespace TrafficManager.Patch._CarAI {
	// [Harmony] Manually patched because struct references are used
	public class CheckOtherVehiclePatch {
		/// <summary>
		/// Determines whether a vehicle is present at a given position.
		/// </summary>
		public static bool Prefix(
			ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics, ref ushort __result) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleID);

			if (debug) {
				Log._Debug($"CheckOtherVehiclePatch.Prefix({vehicleID}, {otherID}) called.");
			}
#endif

			IExtVehicleManager extVehMan = Constants.ManagerFactory.ExtVehicleManager;
			if (
				Options.emergencyAI &&
				(extVehMan.ExtVehicles[otherID].flags & Traffic.Enums.ExtVehicleFlags.Stopped) != ExtVehicleFlags.None &&
				(otherData.GetLastFramePosition() - extVehMan.ExtVehicles[otherID].stopPosition).sqrMagnitude < 0.1f
				//otherData.GetLastFrameVelocity().sqrMagnitude < 0.01f
			) {
#if DEBUG
				if (debug) {
					Log._Debug($"CheckOtherVehiclePatch.Prefix({vehicleID}, {otherID}): Other vehicle is stopped. ignoring");
				}
#endif

				__result = otherData.m_nextGridVehicle;
				return false;
			}
			
			return true;
		}
	}
}

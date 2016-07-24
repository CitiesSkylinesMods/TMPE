using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class VehicleStateManager {
		/// <summary>
		/// Known vehicles and their current known positions. Index: vehicle id
		/// </summary>
		private static VehicleState[] VehicleStates = null;

		static VehicleStateManager() {
			VehicleStates = new VehicleState[VehicleManager.MAX_VEHICLE_COUNT];
			for (ushort i = 0; i < VehicleManager.MAX_VEHICLE_COUNT; ++i) {
				VehicleStates[i] = new VehicleState(i);
			}
		}

		public static VehicleState GetVehicleState(ushort vehicleId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.GetVehicleState");
#endif
			VehicleState ret = VehicleStates[vehicleId];
			if (ret.Valid) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.GetVehicleState");
#endif
				return ret;
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.GetVehicleState");
#endif
			return null;
		}

		internal static VehicleState _GetVehicleState(ushort vehicleId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager._GetVehicleState");
#endif
			VehicleState ret = VehicleStates[vehicleId];
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager._GetVehicleState");
#endif
			return ret;
		}

		/*public static VehiclePosition GetVehiclePosition(ushort vehicleId) {
			VehicleState state = GetVehicleState(vehicleId);
			if (state == null)
				return null;
			return state.GetCurrentPosition();
		}*/

		internal static void OnLevelUnloading() {
			for (int i = 0; i < VehicleStates.Length; ++i)
				VehicleStates[i].Valid = false;
		}

		internal static void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.UpdateVehiclePos(1)");
#endif
			VehicleStates[vehicleId].UpdatePosition(ref vehicleData);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.UpdateVehiclePos(1)");
#endif
		}

		internal static void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.UpdateVehiclePos(2)");
#endif
			VehicleStates[vehicleId].UpdatePosition(ref vehicleData, ref curPos, ref nextPos);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.UpdateVehiclePos(2)");
#endif
		}

		internal static void LogTraffic(ushort vehicleId, ref Vehicle vehicleData, bool logSpeed) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.LogTraffic");
#endif
			VehicleState state = GetVehicleState(vehicleId);
			if (state == null)
				return;

			ushort length = (ushort)Mathf.RoundToInt(vehicleData.CalculateTotalLength(vehicleId));
			ushort? speed = logSpeed ? (ushort?)Mathf.RoundToInt(vehicleData.GetLastFrameData().m_velocity.magnitude) : null;

			state.ProcessCurrentPathPosition(ref vehicleData, delegate (ref PathUnit.Position pos) {
				CustomRoadAI.AddTraffic(pos.m_segment, pos.m_lane, length, speed);
			});
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.LogTraffic");
#endif
		}

		internal static void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.OnReleaseVehicle");
#endif
			VehicleState state = _GetVehicleState(vehicleId);
			state.Valid = false;
			state.VehicleType = ExtVehicleType.None;
			//VehicleStates[vehicleId].Reset();
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.OnReleaseVehicle");
#endif
		}

		internal static void OnPathFindReady(ushort vehicleId, ref Vehicle vehicleData) {
			//Log._Debug($"VehicleStateManager: OnPathFindReady({vehicleId})");
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.OnPathFindReady");
#endif
			VehicleStates[vehicleId].OnPathFindReady(ref vehicleData);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.OnPathFindReady");
#endif
		}

		internal static void InitAllVehicles() {
			//Log._Debug("VehicleStateManager: InitAllVehicles()");
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			for (ushort i = 0; i < vehicleManager.m_vehicles.m_size; ++i) {
				try {
					OnPathFindReady(i, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[i]);
				} catch (Exception e) {
					Log.Error("VehicleStateManager: InitAllVehicles Error: " + e.ToString());
				}
			}
		}

		internal static ExtVehicleType? DetermineVehicleType(ushort vehicleId, ref Vehicle vehicleData) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.DetermineVehicleType");
#endif
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.DetermineVehicleType");
#endif
				VehicleStates[vehicleId].VehicleType = ExtVehicleType.Emergency;
				return ExtVehicleType.Emergency;
			}

			VehicleAI ai = vehicleData.Info.m_vehicleAI;
#if TRACE
			Singleton<CodeProfiler>.instance.Start("VehicleStateManager.DetermineVehicleTypeFromAIType");
#endif
			ExtVehicleType? ret = DetermineVehicleTypeFromAIType(ai, false);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.DetermineVehicleTypeFromAIType");
#endif
			VehicleStates[vehicleId].VehicleType = ret != null ? (ExtVehicleType)ret : ExtVehicleType.None;
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("VehicleStateManager.DetermineVehicleType");
#endif
			return ret;
		}

		private static ExtVehicleType? DetermineVehicleTypeFromAIType(VehicleAI ai, bool emergencyOnDuty) {
			if (emergencyOnDuty)
				return ExtVehicleType.Emergency;

			switch (ai.m_info.m_vehicleType) {
				case VehicleInfo.VehicleType.Bicycle:
					return ExtVehicleType.Bicycle;
				case VehicleInfo.VehicleType.Car:
					if (ai is PassengerCarAI)
						return ExtVehicleType.PassengerCar;
					if (ai is AmbulanceAI || ai is FireTruckAI || ai is PoliceCarAI || ai is HearseAI || ai is GarbageTruckAI || ai is MaintenanceTruckAI || ai is SnowTruckAI) {
						return ExtVehicleType.Service;
					}
					if (ai is CarTrailerAI)
						return ExtVehicleType.None;
					if (ai is BusAI)
						return ExtVehicleType.Bus;
					if (ai is TaxiAI)
						return ExtVehicleType.Taxi;
					if (ai is CargoTruckAI)
						return ExtVehicleType.CargoTruck;
					break;
				case VehicleInfo.VehicleType.Metro:
				case VehicleInfo.VehicleType.Train:
					if (ai is PassengerTrainAI)
						return ExtVehicleType.PassengerTrain;
					//if (ai is CargoTrainAI)
					return ExtVehicleType.CargoTrain;
				//break;
				case VehicleInfo.VehicleType.Tram:
					return ExtVehicleType.Tram;
				case VehicleInfo.VehicleType.Ship:
					if (ai is PassengerShipAI)
						return ExtVehicleType.PassengerShip;
					//if (ai is CargoShipAI)
					return ExtVehicleType.CargoShip;
				//break;
				case VehicleInfo.VehicleType.Plane:
					//if (ai is PassengerPlaneAI)
					return ExtVehicleType.PassengerPlane;
					//break;
			}
#if DEBUGVSTATE
			Log._Debug($"Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
#endif
			return null;
		}
	}
}

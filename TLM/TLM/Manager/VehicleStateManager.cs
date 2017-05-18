#define USEPATHWAITCOUNTERx

using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Manager {
	public class VehicleStateManager : AbstractCustomManager {
		public static readonly VehicleStateManager Instance = new VehicleStateManager();

		public const VehicleInfo.VehicleType RECKLESS_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		public const float MIN_SPEED = 8f * 0.2f; // 10 km/h
		public const float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h
		public const float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h
		public const float WET_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float WET_ROADS_FACTOR = 0.75f;
		public const float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float BROKEN_ROADS_FACTOR = 0.75f;

		/// <summary>
		/// Determines if vehicles should be cleared
		/// </summary>
		private static bool ClearTrafficRequested = false;

		static VehicleStateManager() {
			Instance = new VehicleStateManager();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"ClearTrafficRequested = {ClearTrafficRequested}");
			Log._Debug($"Vehicle states:");
			for (int i = 0; i < VehicleStates.Length; ++i) {
				if (VehicleStates[i] == null || !VehicleStates[i].Valid) {
					continue;
				}
				Log._Debug($"Vehicle {i}: {VehicleStates[i]}");
			}
		}

		/// <summary>
		/// Known vehicles and their current known positions. Index: vehicle id
		/// </summary>
		private VehicleState[] VehicleStates = null;

		private VehicleStateManager() {
			VehicleStates = new VehicleState[VehicleManager.MAX_VEHICLE_COUNT];
			for (uint i = 0; i < VehicleManager.MAX_VEHICLE_COUNT; ++i) {
				VehicleStates[i] = new VehicleState((ushort)i);
			}
		}

		/// <summary>
		/// Determines if the given vehicle is driven by a reckless driver
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <returns></returns>
		public bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0)
				return true;
			if (Options.evacBussesMayIgnoreRules && vehicleData.Info.GetService() == ItemClass.Service.Disaster)
				return true;
			if (Options.recklessDrivers == 3)
				return false;

			return ((vehicleData.Info.m_vehicleType & RECKLESS_VEHICLE_TYPES) != VehicleInfo.VehicleType.None) && (uint)vehicleId % (Options.getRecklessDriverModulo()) == 0;
		}

		/// <summary>
		/// Determines the state of the given vehicle.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <returns>the vehicle state if the state is valid, null otherwise</returns>
		public VehicleState GetVehicleState(ushort vehicleId) {
			VehicleState ret = VehicleStates[vehicleId];
			if (ret.Valid) {
				return ret;
			}
			return null;
		}

		/// <summary>
		/// Determines the state of the given vehicle.
		/// The returned vehicle state is not necessarily valid.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <returns>the vehicle state</returns>
		internal VehicleState _GetVehicleState(ushort vehicleId) {
			VehicleState ret = VehicleStates[vehicleId];
			return ret;
		}

		internal void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData) {
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			if (reversed) {
				ushort frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
				VehicleStates[frontVehicleId].UpdatePosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId]);
			} else {
				VehicleStates[vehicleId].UpdatePosition(ref vehicleData);
			}
		}

		internal void UpdateTrailerPos(ushort trailerId, ref Vehicle trailerData, ushort vehicleId, ref Vehicle vehicleData) {
			VehicleStates[trailerId].UpdatePosition(ref trailerData, vehicleId, ref vehicleData);
		}

		internal void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			if (reversed) {
				ushort frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
				VehicleStates[frontVehicleId].UpdatePosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], ref curPos, ref nextPos);
			} else {
				VehicleStates[vehicleId].UpdatePosition(ref vehicleData, ref curPos, ref nextPos);
			}
		}

		internal void LogTraffic(ushort vehicleId, ref Vehicle vehicleData, bool logSpeed) {
			VehicleState state = GetVehicleState(vehicleId);
			if (state == null)
				return;

			ushort length = (ushort)state.TotalLength;
			if (length == 0)
				return;
			ushort? speed = logSpeed ? (ushort?)Mathf.RoundToInt(vehicleData.GetLastFrameData().m_velocity.magnitude) : null;

			state.ProcessCurrentPathPosition(ref vehicleData, delegate (ref PathUnit.Position pos) {
				TrafficMeasurementManager.Instance.AddTraffic(pos.m_segment, pos.m_lane, length, speed);
			});
		}

		internal void OnReleaseVehicle(ushort vehicleId) {
#if DEBUG
			//Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}) called.");
#endif
			VehicleState state = _GetVehicleState(vehicleId);
			state.VehicleType = ExtVehicleType.None;
			ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
			if (driverExtInstance != null) {
				//driverExtInstance.FailedParkingAttempts = 0;
				driverExtInstance.Reset();
			}
			//state.DriverInstanceId = 0;
#if USEPATHWAITCOUNTER
			state.PathWaitCounter = 0;
#endif
			state.Valid = false;
			//VehicleStates[vehicleId].Reset();
		}

		internal void OnVehicleSpawned(ushort vehicleId, ref Vehicle vehicleData) {
			//Log._Debug($"VehicleStateManager: OnPathFindReady({vehicleId})");
			VehicleStates[vehicleId].OnVehicleSpawned(ref vehicleData);
		}

		internal void InitAllVehicles() {
			Log._Debug("VehicleStateManager: InitAllVehicles()");
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				for (uint vehicleId = 0; vehicleId < VehicleManager.MAX_VEHICLE_COUNT; ++vehicleId) {
					if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags & Vehicle.Flags.Created) == 0)
						continue;

					try {
						DetermineVehicleType((ushort)vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]);
						OnVehicleSpawned((ushort)vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]);
					} catch (Exception e) {
						Log.Error("VehicleStateManager: InitAllVehicles Error: " + e.ToString());
					}
				}
			}
		}

		internal ExtVehicleType? DetermineVehicleType(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
				VehicleStates[vehicleId].VehicleType = ExtVehicleType.Emergency;
				return ExtVehicleType.Emergency;
			}

			VehicleAI ai = vehicleData.Info.m_vehicleAI;
			ExtVehicleType? ret = DetermineVehicleTypeFromAIType(ai, false);

			if (ret != null) {
				VehicleStates[vehicleId].VehicleType = (ExtVehicleType)ret;
				if ((ExtVehicleType)ret == ExtVehicleType.CargoTruck) {
					VehicleStates[vehicleId].HeavyVehicle = ((CargoTruckAI)ai).m_isHeavyVehicle;
				}
			} else {
				VehicleStates[vehicleId].VehicleType = ExtVehicleType.None;
				VehicleStates[vehicleId].HeavyVehicle = false;
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Could not determine vehicle type of {vehicleId}. Info={vehicleData.Info?.name}");
#endif
			}
			return ret;
		}

		private ExtVehicleType? DetermineVehicleTypeFromAIType(VehicleAI ai, bool emergencyOnDuty) {
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
				case VehicleInfo.VehicleType.Monorail:
					if (ai is CargoTrainAI)
						return ExtVehicleType.CargoTrain;
					return ExtVehicleType.PassengerTrain;
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
				case VehicleInfo.VehicleType.Helicopter:
					//if (ai is PassengerPlaneAI)
					return ExtVehicleType.Helicopter;
				//break;
				case VehicleInfo.VehicleType.Ferry:
					return ExtVehicleType.Ferry;
				case VehicleInfo.VehicleType.Blimp:
					return ExtVehicleType.Blimp;
				case VehicleInfo.VehicleType.CableCar:
					return ExtVehicleType.CableCar;
			}
#if DEBUGVSTATE
			Log._Debug($"Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
#endif
			return null;
		}

		internal void SimulationStep() { // TODO refactor
			try {
				if (ClearTrafficRequested) {
					ClearTraffic();
					ClearTrafficRequested = false;
				}
			} finally { }
		}

		internal void ClearTraffic() {
			try {
				Monitor.Enter(Singleton<VehicleManager>.instance);

				for (ushort i = 0; i < Singleton<VehicleManager>.instance.m_vehicles.m_size; ++i) {
					if (
						(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[i].m_flags & Vehicle.Flags.Created) != 0)
						Singleton<VehicleManager>.instance.ReleaseVehicle(i);
				}

				TrafficMeasurementManager.Instance.ResetTrafficStats();
			} catch (Exception ex) {
				Log.Error($"Error occured while trying to clear traffic: {ex.ToString()}");
			} finally {
				Monitor.Exit(Singleton<VehicleManager>.instance);
			}
		}

		internal void RequestClearTraffic() {
			ClearTrafficRequested = true;
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < VehicleStates.Length; ++i)
				VehicleStates[i].Valid = false;
		}

		public override void OnAfterLoadData() {
			base.OnAfterLoadData();
			InitAllVehicles();
		}
	}
}

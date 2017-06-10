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

		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail;
		public const VehicleInfo.VehicleType RECKLESS_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		public const float MIN_SPEED = 8f * 0.2f; // 10 km/h
		public const float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h
		public const float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h
		public const float WET_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float WET_ROADS_FACTOR = 0.75f;
		public const float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float BROKEN_ROADS_FACTOR = 0.75f;

		static VehicleStateManager() {
			Instance = new VehicleStateManager();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Vehicle states:");
			for (int i = 0; i < VehicleStates.Length; ++i) {
				if (!VehicleStates[i].spawned) {
					continue;
				}
				Log._Debug($"Vehicle {i}: {VehicleStates[i]}");
			}
		}

		/// <summary>
		/// Known vehicles and their current known positions. Index: vehicle id
		/// </summary>
		internal VehicleState[] VehicleStates = null;

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
		/*public VehicleState GetVehicleState(ushort vehicleId) {
			VehicleState ret = VehicleStates[vehicleId];
			if (ret.Valid) {
				return ret;
			}
			return null;
		}*/

		/// <summary>
		/// Determines the state of the given vehicle.
		/// The returned vehicle state is not necessarily valid.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <returns>the vehicle state</returns>
		/*internal VehicleState _GetVehicleState(ushort vehicleId) {
			VehicleState ret = VehicleStates[vehicleId];
			return ret;
		}*/

		/*internal void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData) {
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			if (reversed) {
				ushort frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
				VehicleStates[frontVehicleId].UpdatePosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId]);
			} else {
				VehicleStates[vehicleId].UpdatePosition(ref vehicleData);
			}
		}*/

		/*internal void UpdateTrailerPos(ushort trailerId, ref Vehicle trailerData, ushort vehicleId, ref Vehicle vehicleData) {
			VehicleStates[trailerId].UpdatePosition(ref trailerData, vehicleId, ref vehicleData);
		}*/

		/*internal void UpdateVehiclePos(ushort vehicleId, ref Vehicle vehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			ushort frontVehicleId = GetFrontVehicleId(vehicleId, ref vehicleData);
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[9])
				Log._Debug($"VehicleStateManager.UpdateVehiclePos({vehicleId}): calling UpdatePosition for front vehicle {frontVehicleId}");
#endif
			VehicleStates[frontVehicleId].UpdatePosition(ref vehicleData, ref curPos, ref nextPos);
		}*/

		internal void LogTraffic(ushort vehicleId, ref Vehicle vehicleData, ushort segmentId, byte laneIndex, bool logSpeed) {
			ushort length = (ushort)VehicleStates[vehicleId].totalLength;
			if (length == 0)
				return;
			ushort? speed = logSpeed ? (ushort?)Mathf.RoundToInt(vehicleData.GetLastFrameData().m_velocity.magnitude) : null;

			TrafficMeasurementManager.Instance.AddTraffic(segmentId, laneIndex, length, speed);
		}

		internal void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

			ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (leading)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();

				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
			}

			connectedVehicleId = vehicleId;
			while (true) {
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

				if (connectedVehicleId == 0) {
					break;
				}

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();
			}
		}

		internal void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != (Vehicle.Flags.Created) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

			ushort frontVehicleId = GetFrontVehicleId(vehicleId, ref vehicleData);
			if (frontVehicleId != vehicleId) {
				return;
			} else {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): calling OnCreate for vehicle {vehicleId}");
#endif
				VehicleStates[vehicleId].OnCreate(ref vehicleData);
			}
		}

		internal void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) != (Vehicle.Flags.Created | Vehicle.Flags.Spawned) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None || vehicleData.m_path <= 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}, path: {vehicleData.m_path}");
#endif
				return;
			}

			ushort frontVehicleId = GetFrontVehicleId(vehicleId, ref vehicleData);
			if (frontVehicleId != vehicleId) {
				return;
			} else {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[9])
					Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): calling OnSpawn for this vehicle");
#endif
				VehicleStates[vehicleId].OnSpawn(ref vehicleData);
			}
		}

		internal void InitAllVehicles() {
			Log._Debug("VehicleStateManager: InitAllVehicles()");
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				for (ushort vehicleId = 0; vehicleId < VehicleManager.MAX_VEHICLE_COUNT; ++vehicleId) {
					Services.VehicleService.ProcessVehicle(vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
						if ((vehicle.m_flags & Vehicle.Flags.Created) == 0) {
							return true;
						}

						OnCreateVehicle(vehicleId, ref vehicle);

						if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
							return true;
						}

						OnSpawnVehicle(vehicleId, ref vehicle);

						return true;
					});
				}
			}
		}

		public ushort GetFrontVehicleId(ushort vehicleId, ref Vehicle vehicleData) {
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			ushort frontVehicleId = vehicleId;
			if (reversed) {
				frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				frontVehicleId = vehicleData.GetFirstVehicle(vehicleId);
			}

			return frontVehicleId;
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < VehicleStates.Length; ++i) {
				VehicleStates[i].OnDespawn();
			}
		}

		public override void OnAfterLoadData() {
			base.OnAfterLoadData();
			InitAllVehicles();
		}
	}
}

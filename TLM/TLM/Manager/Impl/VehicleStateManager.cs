using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	public class VehicleStateManager : AbstractCustomManager, IVehicleStateManager {
		public static readonly VehicleStateManager Instance = new VehicleStateManager();

		public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail;

		/// <summary>
		/// Known vehicles and their current known positions. Index: vehicle id
		/// </summary>
		internal VehicleState[] VehicleStates = null;

		static VehicleStateManager() {
			Instance = new VehicleStateManager();
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Vehicle states:");
			for (int i = 0; i < VehicleStates.Length; ++i) {
				if ((VehicleStates[i].flags & VehicleState.Flags.Spawned) == VehicleState.Flags.None) {
					continue;
				}
				Log._Debug($"Vehicle {i}: {VehicleStates[i]}");
			}
		}

		private VehicleStateManager() {
			VehicleStates = new VehicleState[VehicleManager.MAX_VEHICLE_COUNT];
			for (uint i = 0; i < VehicleManager.MAX_VEHICLE_COUNT; ++i) {
				VehicleStates[i] = new VehicleState((ushort)i);
			}
		}

		internal void LogTraffic(ushort vehicleId) {
			LogTraffic(vehicleId, ref VehicleStates[vehicleId]);
		}

		protected void LogTraffic(ushort vehicleId, ref VehicleState state) {
			if (state.currentSegmentId == 0) {
				return;
			}
#if MEASUREDENSITY
			ushort length = (ushort)state.totalLength;
			if (length == 0) {
				return;
			}
#endif

			TrafficMeasurementManager.Instance.AddTraffic(state.currentSegmentId, state.currentLaneIndex
#if MEASUREDENSITY
				, length
#endif
				/*, (ushort)state.velocity*/
				, (ushort) state.SqrVelocity);
		}

		internal void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[9])
				Log._Debug($"VehicleStateManager.OnCreateVehicle({vehicleId}): calling OnCreate for vehicle {vehicleId}");
#endif

			VehicleStates[vehicleId].OnCreate(ref vehicleData);
		}

		internal ExtVehicleType OnStartPathFind(ushort vehicleId, ref Vehicle vehicleData, ExtVehicleType? vehicleType) {
			if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None ||
				(vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
				return ExtVehicleType.None;
			}

			/*ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): overriding vehicle type for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (leading)");
#endif
				VehicleStates[connectedVehicleId].vehicleType = type;

				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
			}*/

			ExtVehicleType ret = VehicleStates[vehicleId].OnStartPathFind(ref vehicleData, vehicleType);

			ushort connectedVehicleId = vehicleId;
			while (true) {
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

				if (connectedVehicleId == 0) {
					break;
				}

#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnStartPathFind({vehicleId}, {vehicleType}): overriding vehicle type for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
				VehicleStates[connectedVehicleId].OnStartPathFind(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId], vehicleType);
			}

			return ret;
		}

		internal void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) != (Vehicle.Flags.Created | Vehicle.Flags.Spawned) ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}, path: {vehicleData.m_path}");
#endif
				return;
			}
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[9])
				Log._Debug($"VehicleStateManager.OnSpawnVehicle({vehicleId}): calling OnSpawn for vehicle {vehicleId}");
#endif

			ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
				VehicleStates[connectedVehicleId].OnSpawn(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId]);
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
			}
		}

		public void SetNextVehicleIdOnSegment(ushort vehicleId, ushort nextVehicleId) {
			VehicleStates[vehicleId].nextVehicleIdOnSegment = nextVehicleId;
		}

		public void SetPreviousVehicleIdOnSegment(ushort vehicleId, ushort previousVehicleId) {
			VehicleStates[vehicleId].previousVehicleIdOnSegment = previousVehicleId;
		}

		internal void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData/*, float? velocity=null*/) {
			if (!Options.prioritySignsEnabled && !Options.timedLightsEnabled) {
				return;
			}

			//float vel = velocity != null ? (float)velocity : vehicleData.GetLastFrameVelocity().magnitude;

			ushort connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
				UpdateVehiclePosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId], ref VehicleStates[connectedVehicleId]/*, vel*/);
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
			}
		}

		protected void UpdateVehiclePosition(ref Vehicle vehicleData, ref VehicleState state/*, float velocity*/) {
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[9])
				Log._Debug($"VehicleStateManager.UpdateVehiclePosition({state.vehicleId}) called");
#endif

			//state.UpdateVelocity(ref vehicleData, velocity);

			if (vehicleData.m_path == 0 || (vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0 ||
				(state.lastPathId == vehicleData.m_path && state.lastPathPositionIndex == vehicleData.m_pathPositionIndex)
			) {
				return;
			}

			PathManager pathManager = Singleton<PathManager>.instance;

			// update vehicle position for timed traffic lights and priority signs
			int coarsePathPosIndex = vehicleData.m_pathPositionIndex >> 1;
			PathUnit.Position curPathPos;
			PathUnit.Position nextPathPos = default(PathUnit.Position);
			if ((vehicleData.m_pathPositionIndex & 1) == 0) {
				curPathPos = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(coarsePathPosIndex);
				pathManager.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(coarsePathPosIndex, out nextPathPos);
			} else {
				uint firstUnitId = vehicleData.m_path;
				int firstCoarsePathPosIndex = coarsePathPosIndex;
				bool invalid = false;
				if (PathUnit.GetNextPosition(ref firstUnitId, ref firstCoarsePathPosIndex, out curPathPos, out invalid)) {
					uint secondUnitId = firstUnitId;
					int secondCoarsePathPosIndex = firstCoarsePathPosIndex;
					PathUnit.GetNextPosition(ref secondUnitId, ref secondCoarsePathPosIndex, out nextPathPos, out invalid);
				}
			}
			state.UpdatePosition(ref vehicleData, ref curPathPos, ref nextPathPos);
		}

		internal void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None ||
				(vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) == 0) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

			ushort /*connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (leading)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();

				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_leadingVehicle;
			}

			*/connectedVehicleId = vehicleId;
			while (connectedVehicleId != 0) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
				VehicleStates[connectedVehicleId].OnDespawn();
				connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
			}
		}

		internal void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[9])
				Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}) called.");
#endif
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created ||
				(vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[9])
					Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
				return;
			}

#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[9])
				Log._Debug($"VehicleStateManager.OnReleaseVehicle({vehicleId}): calling OnRelease for vehicle {vehicleId}");
#endif

			VehicleStates[vehicleId].OnRelease(ref vehicleData);
		}

		internal void InitAllVehicles() {
			Log._Debug("VehicleStateManager: InitAllVehicles()");
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				for (uint vehicleId = 0; vehicleId < VehicleManager.MAX_VEHICLE_COUNT; ++vehicleId) {
					Services.VehicleService.ProcessVehicle((ushort)vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
						if ((vehicle.m_flags & Vehicle.Flags.Created) == 0) {
							return true;
						}

						OnCreateVehicle(vId, ref vehicle);

						if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
							OnStartPathFind(vId, ref vehicle, ExtVehicleType.Emergency);
						}

						if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
							return true;
						}

						OnSpawnVehicle(vId, ref vehicle);
						
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

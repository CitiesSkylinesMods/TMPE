using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	public class CustomTrainAI : TrainAI {
		public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			try {
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None) {
					byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
					if ((pathFindFlags & 4) != 0) {
						this.PathFindReady(vehicleId, ref vehicleData);
					} else if ((pathFindFlags & 8) != 0 || vehicleData.m_path == 0u) {
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						vehicleData.m_path = 0u;
						vehicleData.Unspawn(vehicleId);
						return;
					}
				} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
					this.TrySpawn(vehicleId, ref vehicleData);
				}

				bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None;
				ushort frontVehicleId;
				if (reversed) {
					frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
				} else {
					frontVehicleId = vehicleId;
				}

				/// NON-STOCK CODE START ///
				try {
					CustomCarAI.HandleVehicle(frontVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], false, false, 5);
				} catch (Exception e) {
					Log.Error("TrainAI TrafficManagerSimulationStep Error: " + e.ToString());
				}
				/// NON-STOCK CODE END ///

				VehicleManager instance = Singleton<VehicleManager>.instance;
				VehicleInfo info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
				info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				bool flag2 = (vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None;
				if (flag2 != reversed) {
					reversed = flag2;
					if (reversed) {
						frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
					} else {
						frontVehicleId = vehicleId;
					}
					info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
					info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					flag2 = ((vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None);
					if (flag2 != reversed) {
						Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
						return;
					}
				}
				if (reversed) {
					frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_leadingVehicle;
					int num2 = 0;
					while (frontVehicleId != 0) {
						info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
						info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
						if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
							return;
						}
						frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_leadingVehicle;
						if (++num2 > 16384) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				} else {
					frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_trailingVehicle;
					int num3 = 0;
					while (frontVehicleId != 0) {
						info = instance.m_vehicles.m_buffer[(int)frontVehicleId].Info;
						info.m_vehicleAI.SimulationStep(frontVehicleId, ref instance.m_vehicles.m_buffer[(int)frontVehicleId], vehicleId, ref vehicleData, 0);
						if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
							return;
						}
						frontVehicleId = instance.m_vehicles.m_buffer[(int)frontVehicleId].m_trailingVehicle;
						if (++num3 > 16384) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
				if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == Vehicle.Flags.None || vehicleData.m_blockCounter == 255) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				}
			} catch (Exception ex) {
				Log.Error("Error in TrainAI.SimulationStep: " + ex.ToString());
			}
		}

		public void TmCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, ref info.m_lanes[position.m_lane]);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, ref info.m_lanes[position.m_lane]);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}
	}
}

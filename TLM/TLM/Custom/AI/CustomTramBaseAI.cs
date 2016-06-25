using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomTramBaseAI : TramBaseAI {
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
				if ((pathFindFlags & 4) != 0) {
					this.PathfindSuccess(vehicleId, ref vehicleData);
					this.PathFindReady(vehicleId, ref vehicleData);
					VehicleStateManager.OnPathFindReady(vehicleId, ref vehicleData); // NON-STOCK CODE
				} else if ((pathFindFlags & 8) != 0 || vehicleData.m_path == 0u) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					this.PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
			} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
				this.TrySpawn(vehicleId, ref vehicleData);
			}

			/// NON-STOCK CODE START ///
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			ushort frontVehicleId;
			if (reversed) {
				frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				frontVehicleId = vehicleId;
			}

			try {
				//Log._Debug($"HandleVehicle for trams. vehicleId={vehicleId} frontVehicleId={frontVehicleId}");
				VehicleStateManager.LogTraffic(frontVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], true);
			} catch (Exception e) {
				Log.Error("TramAI CustomSimulationStep (1) Error: " + e.ToString());
			}

			try {
				VehicleStateManager.UpdateVehiclePos(frontVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId]);
			} catch (Exception e) {
				Log.Error("TramAI CustomSimulationStep (2) Error: " + e.ToString());
			}
			/// NON-STOCK CODE END ///

			VehicleManager instance = Singleton<VehicleManager>.instance;
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)vehicleId].Info;
			info.m_vehicleAI.SimulationStep(vehicleId, ref instance.m_vehicles.m_buffer[(int)vehicleId], vehicleId, ref vehicleData, 0);
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
				return;
			}
			ushort trailingVehicle = instance.m_vehicles.m_buffer[(int)vehicleId].m_trailingVehicle;
			int num = 0;
			while (trailingVehicle != 0) {
				info = instance.m_vehicles.m_buffer[(int)trailingVehicle].Info;
				info.m_vehicleAI.SimulationStep(trailingVehicle, ref instance.m_vehicles.m_buffer[(int)trailingVehicle], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				trailingVehicle = instance.m_vehicles.m_buffer[(int)trailingVehicle].m_trailingVehicle;
				if (++num > 16384) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == 0 || (vehicleData.m_blockCounter == 255 && Options.enableDespawning)) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
			VehicleInfo info = this.m_info;
			bool allowUnderground;
			bool allowUnderground2;
			if (info.m_vehicleType == VehicleInfo.VehicleType.Metro) {
				allowUnderground = true;
				allowUnderground2 = true;
			} else {
				allowUnderground = ((vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0);
				allowUnderground2 = false;
			}
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground2, false, 32f, out endPosA, out endPosB, out num3, out num4)) {
				if (!startBothWays || num2 > num * 1.2f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num4 > num3 * 1.2f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				if (Singleton<CustomPathManager>.instance.CreatePath(ExtVehicleType.Tram, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, false, false, true, false)) {
					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}

		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			Vector3 b = netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].CalculatePosition((float)prevOffset * 0.003921569f);
			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 a = lastFrameData.m_position;
			Vector3 a2 = lastFrameData.m_position;
			Vector3 b2 = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			a += b2;
			a2 -= b2;
			float crazyValue = 0.5f * lastFrameData.m_velocity.sqrMagnitude / this.m_info.m_braking;
			float a3 = Vector3.Distance(a, b);
			float b3 = Vector3.Distance(a2, b);
			if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
				Segment3 segment;
				segment.a = pos;
				ushort targetNodeId;
				ushort nextTargetNodeId;
				if (offset < position.m_offset) {
					segment.b = pos + dir.normalized * this.m_info.m_generatedInfo.m_size.z;
					targetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_startNode;
					nextTargetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				} else {
					segment.b = pos - dir.normalized * this.m_info.m_generatedInfo.m_size.z;
					targetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_endNode;
					nextTargetNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				}
				ushort prevTargetNodeId;
				if (prevOffset == 0) {
					prevTargetNodeId = netManager.m_segments.m_buffer[(int)prevPos.m_segment].m_startNode;
				} else {
					prevTargetNodeId = netManager.m_segments.m_buffer[(int)prevPos.m_segment].m_endNode;
				}
				if (targetNodeId == prevTargetNodeId) {
					if (!CustomVehicleAI.MayChangeSegment(vehicleId, ref vehicleData, ref lastFrameData, false, ref prevPos, prevTargetNodeId, prevLaneID, ref position, targetNodeId, laneID, ref nextPosition, nextTargetNodeId, out maxSpeed))
						return;
				}
			}
			NetInfo info = netManager.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				//maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, info.m_lanes[(int)position.m_lane].m_speedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]), netManager.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				//maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, info.m_lanes[(int)position.m_lane].m_speedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]), instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}
	}
}

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
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None) {
				byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
				if ((pathFindFlags & 4) != 0) {
					this.PathfindSuccess(vehicleId, ref vehicleData);
					this.PathFindReady(vehicleId, ref vehicleData);
				} else if ((pathFindFlags & 8) != 0 || vehicleData.m_path == 0u) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					this.PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
			} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
				this.TrySpawn(vehicleId, ref vehicleData);
			}

			/// NON-STOCK CODE START ///
			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None;
			ushort frontVehicleId;
			if (reversed) {
				frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				frontVehicleId = vehicleId;
			}

			try {
				//Log._Debug($"HandleVehicle for trams. vehicleId={vehicleId} frontVehicleId={frontVehicleId}");
				CustomVehicleAI.HandleVehicle(frontVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], false, false, 2);
			} catch (Exception e) {
				Log.Error("TrainAI TrafficManagerSimulationStep Error: " + e.ToString());
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
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == Vehicle.Flags.None || (vehicleData.m_blockCounter == 255 && Options.enableDespawning)) {
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
				allowUnderground = ((vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None);
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

		public void CustomCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].CalculatePosition((float)prevOffset * 0.003921569f);
			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 a = lastFrameData.m_position;
			Vector3 a2 = lastFrameData.m_position;
			Vector3 b2 = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			a += b2;
			a2 -= b2;
			float num = 0.5f * lastFrameData.m_velocity.sqrMagnitude / this.m_info.m_braking;
			float a3 = Vector3.Distance(a, b);
			float b3 = Vector3.Distance(a2, b);
			if (Mathf.Min(a3, b3) >= num - 1f) {
				Segment3 segment;
				segment.a = pos;
				ushort num2;
				ushort num3;
				if (offset < position.m_offset) {
					segment.b = pos + dir.normalized * this.m_info.m_generatedInfo.m_size.z;
					num2 = instance.m_segments.m_buffer[(int)position.m_segment].m_startNode;
					num3 = instance.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				} else {
					segment.b = pos - dir.normalized * this.m_info.m_generatedInfo.m_size.z;
					num2 = instance.m_segments.m_buffer[(int)position.m_segment].m_endNode;
					num3 = instance.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				}
				ushort num4;
				if (prevOffset == 0) {
					num4 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_startNode;
				} else {
					num4 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_endNode;
				}
				if (num2 == num4) {
					NetNode.Flags flags = instance.m_nodes.m_buffer[(int)num2].m_flags;
					NetLane.Flags flags2 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_flags;
					bool flag = (flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
					bool flag2 = (flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
					bool flag3 = (flags2 & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
					bool checkSpace = !Options.allowEnterBlockedJunctions; // NON-STOCK CODE
					if (checkSpace && (flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction && instance.m_nodes.m_buffer[(int)num2].CountSegments() != 2) {
						float len = vehicleData.CalculateTotalLength(vehicleID) + 2f;
						if (!instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(len)) {
							bool flag4 = false;
							if (nextPosition.m_segment != 0 && instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_length < 30f) {
								NetNode.Flags flags3 = instance.m_nodes.m_buffer[(int)num3].m_flags;
								if ((flags3 & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction || instance.m_nodes.m_buffer[(int)num3].CountSegments() == 2) {
									uint laneID2 = PathManager.GetLaneID(nextPosition);
									if (laneID2 != 0u) {
										flag4 = instance.m_lanes.m_buffer[(int)((UIntPtr)laneID2)].CheckSpace(len);
									}
								}
							}
							if (!flag4) {
								maxSpeed = 0f;
								return;
							}
						}
					}
					if (flag && (!flag3 || flag2)) {
						uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
						uint num5 = (uint)(((int)num4 << 8) / 32768);
						uint num6 = currentFrameIndex - num5 & 255u;
						RoadBaseAI.TrafficLightState vehicleLightState;
						RoadBaseAI.TrafficLightState pedestrianLightState;
						bool flag5;
						bool pedestrians;
						/// NON-STOCK CODE START ///
						CustomRoadAI.GetTrafficLightState(vehicleID, ref vehicleData, num4, prevPos.m_segment, position.m_segment, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5, out pedestrians);
						/// NON-STOCK CODE END ///
						//RoadBaseAI.GetTrafficLightState(num4, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5, out pedestrians);
						if (!flag5 && num6 >= 196u) {
							flag5 = true;
							RoadBaseAI.SetTrafficLightState(num4, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num5, vehicleLightState, pedestrianLightState, flag5, pedestrians);
						}
						switch (vehicleLightState) {
							case RoadBaseAI.TrafficLightState.RedToGreen:
								if (num6 < 60u) {
									maxSpeed = 0f;
									return;
								}
								break;
							case RoadBaseAI.TrafficLightState.Red:
								maxSpeed = 0f;
								return;
							case RoadBaseAI.TrafficLightState.GreenToRed:
								if (num6 >= 30u) {
									maxSpeed = 0f;
									return;
								}
								break;
						}
					}
				}
			}
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				//maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, info.m_lanes[(int)position.m_lane].m_speedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
				maxSpeed = CalculateTargetSpeed(vehicleID, ref vehicleData, SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]), instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
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

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
	public class CustomTrainAI : TrainAI { // correct would be to inherit from VehicleAI (in order to keep the correct references to `base`)
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
					CustomVehicleAI.HandleVehicle(frontVehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[frontVehicleId], false, false, 5);
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
				if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == Vehicle.Flags.None || (vehicleData.m_blockCounter == 255 && Options.enableDespawning)) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				}
			} catch (Exception ex) {
				Log.Error("Error in TrainAI.SimulationStep: " + ex.ToString());
			}
		}

		/*public void TmCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}*/

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
			/// NON-STOCK CODE START ///
			ExtVehicleType? vehicleType = CustomVehicleAI.DetermineVehicleTypeFromVehicle(vehicleID, ref vehicleData);
			//Log._Debug($"CustomTrainAI.CustomStartPathFind. vehicleID={vehicleID}. type={this.GetType().ToString()} vehicleType={vehicleType}");
			/// NON-STOCK CODE END ///

			VehicleInfo info = this.m_info;
			if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == Vehicle.Flags.None && Vector3.Distance(startPos, endPos) < 100f) {
				startPos = endPos;
			}
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
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground2, false, 32f, out endPosA, out endPosB, out num3, out num4)) {
				if (!startBothWays || num2 > num * 1.2f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num4 > num3 * 1.2f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				bool res = false;
				if (vehicleType == null)
					res = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, false, false, true, false);
				else
					res = Singleton<CustomPathManager>.instance.CreatePath((ExtVehicleType)vehicleType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, false, false, true, false);
				if (res) {
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

		public void CustomCheckNextLane(ushort vehicleID, ref Vehicle vehicleData, ref float maxSpeed, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, Bezier3 bezier) {
			NetManager instance = Singleton<NetManager>.instance;
			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 a = lastFrameData.m_position;
			Vector3 a2 = lastFrameData.m_position;
			Vector3 b = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			a += b;
			a2 -= b;
			float num = 0.5f * lastFrameData.m_velocity.sqrMagnitude / this.m_info.m_braking;
			float a3 = Vector3.Distance(a, bezier.a);
			float b2 = Vector3.Distance(a2, bezier.a);
			if (Mathf.Min(a3, b2) >= num - 5f) {
				if (!instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(1000f, vehicleID)) {
					maxSpeed = 0f;
					return;
				}
				Vector3 vector = bezier.Position(0.5f);
				Segment3 segment;
				if (Vector3.SqrMagnitude(vehicleData.m_segment.a - vector) < Vector3.SqrMagnitude(bezier.a - vector)) {
					segment = new Segment3(vehicleData.m_segment.a, vector);
				} else {
					segment = new Segment3(bezier.a, vector);
				}
				if (segment.LengthSqr() >= 3f) {
					segment.a += (segment.b - segment.a).normalized * 2.5f;
					if (CustomTrainAI.CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID)) {
						maxSpeed = 0f;
						return;
					}
				}
				segment = new Segment3(vector, bezier.d);
				if (segment.LengthSqr() >= 1f && CustomTrainAI.CheckOverlap(vehicleID, ref vehicleData, segment, vehicleID)) {
					maxSpeed = 0f;
					return;
				}
				ushort num2;
				if (offset < position.m_offset) {
					num2 = instance.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				} else {
					num2 = instance.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				}
				ushort num3;
				if (prevOffset == 0) {
					num3 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_startNode;
				} else {
					num3 = instance.m_segments.m_buffer[(int)prevPos.m_segment].m_endNode;
				}
				if (num2 == num3) {
					NetNode.Flags flags = instance.m_nodes.m_buffer[(int)num2].m_flags;
					if ((flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
						uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
						uint num4 = (uint)(((int)num3 << 8) / 32768);
						uint num5 = currentFrameIndex - num4 & 255u;
						RoadBaseAI.TrafficLightState vehicleLightState;
						RoadBaseAI.TrafficLightState pedestrianLightState;
						bool flag;
						bool pedestrians;
						/// NON-STOCK CODE START ///
						CustomRoadAI.GetTrafficLightState(vehicleID, ref vehicleData, num3, prevPos.m_segment, position.m_segment, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num4, out vehicleLightState, out pedestrianLightState, out flag, out pedestrians);
						/// NON-STOCK CODE END ///
						//RoadBaseAI.GetTrafficLightState(num3, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num4, out vehicleLightState, out pedestrianLightState, out flag, out pedestrians);
						if (!flag && num5 >= 196u) {
							flag = true;
							RoadBaseAI.SetTrafficLightState(num3, ref instance.m_segments.m_buffer[(int)prevPos.m_segment], currentFrameIndex - num4, vehicleLightState, pedestrianLightState, flag, pedestrians);
						}
						switch (vehicleLightState) {
							case RoadBaseAI.TrafficLightState.RedToGreen:
								if (num5 < 60u) {
									maxSpeed = 0f;
									return;
								}
								break;
							case RoadBaseAI.TrafficLightState.Red:
								maxSpeed = 0f;
								return;
							case RoadBaseAI.TrafficLightState.GreenToRed:
								if (num5 >= 30u) {
									maxSpeed = 0f;
									return;
								}
								break;
						}
					}
				}
			}
		}

		protected static bool CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle) {
			Log.Error("CustomTrainAI.CheckOverlap (1) called.");
			return false;
		}

		protected static ushort CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle, ushort otherID, ref Vehicle otherData, ref bool overlap, Vector3 min, Vector3 max) {
			Log.Error("CustomTrainAI.CheckOverlap (2) called.");
			return 0;
		}
	}
}
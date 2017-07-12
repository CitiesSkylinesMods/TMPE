#define EXTRAPFx
#define QUEUEDSTATSx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using System.Runtime.CompilerServices;
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	class CustomVehicleAI : VehicleAI { // TODO inherit from PrefabAI (in order to keep the correct references to `base`)
		//private static readonly int MIN_BLOCK_COUNTER_PATH_RECALC_VALUE = 3;

		public void CustomCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		protected virtual void CalculateSegPos(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[laneID].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		protected void CustomUpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int index, int max, float minSqrDistanceA, float minSqrDistanceB) {
			PathManager pathMan = Singleton<PathManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			Vector4 targetPos0 = vehicleData.m_targetPos0;
			targetPos0.w = 1000f;
			float minSqrDistA = minSqrDistanceA;
			uint pathId = vehicleData.m_path;
			byte pathPosIndex = vehicleData.m_pathPositionIndex;
			byte lastPathOffset = vehicleData.m_lastPathOffset;
			if (pathPosIndex == 255) {
				pathPosIndex = 0;
				if (index <= 0) {
					vehicleData.m_pathPositionIndex = 0;
				}
				if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathId].CalculatePathPositionOffset(pathPosIndex >> 1, targetPos0, out lastPathOffset)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
			}

			PathUnit.Position position;
			if (!pathMan.m_pathUnits.m_buffer[pathId].GetPosition(pathPosIndex >> 1, out position)) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}

			NetInfo curSegmentInfo = netManager.m_segments.m_buffer[(int)position.m_segment].Info;
			if (curSegmentInfo.m_lanes.Length <= (int)position.m_lane) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}

			uint curLaneId = PathManager.GetLaneID(position);
			NetInfo.Lane laneInfo = curSegmentInfo.m_lanes[(int)position.m_lane];
			Bezier3 bezier;
			bool firstIter = true; // NON-STOCK CODE
			while (true) {
				if ((pathPosIndex & 1) == 0) {
					if (laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
						bool first = true;
						while (lastPathOffset != position.m_offset) {
							if (first) {
								first = false;
							} else {
								float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos0, refPos);
								int pathOffsetDelta;
								if (distDiff < 0f) {
									pathOffsetDelta = 4;
								} else {
									pathOffsetDelta = 4 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (netManager.m_lanes.m_buffer[curLaneId].m_length + 1f)));
								}
								if (lastPathOffset > position.m_offset) {
									lastPathOffset = (byte)Mathf.Max((int)lastPathOffset - pathOffsetDelta, (int)position.m_offset);
								} else if (lastPathOffset < position.m_offset) {
									lastPathOffset = (byte)Mathf.Min((int)lastPathOffset + pathOffsetDelta, (int)position.m_offset);
								}
							}
							Vector3 curSegPos;
							Vector3 curSegDir;
							float curSegOffset;
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, curLaneId, lastPathOffset, out curSegPos, out curSegDir, out curSegOffset);
							targetPos0.Set(curSegPos.x, curSegPos.y, curSegPos.z, Mathf.Min(targetPos0.w, curSegOffset));
							float refPosSqrDist = (curSegPos - refPos).sqrMagnitude;
							if (refPosSqrDist >= minSqrDistA) {
								if (index <= 0) {
									vehicleData.m_lastPathOffset = lastPathOffset;
								}
								vehicleData.SetTargetPos(index++, targetPos0);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos0;
								targetPos0.w = 1000f;
								if (index == max) {
									return;
								}
							}
						}
					}
					pathPosIndex += 1;
					lastPathOffset = 0;
					if (index <= 0) {
						vehicleData.m_pathPositionIndex = pathPosIndex;
						vehicleData.m_lastPathOffset = lastPathOffset;
					}
				}

				int nextPathPosIndex = (pathPosIndex >> 1) + 1;
				uint nextPathId = pathId;
				if (nextPathPosIndex >= (int)pathMan.m_pathUnits.m_buffer[pathId].m_positionCount) {
					nextPathPosIndex = 0;
					nextPathId = pathMan.m_pathUnits.m_buffer[pathId].m_nextPathUnit;
					if (nextPathId == 0u) {
						if (index <= 0) {
							Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
							vehicleData.m_path = 0u;
						}
						targetPos0.w = 1f;
						vehicleData.SetTargetPos(index++, targetPos0);
						return;
					}
				}

				PathUnit.Position nextPathPos;
				if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetPosition(nextPathPosIndex, out nextPathPos)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				NetInfo nextSegmentInfo = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].Info;
				if (nextSegmentInfo.m_lanes.Length <= (int)nextPathPos.m_lane) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				/*if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != (Vehicle.Flags)0) {
					bestLaneIndex = FindBestLane(vehicleID, ref vehicleData, nextPathPos);
				}*/
				// NON-STOCK CODE START
				if (firstIter &&
					this.m_info.m_vehicleType == VehicleInfo.VehicleType.Car &&
					!this.m_info.m_isLargeVehicle &&
					VehicleBehaviorManager.Instance.MayFindBestLane(vehicleID, ref vehicleData, ref VehicleStateManager.Instance.VehicleStates[vehicleID])
				) {
					int bestLaneIndex = nextPathPos.m_lane;
					uint next2PathId = nextPathId;
					int next2PathPosIndex = nextPathPosIndex;
					bool next2Invalid;
					PathUnit.Position next2PathPos;
					PathUnit.Position next3PathPos;
					PathUnit.Position next4PathPos;
					if (PathUnit.GetNextPosition(ref next2PathId, ref next2PathPosIndex, out next2PathPos, out next2Invalid)) {
						uint next3PathId = next2PathId;
						int next3PathPosIndex = next2PathPosIndex;
						bool next3Invalid;
						if (PathUnit.GetNextPosition(ref next3PathId, ref next3PathPosIndex, out next3PathPos, out next3Invalid)) {
							uint next4PathId = next3PathId;
							int next4PathPosIndex = next3PathPosIndex;
							bool next4Invalid;
							if (! PathUnit.GetNextPosition(ref next4PathId, ref next4PathPosIndex, out next4PathPos, out next4Invalid)) {
								next4PathPos = default(PathUnit.Position);
							}
						} else {
							next3PathPos = default(PathUnit.Position);
							next4PathPos = default(PathUnit.Position);
						}
					} else {
						next2PathPos = default(PathUnit.Position);
						next3PathPos = default(PathUnit.Position);
						next4PathPos = default(PathUnit.Position);
					}

					bestLaneIndex = VehicleBehaviorManager.Instance.FindBestLane(vehicleID, ref vehicleData, ref VehicleStateManager.Instance.VehicleStates[vehicleID], curLaneId, position, curSegmentInfo, nextPathPos, nextSegmentInfo, next2PathPos, next3PathPos, next4PathPos);

					if (bestLaneIndex != (int)nextPathPos.m_lane) {
						nextPathPos.m_lane = (byte)bestLaneIndex;
						pathMan.m_pathUnits.m_buffer[nextPathId].SetPosition(nextPathPosIndex, nextPathPos);
					}
				}
				// NON-STOCK CODE END

				uint nextLaneId = PathManager.GetLaneID(nextPathPos);
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[(int)nextPathPos.m_lane];
				ushort curSegStartNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				ushort curSegEndNodeId = netManager.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				ushort nextSegStartNodeId = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_startNode;
				ushort nextSegEndNodeId = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_endNode;
				if (nextSegStartNodeId != curSegStartNodeId &&
					nextSegStartNodeId != curSegEndNodeId &&
					nextSegEndNodeId != curSegStartNodeId &&
					nextSegEndNodeId != curSegEndNodeId &&
					((netManager.m_nodes.m_buffer[(int)curSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)curSegEndNodeId].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None &&
					((netManager.m_nodes.m_buffer[(int)nextSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)nextSegEndNodeId].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian) {
					if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == (Vehicle.Flags)0) {
						byte inOffset = position.m_offset;
						byte outOffset = position.m_offset;
						if (this.ParkVehicle(vehicleID, ref vehicleData, position, nextPathId, nextPathPosIndex << 1, out outOffset)) {
							if (outOffset != inOffset) {
								if (index <= 0) {
									vehicleData.m_pathPositionIndex = (byte)((int)vehicleData.m_pathPositionIndex & -2);
									vehicleData.m_lastPathOffset = inOffset;
								}
								position.m_offset = outOffset;
								pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].SetPosition(pathPosIndex >> 1, position);
							}
							vehicleData.m_flags |= Vehicle.Flags.Parking;
						} else {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					}
					return;
				}

				if ((byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) == 0) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				if (nextLaneInfo.m_vehicleType != this.m_info.m_vehicleType &&
					this.NeedChangeVehicleType(vehicleID, ref vehicleData, nextPathPos, nextLaneId, nextLaneInfo.m_vehicleType, ref targetPos0)
				) {
					float targetPos0ToRefPosSqrDist = ((Vector3)targetPos0 - refPos).sqrMagnitude;
					if (targetPos0ToRefPosSqrDist >= minSqrDistA) {
						vehicleData.SetTargetPos(index++, targetPos0);
					}
					if (index <= 0) {
						while (index < max) {
							vehicleData.SetTargetPos(index++, targetPos0);
						}
						if (nextPathId != vehicleData.m_path) {
							Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
						}
						vehicleData.m_pathPositionIndex = (byte)(nextPathPosIndex << 1);
						PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos0, out vehicleData.m_lastPathOffset);
						if (vehicleID != 0 && !this.ChangeVehicleType(vehicleID, ref vehicleData, nextPathPos, nextLaneId)) {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					} else {
						while (index < max) {
							vehicleData.SetTargetPos(index++, targetPos0);
						}
					}
					return;
				}

				if (nextPathPos.m_segment != position.m_segment && vehicleID != 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
				}

				byte nextSegOffset = 0;
				if ((vehicleData.m_flags & Vehicle.Flags.Flying) != (Vehicle.Flags)0) {
					nextSegOffset = (byte)((nextPathPos.m_offset < 128) ? 255 : 0);
				} else if (curLaneId != nextLaneId && laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos0, out nextSegOffset);
					bezier = default(Bezier3);
					Vector3 curSegDir;
					float maxSpeed;
					this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, curLaneId, position.m_offset, out bezier.a, out curSegDir, out maxSpeed);
					bool calculateNextNextPos = lastPathOffset == 0;
					if (calculateNextNextPos) {
						if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0) {
							calculateNextNextPos = (vehicleData.m_trailingVehicle == 0);
						} else {
							calculateNextNextPos = (vehicleData.m_leadingVehicle == 0);
						}
					}
					Vector3 nextSegDir;
					float nextMaxSpeed;
					if (calculateNextNextPos) {
						PathUnit.Position nextNextPathPos;
						if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetNextPosition(nextPathPosIndex, out nextNextPathPos)) {
							nextNextPathPos = default(PathUnit.Position);
						}
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextNextPathPos, nextPathPos, nextLaneId, nextSegOffset, position, curLaneId, position.m_offset, index, out bezier.d, out nextSegDir, out nextMaxSpeed);
					} else {
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPathPos, nextLaneId, nextSegOffset, out bezier.d, out nextSegDir, out nextMaxSpeed);
					}
					if (nextMaxSpeed < 0.01f || (netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
						if (index <= 0) {
							vehicleData.m_lastPathOffset = lastPathOffset;
						}
						targetPos0 = bezier.a;
						targetPos0.w = 0f;
						while (index < max) {
							vehicleData.SetTargetPos(index++, targetPos0);
						}
						return;
					}
					if (position.m_offset == 0) {
						curSegDir = -curSegDir;
					}
					if (nextSegOffset < nextPathPos.m_offset) {
						nextSegDir = -nextSegDir;
					}
					curSegDir.Normalize();
					nextSegDir.Normalize();
					float dist;
					NetSegment.CalculateMiddlePoints(bezier.a, curSegDir, bezier.d, nextSegDir, true, true, out bezier.b, out bezier.c, out dist);
					if (dist > 1f) {
						ushort nextNodeId;
						if (nextSegOffset == 0) {
							nextNodeId = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_startNode;
						} else if (nextSegOffset == 255) {
							nextNodeId = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_endNode;
						} else {
							nextNodeId = 0;
						}
						float curve = 1.57079637f * (1f + Vector3.Dot(curSegDir, nextSegDir));
						if (dist > 1f) {
							curve /= dist;
						}
						nextMaxSpeed = Mathf.Min(nextMaxSpeed, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve));
						while (lastPathOffset < 255) {
							float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos0, refPos);
							int pathOffsetDelta;
							if (distDiff < 0f) {
								pathOffsetDelta = 8;
							} else {
								pathOffsetDelta = 8 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (dist + 1f)));
							}
							lastPathOffset = (byte)Mathf.Min((int)lastPathOffset + pathOffsetDelta, 255);
							Vector3 bezierPos = bezier.Position((float)lastPathOffset * 0.003921569f);
							targetPos0.Set(bezierPos.x, bezierPos.y, bezierPos.z, Mathf.Min(targetPos0.w, nextMaxSpeed));
							float sqrMagnitude2 = (bezierPos - refPos).sqrMagnitude;
							if (sqrMagnitude2 >= minSqrDistA) {
								if (index <= 0) {
									vehicleData.m_lastPathOffset = lastPathOffset;
								}
								if (nextNodeId != 0) {
									this.UpdateNodeTargetPos(vehicleID, ref vehicleData, nextNodeId, ref netManager.m_nodes.m_buffer[(int)nextNodeId], ref targetPos0, index);
								}
								vehicleData.SetTargetPos(index++, targetPos0);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos0;
								targetPos0.w = 1000f;
								if (index == max) {
									return;
								}
							}
						}
					}
				} else {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos0, out nextSegOffset);
				}

				if (index <= 0) {
					if (nextPathPosIndex == 0) {
						Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
					}
					if (nextPathPosIndex >= (int)(pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_positionCount - 1) && pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_nextPathUnit == 0u && vehicleID != 0) {
						this.ArrivingToDestination(vehicleID, ref vehicleData);
					}
				}

				pathId = nextPathId;
				pathPosIndex = (byte)(nextPathPosIndex << 1);
				lastPathOffset = nextSegOffset;
				if (index <= 0) {
					vehicleData.m_pathPositionIndex = pathPosIndex;
					vehicleData.m_lastPathOffset = lastPathOffset;
					vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | nextSegmentInfo.m_setVehicleFlags);
					if (this.LeftHandDrive(nextLaneInfo)) {
						vehicleData.m_flags |= Vehicle.Flags.LeftHandDrive;
					} else {
						vehicleData.m_flags &= (Vehicle.Flags.Created | Vehicle.Flags.Deleted | Vehicle.Flags.Spawned | Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget | Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped | Vehicle.Flags.Leaving | Vehicle.Flags.Arriving | Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff | Vehicle.Flags.Flying | Vehicle.Flags.Landing | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.Importing | Vehicle.Flags.Exporting | Vehicle.Flags.Parking | Vehicle.Flags.CustomName | Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading | Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic | Vehicle.Flags.Underground | Vehicle.Flags.Transition | Vehicle.Flags.InsideBuilding);
					}
				}
				position = nextPathPos;
				curLaneId = nextLaneId;
				laneInfo = nextLaneInfo;
				firstIter = false; // NON-STOCK CODE
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int FindBestLane(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position) {
			Log.Error("CustomVehicleAI.FindBestLane called");
			return 0;
		}
	}
}

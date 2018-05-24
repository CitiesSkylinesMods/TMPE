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
using CSUtil.Commons.Benchmark;

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
				float laneSpeedLimit;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
#if BENCHMARK
				}
#endif
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		protected void CustomUpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int targetPosIndex, int maxTargetPosIndex, float minSqrDistanceA, float minSqrDistanceB) {
			PathManager pathMan = Singleton<PathManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			Vector4 targetPos = vehicleData.m_targetPos0;
			targetPos.w = 1000f;
			float minSqrDistA = minSqrDistanceA;
			uint pathId = vehicleData.m_path;
			byte finePathPosIndex = vehicleData.m_pathPositionIndex;
			byte lastPathOffset = vehicleData.m_lastPathOffset;

			// initial position
			if (finePathPosIndex == 255) {
				finePathPosIndex = 0;
				if (targetPosIndex <= 0) {
					vehicleData.m_pathPositionIndex = 0;
				}

				if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathId].CalculatePathPositionOffset(finePathPosIndex >> 1, targetPos, out lastPathOffset)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
			}

			// get current path position, check for errors
			PathUnit.Position currentPosition;
			if (!pathMan.m_pathUnits.m_buffer[pathId].GetPosition(finePathPosIndex >> 1, out currentPosition)) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}

			// get current segment info, check for errors
			NetInfo curSegmentInfo = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].Info;
			if (curSegmentInfo.m_lanes.Length <= (int)currentPosition.m_lane) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}

			// main loop
			uint curLaneId = PathManager.GetLaneID(currentPosition);
			NetInfo.Lane laneInfo = curSegmentInfo.m_lanes[(int)currentPosition.m_lane];
			Bezier3 bezier;
			bool firstIter = true; // NON-STOCK CODE
			while (true) {
				if ((finePathPosIndex & 1) == 0) {
					// vehicle is not in transition
					if (laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
						bool first = true;
						while (lastPathOffset != currentPosition.m_offset) {
							// catch up and update target position until we get to the current segment offset
							if (first) {
								first = false;
							} else {
								float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
								int pathOffsetDelta;
								if (distDiff < 0f) {
									pathOffsetDelta = 4;
								} else {
									pathOffsetDelta = 4 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (netManager.m_lanes.m_buffer[curLaneId].m_length + 1f)));
								}
								if (lastPathOffset > currentPosition.m_offset) {
									lastPathOffset = (byte)Mathf.Max((int)lastPathOffset - pathOffsetDelta, (int)currentPosition.m_offset);
								} else if (lastPathOffset < currentPosition.m_offset) {
									lastPathOffset = (byte)Mathf.Min((int)lastPathOffset + pathOffsetDelta, (int)currentPosition.m_offset);
								}
							}

							Vector3 curSegPos;
							Vector3 curSegDir;
							float curSegOffset;
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, currentPosition, curLaneId, lastPathOffset, out curSegPos, out curSegDir, out curSegOffset);
							targetPos.Set(curSegPos.x, curSegPos.y, curSegPos.z, Mathf.Min(targetPos.w, curSegOffset));
							float refPosSqrDist = (curSegPos - refPos).sqrMagnitude;
							if (refPosSqrDist >= minSqrDistA) {
								if (targetPosIndex <= 0) {
									vehicleData.m_lastPathOffset = lastPathOffset;
								}
								vehicleData.SetTargetPos(targetPosIndex++, targetPos);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos;
								targetPos.w = 1000f;
								if (targetPosIndex == maxTargetPosIndex) {
									// maximum target position index reached
									return;
								}
							}
						}
					}

					// set vehicle in transition
					finePathPosIndex += 1;
					lastPathOffset = 0;
					if (targetPosIndex <= 0) {
						vehicleData.m_pathPositionIndex = finePathPosIndex;
						vehicleData.m_lastPathOffset = lastPathOffset;
					}
				}

				// vehicle is in transition now

				/*
				 * coarse path position format: 0..11 (always equals 'fine path position' / 2 == 'fine path position' >> 1)
				 * fine path position format: 0..23
				 */

				// find next path unit (or abort if at path end)
				int nextCoarsePathPosIndex = (finePathPosIndex >> 1) + 1;
				uint nextPathId = pathId;
				if (nextCoarsePathPosIndex >= (int)pathMan.m_pathUnits.m_buffer[pathId].m_positionCount) {
					nextCoarsePathPosIndex = 0;
					nextPathId = pathMan.m_pathUnits.m_buffer[pathId].m_nextPathUnit;
					if (nextPathId == 0u) {
						if (targetPosIndex <= 0) {
							Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
							vehicleData.m_path = 0u;
						}
						targetPos.w = 1f;
						vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						return;
					}
				}

				// check for errors
				PathUnit.Position nextPathPos;
				if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetPosition(nextCoarsePathPosIndex, out nextPathPos)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				// check for errors
				NetInfo nextSegmentInfo = netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].Info;
				if (nextSegmentInfo.m_lanes.Length <= (int)nextPathPos.m_lane) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				// find next lane (emergency vehicles / dynamic lane selection)
				int bestLaneIndex = nextPathPos.m_lane;
				if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != (Vehicle.Flags)0) {
					bestLaneIndex = FindBestLane(vehicleID, ref vehicleData, nextPathPos);
				} else {
					// NON-STOCK CODE START
					if (firstIter &&
						this.m_info.m_vehicleType == VehicleInfo.VehicleType.Car &&
						!this.m_info.m_isLargeVehicle
					) {
						bool mayFindBestLane = false;
#if BENCHMARK
						using (var bm = new Benchmark(null, "MayFindBestLane")) {
#endif
							mayFindBestLane = VehicleBehaviorManager.Instance.MayFindBestLane(vehicleID, ref vehicleData, ref VehicleStateManager.Instance.VehicleStates[vehicleID]);
#if BENCHMARK
						}
#endif

						if (mayFindBestLane) {
							uint next2PathId = nextPathId;
							int next2PathPosIndex = nextCoarsePathPosIndex;
							bool next2Invalid;
							PathUnit.Position next2PathPos;
							NetInfo next2SegmentInfo = null;
							PathUnit.Position next3PathPos;
							NetInfo next3SegmentInfo = null;
							PathUnit.Position next4PathPos;
							if (PathUnit.GetNextPosition(ref next2PathId, ref next2PathPosIndex, out next2PathPos, out next2Invalid)) {
								next2SegmentInfo = netManager.m_segments.m_buffer[(int)next2PathPos.m_segment].Info;

								uint next3PathId = next2PathId;
								int next3PathPosIndex = next2PathPosIndex;
								bool next3Invalid;
								if (PathUnit.GetNextPosition(ref next3PathId, ref next3PathPosIndex, out next3PathPos, out next3Invalid)) {
									next3SegmentInfo = netManager.m_segments.m_buffer[(int)next3PathPos.m_segment].Info;

									uint next4PathId = next3PathId;
									int next4PathPosIndex = next3PathPosIndex;
									bool next4Invalid;
									if (!PathUnit.GetNextPosition(ref next4PathId, ref next4PathPosIndex, out next4PathPos, out next4Invalid)) {
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

#if BENCHMARK
							using (var bm = new Benchmark(null, "FindBestLane")) {
#endif
								bestLaneIndex = VehicleBehaviorManager.Instance.FindBestLane(vehicleID, ref vehicleData, ref VehicleStateManager.Instance.VehicleStates[vehicleID], curLaneId, currentPosition, curSegmentInfo, nextPathPos, nextSegmentInfo, next2PathPos, next2SegmentInfo, next3PathPos, next3SegmentInfo, next4PathPos);
#if BENCHMARK
							}
#endif
						}
						// NON-STOCK CODE END
					}
				}

				// update lane index
				if (bestLaneIndex != (int)nextPathPos.m_lane) {
					nextPathPos.m_lane = (byte)bestLaneIndex;
					pathMan.m_pathUnits.m_buffer[nextPathId].SetPosition(nextCoarsePathPosIndex, nextPathPos);
#if BENCHMARK
					using (var bm = new Benchmark(null, "AddTraffic")) {
#endif
						// prevent multiple lane changes to the same lane from happening at the same time
						TrafficMeasurementManager.Instance.AddTraffic(nextPathPos.m_segment, nextPathPos.m_lane
#if MEASUREDENSITY
								, VehicleStateManager.Instance.VehicleStates[vehicleID].totalLength
#endif
								, 0); // NON-STOCK CODE
#if BENCHMARK
					}
#endif
				}

				// check for errors
				uint nextLaneId = PathManager.GetLaneID(nextPathPos);
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[(int)nextPathPos.m_lane];
				ushort curSegStartNodeId = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].m_startNode;
				ushort curSegEndNodeId = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].m_endNode;
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

				// park vehicle
				if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian) {
					if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == (Vehicle.Flags)0) {
						byte inOffset = currentPosition.m_offset;
						byte outOffset = currentPosition.m_offset;
						if (this.ParkVehicle(vehicleID, ref vehicleData, currentPosition, nextPathId, nextCoarsePathPosIndex << 1, out outOffset)) {
							if (outOffset != inOffset) {
								if (targetPosIndex <= 0) {
									vehicleData.m_pathPositionIndex = (byte)((int)vehicleData.m_pathPositionIndex & -2);
									vehicleData.m_lastPathOffset = inOffset;
								}
								currentPosition.m_offset = outOffset;
								pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].SetPosition(finePathPosIndex >> 1, currentPosition);
							}
							vehicleData.m_flags |= Vehicle.Flags.Parking;
						} else {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					}
					return;
				}

				// check for errors
				if ((byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) == 0) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				// change vehicle
				if (nextLaneInfo.m_vehicleType != this.m_info.m_vehicleType &&
					this.NeedChangeVehicleType(vehicleID, ref vehicleData, nextPathPos, nextLaneId, nextLaneInfo.m_vehicleType, ref targetPos)
				) {
					float targetPos0ToRefPosSqrDist = ((Vector3)targetPos - refPos).sqrMagnitude;
					if (targetPos0ToRefPosSqrDist >= minSqrDistA) {
						vehicleData.SetTargetPos(targetPosIndex++, targetPos);
					}
					if (targetPosIndex <= 0) {
						while (targetPosIndex < maxTargetPosIndex) {
							vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						}
						if (nextPathId != vehicleData.m_path) {
							Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
						}
						vehicleData.m_pathPositionIndex = (byte)(nextCoarsePathPosIndex << 1);
						PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out vehicleData.m_lastPathOffset);
						if (vehicleID != 0 && !this.ChangeVehicleType(vehicleID, ref vehicleData, nextPathPos, nextLaneId)) {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					} else {
						while (targetPosIndex < maxTargetPosIndex) {
							vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						}
					}
					return;
				}

				// unset leaving flag
				if (nextPathPos.m_segment != currentPosition.m_segment && vehicleID != 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
				}

				// calculate next segment offset
				byte nextSegOffset = 0;
				if ((vehicleData.m_flags & Vehicle.Flags.Flying) != (Vehicle.Flags)0) {
					nextSegOffset = (byte)((nextPathPos.m_offset < 128) ? 255 : 0);
				} else if (curLaneId != nextLaneId && laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);
					bezier = default(Bezier3);
					Vector3 curSegDir;
					float maxSpeed;
					this.CalculateSegmentPosition(vehicleID, ref vehicleData, currentPosition, curLaneId, currentPosition.m_offset, out bezier.a, out curSegDir, out maxSpeed);
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
						if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetNextPosition(nextCoarsePathPosIndex, out nextNextPathPos)) {
							nextNextPathPos = default(PathUnit.Position);
						}
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextNextPathPos, nextPathPos, nextLaneId, nextSegOffset, currentPosition, curLaneId, currentPosition.m_offset, targetPosIndex, out bezier.d, out nextSegDir, out nextMaxSpeed);
					} else {
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPathPos, nextLaneId, nextSegOffset, out bezier.d, out nextSegDir, out nextMaxSpeed);
					}
					if (nextMaxSpeed < 0.01f || (netManager.m_segments.m_buffer[(int)nextPathPos.m_segment].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
						if (targetPosIndex <= 0) {
							vehicleData.m_lastPathOffset = lastPathOffset;
						}
						targetPos = bezier.a;
						targetPos.w = 0f;
						while (targetPosIndex < maxTargetPosIndex) {
							vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						}
						return;
					}
					if (currentPosition.m_offset == 0) {
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
							float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
							int pathOffsetDelta;
							if (distDiff < 0f) {
								pathOffsetDelta = 8;
							} else {
								pathOffsetDelta = 8 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (dist + 1f)));
							}
							lastPathOffset = (byte)Mathf.Min((int)lastPathOffset + pathOffsetDelta, 255);
							Vector3 bezierPos = bezier.Position((float)lastPathOffset * 0.003921569f);
							targetPos.Set(bezierPos.x, bezierPos.y, bezierPos.z, Mathf.Min(targetPos.w, nextMaxSpeed));
							float sqrMagnitude2 = (bezierPos - refPos).sqrMagnitude;
							if (sqrMagnitude2 >= minSqrDistA) {
								if (targetPosIndex <= 0) {
									vehicleData.m_lastPathOffset = lastPathOffset;
								}
								if (nextNodeId != 0) {
									this.UpdateNodeTargetPos(vehicleID, ref vehicleData, nextNodeId, ref netManager.m_nodes.m_buffer[(int)nextNodeId], ref targetPos, targetPosIndex);
								}
								vehicleData.SetTargetPos(targetPosIndex++, targetPos);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos;
								targetPos.w = 1000f;
								if (targetPosIndex == maxTargetPosIndex) {
									return;
								}
							}
						}
					}
				} else {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);
				}

				// check for arrival
				if (targetPosIndex <= 0) {
					if (nextCoarsePathPosIndex == 0) {
						Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
					}
					if (nextCoarsePathPosIndex >= (int)(pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_positionCount - 1) && pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_nextPathUnit == 0u && vehicleID != 0) {
						this.ArrivingToDestination(vehicleID, ref vehicleData);
					}
				}

				// prepare next loop iteration: go to next path position
				pathId = nextPathId;
				finePathPosIndex = (byte)(nextCoarsePathPosIndex << 1);
				lastPathOffset = nextSegOffset;
				if (targetPosIndex <= 0) {
					vehicleData.m_pathPositionIndex = finePathPosIndex;
					vehicleData.m_lastPathOffset = lastPathOffset;
					vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | nextSegmentInfo.m_setVehicleFlags);
					if (this.LeftHandDrive(nextLaneInfo)) {
						vehicleData.m_flags |= Vehicle.Flags.LeftHandDrive;
					} else {
						vehicleData.m_flags &= (Vehicle.Flags.Created | Vehicle.Flags.Deleted | Vehicle.Flags.Spawned | Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget | Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped | Vehicle.Flags.Leaving | Vehicle.Flags.Arriving | Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff | Vehicle.Flags.Flying | Vehicle.Flags.Landing | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.Importing | Vehicle.Flags.Exporting | Vehicle.Flags.Parking | Vehicle.Flags.CustomName | Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading | Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic | Vehicle.Flags.Underground | Vehicle.Flags.Transition | Vehicle.Flags.InsideBuilding);
					}
				}
				currentPosition = nextPathPos;
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

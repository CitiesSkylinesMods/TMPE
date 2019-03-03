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
using TrafficManager.RedirectionFramework.Attributes;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Custom.AI {
	[TargetType(typeof(VehicleAI))]
	public class CustomVehicleAI : VehicleAI { // TODO inherit from PrefabAI (in order to keep the correct references to `base`)
		[RedirectMethod]
		public void CustomCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		[RedirectMethod]
		public void CustomCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		public void CalculateSegPos(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[laneID].CalculatePositionAndDirection((float)offset * Constants.BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR, out pos, out dir);
			NetInfo info = netManager.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				float laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		[RedirectMethod]
		protected void CustomUpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int targetPosIndex, int maxTargetPosIndex, float minSqrDistanceA, float minSqrDistanceB) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[21] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleID);

			if (debug) {
				Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}) called.\n" +
					$"\trefPos={refPos}\n" +
					$"\ttargetPosIndex={targetPosIndex}\n" +
					$"\tmaxTargetPosIndex={maxTargetPosIndex}\n" +
					$"\tminSqrDistanceA={minSqrDistanceA}\n" +
					$"\tminSqrDistanceB={minSqrDistanceB}\n" + 
					$"\tvehicleData.m_path={vehicleData.m_path}\n" +
					$"\tvehicleData.m_pathPositionIndex={vehicleData.m_pathPositionIndex}\n" +
					$"\tvehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}"
				);
			}
#endif
			PathManager pathMan = Singleton<PathManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;

			Vector4 targetPos = vehicleData.m_targetPos0;
			targetPos.w = 1000f;
			float minSqrDistA = minSqrDistanceA;
			uint pathId = vehicleData.m_path;
			byte finePathPosIndex = vehicleData.m_pathPositionIndex;
			byte pathOffset = vehicleData.m_lastPathOffset;

			// initial position
			if (finePathPosIndex == 255) {
#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Initial position. finePathPosIndex={finePathPosIndex}");
				}
#endif

				finePathPosIndex = 0;
				if (targetPosIndex <= 0) {
					vehicleData.m_pathPositionIndex = 0;
				}

				if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathId].CalculatePathPositionOffset(finePathPosIndex >> 1, targetPos, out pathOffset)) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Could not calculate path position offset. ABORT. pathId={pathId}, finePathPosIndex={finePathPosIndex}, targetPos={targetPos}");
					}
#endif
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
			}

			// get current path position, check for errors
			PathUnit.Position currentPosition;
			if (!pathMan.m_pathUnits.m_buffer[pathId].GetPosition(finePathPosIndex >> 1, out currentPosition)) {
#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Could not get current position. ABORT. pathId={pathId}, finePathPosIndex={finePathPosIndex}");
				}
#endif
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}

			// get current segment info, check for errors
			NetInfo curSegmentInfo = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].Info;
			if (curSegmentInfo.m_lanes.Length <= (int)currentPosition.m_lane) {
#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Invalid lane index. ABORT. curSegmentInfo.m_lanes.Length={curSegmentInfo.m_lanes.Length}, currentPosition.m_lane={currentPosition.m_lane}");
				}
#endif
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}


			// main loop
			uint curLaneId = PathManager.GetLaneID(currentPosition);
#if DEBUG
			if (debug) {
				Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): currentPosition=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, off={currentPosition.m_offset}], targetPos={targetPos}, curLaneId={curLaneId}");
			}
#endif
			NetInfo.Lane laneInfo = curSegmentInfo.m_lanes[(int)currentPosition.m_lane];
			Bezier3 bezier;
			bool firstIter = true; // NON-STOCK CODE
			IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager; // NON-STOCK CODE
			IEmergencyBehaviorManager emergencyMan = Constants.ManagerFactory.EmergencyBehaviorManager; // NON-STOCK CODE
			while (true) {
				if ((finePathPosIndex & 1) == 0) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is not in transition.");
					}
#endif

					// vehicle is not in transition
					if (laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is not in transition and no cargo lane. finePathPosIndex={finePathPosIndex}, pathOffset={pathOffset}, currentPosition.m_offset={currentPosition.m_offset}");
						}
#endif

						int iter = -1;
						while (pathOffset != currentPosition.m_offset) {
							++iter;
							// catch up and update target position until we get to the current segment offset

							Vector3 curSegPos;
							Vector3 curSegDir;
							float maxSpeed;

							if (iter != 0) {
								float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
								int pathOffsetDelta;
								if (distDiff < 0f) {
									pathOffsetDelta = 4;
								} else {
									pathOffsetDelta = 4 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (netManager.m_lanes.m_buffer[curLaneId].m_length + 1f)));
								}
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculated pathOffsetDelta={pathOffsetDelta}. distDiff={distDiff}, targetPos={targetPos}, refPos={refPos}");
								}
#endif

								if (pathOffset > currentPosition.m_offset) {
									pathOffset = (byte)Mathf.Max((int)pathOffset - pathOffsetDelta, (int)currentPosition.m_offset);
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): pathOffset > currentPosition.m_offset: Calculated pathOffset={pathOffset}");
									}
#endif
								} else if (pathOffset < currentPosition.m_offset) {
									pathOffset = (byte)Mathf.Min((int)pathOffset + pathOffsetDelta, (int)currentPosition.m_offset);
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): pathOffset < currentPosition.m_offset: Calculated pathOffset={pathOffset}");
									}
#endif
								}
							}

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Catch-up iteration {iter}.");
							}
#endif

							this.CalculateSegmentPosition(vehicleID, ref vehicleData, currentPosition, curLaneId, pathOffset, out curSegPos, out curSegDir, out maxSpeed);

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculated segment position at iteration {iter}. IN: pathOffset={pathOffset}, currentPosition=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, off={currentPosition.m_offset}], curLaneId={curLaneId}, OUT: curSegPos={curSegPos}, curSegDir={curSegDir}, maxSpeed={maxSpeed}");
							}
#endif

							// NON-STOCK CODE START
							bool mustStop = false;
							if (Options.emergencyAI) {
								bool stopped = (extVehicleMan.ExtVehicles[vehicleID].flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None;

								emergencyMan.AdaptSegmentPosition(vehicleID, ref vehicleData, ref extVehicleMan.ExtVehicles[vehicleID], currentPosition, curLaneId, ref pathOffset, ref curSegPos, curSegDir, ref maxSpeed);

								bool stopRequested = (extVehicleMan.ExtVehicles[vehicleID].flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None;
								mustStop = stopRequested && stopped;
							}
							// NON-STOCK CODE END

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Adapted position for emergency. curSegPos={curSegPos}, maxSpeed={maxSpeed}");
							}
#endif

							targetPos.Set(curSegPos.x, curSegPos.y, curSegPos.z, Mathf.Min(targetPos.w, maxSpeed));
#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculated targetPos={targetPos}");
							}
#endif

							// NON-STOCK CODE START
							if (mustStop) {
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Emergency stop! FINISH.");
								}
#endif
								if (targetPosIndex <= 0) {
									vehicleData.m_lastPathOffset = pathOffset;
								}

								while (targetPosIndex < maxTargetPosIndex) {
									vehicleData.SetTargetPos(targetPosIndex++, targetPos);
								}
								return;
							}
							// NON-STOCK CODE END

							float refPosSqrDist = (curSegPos - refPos).sqrMagnitude;
#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Set targetPos={targetPos}, refPosSqrDist={refPosSqrDist}, curSegPos={curSegPos}, refPos={refPos}");
							}
#endif
							if (refPosSqrDist >= minSqrDistA) {
								if (targetPosIndex <= 0) {
									vehicleData.m_lastPathOffset = pathOffset;
								}
								vehicleData.SetTargetPos(targetPosIndex++, targetPos);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos;
								targetPos.w = 1000f;
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): refPosSqrDist >= minSqrDistA: targetPosIndex={targetPosIndex}, refPosSqrDist={refPosSqrDist}, minSqrDistA ={minSqrDistA}, refPos={refPos}, targetPos={targetPos}");
								}
#endif
								if (targetPosIndex == maxTargetPosIndex) {
									// maximum target position index reached
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Maximum target position index reached ({maxTargetPosIndex}). ABORT.");
									}
#endif
									return;
								}
							} else {
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): refPosSqrDist < minSqrDistA: refPosSqrDist={refPosSqrDist}, targetPosIndex={targetPosIndex}, minSqrDistA={minSqrDistA}, refPos={refPos}, targetPos={targetPos}, curSegPos={curSegPos}");
								}
#endif
							}
						}

#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Catch up complete.");
						}
#endif
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Lane is cargo lane. No catch up.");
						}
#endif
					}

					// set vehicle in transition
					finePathPosIndex += 1;
					pathOffset = 0;
					if (targetPosIndex <= 0) {
						vehicleData.m_pathPositionIndex = finePathPosIndex;
						vehicleData.m_lastPathOffset = pathOffset;
					}
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is in transition now. finePathPosIndex={finePathPosIndex}, pathOffset={pathOffset}, targetPosIndex={targetPosIndex}, vehicleData.m_pathPositionIndex={vehicleData.m_pathPositionIndex}, vehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}");
					}
#endif
				} else {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is in transition.");
					}
#endif
				}

				if ((vehicleData.m_flags2 & Vehicle.Flags2.EndStop) != 0) {
					if (targetPosIndex <= 0) {
						targetPos.w = 0f;
						if (VectorUtils.LengthSqrXZ(vehicleData.GetLastFrameVelocity()) < 0.01f) {
							vehicleData.m_flags2 &= ~Vehicle.Flags2.EndStop;
						}
					} else {
						targetPos.w = 1f;
					}
					while (targetPosIndex < maxTargetPosIndex) {
						vehicleData.SetTargetPos(targetPosIndex++, targetPos);
					}
					return;
				}

				// vehicle is in transition now

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is in transition now.");
				}
#endif

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
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Path end reached. ABORT. nextCoarsePathPosIndex={nextCoarsePathPosIndex}, finePathPosIndex={finePathPosIndex}, targetPosIndex={targetPosIndex}, targetPos={targetPos}");
						}
#endif
						return;
					}
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Found next path unit. nextCoarsePathPosIndex={nextCoarsePathPosIndex}, nextPathId={nextPathId}");
				}
#endif

				// get next path position, check for errors
				PathUnit.Position nextPosition;
				if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetPosition(nextCoarsePathPosIndex, out nextPosition)) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Could not get position for nextCoarsePathPosIndex={nextCoarsePathPosIndex}. ABORT.");
					}
#endif
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Got next path position nextPosition=[seg={nextPosition.m_segment}, lane={nextPosition.m_lane}, off={nextPosition.m_offset}]");
				}
#endif

				// check for errors
				NetInfo nextSegmentInfo = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].Info;
				if (nextSegmentInfo.m_lanes.Length <= (int)nextPosition.m_lane) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Invalid lane index. ABORT. nextSegmentInfo.m_lanes.Length={nextSegmentInfo.m_lanes.Length}, nextPosition.m_lane={nextPosition.m_lane}");
					}
#endif
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				// find next lane (emergency vehicles / dynamic lane selection)
				int bestLaneIndex = nextPosition.m_lane;
				if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != (Vehicle.Flags)0) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Finding best lane for emergency vehicles. Before: bestLaneIndex={bestLaneIndex}");
					}
#endif

					EmergencyBehavior behavior = EmergencyBehavior.None;
					if (Options.emergencyAI) {
						behavior = Constants.ManagerFactory.EmergencyBehaviorManager.GetEmergencyBehavior(true, nextPosition, PathManager.GetLaneID(nextPosition), ref netManager.m_segments.m_buffer[(int)nextPosition.m_segment], ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[nextPosition.m_segment]);
					}

					if (behavior != EmergencyBehavior.None) {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Using emergency behavior logic. behavior={behavior}");
						}
#endif
						bestLaneIndex = Constants.ManagerFactory.EmergencyBehaviorManager.GetRescueLane(behavior, nextPosition);
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Using vanilla emergency logic.");
						}
#endif
						bestLaneIndex = FindBestLane(vehicleID, ref vehicleData, nextPosition);
					}

#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Found best lane for emergency vehicles. After: bestLaneIndex={bestLaneIndex}");
					}
#endif
				} else {
					// NON-STOCK CODE START
					if (firstIter &&
						this.m_info.m_vehicleType == VehicleInfo.VehicleType.Car
					) {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Finding best lane for regular vehicles. Before: bestLaneIndex={bestLaneIndex}");
						}
#endif

						EmergencyBehavior behavior = EmergencyBehavior.None;
						if (Options.emergencyAI) {
							behavior = Constants.ManagerFactory.EmergencyBehaviorManager.GetEmergencyBehavior(false, nextPosition, PathManager.GetLaneID(nextPosition), ref netManager.m_segments.m_buffer[(int)nextPosition.m_segment], ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[nextPosition.m_segment]);
						}

						if (behavior != EmergencyBehavior.None) {
#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Using emergency behavior logic. behavior={behavior}");
							}
#endif
							bestLaneIndex = Constants.ManagerFactory.EmergencyBehaviorManager.GetEvasionLane(behavior, nextPosition);
						} else if (!this.m_info.m_isLargeVehicle) {
							if (VehicleBehaviorManager.Instance.MayFindBestLane(vehicleID, ref vehicleData, ref ExtVehicleManager.Instance.ExtVehicles[vehicleID])) {
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Using DLS.");
								}
#endif

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

								bestLaneIndex = VehicleBehaviorManager.Instance.FindBestLane(vehicleID, ref vehicleData, ref ExtVehicleManager.Instance.ExtVehicles[vehicleID], curLaneId, currentPosition, curSegmentInfo, nextPosition, nextSegmentInfo, next2PathPos, next2SegmentInfo, next3PathPos, next3SegmentInfo, next4PathPos);
							}
						}
						// NON-STOCK CODE END
					}
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Best lane found. bestLaneIndex={bestLaneIndex} nextPosition.m_lane={nextPosition.m_lane}");
				}
#endif

				// update lane index
				if (bestLaneIndex != (int)nextPosition.m_lane) {
					nextPosition.m_lane = (byte)bestLaneIndex;
					pathMan.m_pathUnits.m_buffer[nextPathId].SetPosition(nextCoarsePathPosIndex, nextPosition);
					// prevent multiple lane changes to the same lane from happening at the same time
					TrafficMeasurementManager.Instance.AddTraffic(nextPosition.m_segment, nextPosition.m_lane, 0); // NON-STOCK CODE
				}

				// check for errors
				uint nextLaneId = PathManager.GetLaneID(nextPosition);
				NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[(int)nextPosition.m_lane];
				ushort curSegStartNodeId = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].m_startNode;
				ushort curSegEndNodeId = netManager.m_segments.m_buffer[(int)currentPosition.m_segment].m_endNode;
				ushort nextSegStartNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_startNode;
				ushort nextSegEndNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_endNode;
				if (nextSegStartNodeId != curSegStartNodeId &&
					nextSegStartNodeId != curSegEndNodeId &&
					nextSegEndNodeId != curSegStartNodeId &&
					nextSegEndNodeId != curSegEndNodeId &&
					((netManager.m_nodes.m_buffer[(int)curSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)curSegEndNodeId].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None &&
					((netManager.m_nodes.m_buffer[(int)nextSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)nextSegEndNodeId].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): General node/flag fault. ABORT. nextSegStartNodeId={nextSegStartNodeId}, curSegStartNodeId={curSegStartNodeId}, nextSegStartNodeId={nextSegStartNodeId}, curSegEndNodeId={curSegEndNodeId}, nextSegEndNodeId={nextSegEndNodeId}, curSegStartNodeId={curSegStartNodeId}, nextSegEndNodeId={nextSegEndNodeId}, curSegEndNodeId={curSegEndNodeId}, flags1={netManager.m_nodes.m_buffer[(int)curSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)curSegEndNodeId].m_flags}, flags2={netManager.m_nodes.m_buffer[(int)nextSegStartNodeId].m_flags | netManager.m_nodes.m_buffer[(int)nextSegEndNodeId].m_flags}");
					}
#endif
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

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Parking vehicle. FINISH. inOffset={inOffset}, outOffset={outOffset}");
							}
#endif
						} else {
#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Parking vehicle failed. ABORT.");
							}
#endif
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Parking vehicle not allowed. ABORT.");
						}
#endif
					}
					return;
				}

				// check for errors
				if ((byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) == 0) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Next lane invalid. ABORT. nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}");
					}
#endif
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}

				// change vehicle
				if (nextLaneInfo.m_vehicleType != this.m_info.m_vehicleType &&
					this.NeedChangeVehicleType(vehicleID, ref vehicleData, nextPosition, nextLaneId, nextLaneInfo.m_vehicleType, ref targetPos)
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
						if (vehicleID != 0 && !this.ChangeVehicleType(vehicleID, ref vehicleData, nextPosition, nextLaneId)) {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					} else {
						while (targetPosIndex < maxTargetPosIndex) {
							vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						}
					}
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Need to change vehicle. FINISH. targetPos0ToRefPosSqrDist={targetPos0ToRefPosSqrDist}, targetPosIndex={targetPosIndex}");
					}
#endif
					return;
				}

				// unset leaving flag
				if (nextPosition.m_segment != currentPosition.m_segment && vehicleID != 0) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Unsetting Leaving flag");
					}
#endif
					vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculating next segment position");
				}
#endif

				// calculate next segment offset
				byte nextSegOffset = 0;
				if ((vehicleData.m_flags & Vehicle.Flags.Flying) != (Vehicle.Flags)0) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Vehicle is flying");
					}
#endif
					nextSegOffset = (byte)((nextPosition.m_offset < 128) ? 255 : 0);
				} else if (curLaneId != nextLaneId && laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);
					bezier = default(Bezier3);
					Vector3 curSegDir;
					float maxSpeed;
					this.CalculateSegmentPosition(vehicleID, ref vehicleData, currentPosition, curLaneId, currentPosition.m_offset, out bezier.a, out curSegDir, out maxSpeed);
					bool calculateNextNextPos = pathOffset == 0;
					if (calculateNextNextPos) {
						if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0) {
							calculateNextNextPos = (vehicleData.m_trailingVehicle == 0);
						} else {
							calculateNextNextPos = (vehicleData.m_leadingVehicle == 0);
						}
					}

#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculated current segment position for regular vehicle. nextSegOffset={nextSegOffset}, bezier.a={bezier.a}, curSegDir={curSegDir}, maxSpeed={maxSpeed}, pathOffset={pathOffset}, calculateNextNextPos={calculateNextNextPos}");
					}
#endif

					Vector3 nextSegDir;
					float curMaxSpeed;
					if (calculateNextNextPos) {
						PathUnit.Position nextNextPosition;
						if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetNextPosition(nextCoarsePathPosIndex, out nextNextPosition)) {
							nextNextPosition = default(PathUnit.Position);
						}

						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextNextPosition, nextPosition, nextLaneId, nextSegOffset, currentPosition, curLaneId, currentPosition.m_offset, targetPosIndex, out bezier.d, out nextSegDir, out curMaxSpeed);

#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Next next path position needs to be calculated. Used triple-calculation. IN: nextNextPosition=[seg={nextNextPosition.m_segment}, lane={nextNextPosition.m_lane}, off={nextNextPosition.m_offset}], nextPosition=[seg={nextPosition.m_segment}, lane={nextPosition.m_lane}, off={nextPosition.m_offset}], nextLaneId={nextLaneId}, nextSegOffset={nextSegOffset}, currentPosition=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, off={currentPosition.m_offset}], curLaneId={curLaneId}, targetPosIndex={targetPosIndex}, OUT: bezier.d={bezier.d}, nextSegDir={nextSegDir}, curMaxSpeed={curMaxSpeed}");
						}
#endif
					} else {
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, nextLaneId, nextSegOffset, out bezier.d, out nextSegDir, out curMaxSpeed);

#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Next next path position needs not to be calculated. Used regular calculation. IN: nextPosition=[seg={nextPosition.m_segment}, lane={nextPosition.m_lane}, off={nextPosition.m_offset}], nextLaneId={nextLaneId}, nextSegOffset={nextSegOffset}, OUT: bezier.d={bezier.d}, nextSegDir={nextSegDir}, curMaxSpeed={curMaxSpeed}");
						}
#endif
					}

					if (curMaxSpeed < 0.01f || (netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_flags & (NetSegment.Flags.Collapsed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None) {
						if (targetPosIndex <= 0) {
							vehicleData.m_lastPathOffset = pathOffset;
						}
						targetPos = bezier.a;
						targetPos.w = 0f;
						while (targetPosIndex < maxTargetPosIndex) {
							vehicleData.SetTargetPos(targetPosIndex++, targetPos);
						}

#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Low max speed or segment flooded/collapsed. FINISH. curMaxSpeed={curMaxSpeed}, flags={netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_flags}, vehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}, targetPos={targetPos}, targetPosIndex={targetPosIndex}");
						}
#endif
						return;
					}

					if (currentPosition.m_offset == 0) {
						curSegDir = -curSegDir;
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): currentPosition.m_offset == 0: inverted curSegDir={curSegDir}");
						}
#endif
					}

					if (nextSegOffset < nextPosition.m_offset) {
						nextSegDir = -nextSegDir;
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): nextSegOffset={nextSegOffset} < nextPosition.m_offset={nextPosition.m_offset}: inverted nextSegDir={nextSegDir}");
						}
#endif
					}

					curSegDir.Normalize();
					nextSegDir.Normalize();
					float dist;
					NetSegment.CalculateMiddlePoints(bezier.a, curSegDir, bezier.d, nextSegDir, true, true, out bezier.b, out bezier.c, out dist);

#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Direction vectors normalied. curSegDir={curSegDir}, nextSegDir={nextSegDir}");
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Calculated bezier middle points. IN: bezier.a={bezier.a}, curSegDir={curSegDir}, bezier.d={bezier.d}, nextSegDir={nextSegDir}, true, true, OUT: bezier.b={bezier.b}, bezier.c={bezier.c}, dist={dist}");
					}
#endif

					if (dist > 1f) {
						ushort nextNodeId;
						if (nextSegOffset == 0) {
							nextNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_startNode;
						} else if (nextSegOffset == 255) {
							nextNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_endNode;
						} else {
							nextNodeId = 0;
						}

						float curve = 1.57079637f * (1f + Vector3.Dot(curSegDir, nextSegDir));
						if (dist > 1f) {
							curve /= dist;
						}

						curMaxSpeed = Mathf.Min(curMaxSpeed, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve));

#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): dist > 1f. nextNodeId={nextNodeId}, curve={curve}, curMaxSpeed={curMaxSpeed}, pathOffset={pathOffset}");
						}
#endif

						// update node target positions
						while (pathOffset < 255) {
							float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
							int pathOffsetDelta;
							if (distDiff < 0f) {
								pathOffsetDelta = 8;
							} else {
								pathOffsetDelta = 8 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (dist + 1f)));
							}

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Preparing to update node target positions (1). pathOffset={pathOffset}, distDiff={distDiff}, pathOffsetDelta={pathOffsetDelta}");
							}
#endif

							pathOffset = (byte)Mathf.Min((int)pathOffset + pathOffsetDelta, 255);
							Vector3 bezierPos = bezier.Position((float)pathOffset * 0.003921569f);
							targetPos.Set(bezierPos.x, bezierPos.y, bezierPos.z, Mathf.Min(targetPos.w, curMaxSpeed));
							float sqrMagnitude2 = (bezierPos - refPos).sqrMagnitude;

#if DEBUG
							if (debug) {
								Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Preparing to update node target positions (2). pathOffset={pathOffset}, bezierPos={bezierPos}, targetPos={targetPos}, sqrMagnitude2={sqrMagnitude2}");
							}
#endif

							if (sqrMagnitude2 >= minSqrDistA) {
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): sqrMagnitude2={sqrMagnitude2} >= minSqrDistA={minSqrDistA}");
								}
#endif
								if (targetPosIndex <= 0) {
									vehicleData.m_lastPathOffset = pathOffset;
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): targetPosIndex <= 0: Setting vehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}");
									}
#endif
								}

								if (nextNodeId != 0) {
									this.UpdateNodeTargetPos(vehicleID, ref vehicleData, nextNodeId, ref netManager.m_nodes.m_buffer[(int)nextNodeId], ref targetPos, targetPosIndex);
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): nextNodeId={nextNodeId} != 0: Updated node target position. targetPos={targetPos}, targetPosIndex={targetPosIndex}");
									}
#endif
								}

								vehicleData.SetTargetPos(targetPosIndex++, targetPos);
								minSqrDistA = minSqrDistanceB;
								refPos = targetPos;
								targetPos.w = 1000f;
#if DEBUG
								if (debug) {
									Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): After updating node target positions. minSqrDistA={minSqrDistA}, refPos={refPos}");
								}
#endif
								if (targetPosIndex == maxTargetPosIndex) {
#if DEBUG
									if (debug) {
										Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): targetPosIndex == maxTargetPosIndex ({maxTargetPosIndex}). FINISH.");
									}
#endif
									return;
								}
							}
						}
					}
				} else {
					PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);
#if DEBUG
					if (debug) {
						Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Same lane or cargo lane. curLaneId={curLaneId}, nextLaneId={nextLaneId}, laneInfo.m_laneType={laneInfo.m_laneType}, targetPos={targetPos}, nextSegOffset={nextSegOffset}");
					}
#endif
				}

				// check for arrival
				if (targetPosIndex <= 0) {
					if ((netManager.m_segments.m_buffer[nextPosition.m_segment].m_flags & NetSegment.Flags.Untouchable) != 0 && (netManager.m_segments.m_buffer[currentPosition.m_segment].m_flags & NetSegment.Flags.Untouchable) == NetSegment.Flags.None) {
						ushort ownerBuildingId = NetSegment.FindOwnerBuilding(nextPosition.m_segment, 363f);
						if (ownerBuildingId != 0) {
							BuildingManager buildingMan = Singleton<BuildingManager>.instance;
							BuildingInfo ownerBuildingInfo = buildingMan.m_buildings.m_buffer[ownerBuildingId].Info;
							InstanceID itemID = default(InstanceID);
							itemID.Vehicle = vehicleID;
							ownerBuildingInfo.m_buildingAI.EnterBuildingSegment(ownerBuildingId, ref buildingMan.m_buildings.m_buffer[ownerBuildingId], nextPosition.m_segment, nextPosition.m_offset, itemID);
						}
					}

					if (nextCoarsePathPosIndex == 0) {
						Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
					}
					if (nextCoarsePathPosIndex >= (int)(pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_positionCount - 1) && pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_nextPathUnit == 0u && vehicleID != 0) {
#if DEBUG
						if (debug) {
							Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Arriving at destination. targetPosIndex={targetPosIndex}, nextCoarsePathPosIndex={nextCoarsePathPosIndex}");
						}
#endif
						this.ArrivingToDestination(vehicleID, ref vehicleData);
					}
				}

				// prepare next loop iteration: go to next path position
				pathId = nextPathId;
				finePathPosIndex = (byte)(nextCoarsePathPosIndex << 1);
				pathOffset = nextSegOffset;
				if (targetPosIndex <= 0) {
					vehicleData.m_pathPositionIndex = finePathPosIndex;
					vehicleData.m_lastPathOffset = pathOffset;
					vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | nextSegmentInfo.m_setVehicleFlags);
					if (this.LeftHandDrive(nextLaneInfo)) {
						vehicleData.m_flags |= Vehicle.Flags.LeftHandDrive;
					} else {
						vehicleData.m_flags &= (Vehicle.Flags.Created | Vehicle.Flags.Deleted | Vehicle.Flags.Spawned | Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget | Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped | Vehicle.Flags.Leaving | Vehicle.Flags.Arriving | Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff | Vehicle.Flags.Flying | Vehicle.Flags.Landing | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.Importing | Vehicle.Flags.Exporting | Vehicle.Flags.Parking | Vehicle.Flags.CustomName | Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading | Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic | Vehicle.Flags.Underground | Vehicle.Flags.Transition | Vehicle.Flags.InsideBuilding);
					}
				}
				currentPosition = nextPosition;
				curLaneId = nextLaneId;
				laneInfo = nextLaneInfo;
				firstIter = false; // NON-STOCK CODE

#if DEBUG
				if (debug) {
					Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Prepared next main loop iteration. currentPosition=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, off={currentPosition.m_offset}], curLaneId={curLaneId}");
				}
#endif
			}
#if DEBUG
			if (debug) {
				Log._Debug($"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): Reached end of method body. FINISH.");
			}
#endif
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int FindBestLane(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position) {
			Log.Error("CustomVehicleAI.FindBestLane called");
			return 0;
		}
	}
}

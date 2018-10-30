using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.Util;
using UnityEngine;
using ColossalFramework.Math;

namespace TrafficManager.Manager.Impl {
	public class EmergencyBehaviorManager : AbstractFeatureManager, IEmergencyBehaviorManager {
		public static EmergencyBehaviorManager Instance { get; private set; } = null;

		private static readonly Vector3 Y_BASE = new Vector3(0, 1, 0);
		private static readonly Vector3 DUMMY_VECTOR = new Vector3(0, 0, 0);
		public const float VEHICLEGRID_CELL_SIZE = 32f;
		public const int VEHICLEGRID_RESOLUTION = 540;

		/// <summary>
		/// Holds all segments where a rescue lane should be established in the middle of the road.
		/// </summary>
		public int[] EmergencySegments { get; private set; }

		/// <summary>
		/// Grid containing vehicles evading on a parking lane
		/// </summary>
		private ushort[] parkingLaneVehicleGrid;

		static EmergencyBehaviorManager() {
			Instance = new EmergencyBehaviorManager();
		}
		
		private EmergencyBehaviorManager() {
			EmergencySegments = new int[NetManager.MAX_SEGMENT_COUNT];
			parkingLaneVehicleGrid = new ushort[VEHICLEGRID_RESOLUTION * VEHICLEGRID_RESOLUTION];
		}

		public EmergencyBehavior GetEmergencyBehavior(bool force, PathUnit.Position position, uint laneId, ref NetSegment segment, ref ExtSegment extSegment) {
			if (! force && ! IsEmergencyActive(position.m_segment)) {
				return EmergencyBehavior.None;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			/*float reservedSpace = 0f;
			Services.NetService.ProcessLane(laneId, delegate (uint lId, ref NetLane lane) {
				reservedSpace = lane.GetReservedSpace();
				return true;
			});*/
			
			if (netManager.m_lanes.m_buffer[laneId].GetReservedSpace() < GlobalConfig.Instance.EmergencyAI.MinReservedSpace) {
				if (force && extSegment.highway) {
					return EmergencyBehavior.InnerLane;
				} else {
					return EmergencyBehavior.None;
				}
			}

			NetInfo segmentInfo = segment.Info;
			if (segmentInfo != null && segmentInfo.m_lanes != null) {
				NetInfo.Direction finalDir = NetInfo.Direction.Forward;
				int numSimilarLanes = 1;
				int outerLaneIndex = 0;

				if (position.m_lane < segmentInfo.m_lanes.Length) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[position.m_lane];
					numSimilarLanes = laneInfo.m_similarLaneCount;
					finalDir = (laneInfo.m_finalDirection & NetInfo.Direction.Both);
					outerLaneIndex = Constants.ManagerFactory.RoutingManager.CalcOuterSimilarLaneIndex(laneInfo);
				}

				//Constants.ManagerFactory.ExtSegmentManager.ExtSegments[position.m_segment]
				if (extSegment.highway) {
					return EmergencyBehavior.RescueLane;
					/*if (extSegment.oneWay) {
						if (Options.advancedAI) {
							ITrafficMeasurementManager trafficMeasurementMan = Constants.ManagerFactory.TrafficMeasurementManager;

							if (
								trafficMeasurementMan.LaneTrafficData[position.m_segment] != null &&
								position.m_lane < trafficMeasurementMan.LaneTrafficData[position.m_segment].Length
							) {
								int meanSpeedInPercent = trafficMeasurementMan.LaneTrafficData[position.m_segment][position.m_lane].meanSpeed / (TrafficMeasurementManager.REF_REL_SPEED / TrafficMeasurementManager.REF_REL_SPEED_PERCENT_DENOMINATOR);
								if (meanSpeedInPercent <= GlobalConfig.Instance.EmergencyAI.HighwayRescueLaneMaxRelMeanSpeed) {
									return EmergencyBehavior.RescueLane;
								} else {
									return EmergencyBehavior.None;
								}
							}
						}

						return EmergencyBehavior.RescueLane;
					} else {
						return EmergencyBehavior.RescueLane;
					}*/
				} else if (segmentInfo.m_hasParkingSpaces) {
					if (!Constants.ManagerFactory.ParkingRestrictionsManager.IsParkingAllowed(position.m_segment, finalDir)) {
						return EmergencyBehavior.DriveOnParkingLane;
					} else if (outerLaneIndex == 0) {
						return EmergencyBehavior.EvadeOnParkingLane;
					} else {
						return EmergencyBehavior.None;
					}
				} else if (segmentInfo.m_canCrossLanes) {
					return EmergencyBehavior.RescueLane;
				} else if (numSimilarLanes > 1) {
					return EmergencyBehavior.InnerLane;
				}
			}
			return EmergencyBehavior.None;
		}

		public int GetRescueLane(EmergencyBehavior behavior, PathUnit.Position position) {
			int? bestLaneIndex = null;
			int? bestOuterIndex = null;

			if (behavior == EmergencyBehavior.None) {
				return position.m_lane;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (segmentInfo != null && segmentInfo.m_lanes != null) {
				NetInfo.Direction finalDir = NetInfo.Direction.Forward;

				if (position.m_lane < segmentInfo.m_lanes.Length) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[position.m_lane];
					finalDir = (laneInfo.m_finalDirection & NetInfo.Direction.Both);
				}

				uint curLaneId = netManager.m_segments.m_buffer[position.m_segment].m_lanes;
				for (int i = 0; i < segmentInfo.m_lanes.Length; i++) {
					if (curLaneId == 0) {
						break;
					}

					NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];
					if (
						(laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) != 0 &&
						(laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram)) != 0 &&
						(laneInfo.m_finalDirection & finalDir) == finalDir
					) {
						int outerIndex = Constants.ManagerFactory.RoutingManager.CalcOuterSimilarLaneIndex(laneInfo);
						if (
							bestOuterIndex == null ||
							((behavior == EmergencyBehavior.RescueLane || behavior == EmergencyBehavior.InnerLane) && outerIndex > bestOuterIndex) ||
							((behavior == EmergencyBehavior.DriveOnParkingLane || behavior == EmergencyBehavior.EvadeOnParkingLane) && outerIndex < bestOuterIndex)
						) {
							bestOuterIndex = outerIndex;
							bestLaneIndex = i;
						}
					}

					curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				}
			}

			return bestLaneIndex != null ? (int)bestLaneIndex : position.m_lane;
		}

		public int GetEvasionLane(EmergencyBehavior behavior, PathUnit.Position position) {
			int? bestLaneIndex = null;

			if (behavior != EmergencyBehavior.InnerLane) {
				return position.m_lane;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			NetInfo segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (segmentInfo != null && segmentInfo.m_lanes != null) {
				NetInfo.Direction finalDir = NetInfo.Direction.Forward;
				int originalOuterIndex = 0;
				int numSimilarLanes = 1;

				if (position.m_lane < segmentInfo.m_lanes.Length) {
					NetInfo.Lane laneInfo = segmentInfo.m_lanes[position.m_lane];
					finalDir = (laneInfo.m_finalDirection & NetInfo.Direction.Both);
					originalOuterIndex = Constants.ManagerFactory.RoutingManager.CalcOuterSimilarLaneIndex(laneInfo);
					numSimilarLanes = laneInfo.m_similarLaneCount;
				}

				if (originalOuterIndex != numSimilarLanes - 1) {
					return position.m_lane;
				}

				int? bestLaneDist = null;
				uint curLaneId = netManager.m_segments.m_buffer[position.m_segment].m_lanes;
				for (int i = 0; i < segmentInfo.m_lanes.Length; i++) {
					if (curLaneId == 0) {
						break;
					}

					NetInfo.Lane laneInfo = segmentInfo.m_lanes[i];
					if (
						(laneInfo.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) != 0 &&
						(laneInfo.m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram)) != 0 &&
						(laneInfo.m_finalDirection & finalDir) == finalDir
					) {
						int outerIndex = Constants.ManagerFactory.RoutingManager.CalcOuterSimilarLaneIndex(laneInfo);
						int laneDist = originalOuterIndex - outerIndex;
						if (laneDist > 0 && (bestLaneDist == null || laneDist < bestLaneDist)) {
							bestLaneDist = laneDist;
							bestLaneIndex = i;
						}
					}

					curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				}
			}

			return bestLaneIndex != null ? (int)bestLaneIndex : position.m_lane;
		}

		public bool IsEmergencyActive(ushort segmentId) {
			if (!Options.emergencyAI) {
				return false;
			}

			return EmergencySegments[segmentId] > 0;
		}

		public void RegisterEmergencyVehicle(ushort segmentId, bool register) {
			if (! Options.emergencyAI) {
				return;
			}

#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24];
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.SetRescueLane({segmentId}, {register}) called.");
#endif

			EmergencySegments[segmentId] = Math.Max(0, EmergencySegments[segmentId] + (register ? 1 : -1));
		}

		/*public bool ApplyRescueTranslation(ushort vehicleId, ref Vehicle vehicle, PathUnit.Position position, ref NetSegment segment, ref NetLane lane, ref Vector3 pos, Vector3 dir, out float sqrDist) {
			bool result = ApplyRescueTranslation(vehicleId, ref vehicle, position, ref segment, ref lane, ref pos, dir);

			if (! result) {
				sqrDist = 0f;
				return false;
			} else {
				//sqrDist = Vector3.Project(pos - vehicle.GetLastFramePosition(), pos - oldPos).sqrMagnitude;
				sqrDist = (pos - vehicle.GetLastFramePosition()).sqrMagnitude;
				return true;
			}
		}*/

		public void AdaptSegmentPosition(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle, PathUnit.Position position, uint laneId, ref byte offset, ref Vector3 pos, Vector3 dir, ref float maxSpeed/*, out EmergencyBehavior behavior*/) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId);
			if (debug) {
				Log._Debug($"EmergencyBehaviorManager.AdaptSegmentPosition({vehicleId}) called. position=[seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], laneId={laneId}, offset={offset}, pos={pos}, dir={dir}, maxSpeed={maxSpeed}");
			}
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			EmergencyBehavior behavior;
			ApplyRescueTranslation(vehicleId, ref vehicle, ref extVehicle, position, laneId, ref offset, ref netManager.m_segments.m_buffer[position.m_segment], ref pos, dir, out behavior);

#if DEBUG
			if (debug) {
				Log._Debug($"EmergencyBehaviorManager.AdaptSegmentPosition({vehicleId}): Applied rescue translation: pos={pos}, behavior={behavior}");
			}
#endif

			if (behavior != EmergencyBehavior.None) {
				extVehicle.flags |= ExtVehicleFlags.Emergency;
			} else {
				extVehicle.flags &= ~ExtVehicleFlags.Emergency;
			}

			if ((extVehicle.flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None) {
#if DEBUG
				if (debug) {
					Log._Debug($"EmergencyBehaviorManager.AdaptSegmentPosition({vehicleId}): Vehicle must stop.");
				}
#endif
				/*if ((vehicle.GetLastFramePosition() - pos).sqrMagnitude <= GlobalConfig.Instance.EmergencyAI.MaxEvasionStopSqrDistance) {
					maxSpeed = 0f;
				} else {
					maxSpeed = VehicleBehaviorManager.MIN_SPEED;
				}*/
				maxSpeed = VehicleBehaviorManager.MIN_SPEED;
			} else if (
				behavior != EmergencyBehavior.None &&
				extVehicle.vehicleType != ExtVehicleType.Emergency &&
				Constants.ManagerFactory.ExtSegmentManager.ExtSegments[position.m_segment].highway
			) {
				maxSpeed /= 2f;
				maxSpeed = Mathf.Min(maxSpeed, VehicleBehaviorManager.MAX_EVASION_SPEED);
				maxSpeed = Mathf.Max(maxSpeed, VehicleBehaviorManager.MIN_SPEED);
			}
		}

		protected void ApplyRescueTranslation(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle, PathUnit.Position position, uint laneId, ref byte offset, ref NetSegment segment, ref Vector3 pos, Vector3 dir, out EmergencyBehavior behavior) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId);

			if (debug) {
				Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}) called.");
			}
#endif
			behavior = EmergencyBehavior.None;

			if (!Options.emergencyAI) {
				return;
			}

			bool stopped = (extVehicle.flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None;

			if ((extVehicle.vehicleType & ExtVehicleType.RoadVehicle) == ExtVehicleType.None || EmergencySegments[position.m_segment] <= 0) {
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): No road vehicle or no emergency. Unstopping vehicle. ABORT.");
#endif
				UnstopVehicle(ref extVehicle);
				return;
			}

			behavior = GetEmergencyBehavior(false, position, laneId, ref segment, ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[position.m_segment]);
#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): Emergency behavior: {behavior}");
#endif
			if (behavior == EmergencyBehavior.None) {
				// Everyone drives on their designated lane
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): No emergency. Unstopping vehicle. ABORT.");
#endif
				UnstopVehicle(ref extVehicle);
				return;
			} else if (behavior == EmergencyBehavior.InnerLane || (behavior == EmergencyBehavior.DriveOnParkingLane && extVehicle.vehicleType != ExtVehicleType.Emergency)) {
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): Driving on full lane. Unstopping vehicle. FINISH.");
#endif
				// Regular vehicles drive on a full lane
				UnstopVehicle(ref extVehicle);
				return;
			} else if (behavior == EmergencyBehavior.EvadeOnParkingLane) {
				if (extVehicle.vehicleType == ExtVehicleType.Emergency) {
					// Emergency vehicles drive on a full lane, regular vehicle evade on the parking lane
#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): Emergency vehicle not evading on parking lane. FINISH.");
#endif
					UnstopVehicle(ref extVehicle);
					return;
				} else if (stopped) {
#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): Stop required. Using stop position. FINISH.");
#endif
					// A stopping position has already been determined. Keep this position.
					pos = extVehicle.stopPosition;
					offset = extVehicle.stopOffset;
					return;
				}
			}

			bool goingToStartNode = position.m_offset == 0; // vehicles moves towards start node
			NetInfo segmentInfo = segment.Info;
			NetInfo.Direction finalDir = NetInfo.Direction.Forward;
			if (segmentInfo == null || position.m_lane >= segmentInfo.m_lanes.Length) {
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.ApplyRescueTranslation({vehicleId}, [seg={position.m_segment}, lane={position.m_lane}, off={position.m_offset}], {offset}, {pos}, {dir}): Invalid lane index {position.m_lane}. ABORT.");
#endif
				UnstopVehicle(ref extVehicle);
				return;
			}

			NetInfo.Lane laneInfo = segmentInfo.m_lanes[position.m_lane];
			finalDir = (laneInfo.m_finalDirection & NetInfo.Direction.Both);
			if (
				extVehicle.vehicleType != ExtVehicleType.Emergency &&
				behavior == EmergencyBehavior.EvadeOnParkingLane
			) {
				// Vehicles on the outer lane search for a free parking position to evade the emergency vehicle
				NetManager netManager = Singleton<NetManager>.instance;

				// Find closest parking lane
				uint parkingLaneId;
				NetInfo.Lane parkingLaneInfo;
				if (segment.GetClosestLane(laneId, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out parkingLaneId, out parkingLaneInfo) &&
					parkingLaneInfo.m_finalDirection == laneInfo.m_direction) {
#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Found parking lane. parkingLaneId={parkingLaneId}");
#endif
					// Vehicle length
					float length = extVehicle.totalLength + 1f;

					// Calculate desired evasion/stop offsets
					int evasionOffset = offset;
					int searchOffsetDelta = Mathf.Max(0, Mathf.CeilToInt(length * 256f / (netManager.m_lanes.m_buffer[parkingLaneId].m_length + 1f)));
					int stopOffsetDelta = Mathf.Max(0, Mathf.CeilToInt((length * 1.5f) * 256f / (netManager.m_lanes.m_buffer[parkingLaneId].m_length + 1f)));
					int stopOffset;
					int searchOffset;
					if (goingToStartNode) {
						searchOffset = evasionOffset - searchOffsetDelta;
						stopOffset = evasionOffset - stopOffsetDelta;
					} else {
						searchOffset = evasionOffset + searchOffsetDelta;
						stopOffset = evasionOffset + stopOffsetDelta;
					}

#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Calculated desired evasionOffset={evasionOffset}, searchOffset={searchOffset}, stopOffset={stopOffset}");
#endif

					if (stopOffset <= 0 || stopOffset >= 255 || stopOffset == evasionOffset || searchOffset <= 0 || searchOffset >= 255 || searchOffset == evasionOffset) {
#if DEBUG
						if (debug)
							Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): stopOffset/searchOffset out of bounds or equal to evasionOffset");
#endif
						return;
					}

					float evasionOff = evasionOffset * Constants.BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
					float searchOff = searchOffset * Constants.BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;
					float stopOff = stopOffset * Constants.BYTE_TO_FLOAT_OFFSET_CONVERSION_FACTOR;

#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Calculated desired evasionOff={evasionOff}, searchOff={searchOff}, stopOff={stopOff}");
#endif

					// Ensure there are (2 * length) units of free space on the parking lane
					float minPos;
					float maxPos;
					// Check for parked vehicles
					if (CustomPassengerCarAI.CheckOverlap(0, ref netManager.m_lanes.m_buffer[parkingLaneId].m_bezier, searchOff, 2.25f * length, out minPos, out maxPos)) {
#if DEBUG
						if (debug)
							Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Overlap with parked vehicle detected. minPos={minPos}, maxPos={maxPos}");
#endif
						return;
					}

					// Check for evading vehicles
					if (CheckOverlap(vehicleId, ref netManager.m_lanes.m_buffer[parkingLaneId].m_bezier, searchOff, 2.25f * length)) {
#if DEBUG
						if (debug)
							Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Overlap with evading vehicle detected.");
#endif

						return;
					}

					// Determine evasion/stop positions
					Vector3 evasionPos = netManager.m_lanes.m_buffer[parkingLaneId].m_bezier.Position(evasionOff);
					Vector3 stopPos = netManager.m_lanes.m_buffer[parkingLaneId].m_bezier.Position(stopOff);
					pos.Set(evasionPos.x, evasionPos.y, evasionPos.z);
					offset = (byte)evasionOffset;
					StopVehicle(ref extVehicle, stopPos, (byte)stopOffset);

#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}, {position.m_offset}): Stopping vehicle on position {stopPos}, evading on {evasionPos}.");
#endif
					return;
				}
			}

			// calculate evasion position
			float evasionFactor = behavior == EmergencyBehavior.RescueLane ? 0.5f : 0.8f;
			Vector3 evasionDir = Vector3.Normalize(Vector3.Cross(Y_BASE, dir)) * laneInfo.m_width * evasionFactor;
#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}): evasionDir={evasionDir}, dir={dir}, pos={pos}");
#endif
			bool invert = goingToStartNode;
			if (behavior == EmergencyBehavior.RescueLane && extVehicle.vehicleType == ExtVehicleType.Emergency) {
				invert = !invert;
			}

			if (invert) {
				evasionDir = -evasionDir;
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}): Emergency vehicle or LHD: evasionDir={evasionDir}");
#endif
			}

			pos.Set(pos.x + evasionDir.x, pos.y + evasionDir.y, pos.z + evasionDir.z);

#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.ApplyRescueLane({vehicleId}, {position.m_segment}, {position.m_lane}): Position adapted. pos={pos}");
#endif
			return;
		}

		private void StopVehicle(ref ExtVehicle extVehicle, Vector3 pos, byte offset) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == extVehicle.vehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.StopVehicle({extVehicle.vehicleId}, {pos}, {offset}) called.");
#endif
			if ((extVehicle.flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None) {
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.StopVehicle({extVehicle.vehicleId}, {pos}, {offset}): Vehicle is already stopped. Unstopping first.");
#endif
				UnstopVehicle(ref extVehicle);
			}

			extVehicle.stopPosition = pos;
			extVehicle.stopOffset = offset;
			extVehicle.flags |= ExtVehicleFlags.Stopped;
			AddToGrid(ref extVehicle);
#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.StopVehicle({extVehicle.vehicleId}, {pos}, {offset}): Vehicle is stopped now.");
#endif
		}

		public void UnstopVehicle(ref ExtVehicle extVehicle) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == extVehicle.vehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.UnstopVehicle({extVehicle.vehicleId}) called.");
#endif
			if ((extVehicle.flags & ExtVehicleFlags.Stopped) == ExtVehicleFlags.None) {
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.UnstopVehicle({extVehicle.vehicleId}): Not stopping vehicle: Vehicle is not stopped.");
#endif
				return;
			}

			RemoveFromGrid(ref extVehicle);
			extVehicle.flags &= ~ExtVehicleFlags.Stopped;
			extVehicle.stopPosition = DUMMY_VECTOR;
			extVehicle.stopOffset = 0;
		}

		/*public bool CheckVehicleStopped(ref ExtVehicle extVehicle, ushort segmentId) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == extVehicle.vehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.UnstopVehicle({extVehicle.vehicleId}) called.");
#endif

			if (!Options.emergencyAI) {
				return false;
			}

			if (
				(extVehicle.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.Emergency &&
				IsEmergencyActive(segmentId) &&
				(extVehicle.flags & ExtVehicleFlags.Stopped) != ExtVehicleFlags.None
			) {
				return true;
			} else {
				UnstopVehicle(ref extVehicle);
				return false;
			}
		}*/

		public bool CheckOverlap(ushort ignoreVehicleId, ref Bezier3 bezier, float offset, float length) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == ignoreVehicleId);
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {offset}, {length}) called.");
#endif
			VehicleManager vehMan = Singleton<VehicleManager>.instance;
			IExtVehicleManager extVehMan = Constants.ManagerFactory.ExtVehicleManager;

			float startOffset = bezier.Travel(offset, length * -0.5f);
			float endOffset = bezier.Travel(offset, length * 0.5f);
			if (startOffset < 0.001f || endOffset > 0.999f) {
				return true;
			}
			bool overlap = false;
			Vector3 posAtOffset = bezier.Position(offset);
			Vector3 tangentAtOffset = bezier.Tangent(offset);
			Vector3 posAtStart = bezier.Position(startOffset);
			Vector3 posAtEnd = bezier.Position(endOffset);
			Vector3 searchMinPos = Vector3.Min(posAtStart, posAtEnd);
			Vector3 searchMaxPos = Vector3.Max(posAtStart, posAtEnd);

			int startX = Mathf.Max((int)((searchMinPos.x - 10f) / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), 0);
			int endX = Mathf.Min((int)((searchMaxPos.x + 10f) / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), VEHICLEGRID_RESOLUTION - 1);
			int startZ = Mathf.Max((int)((searchMinPos.z - 10f) / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), 0);
			int endZ = Mathf.Min((int)((searchMaxPos.z + 10f) / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), VEHICLEGRID_RESOLUTION - 1);

#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {offset}, {length}): startX={startX}, endX={endX}, startZ={startZ}, endZ={endZ}");
#endif

			for (int i = startZ; i <= endZ; i++) {
				for (int j = startX; j <= endX; j++) {
					ushort curVehicleId = parkingLaneVehicleGrid[i * VEHICLEGRID_RESOLUTION + j];
					int numIter = 0;
					while (curVehicleId != 0) {
						curVehicleId = CheckOverlap(ignoreVehicleId, ref bezier, posAtOffset, tangentAtOffset, /*offset,*/ length, ref vehMan.m_vehicles.m_buffer[curVehicleId], ref extVehMan.ExtVehicles[curVehicleId], ref overlap/*, ref searchMinPos, ref searchMaxPos*/);
						if (overlap) {
#if DEBUG
							if (debug)
								Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {offset}, {length}): Overlap with vehicle {curVehicleId} detected at X={j}, Z={i}.");
#endif
							return true;
						}

						if (++numIter > VehicleManager.MAX_VEHICLE_COUNT) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
			}
#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {offset}, {length}): No overlap detected.");
#endif
			return false;
		}

		private static ushort CheckOverlap(ushort ignoreVehicleId, ref Bezier3 bezier, Vector3 pos, Vector3 dir, /*float offset,*/ float length, ref Vehicle otherVehicle, ref ExtVehicle extOtherVehicle, ref bool overlap/*, ref float minPos, ref float maxPos*/) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == ignoreVehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {pos}, {dir}, {length}, {extOtherVehicle.vehicleId}) called.");
#endif
			if (extOtherVehicle.vehicleId != ignoreVehicleId) {
				VehicleInfo info = otherVehicle.Info;
				Vector3 otherPos = extOtherVehicle.stopPosition;
				Vector3 dirToOther = otherPos - pos;
				float conflictLength = (length + info.m_generatedInfo.m_size.z) * 0.5f + 1f;
				float dirToOtherLength = dirToOther.magnitude;
				if (dirToOtherLength < conflictLength - 0.5f) {
					overlap = true;
#if DEBUG
					if (debug)
						Log._Debug($"EmergencyBehaviorManager.CheckOverlap({ignoreVehicleId}, {bezier}, {pos}, {dir}, {length}, {extOtherVehicle.vehicleId}): Overlap with vehicle {extOtherVehicle.vehicleId} detected. otherPos={otherPos}, dirToOther={dirToOther}, dirToOtherLength={dirToOtherLength}, conflictLength={conflictLength}");
#endif

						/*float travelMin;
						float travelMax;
						if (Vector3.Dot(dirToOther, dir) < 0f) {
							// both vehicles look in different directions (< -90° or > +90°): inverse search direction
							dirToOtherLength = -dirToOtherLength;
						}

						travelMin = dirToOtherLength - conflictLength;
						travelMax = dirToOtherLength + conflictLength;

						minPos = Mathf.Min(minPos, bezier.Travel(offset, travelMin));
						maxPos = Mathf.Max(maxPos, bezier.Travel(offset, travelMax));*/
				}
			}
			return extOtherVehicle.nextParkingLaneVehicleId;
		}

		private void CalculateGridPosition(Vector3 pos, out int gridX, out int gridZ) {
			gridX = Mathf.Clamp((int)(pos.x / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), 0, VEHICLEGRID_RESOLUTION - 1);
			gridZ = Mathf.Clamp((int)(pos.z / VEHICLEGRID_CELL_SIZE + VEHICLEGRID_RESOLUTION / 2f), 0, VEHICLEGRID_RESOLUTION - 1);
		}

		private void AddToGrid(ref ExtVehicle extVehicle) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == extVehicle.vehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.AddToGrid({extVehicle.vehicleId}) called.");
#endif

			int gridX;
			int gridZ;
			CalculateGridPosition(extVehicle.stopPosition, out gridX, out gridZ);
			int index = gridZ * VEHICLEGRID_RESOLUTION + gridX;

			extVehicle.nextParkingLaneVehicleId = parkingLaneVehicleGrid[index];
			parkingLaneVehicleGrid[index] = extVehicle.vehicleId;

#if DEBUG
			if (debug)
				Log._Debug($"EmergencyBehaviorManager.AddToGrid({extVehicle.vehicleId}): Added to grid at X={gridX}, Z={gridZ}, index={index}, extVehicle.nextParkingLaneVehicleId={extVehicle.nextParkingLaneVehicleId}");
#endif
		}

		private void RemoveFromGrid(ref ExtVehicle extVehicle) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[24] &&
				(GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) &&
				(GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == extVehicle.vehicleId);

			if (debug)
				Log._Debug($"EmergencyBehaviorManager.RemoveFromGrid({extVehicle.vehicleId}) called.");
#endif

			IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;

			int gridX;
			int gridZ;
			CalculateGridPosition(extVehicle.stopPosition, out gridX, out gridZ);
			int index = gridZ * VEHICLEGRID_RESOLUTION + gridX;

			ushort prevVehicleId = 0;
			ushort curVehId = parkingLaneVehicleGrid[index];
			int numIter = 0;
			while (curVehId != 0) {
				if (curVehId != extVehicle.vehicleId) {
					prevVehicleId = curVehId;
					curVehId = extVehicleMan.ExtVehicles[curVehId].nextParkingLaneVehicleId;
					if (++numIter > VehicleManager.MAX_VEHICLE_COUNT) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
					continue;
				}

				if (prevVehicleId == 0) {
					// replace first entry
					parkingLaneVehicleGrid[index] = extVehicle.nextParkingLaneVehicleId;
				} else {
					extVehicleMan.ExtVehicles[prevVehicleId].nextParkingLaneVehicleId = extVehicle.nextParkingLaneVehicleId;
				}
#if DEBUG
				if (debug)
					Log._Debug($"EmergencyBehaviorManager.RemoveFromGrid({extVehicle.vehicleId}): Removed from grid (prevVehicleId={prevVehicleId}).");
#endif
				break;
			}
			extVehicle.nextParkingLaneVehicleId = 0;
		}

		protected override void OnEnableFeatureInternal() {

		}

		protected override void OnDisableFeatureInternal() {
			Reset();
		}

		private void Reset() {
			for (int i = 0; i < EmergencySegments.Length; ++i) {
				EmergencySegments[i] = 0;
			}
			for (int i = 0; i < parkingLaneVehicleGrid.Length; ++i) {
				parkingLaneVehicleGrid[i] = 0;
			}

			IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
			for (int i = 0; i < VehicleManager.MAX_VEHICLE_COUNT; ++i) {
				extVehicleMan.ExtVehicles[i].flags &= ~ExtVehicleFlags.Stopped;
				extVehicleMan.ExtVehicles[i].stopPosition = DUMMY_VECTOR;
				extVehicleMan.ExtVehicles[i].nextParkingLaneVehicleId = 0;
			}
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			Reset();
		}
	}
}

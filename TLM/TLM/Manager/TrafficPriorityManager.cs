using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using System.Threading;
using TrafficManager.Util;
using TrafficManager.Traffic;
using TrafficManager.Geometry;

namespace TrafficManager.Manager {
	public class TrafficPriorityManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<int[]>>, ICustomDataManager<List<Configuration.PrioritySegment>> {
		public static TrafficPriorityManager Instance { get; private set; } = null;

		static TrafficPriorityManager() {
			Instance = new TrafficPriorityManager();
		}

		public const float MAX_SQR_STOP_VELOCITY = 0.01f;
		public const float MAX_SQR_YIELD_VELOCITY = 0.09f;
		public const float MAX_YIELD_VELOCITY = 0.3f;

		/// <summary>
		/// List of segments that are connected to roads with timed traffic lights or priority signs. Index: segment id
		/// </summary>
		public TrafficSegment[] TrafficSegments = null;

		private TrafficPriorityManager() {
			TrafficSegments = new TrafficSegment[Singleton<NetManager>.instance.m_segments.m_size];
		}

		public SegmentEnd AddPrioritySegment(ushort nodeId, ushort segmentId, SegmentEnd.PriorityType type) {
			if (!NetUtil.IsNodeValid(nodeId))
				return null;
			if (!NetUtil.IsSegmentValid(segmentId))
				return null;

#if DEBUG
			Log._Debug("adding PrioritySegment @ node " + nodeId + ", seg. " + segmentId + ", type " + type);
#endif

			SubscribeToSegmentGeometry(segmentId);

			SegmentEnd ret = null;
			var trafficSegment = TrafficSegments[segmentId];
			if (trafficSegment != null) { // do not replace with IsPrioritySegment!
				trafficSegment.Segment = segmentId;

#if DEBUG
				Log._Debug("Priority segment already exists. Node1=" + trafficSegment.Node1 + " Node2=" + trafficSegment.Node2);
#endif

				if (trafficSegment.Node1 == nodeId || trafficSegment.Node1 == 0) {
					// overwrite/add Node1
					if (trafficSegment.Instance1 != null)
						trafficSegment.Instance1.Destroy();

					trafficSegment.Node1 = nodeId;
					ret = new SegmentEnd(nodeId, segmentId, type);
					TrafficSegments[segmentId].Instance1 = ret;

					return ret;
				}

				if (trafficSegment.Node2 != 0) {
					// overwrite Node2
					trafficSegment.Instance2.Destroy();
					trafficSegment.Node2 = nodeId;
					ret = new SegmentEnd(nodeId, segmentId, type);
					trafficSegment.Instance2 = ret;
				} else {
					// add Node2
#if DEBUG
					Log._Debug("Adding as Node2");
#endif
					trafficSegment.Node2 = nodeId;
					ret = new SegmentEnd(nodeId, segmentId, type);
					trafficSegment.Instance2 = ret;
				}
			} else {
				// add Node1
#if DEBUG
				Log._Debug("Adding as Node1");
#endif
				trafficSegment = new TrafficSegment();
				trafficSegment.Segment = segmentId;
				trafficSegment.Node1 = nodeId;
				ret = new SegmentEnd(nodeId, segmentId, type);
				trafficSegment.Instance1 = ret;
				TrafficSegments[segmentId] = trafficSegment;
			}
			return ret;
		}

		public void RemovePrioritySegments(ushort nodeId) { // priorityNodes: OK
#if DEBUG
			Log._Debug($"TrafficPriorityManager.RemovePrioritySegments: Removing priority segments from node {nodeId}");
#endif

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				RemovePrioritySegment(nodeId, segmentId);
			}
		}

		public List<SegmentEnd> GetPrioritySegments(ushort nodeId) {
			List<SegmentEnd> ret = new List<SegmentEnd>();
			if (!NetUtil.IsNodeValid(nodeId)) {
				return ret;
			}

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				SegmentEnd end = GetPrioritySegment(nodeId, segmentId);
				if (end != null) {
					ret.Add(end);
				}
			}

			return ret;
		}

		public bool IsPrioritySegment(ushort nodeId, ushort segmentId) {
			if (nodeId <= 0 || segmentId <= 0) {
				return false;
			}

			if (TrafficSegments[segmentId] != null) {
				var prioritySegment = TrafficSegments[segmentId];

				/*NetManager netManager = Singleton<NetManager>.instance;
				if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
					RemovePrioritySegment(nodeId, segmentId);
					CustomTrafficLightsManager.Instance.RemoveSegmentLights(segmentId);
					return false;
				}*/

				if (prioritySegment.Node1 == nodeId || prioritySegment.Node2 == nodeId) {
					return true;
				}
			}

			return false;
		}

		public bool IsPriorityNode(ushort nodeId, bool onlyValidType=true) {
			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_nodes.m_buffer[nodeId].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
				return false;
			}

			for (int i = 0; i < 8; ++i) {
				ushort segmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(i);

				if (segmentId == 0)
					continue;

				SegmentEnd end = GetPrioritySegment(nodeId, segmentId);
				if (end != null && (!onlyValidType || end.Type != SegmentEnd.PriorityType.None)) {
					return true;
				}
			}

			return false;
		}

		public SegmentEnd GetPrioritySegment(ushort nodeId, ushort segmentId) {
			/*if (!IsPrioritySegment(nodeId, segmentId)) {
				return null;
			}*/

			var prioritySegment = TrafficSegments[segmentId];

			if (prioritySegment == null) {
				return null;
			}

			if (prioritySegment.Node1 == nodeId) {
				return prioritySegment.Instance1;
			}

			SegmentEnd ret = prioritySegment.Node2 == nodeId ?
				prioritySegment.Instance2 : null;
			return ret;
		}

		internal void RemovePrioritySegment(ushort segmentId) {
			if (TrafficSegments[segmentId] == null)
				return;

			var prioritySegment = TrafficSegments[segmentId];

			prioritySegment.Node1 = 0;
			if (prioritySegment.Instance1 != null)
				prioritySegment.Instance1.Destroy();
			prioritySegment.Instance1 = null;

			prioritySegment.Node2 = 0;
			if (prioritySegment.Instance2 != null)
				prioritySegment.Instance2.Destroy();
			prioritySegment.Instance2 = null;

			TrafficSegments[segmentId] = null;
			UnsubscribeFromSegmentGeometry(segmentId);
		}

		internal void RemovePrioritySegment(ushort nodeId, ushort segmentId) {
			if (TrafficSegments[segmentId] == null)
				return;
#if DEBUG
			Log._Debug($"TrafficPriorityManager.RemovePrioritySegment: Removing priority segment from segment {segmentId} @ {nodeId}");
#endif
			var prioritySegment = TrafficSegments[segmentId];

			if (prioritySegment.Node1 == nodeId) {
				prioritySegment.Node1 = 0;
				if (prioritySegment.Instance1 != null)
					prioritySegment.Instance1.Destroy();
				prioritySegment.Instance1 = null;
			}
			if (prioritySegment.Node2 == nodeId) {
				prioritySegment.Node2 = 0;
				if (prioritySegment.Instance2 != null)
					prioritySegment.Instance2.Destroy();
				prioritySegment.Instance2 = null;
			}

			if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0) {
				TrafficSegments[segmentId] = null;
				UnsubscribeFromSegmentGeometry(segmentId);
			}
		}

		/// <summary>
		/// Adds priority segments at a node
		/// </summary>
		/// <param name="nodeId"></param>
		/// <returns>number of priority segments added</returns>
		internal byte AddPriorityNode(ushort nodeId, bool overwrite=false) {
			if (nodeId <= 0)
				return 0;

			byte ret = 0;
			for (var i = 0; i < 8; i++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(i);

				if (segmentId == 0)
					continue;
				if (!overwrite && IsPrioritySegment(nodeId, segmentId))
					continue;
				/*if (SegmentGeometry.Get(segmentId).IsOutgoingOneWay(nodeId))
					continue;*/ // we need this for pedestrian traffic lights

				if (AddPrioritySegment(nodeId, segmentId, SegmentEnd.PriorityType.None) != null)
					++ret;
			}
			return ret;
		}

		/// <summary>
		/// Checks if a vehicle (the target vehicle) has to wait for other incoming vehicles at a junction with priority signs.
		/// </summary>
		/// <param name="targetVehicleId">target vehicle</param>
		/// <param name="targetVehicleData">target vehicle data</param>
		/// <param name="curPos">Current path unit the target vehicle is located at</param>
		/// <param name="nextPos">Next path unit the target vehicle will be located at</param>
		/// <returns>true if the target vehicle must wait for other vehicles, false otherwise</returns>
		public bool HasIncomingVehiclesWithHigherPriority(ushort targetVehicleId, ref Vehicle targetVehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
			try {
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				NetManager netManager = Singleton<NetManager>.instance;
				LaneConnectionManager connManager = LaneConnectionManager.Instance;
				VehicleStateManager vehStateManager = VehicleStateManager.Instance;

				uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

				ushort transitNodeId = VehicleState.GetTransitNodeId(ref curPos, ref nextPos);
				Vector3 transitNodePos = netManager.m_nodes.m_buffer[transitNodeId].m_position;

				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[transitNodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
					return false;
				}

#if DEBUG
				//bool debug = nodeId == 30634;
				//bool debug = transitNodeId == 21371;
				bool debug = false; // transitNodeId == 27423;
				if (debug) {
					Log._Debug($"HasIncomingVehicles: ##### Checking vehicle {targetVehicleId} at node {transitNodeId}. Coming from seg. {curPos.m_segment}, lane {curPos.m_lane}, going to seg. {nextPos.m_segment}, lane {nextPos.m_lane}");
				}
#else
				bool debug = false;
#endif

				/*VehicleState targetVehicleState = VehicleStateManager.GetVehicleState(targetVehicleId);
				if (targetVehicleState == null) {
#if DEBUG
					Log.Warning($"HasIncomingVehicles: vehicle {targetVehicleId} @ node {transitNodeId}: Target state is invalid!");
#endif
					return false;
				}*/

#if DEBUG
				if (debug) {
					Log._Debug($"HasIncomingVehicles: {targetVehicleId} @ {transitNodeId}, fromSegment: {curPos.m_segment}, toSegment: {nextPos.m_segment}");
				}
#endif

				var targetFromPrioritySegment = GetPrioritySegment(transitNodeId, curPos.m_segment);
				if (targetFromPrioritySegment == null) {
#if DEBUG
					if (debug) {
						Log._Debug($"HasIncomingVehicles: source priority segment not found.");
					}
#endif
					return false;
				}

				float targetTimeToTransitNode = Single.NaN;
				if (Options.simAccuracy <= 1) {
					Vector3 targetToNode = transitNodePos - targetVehicleData.GetLastFramePosition();
					Vector3 targetVel = targetVehicleData.GetLastFrameVelocity();
					float targetSpeed = targetVel.magnitude;
					float targetDistanceToTransitNode = targetToNode.magnitude;

					if (targetSpeed > 0)
						targetTimeToTransitNode = targetDistanceToTransitNode / targetSpeed;
					else
						targetTimeToTransitNode = 0;
				}

				//SegmentGeometry srcGeometry = SegmentGeometry.Get(targetVehiclePos.SourceSegmentId);
				//Direction targetToDir = srcGeometry.GetDirection(targetVehiclePos.TargetSegmentId, srcGeometry.StartNodeId() == nodeId);

				// get all cars
				for (var s = 0; s < 8; s++) {
					var incomingSegmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[transitNodeId].GetSegment(s);

					if (incomingSegmentId == 0 || incomingSegmentId == curPos.m_segment)
						continue;

					var incomingFromPrioritySegment = GetPrioritySegment(transitNodeId, incomingSegmentId);
					if (incomingFromPrioritySegment == null) {
#if DEBUG
						if (debug) {
							Log._Debug($"HasIncomingVehicles: Segment {incomingSegmentId} @ {transitNodeId} is not a priority segment.");
						}
#endif
						continue; // should not happen
					}

					SegmentGeometry incomingGeometry = SegmentGeometry.Get(incomingSegmentId);
					if (incomingGeometry.IsOutgoingOneWay(incomingGeometry.StartNodeId() == transitNodeId)) {
#if DEBUG
						if (debug) {
							Log._Debug($"HasIncomingVehicles: Incoming segment {incomingSegmentId}");
						}
#endif
						continue;
					}

					
					bool targetOnMain = targetFromPrioritySegment.Type == SegmentEnd.PriorityType.Main || targetFromPrioritySegment.Type == SegmentEnd.PriorityType.None;
					bool incomingOnMain = incomingFromPrioritySegment.Type == SegmentEnd.PriorityType.Main || incomingFromPrioritySegment.Type == SegmentEnd.PriorityType.None;

#if DEBUG
					if (debug) {
						Log._Debug($"HasIncomingVehicles: targetOnMain={targetOnMain} incomingOnMain={incomingOnMain}");
					}
#endif

					ushort incomingVehicleId = incomingFromPrioritySegment.FirstRegisteredVehicleId;
					while (incomingVehicleId != 0) {
#if DEBUG
						if (debug) {
							Log._Debug("");
							Log._Debug($"HasIncomingVehicles: checking incoming vehicle {incomingVehicleId} @ seg. {incomingFromPrioritySegment.SegmentId}");
						}
#endif

						VehicleState incomingState = vehStateManager._GetVehicleState(incomingVehicleId);
						if (! incomingState.Valid) {
#if DEBUG
							if (debug)
								Log._Debug($"HasIncomingVehicles: Incoming vehicle {incomingVehicleId}: state is invalid. *IGNORING*");
#endif
							incomingVehicleId = incomingState.NextVehicleIdOnSegment;
							continue;
						}

#if DEBUG
						if (debug)
							Log._Debug($"HasIncomingVehicles: Checking against incoming vehicle {incomingVehicleId}.");
#endif

						if (incomingState.JunctionTransitState != VehicleJunctionTransitState.None) {
							bool conflicting = false;
							incomingState.ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref vehManager.m_vehicles.m_buffer[incomingVehicleId], ref curPos, ref nextPos, ref targetVehicleData, delegate (ref Vehicle incomingVehicleData, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, ref Vehicle targetVehData, ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos) {

							bool incomingStateChangedRecently = incomingState.IsJunctionTransitStateNew();
							if (
								incomingState.JunctionTransitState == VehicleJunctionTransitState.Enter ||
								(incomingState.JunctionTransitState == VehicleJunctionTransitState.Leave && incomingStateChangedRecently)) {
									// incoming vehicle is (1) entering the junction or (2) leaving but last state update ocurred very recently.
									Vector3 incomingPos = incomingVehicleData.GetLastFramePosition();
									Vector3 incomingVel = incomingVehicleData.GetLastFrameVelocity();
									Vector3 incomingToNode = transitNodePos - incomingPos;

									// check if incoming vehicle moves towards node
									float dot = Vector3.Dot(incomingToNode, incomingVel);
									if (dot <= 0) {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is moving away from the transit node ({dot}). *IGNORING*");
#endif
										return;
									}
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is moving towards the transit node ({dot}). Distance: {incomingToNode.magnitude}");
#endif

									// check if estimated approach time of the incoming vehicle is within bounds (only if incoming vehicle is far enough away from the junction and target vehicle is moving)
									if (Options.simAccuracy <= 1 &&
											!Single.IsInfinity(targetTimeToTransitNode) &&
											!Single.IsNaN(targetTimeToTransitNode) &&
											incomingToNode.sqrMagnitude > GlobalConfig.Instance.MaxPriorityCheckSqrDist) {
										// check speeds
										float incomingSpeed = incomingVel.magnitude;
										float incomingDistanceToTransitNode = incomingToNode.magnitude;
										float incomingTimeToTransitNode = Single.NaN;

										if (incomingSpeed > 0)
											incomingTimeToTransitNode = incomingDistanceToTransitNode / incomingSpeed;
										else
											incomingTimeToTransitNode = Single.PositiveInfinity;

										float timeDiff = Mathf.Abs(incomingTimeToTransitNode - targetTimeToTransitNode);
										if (timeDiff > GlobalConfig.Instance.MaxPriorityApproachTime) {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference to large. *IGNORING*");
#endif
											return;
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} needs {incomingTimeToTransitNode} time units to get to the node where target needs {targetTimeToTransitNode} time units (diff = {timeDiff}). Difference within bounds.");
#endif
										}
									} else {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Target is stopped.");
#endif
									}
								} else if (incomingState.JunctionTransitState == VehicleJunctionTransitState.Leave) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is LEAVING but state update did not occur recently.");
#endif

									float incomingSqrSpeed = incomingVehicleData.GetLastFrameVelocity().sqrMagnitude;
									if (incomingSqrSpeed <= MAX_SQR_STOP_VELOCITY) {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is LEAVING but not moving. -> BLOCKED");
#endif
										incomingState.JunctionTransitState = VehicleJunctionTransitState.Blocked;
										incomingStateChangedRecently = true;
									}
								}

								if (!incomingStateChangedRecently &&
									(incomingState.JunctionTransitState == VehicleJunctionTransitState.Blocked ||
									(incomingState.JunctionTransitState == VehicleJunctionTransitState.Stop && targetVehicleId < incomingVehicleId))
								) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is BLOCKED and has waited a bit or is STOP and targetVehicleId {targetVehicleId} < incomingVehicleId {incomingVehicleId}. *IGNORING*");
#endif

									// incoming vehicle waits because the junction is blocked and we waited a little. Allow target vehicle to enter the junciton.
									return;
								}

								// check priority rules
								if (HasVehiclePriority(debug, transitNodeId, targetVehicleId, ref targetVehData, ref targetCurPos, ref targetNextPos, targetOnMain, incomingVehicleId, ref incomingCurPos, ref incomingNextPos, incomingOnMain, targetFromPrioritySegment, incomingFromPrioritySegment)) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is not conflicting.");
#endif
									return;
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"==========> HasIncomingVehicles: Incoming {incomingVehicleId} IS conflicting.");
#endif
									conflicting = true;
									return;
								}
							});

							if (conflicting) {
								// the target vehicle must wait
								return true;
							}
						} else {
#if DEBUG
							if (debug)
								Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) is not conflicting ({incomingState.JunctionTransitState}).");
#endif
						}

						// check next incoming vehicle
						incomingVehicleId = incomingState.NextVehicleIdOnSegment;
					}
				}

				return false;
			} catch (Exception e) {
				Log.Error($"HasIncomingVehicles: Error occurred: {e.ToString()}");
            }
			return false;
		}

		/// <summary>
		/// Implements priority checking for two vehicles approaching or waiting at a junction.
		/// </summary>
		/// <param name="debug"></param>
		/// <param name="transitNodeId">id of the junction</param>
		/// <param name="targetVehicleId">target vehicle for which priority is being checked</param>
		/// <param name="targetVehicleData">target vehicle data</param>
		/// <param name="targetCurPos">target vehicle current path position</param>
		/// <param name="targetNextPos">target vehicle next path position</param>
		/// <param name="targetIsOnMainRoad">true if the target vehicle is coming from a main road</param>
		/// <param name="incomingVehicleId">possibly conflicting incoming vehicle</param>
		/// <param name="incomingCurPos">incoming vehicle current path position</param>
		/// <param name="incomingNextPos">incoming vehicle next path position</param>
		/// <param name="incomingIsOnMainRoad">true if the incoming vehicle is coming from a main road</param>
		/// <param name="targetEnd">segment end the target vehicle is coming from</param>
		/// <param name="incomingEnd">segment end the incoming vehicle is coming from</param>
		/// <returns>true if the target vehicle has priority, false otherwise</returns>
		private bool HasVehiclePriority(bool debug, ushort transitNodeId, ushort targetVehicleId, ref Vehicle targetVehicleData, ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos, bool targetIsOnMainRoad, ushort incomingVehicleId, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, bool incomingIsOnMainRoad, SegmentEnd targetEnd, SegmentEnd incomingEnd) {
			try {
#if DEBUG
				//debug = targetEnd.NodeId == 16015;
				//debug = nodeId == 13531;
				if (debug) {
					Log._Debug("");
					Log._Debug($"  HasVehiclePriority: *** Checking if {targetVehicleId} (main road = {targetIsOnMainRoad}) @ (seg. {targetCurPos.m_segment}, lane {targetCurPos.m_lane}) -> (seg. {targetNextPos.m_segment}, lane {targetNextPos.m_lane}) has priority over {incomingVehicleId} (main road = {incomingIsOnMainRoad}) @ (seg. {incomingCurPos.m_segment}, lane {incomingCurPos.m_lane}) -> (seg. {incomingNextPos.m_segment}, lane {incomingNextPos.m_lane}).");
                }
#endif

				if (targetEnd.NodeId != incomingEnd.NodeId) {
					Log.Error($"HasVehiclePriority: Incompatible SegmentEnds!");
					return true;
				}

				ushort nodeId = targetEnd.NodeId;

				// delete invalid target car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[targetVehicleId].m_flags & Vehicle.Flags.Created) == 0) {
					targetEnd.RequestCleanup();
					return true;
				}

				// delete invalid incoming car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId].m_flags & Vehicle.Flags.Created) == 0) {
					incomingEnd.RequestCleanup();
					return true;
				}

				//var targetVehState = VehicleStateManager.GetVehicleState(targetCarId);
				//var incomingVehState = VehicleStateManager.GetVehicleState(incomingCarId);

				/*if (targetVehState == null) {
					targetEnd.RequestCleanup();
					return true;
				}*/

				if ((targetVehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
					// target vehicle is on emergency
					return true;
				}

				/*if (incomingVehState == null) {
					incomingEnd.RequestCleanup();
					return true;
				}*/

				//var targetVehPos = targetVehState.GetCurrentPosition();

				// check if target is on main road and incoming is on low-priority road
				if (targetIsOnMainRoad && !incomingIsOnMainRoad) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Target is on main road and incoming is not. Target HAS PRIORITY.");
                    }
#endif
					return true;
				}

				//         TOP
				//          |
				//          |
				// LEFT --- + --- RIGHT
				//          |
				//          |
				//        BOTTOM

				// We assume the target car is coming from BOTTOM.

				SegmentGeometry targetGeometry = SegmentGeometry.Get(targetCurPos.m_segment);
				SegmentGeometry incomingGeometry = SegmentGeometry.Get(incomingCurPos.m_segment);
				bool isTargetStartNode = targetGeometry.StartNodeId() == nodeId;
				ArrowDirection targetToDir = targetGeometry.GetDirection(targetNextPos.m_segment, isTargetStartNode); // target direction of target vehicle (relative to incoming direction of target vehicle)
				ArrowDirection incomingFromRelDir = targetGeometry.GetDirection(incomingCurPos.m_segment, isTargetStartNode); // incoming direction of incoming vehicle (relative to incoming direction of target vehicle)
				ArrowDirection incomingToDir = incomingGeometry.GetDirection(incomingNextPos.m_segment, incomingGeometry.StartNodeId() == nodeId); // target direction of incoming vehicle (relative to incoming direction of incoming vehicle)
#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingFromRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
                }
#endif

				if (IsLeftHandDrive()) {
					// mirror situation for left-hand traffic systems
					targetToDir = InvertLeftRight(targetToDir);
					incomingFromRelDir = InvertLeftRight(incomingFromRelDir);
					incomingToDir = InvertLeftRight(incomingToDir);
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: LHD! targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingFromRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
					}
#endif
				}

				bool sameTargets = false;
				bool laneOrderCorrect = false;
				if (targetNextPos.m_segment == incomingNextPos.m_segment) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment.");
					}
#endif

					// target and incoming are both going to same segment
					sameTargets = true;
					if (targetNextPos.m_lane == incomingNextPos.m_lane && targetCurPos.m_segment != incomingCurPos.m_segment) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment AND lane. lane order is incorrect!");
						}
#endif
						// both are going to the same lane. lane order is always incorrect
						laneOrderCorrect = false;
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target and incoming are going to the same segment BUT NOT to the same lane. Determining if lane order is correct.");
						}
#endif
						switch (targetToDir) {
							case ArrowDirection.Left:
								laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
								}
#endif
								break;
							case ArrowDirection.Forward:
							default:
								switch (incomingFromRelDir) {
									case ArrowDirection.Left:
									case ArrowDirection.Forward:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, incomingNextPos.m_lane, targetNextPos.m_lane); // stay right
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from LEFT or FORWARD ({incomingFromRelDir}). Checking if lane {targetNextPos.m_lane} is RIGHT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case ArrowDirection.Right:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from RIGHT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case ArrowDirection.Turn:
									default:
										laneOrderCorrect = true;
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from TURN (should not happen). Result: {laneOrderCorrect}");
										}
#endif
										break;
								}
								break;
							case ArrowDirection.Right:
								laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, incomingNextPos.m_lane, targetNextPos.m_lane); // stay right
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going RIGHT. Checking if lane {targetNextPos.m_lane} is RIGHT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
								}
#endif
								break;
						}
						//laneOrderCorrect = IsLaneOrderConflictFree(targetNextPos.m_segment, targetNextPos.m_lane, incomingNextPos.m_lane); // FIXME
#if DEBUG
						if (debug) {
							Log._Debug($"    HasVehiclePriority: >>> Lane order correct? {laneOrderCorrect}");
						}
#endif
					}
				}

				if (sameTargets && laneOrderCorrect) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Lane order between car {targetVehicleId} and {incomingVehicleId} is correct. Target HAS PRIORITY.");
					}
#endif
					return true;
				}

				if (!targetIsOnMainRoad && !incomingIsOnMainRoad) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Both target {targetVehicleId} and incoming {incomingVehicleId} are coming from a low-priority road.");
					}
#endif

					// the right-most vehicle has priority
					if (incomingFromRelDir == ArrowDirection.Left) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Incoming comes from left. Target HAS PRIORITY!");
						}
#endif
						return true;
					} else if (incomingFromRelDir == ArrowDirection.Right) {
#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Incoming comes from right. Target MUST WAIT!");
						}
#endif
						return false;
					} else {
						// turn/forward
					}
				}
				bool incomingCrossingStreet = incomingToDir == ArrowDirection.Forward || incomingToDir == ArrowDirection.Left;

#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: !!!!!! Lane order is INCORRECT or both are NOT GOING TO SAME target. incomingCrossingStreet={incomingCrossingStreet}");
				}
#endif

				bool ret;
				switch (targetToDir) {
					case ArrowDirection.Right:
						// target: BOTTOM->RIGHT
						if ((!targetIsOnMainRoad && incomingIsOnMainRoad) && sameTargets && !laneOrderCorrect) {
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going RIGHT and is on low-priority road turning right. the other vehicle is on a priority road. target MUST WAIT.");
							}
#endif
							return false; // vehicle must wait for incoming vehicle on priority road
						}

#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target is going RIGHT without conflict (targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, sameTargets={sameTargets}, laneOrderCorrect={laneOrderCorrect}). target HAS PRIORITY.");
                        }
#endif

						return true;
					case ArrowDirection.Forward:
					default:
						// target: BOTTOM->TOP
						switch (incomingFromRelDir) {
							case ArrowDirection.Right:
								ret = !incomingIsOnMainRoad && !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from RIGHT. incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Left:
								ret = targetIsOnMainRoad || !incomingCrossingStreet; // TODO check
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Forward:
							default:
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from FORWARD. result=True");
								}
#endif
								return true;
						}
					case ArrowDirection.Left:
						// target: BOTTOM->LEFT
						switch (incomingFromRelDir) {
							case ArrowDirection.Right:
								ret = !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from RIGHT. incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
                                }
#endif
								return ret;
							case ArrowDirection.Left:
								if (targetIsOnMainRoad && incomingIsOnMainRoad) { // bent priority road
									ret = true;
								} else {
									ret = !incomingCrossingStreet;
								}
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
								}
#endif
								return ret;
							case ArrowDirection.Forward:
							default:
								ret = incomingToDir == ArrowDirection.Left || incomingToDir == ArrowDirection.Turn;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from FORWARD. incomingToDir={incomingToDir}. result={ret}");
								}
#endif
								return ret;
						}
				}
			} catch (Exception e) {
				Log.Error("Error occured: " + e.ToString());
			}

#if DEBUG
			if (debug) {
				Log._Debug($"  HasVehiclePriority: ALL CHECKS FAILED. returning FALSE.");
			}
#endif
			return false;
		}

		private static ArrowDirection InvertLeftRight(ArrowDirection dir) {
			if (dir == ArrowDirection.Left)
				dir = ArrowDirection.Right;
			else if (dir == ArrowDirection.Right)
				dir = ArrowDirection.Left;
			return dir;
		}

		/// <summary>
		/// Checks if lane <paramref name="leftLaneIndex"/> lies to the left of lane <paramref name="rightLaneIndex"/>.
		/// </summary>
		/// <param name="debug"></param>
		/// <param name="segmentId"></param>
		/// <param name="nodeId"></param>
		/// <param name="leftLaneIndex"></param>
		/// <param name="rightLaneIndex"></param>
		/// <returns></returns>
		public bool IsLaneOrderConflictFree(bool debug, ushort segmentId, ushort nodeId, byte leftLaneIndex, byte rightLaneIndex) { // TODO I think this is incorrect. See TrafficLightTool._guiLaneChangeWindow
			try {
				if (leftLaneIndex == rightLaneIndex) {
					return false;
				}

				NetManager netManager = Singleton<NetManager>.instance;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				NetInfo.Direction dir = nodeId == netManager.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				NetInfo.Direction dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
				NetInfo.Direction dir3 = TrafficPriorityManager.IsLeftHandDrive() ? NetInfo.InvertDirection(dir2) : dir2;

				NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
				NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

#if DEBUG
				if (debug) {
					Log._Debug($"    IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): dir={dir}, dir2={dir2}, dir3={dir3} laneDir={leftLane.m_direction}, leftLanePos={leftLane.m_position}, rightLanePos={rightLane.m_position}");
				}
#endif

				bool ret = (dir3 == NetInfo.Direction.Forward) ^ (leftLane.m_position < rightLane.m_position);
				return ret;
			} catch (Exception e) {
				Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e.ToString()}");
            }
			return true;
		}

		/// <summary>
		/// Determines if the map uses a left-hand traffic system
		/// </summary>
		/// <returns>true if vehicles drive on the left side of the road, false otherwise</returns>
		public static bool IsLeftHandDrive() {
			return Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
		}

		public void SegmentSimulationStep(ushort segmentId) {
			// simulate segment-ends
			TrafficSegment trafficSegment = TrafficSegments[segmentId];
			if (trafficSegment == null) {
				return;
			}
			trafficSegment.Instance1?.SimulationStep();
			trafficSegment.Instance2?.SimulationStep();
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
			RemovePrioritySegment(geometry.SegmentId);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {
			HousekeepNode(geometry.StartNodeId());
			HousekeepNode(geometry.EndNodeId());

			TrafficSegment trafficSegment = TrafficSegments[geometry.SegmentId];
			if (trafficSegment == null) {
				return;
			}
			trafficSegment.Instance1?.Reset();
			trafficSegment.Instance2?.Reset();
		}

		protected void HousekeepNode(ushort nodeId) {
			if (!NetUtil.IsNodeValid(nodeId)) {
				RemovePrioritySegments(nodeId);
				return;
			}
			
			if (! TrafficLightSimulationManager.Instance.HasSimulation(nodeId)) {
				if (TrafficLightManager.Instance.HasTrafficLight(nodeId) || !IsPriorityNode(nodeId, true)) {
					RemovePrioritySegments(nodeId);
					return;
				}
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < TrafficSegments.Length; ++i)
				TrafficSegments[i] = null;
		}

		[Obsolete]
		public bool LoadData(List<int[]> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} priority segments (old method)");
			foreach (var segment in data) {
				try {
					if (segment.Length < 3)
						continue;

					if ((SegmentEnd.PriorityType)segment[2] == SegmentEnd.PriorityType.None) {
						continue;
					}
					if (!NetUtil.IsNodeValid((ushort)segment[0])) {
						continue;
					}
					if (!NetUtil.IsSegmentValid((ushort)segment[1])) {
						continue;
					}

					if (IsPrioritySegment((ushort)segment[0], (ushort)segment[1])) {
						Log._Debug($"Loading priority segment: segment {segment[1]} @ node {segment[0]} is already a priority segment");
						GetPrioritySegment((ushort)segment[0], (ushort)segment[1]).Type = (SegmentEnd.PriorityType)segment[2];
						continue;
					}
					AddPrioritySegment((ushort)segment[0], (ushort)segment[1], (SegmentEnd.PriorityType)segment[2]);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from Priority segments: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		[Obsolete]
		public List<int[]> SaveData(ref bool success) {
			return null;
		}

		public bool LoadData(List<Configuration.PrioritySegment> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} priority segments (new method)");
			foreach (var prioSegData in data) {
				try {
					if ((SegmentEnd.PriorityType)prioSegData.priorityType == SegmentEnd.PriorityType.None) {
						continue;
					}
					if (!NetUtil.IsNodeValid(prioSegData.nodeId)) {
						continue;
					}
					if (!NetUtil.IsSegmentValid(prioSegData.segmentId)) {
						continue;
					}

					if (IsPrioritySegment(prioSegData.nodeId, prioSegData.segmentId)) {
						Log._Debug($"Loading priority segment: segment {prioSegData.segmentId} @ node {prioSegData.nodeId} is already a priority segment");
						GetPrioritySegment(prioSegData.nodeId, prioSegData.segmentId).Type = (SegmentEnd.PriorityType)prioSegData.priorityType;
						continue;
					}
					AddPrioritySegment(prioSegData.nodeId, prioSegData.segmentId, (SegmentEnd.PriorityType)prioSegData.priorityType);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading data from Priority segments: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		List<Configuration.PrioritySegment> ICustomDataManager<List<Configuration.PrioritySegment>>.SaveData(ref bool success) {
			List<Configuration.PrioritySegment> ret = new List<Configuration.PrioritySegment>();
			for (int i = 0; i < TrafficSegments.Length; i++) {
				try {
					if (TrafficSegments[i] == null)
						continue;

					TrafficSegment trafficSegment = TrafficSegments[i];

					if (trafficSegment.Node1 != 0 && trafficSegment.Instance1 != null && trafficSegment.Instance1.Type != SegmentEnd.PriorityType.None) {
						Log._Debug($"Saving Priority Segment of type: {trafficSegment.Instance1.Type} @ node {trafficSegment.Node1}, seg. {i}");
						ret.Add(new Configuration.PrioritySegment((ushort)i, trafficSegment.Node1, (int)trafficSegment.Instance1.Type));
					}

					if (trafficSegment.Node2 != 0 && trafficSegment.Instance2 != null && trafficSegment.Instance2.Type != SegmentEnd.PriorityType.None) {
						Log._Debug($"Saving Priority Segment of type: {trafficSegment.Instance2.Type} @ node {trafficSegment.Node2}, seg. {i}");
						ret.Add(new Configuration.PrioritySegment((ushort)i, trafficSegment.Node2, (int)trafficSegment.Instance2.Type));
					}
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving priority segment @ {i}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}

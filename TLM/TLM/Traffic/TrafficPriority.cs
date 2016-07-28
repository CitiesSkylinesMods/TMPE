using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;
using System.Threading;

namespace TrafficManager.Traffic {
	class TrafficPriority {
		public static float maxStopVelocity = 0.1f;
		public static float maxYieldVelocity = 0.3f;

		/// <summary>
		/// Dictionary of segments that are connected to roads with timed traffic lights or priority signs. Index: segment id
		/// </summary>
		public static TrafficSegment[] TrafficSegments = null;

		/// <summary>
		/// Determines if vehicles should be cleared
		/// </summary>
		private static bool ClearTrafficRequested = false;

		static TrafficPriority() {
			TrafficSegments = new TrafficSegment[Singleton<NetManager>.instance.m_segments.m_size];
		}

		public static SegmentEnd AddPrioritySegment(ushort nodeId, ushort segmentId, SegmentEnd.PriorityType type) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.AddPrioritySegment");
#endif
			if (nodeId <= 0 || segmentId <= 0) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.AddPrioritySegment");
#endif
				return null;
			}

#if DEBUG
			Log._Debug("adding PrioritySegment @ node " + nodeId + ", seg. " + segmentId + ", type " + type);
#endif

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
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.AddPrioritySegment");
#endif
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
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.AddPrioritySegment");
#endif
			return ret;
		}

		public static void RemovePrioritySegments(ushort nodeId) { // priorityNodes: OK
			if (nodeId <= 0)
				return;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				RemovePrioritySegment(nodeId, segmentId);
			}
		}

		public static List<SegmentEnd> GetPrioritySegments(ushort nodeId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.GetPrioritySegments");
#endif
			List<SegmentEnd> ret = new List<SegmentEnd>();
			if (nodeId <= 0) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegments");
#endif
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

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegments");
#endif
			return ret;
		}

		public static bool IsPrioritySegment(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.IsPrioritySegment");
#endif
			if (nodeId <= 0 || segmentId <= 0) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPrioritySegment");
#endif
				return false;
			}

			if (TrafficSegments[segmentId] != null) {
				var prioritySegment = TrafficSegments[segmentId];

				NetManager netManager = Singleton<NetManager>.instance;
				if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
					RemovePrioritySegment(nodeId, segmentId);
					CustomTrafficLights.RemoveSegmentLights(segmentId);
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPrioritySegment");
#endif
					return false;
				}

				if (prioritySegment.Node1 == nodeId || prioritySegment.Node2 == nodeId) {
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPrioritySegment");
#endif
					return true;
				}
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPrioritySegment");
#endif
			return false;
		}

		public static bool IsPriorityNode(ushort nodeId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.IsPriorityNode");
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_nodes.m_buffer[nodeId].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPriorityNode");
#endif
				return false;
			}

			for (int i = 0; i < 8; ++i) {
				ushort segmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(i);

				if (segmentId == 0)
					continue;

				if (IsPrioritySegment(nodeId, segmentId)) {
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPriorityNode");
#endif
					return true;
				}
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsPriorityNode");
#endif
			return false;
		}

		public static SegmentEnd GetPrioritySegment(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.GetPrioritySegment");
#endif
			/*if (!IsPrioritySegment(nodeId, segmentId)) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegment");
#endif
				return null;
			}*/

			var prioritySegment = TrafficSegments[segmentId];

			if (prioritySegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegment");
#endif
				return null;
			}

			if (prioritySegment.Node1 == nodeId) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegment");
#endif
				return prioritySegment.Instance1;
			}

			SegmentEnd ret = prioritySegment.Node2 == nodeId ?
				prioritySegment.Instance2 : null;
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.GetPrioritySegment");
#endif
			return ret;
		}

		internal static void RemovePrioritySegment(ushort nodeId, ushort segmentId) {
			if (nodeId <= 0 || segmentId <= 0 || TrafficSegments[segmentId] == null)
				return;
			var prioritySegment = TrafficSegments[segmentId];

#if DEBUG
			Log._Debug($"TrafficPriority.RemovePrioritySegment: Removing SegmentEnd {segmentId} @ {nodeId}");
#endif

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

			if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0)
				TrafficSegments[segmentId] = null;
		}

		internal static void ClearTraffic() {
			try {
				Monitor.Enter(Singleton<VehicleManager>.instance);

				for (ushort i = 0; i < Singleton<VehicleManager>.instance.m_vehicles.m_size; ++i) {
					if (
						(Singleton<VehicleManager>.instance.m_vehicles.m_buffer[i].m_flags & Vehicle.Flags.Created) != 0 /*&&
						Singleton<VehicleManager>.instance.m_vehicles.m_buffer[i].Info.m_vehicleType == VehicleInfo.VehicleType.Car*/)
						Singleton<VehicleManager>.instance.ReleaseVehicle(i);
				}

				CustomRoadAI.resetTrafficStats();
			} catch (Exception ex) {
				Log.Error($"Error occured while trying to clear traffic: {ex.ToString()}");
			} finally {
				Monitor.Exit(Singleton<VehicleManager>.instance);
			}
		}

		internal static void RequestClearTraffic() {
			ClearTrafficRequested = true;
		}

		/// <summary>
		/// Adds/Sets a node as a priority node
		/// </summary>
		/// <param name="nodeId"></param>
		/// <returns>number of priority segments added</returns>
		internal static byte AddPriorityNode(ushort nodeId) {
			if (nodeId <= 0)
				return 0;

			byte ret = 0;
			for (var i = 0; i < 8; i++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(i);

				if (segmentId == 0)
					continue;
				if (TrafficPriority.IsPrioritySegment(nodeId, segmentId))
					continue;
				/*if (SegmentGeometry.Get(segmentId).IsOutgoingOneWay(nodeId))
					continue;*/ // we need this for pedestrian traffic lights

				TrafficPriority.AddPrioritySegment(nodeId, segmentId, SegmentEnd.PriorityType.None);
				++ret;
			}
			return ret;
		}

		public static bool HasIncomingVehiclesWithHigherPriority(ushort targetVehicleId, ref Vehicle targetVehicleData, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
#endif

			try {
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				NetManager netManager = Singleton<NetManager>.instance;
				LaneConnectionManager connManager = LaneConnectionManager.Instance();
				VehicleStateManager vehStateManager = VehicleStateManager.Instance();

				uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

				ushort transitNodeId = VehicleState.GetTransitNodeId(ref curPos, ref nextPos);
				Vector3 transitNodePos = netManager.m_nodes.m_buffer[transitNodeId].m_position;

				if ((Singleton<NetManager>.instance.m_nodes.m_buffer[transitNodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
#endif
					return false;
				}

#if DEBUG
				//bool debug = nodeId == 30634;
				//bool debug = transitNodeId == 21371;
				bool debug = false; // transitNodeId == 9367;
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
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
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
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
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

						if (incomingState.JunctionTransitState == VehicleJunctionTransitState.Leave || incomingState.JunctionTransitState == VehicleJunctionTransitState.Enter || incomingState.JunctionTransitState == VehicleJunctionTransitState.Stop) {
								

							bool conflicting = false;
							incomingState.ProcessCurrentAndNextPathPositionAndOtherVehicleCurrentAndNextPathPosition(ref vehManager.m_vehicles.m_buffer[incomingVehicleId], ref curPos, ref nextPos, ref targetVehicleData, delegate (ref Vehicle incomingVehicleData, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, ref Vehicle targetVehData, ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos) {
								if (incomingState.JunctionTransitState != VehicleJunctionTransitState.Stop && (incomingState.JunctionTransitState != VehicleJunctionTransitState.Leave || (incomingState.LastStateUpdate >> VehicleState.STATE_UPDATE_SHIFT) < (frame >> VehicleState.STATE_UPDATE_SHIFT))) {
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
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is moving towards the transit node ({dot}).");
#endif

									if (Options.simAccuracy <= 1 && !Single.IsInfinity(targetTimeToTransitNode) && !Single.IsNaN(targetTimeToTransitNode)) {
										float incomingSpeed = incomingVel.magnitude;
										float incomingDistanceToTransitNode = incomingToNode.magnitude;
										float incomingTimeToTransitNode = Single.NaN;

										if (incomingSpeed > 0)
											incomingTimeToTransitNode = incomingDistanceToTransitNode / incomingSpeed;
										else
											incomingTimeToTransitNode = Single.PositiveInfinity;

										float timeDiff = Mathf.Abs(incomingTimeToTransitNode - targetTimeToTransitNode);
										if (timeDiff > 10f) {
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
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is LEAVING but state update occurred recently.");
#endif
								}

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
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
#endif
								return true;
							}
						} else {
#if DEBUG
							if (debug)
								Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) is not conflicting ({incomingState.JunctionTransitState}).");
#endif
						}

						incomingVehicleId = incomingState.NextVehicleIdOnSegment;
					}
				}

#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
#endif
				return false;
			} catch (Exception e) {
				Log.Error($"HasIncomingVehicles: Error occurred: {e.ToString()}");
            }
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasIncomingVehiclesWithHigherPriority");
#endif
			return false;
		}

		private static bool HasVehiclePriority(bool debug, ushort transitNodeId, ushort targetCarId, ref Vehicle targetVehicleData, ref PathUnit.Position targetCurPos, ref PathUnit.Position targetNextPos, bool targetIsOnMainRoad, ushort incomingCarId, ref PathUnit.Position incomingCurPos, ref PathUnit.Position incomingNextPos, bool incomingIsOnMainRoad, SegmentEnd targetEnd, SegmentEnd incomingEnd) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.HasVehiclePriority");
#endif
			try {
#if DEBUG
				//debug = targetEnd.NodeId == 16015;
				//debug = nodeId == 13531;
				if (debug) {
					Log._Debug("");
					Log._Debug($"  HasVehiclePriority: *** Checking if {targetCarId} (main road = {targetIsOnMainRoad}) @ (seg. {targetCurPos.m_segment}, lane {targetCurPos.m_lane}) -> (seg. {targetNextPos.m_segment}, lane {targetNextPos.m_lane}) has priority over {incomingCarId} (main road = {incomingIsOnMainRoad}) @ (seg. {incomingCurPos.m_segment}, lane {incomingCurPos.m_lane}) -> (seg. {incomingNextPos.m_segment}, lane {incomingNextPos.m_lane}).");
                }
#endif

				if (targetEnd.NodeId != incomingEnd.NodeId) {
					Log.Error($"HasVehiclePriority: Incompatible SegmentEnds!");
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}

				ushort nodeId = targetEnd.NodeId;

				// delete invalid target car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[targetCarId].m_flags & Vehicle.Flags.Created) == 0) {
					targetEnd.RequestCleanup();
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}

				// delete invalid incoming car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingCarId].m_flags & Vehicle.Flags.Created) == 0) {
					incomingEnd.RequestCleanup();
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}

				//var targetVehState = VehicleStateManager.GetVehicleState(targetCarId);
				//var incomingVehState = VehicleStateManager.GetVehicleState(incomingCarId);

				/*if (targetVehState == null) {
					targetEnd.RequestCleanup();
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}*/

				if ((targetVehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
					// target vehicle is on emergency
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}

				/*if (incomingVehState == null) {
					incomingEnd.RequestCleanup();
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}*/

				//var targetVehPos = targetVehState.GetCurrentPosition();

				bool incomingIsOnEmergency = false;// (incomingVehState.VehicleType & ExtVehicleType.Emergency) != ExtVehicleType.None;

				// check if target is on main road and incoming is on low-priority road
				if (targetIsOnMainRoad && !incomingIsOnMainRoad && !incomingIsOnEmergency) {
#if DEBUG
					if (debug) {
						Log._Debug($"  HasVehiclePriority: Target is on main road and incoming is not. Target HAS PRIORITY.");
                    }
#endif
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
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
				Direction targetToDir = targetGeometry.GetDirection(targetNextPos.m_segment, isTargetStartNode);
				Direction incomingFromRelDir = targetGeometry.GetDirection(incomingCurPos.m_segment, isTargetStartNode);
				Direction incomingToDir = incomingGeometry.GetDirection(incomingNextPos.m_segment, incomingGeometry.StartNodeId() == nodeId);
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
							case Direction.Left:
								laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
								}
#endif
								break;
							case Direction.Forward:
							default:
								switch (incomingFromRelDir) {
									case Direction.Left:
									case Direction.Forward:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, incomingNextPos.m_lane, targetNextPos.m_lane); // stay right
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from LEFT or FORWARD ({incomingFromRelDir}). Checking if lane {targetNextPos.m_lane} is RIGHT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case Direction.Right:
										laneOrderCorrect = IsLaneOrderConflictFree(debug, targetNextPos.m_segment, transitNodeId, targetNextPos.m_lane, incomingNextPos.m_lane); // stay left
#if DEBUG
										if (debug) {
											Log._Debug($"  HasVehiclePriority: Target is going FORWARD and incoming is coming from RIGHT. Checking if lane {targetNextPos.m_lane} is LEFT to {incomingNextPos.m_lane}. Result: {laneOrderCorrect}");
										}
#endif
										break;
									case Direction.Turn:
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
							case Direction.Right:
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
						Log._Debug($"  HasVehiclePriority: Lane order between car {targetCarId} and {incomingCarId} is correct. Target HAS PRIORITY.");
					}
#endif
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
					return true;
				}

				bool incomingCrossingStreet = incomingToDir == Direction.Forward || incomingToDir == Direction.Left;

#if DEBUG
				if (debug) {
					Log._Debug($"  HasVehiclePriority: !!!!!! Lane order is INCORRECT or both are NOT GOING TO SAME target. incomingCrossingStreet={incomingCrossingStreet}");
				}
#endif

				bool ret;
				switch (targetToDir) {
					case Direction.Right:
						// target: BOTTOM->RIGHT
						if (((!targetIsOnMainRoad && incomingIsOnMainRoad) || incomingIsOnEmergency) && sameTargets && !laneOrderCorrect) {
#if DEBUG
							if (debug) {
								Log._Debug($"  HasVehiclePriority: Target is going RIGHT and is on low-priority road turning right. the other vehicle is on a priority road. target MUST WAIT.");
							}
#endif
#if TRACE
							Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
							return false; // vehicle must wait for incoming vehicle on priority road
						}

#if DEBUG
						if (debug) {
							Log._Debug($"  HasVehiclePriority: Target is going RIGHT without conflict (targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingIsOnEmergency={incomingIsOnEmergency}, sameTargets={sameTargets}, laneOrderCorrect={laneOrderCorrect}). target HAS PRIORITY.");
                        }
#endif

#if TRACE
						Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
						return true;
					case Direction.Forward:
					default:
						// target: BOTTOM->TOP
						switch (incomingFromRelDir) {
							case Direction.Right:
								ret = !incomingIsOnMainRoad && !incomingIsOnEmergency && !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from RIGHT. incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingIsOnEmergency={incomingIsOnEmergency}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
								return ret;
							case Direction.Left:
								ret = (targetIsOnMainRoad && !incomingIsOnEmergency) || !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnEmergency={incomingIsOnEmergency}, incomingCrossingStreet={incomingCrossingStreet}, result={ret}");
								}
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
								return ret;
							case Direction.Forward:
							default:
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going FORWARD, incoming is coming from FORWARD. result=True");
								}
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
								return true;
						}
					case Direction.Left:
						// target: BOTTOM->LEFT
						switch (incomingFromRelDir) {
							case Direction.Right:
								ret = !incomingCrossingStreet;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from RIGHT. incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
                                }
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
								return ret;
							case Direction.Left:
								if (targetIsOnMainRoad && incomingIsOnMainRoad && !incomingIsOnEmergency) { // bent priority road
									ret = true;
								} else {
									ret = !incomingCrossingStreet;
								}
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from LEFT. targetIsOnMainRoad={targetIsOnMainRoad}, incomingIsOnMainRoad={incomingIsOnMainRoad}, incomingIsOnEmergency={incomingIsOnEmergency}, incomingCrossingStreet={incomingCrossingStreet}. result={ret}");
								}
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
								return ret;
							case Direction.Forward:
							default:
								ret = incomingToDir == Direction.Left || incomingToDir == Direction.Turn;
#if DEBUG
								if (debug) {
									Log._Debug($"  HasVehiclePriority: Target is going LEFT, incoming is coming from FORWARD. incomingToDir={incomingToDir}. result={ret}");
								}
#endif
#if TRACE
								Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
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
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.HasVehiclePriority");
#endif
			return false;
		}

		private static Direction InvertLeftRight(Direction dir) {
			if (dir == Direction.Left)
				dir = Direction.Right;
			else if (dir == Direction.Right)
				dir = Direction.Left;
			return dir;
		}

		internal static void OnLevelLoading() {
			try {
				//TrafficPriority.fixJunctions(); // TODO maybe remove this
			} catch (Exception e) {
				Log.Error($"OnLevelLoading: {e.ToString()}");
            }
		}

		internal static void OnLevelUnloading() {
			TrafficLightSimulation.TrafficLightSimulations.Clear();
			for (int i = 0; i < TrafficSegments.Length; ++i)
				TrafficSegments[i] = null;
		}

		public static bool IsLaneOrderConflictFree(bool debug, ushort segmentId, ushort nodeId, byte leftLaneIndex, byte rightLaneIndex) { // TODO I think this is incorrect. See TrafficLightTool._guiLaneChangeWindow
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.IsLaneOrderConflictFree");
#endif
			try {
				if (leftLaneIndex == rightLaneIndex) {
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsLaneOrderConflictFree");
#endif
					return false;
				}

				NetManager netManager = Singleton<NetManager>.instance;

				NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;

				NetInfo.Direction dir = nodeId == netManager.m_segments.m_buffer[segmentId].m_startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				NetInfo.Direction dir2 = ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
				NetInfo.Direction dir3 = TrafficPriority.IsLeftHandDrive() ? NetInfo.InvertDirection(dir2) : dir2;

				NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
				NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

#if DEBUG
				if (debug) {
					Log._Debug($"    IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): dir={dir}, dir2={dir2}, dir3={dir3} laneDir={leftLane.m_direction}, leftLanePos={leftLane.m_position}, rightLanePos={rightLane.m_position}");
				}
#endif

				bool ret = (dir3 == NetInfo.Direction.Forward) ^ (leftLane.m_position < rightLane.m_position);
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsLaneOrderConflictFree");
#endif
				return ret;
			} catch (Exception e) {
				Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e.ToString()}");
            }
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.IsLaneOrderConflictFree");
#endif
			return true;
		}

		/// <summary>
		/// Determines if the map uses a left-hand traffic system
		/// </summary>
		/// <returns></returns>
		public static bool IsLeftHandDrive() {
			return Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
		}

		//private static ushort nextValidityCheckedSegment = 0;

		public static void SegmentSimulationStep(ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficPriority.SegmentSimulationStep");
#endif
			if (ClearTrafficRequested) {
				TrafficPriority.ClearTraffic();
				ClearTrafficRequested = false;
			}

			// simulate segment-ends
			TrafficSegment trafficSegment = TrafficSegments[segmentId];
			if (trafficSegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("TrafficPriority.SegmentSimulationStep");
#endif
				return;
			}
			trafficSegment.Instance1?.SimulationStep();
			trafficSegment.Instance2?.SimulationStep();
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficPriority.SegmentSimulationStep");
#endif
		}
	}
}

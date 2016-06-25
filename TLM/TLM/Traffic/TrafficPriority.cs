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
		private static uint[] segmentsCheckLoadBalanceMod = new uint[] { 127, 255, 511, 1023, 2047 };
		
		public static float maxStopVelocity = 0.5f;

		/// <summary>
		/// Dictionary of segments that are connected to roads with timed traffic lights or priority signs. Index: segment id
		/// </summary>
		public static TrafficSegment[] TrafficSegments = null;

		/// <summary>
		/// Nodes that have timed traffic lights or priority signs
		/// </summary>
		private static HashSet<ushort> priorityNodes = new HashSet<ushort>();

		/// <summary>
		/// Determines if vehicles should be cleared
		/// </summary>
		private static bool ClearTrafficRequested = false;

		static TrafficPriority() {
			TrafficSegments = new TrafficSegment[Singleton<NetManager>.instance.m_segments.m_size];
		}

		public static SegmentEnd AddPrioritySegment(ushort nodeId, ushort segmentId, SegmentEnd.PriorityType type) {
			if (nodeId <= 0 || segmentId <= 0)
				return null;

#if DEBUG
			Log._Debug("adding PrioritySegment @ node " + nodeId + ", seg. " + segmentId + ", type " + type);
#endif

			SegmentEnd ret = null;
			var trafficSegment = TrafficSegments[segmentId];
			if (trafficSegment != null) { // do not replace with IsPrioritySegment!
				trafficSegment.Segment = segmentId;

#if DEBUG
				Log.Warning("Priority segment already exists. Node1=" + trafficSegment.Node1 + " Node2=" + trafficSegment.Node2);
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
					rebuildPriorityNodes();
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
			priorityNodes.Add(nodeId);
			return ret;
		}

		public static void RemovePrioritySegments(ushort nodeId) { // priorityNodes: OK
			if (nodeId <= 0)
				return;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (segmentId <= 0)
					continue;

				RemovePrioritySegment(nodeId, segmentId, false);
			}
			priorityNodes.Remove(nodeId);
		}

		public static List<SegmentEnd> GetPrioritySegments(ushort nodeId) {
			List<SegmentEnd> ret = new List<SegmentEnd>();
			if (nodeId <= 0)
				return ret;

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

		public static bool IsPrioritySegment(ushort nodeId, ushort segmentId) {
			if (nodeId <= 0 || segmentId <= 0)
				return false;

			if (TrafficSegments[segmentId] != null) {
				var prioritySegment = TrafficSegments[segmentId];

				NetManager netManager = Singleton<NetManager>.instance;
				if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
					RemovePrioritySegment(nodeId, segmentId);
					CustomTrafficLights.RemoveSegmentLights(segmentId);
					return false;
				}

				if (prioritySegment.Node1 == nodeId || prioritySegment.Node2 == nodeId) {
					return true;
				}
			}

			return false;
		}

		public static bool IsPriorityNode(ushort nodeId) {
			return priorityNodes.Contains(nodeId);
		}

		public static HashSet<ushort> GetPriorityNodes() {
			return priorityNodes;
		}

		public static SegmentEnd GetPrioritySegment(ushort nodeId, ushort segmentId) {
			if (!IsPrioritySegment(nodeId, segmentId)) return null;

			var prioritySegment = TrafficSegments[segmentId];

			if (prioritySegment.Node1 == nodeId) {
				return prioritySegment.Instance1;
			}

			return prioritySegment.Node2 == nodeId ?
				prioritySegment.Instance2 : null;
		}

		internal static void RemovePrioritySegment(ushort nodeId, ushort segmentId, bool rebuildNodes = true) { // priorityNodes: OK
			if (nodeId <= 0 || segmentId <= 0 || TrafficSegments[segmentId] == null)
				return;
			var prioritySegment = TrafficSegments[segmentId];

#if DEBUG
			Log._Debug($"TrafficPriority.RemovePrioritySegment: Removing SegmentEnd {segmentId} @ {nodeId}");
#endif

			if (prioritySegment.Node1 == nodeId) {
				prioritySegment.Node1 = 0;
				prioritySegment.Instance1.Destroy();
				prioritySegment.Instance1 = null;
			}
			if (prioritySegment.Node2 == nodeId) {
				prioritySegment.Node2 = 0;
				prioritySegment.Instance2.Destroy();
				prioritySegment.Instance2 = null;
			}

			if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0)
				TrafficSegments[segmentId] = null;
			if (rebuildNodes)
				rebuildPriorityNodes();
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
				Log.Error($"Error occured when trying to clear traffic: {ex.ToString()}");
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

		public static bool HasIncomingVehiclesWithHigherPriority(ushort targetVehicleId, ushort nodeId) {
			try {
#if DEBUG
				//bool debug = nodeId == 30634;
				bool debug = nodeId == 30921;
				if (debug) {
					Log._Debug($"HasIncomingVehicles: Checking vehicle {targetVehicleId} at node {nodeId}");
				}
#else
				bool debug = false;
#endif

				VehicleState targetVehicleState = VehicleStateManager.GetVehicleState(targetVehicleId);
				if (targetVehicleState == null) {
#if DEBUG
					Log.Warning($"HasIncomingVehicles: vehicle {targetVehicleId} @ node {nodeId}: Target state is invalid!");
#endif
					return false;
				}
				VehiclePosition targetVehiclePos = targetVehicleState.GetCurrentPosition();
				if (targetVehiclePos == null) {
#if DEBUG
					Log.Warning($"HasIncomingVehicles: vehicle {targetVehicleId} @ node {nodeId}: Target position is invalid!");
#endif
					return false;
				}

				if (targetVehiclePos.TransitNodeId != nodeId) {
#if DEBUG
					if (debug) {
						Log._Debug($"HasIncomingVehicles: The vehicle {targetVehicleId} is not driving on a segment adjacent to node {nodeId} (it is driving on segment {targetVehiclePos.TargetSegmentId}, to node {targetVehiclePos.TransitNodeId}, to segment {targetVehiclePos.TargetSegmentId}.");
					}
#endif
					return false;
				}

#if DEBUG
				if (debug) {
					Log._Debug($"HasIncomingVehicles: {targetVehicleId} @ {nodeId}, fromSegment: {targetVehiclePos.SourceSegmentId}, toSegment: {targetVehiclePos.TargetSegmentId}");
				}
#endif

				var targetFromPrioritySegment = GetPrioritySegment(nodeId, targetVehiclePos.SourceSegmentId);
				if (targetFromPrioritySegment == null) {
#if DEBUG
					if (debug) {
						Log._Debug($"HasIncomingVehicles: source priority segment not found.");
					}
#endif
					return false;
				}

				//SegmentGeometry srcGeometry = SegmentGeometry.Get(targetVehiclePos.SourceSegmentId);
				//Direction targetToDir = srcGeometry.GetDirection(targetVehiclePos.TargetSegmentId, srcGeometry.StartNodeId() == nodeId);

				// get all cars
				for (var s = 0; s < 8; s++) {
					var incomingSegmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

					if (incomingSegmentId == 0 || incomingSegmentId == targetVehiclePos.SourceSegmentId)
						continue;

					if (!IsPrioritySegment(nodeId, incomingSegmentId)) {
#if DEBUG
						if (debug) {
							Log._Debug($"HasIncomingVehicles: Segment {incomingSegmentId} @ {nodeId} is not a priority segment (1).");
						}
#endif
						continue;
					}

					var incomingFromPrioritySegment = GetPrioritySegment(nodeId, incomingSegmentId);
					if (incomingFromPrioritySegment == null) {
#if DEBUG
						if (debug) {
							Log._Debug($"HasIncomingVehicles: Segment {incomingSegmentId} @ {nodeId} is not a priority segment (2).");
						}
#endif
						continue; // should not happen
					}

					SegmentGeometry incomingGeometry = SegmentGeometry.Get(incomingSegmentId);
					if (incomingGeometry.IsOutgoingOneWay(incomingGeometry.StartNodeId() == nodeId)) {
						continue;
					}

					if ((Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
						if (targetFromPrioritySegment.Type == SegmentEnd.PriorityType.Main || targetFromPrioritySegment.Type == SegmentEnd.PriorityType.None) {
#if DEBUG
							if (debug)
								Log._Debug($"HasIncomingVehicles: Target {targetVehicleId} is on a main road @ {nodeId}.");
#endif
							// target is on a main segment
							if (incomingFromPrioritySegment.Type != SegmentEnd.PriorityType.Main && incomingFromPrioritySegment.Type != SegmentEnd.PriorityType.None) {
								continue; // ignore cars coming from low priority segments (yield/stop)
										  // count incoming cars from other main segment
							}

							foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.GetRegisteredVehicles()) {
								ushort incomingVehicleId = e.Key;
								VehiclePosition incomingVehiclePos = e.Value;

#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Checking agains incoming vehicle {incomingVehicleId}.");
#endif

								if (incomingVehicleId == 0)
									continue;

								if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId].GetLastFrameVelocity().magnitude > maxStopVelocity) {
									if (HasVehiclePriority(debug, targetVehicleId, true, incomingVehicleId, true, incomingVehiclePos, targetFromPrioritySegment, incomingFromPrioritySegment)) {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} is not conflicting.");
#endif
										continue;
									} else {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} IS conflicting.");
#endif
										return true;
									}
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) is not conflicting due to low speed.");
#endif
								}
							}
						} else {
							// target car is on a low-priority segment
#if DEBUG
							if (debug)
								Log._Debug($"HasIncomingVehicles: Target {targetVehicleId} is on a low priority road @ {nodeId}. Incoming vehicles: {incomingFromPrioritySegment.GetRegisteredVehicleCount()}");
#endif

							// Main - Yield/Stop
							foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.GetRegisteredVehicles()) {
								ushort incomingVehicleId = e.Key;
								VehiclePosition incomingVehiclePos = e.Value;
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Checking agains incoming vehicle {incomingVehicleId}.");
#endif

								if (incomingVehicleId == 0)
									continue;

								/*VehiclePosition otherVehiclePos = VehicleStateManager.GetVehiclePosition(incomingVehicleId);
								if (otherVehiclePos == null) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Could not find current vehicle position of incoming vehicle {incomingVehicleId}.");
#endif
									continue;
								}*/

								if (incomingFromPrioritySegment.Type == SegmentEnd.PriorityType.Main || incomingFromPrioritySegment.Type == SegmentEnd.PriorityType.None) {
									if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId].GetLastFrameVelocity().magnitude > maxStopVelocity) {
										if (HasVehiclePriority(debug, targetVehicleId, false, incomingVehicleId, true, incomingVehiclePos, targetFromPrioritySegment, incomingFromPrioritySegment)) {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) is not conflicting.");
#endif
											continue;
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) IS conflicting.");
#endif
											return true;
										}
									} else {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (main) is not conflicting due to low speed.");
#endif
									}
								} else {
									if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId].GetLastFrameVelocity().magnitude > 0.5f) {
										if (HasVehiclePriority(debug, targetVehicleId, false, incomingVehicleId, false, incomingVehiclePos, targetFromPrioritySegment, incomingFromPrioritySegment)) {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (low) is not conflicting.");
#endif
											continue;
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (low) IS conflicting.");
#endif
											return true;
										}
									} else {
#if DEBUG
										if (debug)
											Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (low) is not conflicting due to low speed.");
#endif
									}
								}
							}
						}
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"HasIncomingVehicles: Node {nodeId} is a traffic light.");
#endif

						// Traffic lights
						if (!CustomTrafficLights.IsSegmentLight(nodeId, incomingSegmentId)) {
#if DEBUG
							if (debug) {
								Log._Debug($"HasIncomingVehicles: Segment {incomingSegmentId} @ {nodeId} does not have live traffic lights.");
							}
#endif
							continue;
						}

						var segmentLights = CustomTrafficLights.GetSegmentLights(nodeId, incomingSegmentId);
						var segmentLight = segmentLights.GetCustomLight(targetVehiclePos.SourceLaneIndex);
						if (segmentLight == null) {
#if DEBUG
							Log._Debug($"HasIncomingVehicles: segmentLight is null for seg. {incomingSegmentId} @ node {nodeId}, vehicle type {targetVehicleState.VehicleType}");
#endif
							continue;
						}

						if (segmentLight.GetLightMain() != RoadBaseAI.TrafficLightState.Green)
							continue;
#if DEBUG
						if (debug)
							Log._Debug($"Segment {incomingSegmentId} @ {nodeId} is a GREEN traffic light.");
#endif

						foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.GetRegisteredVehicles()) {
							ushort incomingVehicleId = e.Key;
							VehiclePosition incomingVehiclePos = e.Value;

							if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingVehicleId].GetLastFrameVelocity().magnitude > maxStopVelocity) {
								if (HasVehiclePriority(debug, targetVehicleId, true, incomingVehicleId, true, incomingVehiclePos, targetFromPrioritySegment, incomingFromPrioritySegment)) {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (light) is not conflicting.");
#endif
									continue;
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (light) IS conflicting.");
#endif
									return true;
								}
							} else {
#if DEBUG
								if (debug)
									Log._Debug($"HasIncomingVehicles: Incoming {incomingVehicleId} (light) is not conflicting due to low speed.");
#endif
							}
						}
					}
				}

				return false;
			} catch (Exception e) {
				Log.Error($"HasIncomingVehicles: Error occurred: {e.ToString()}");
            }
			return false;
		}

		protected static bool HasVehiclePriority(bool debug, ushort targetCarId, bool targetIsOnMainRoad, ushort incomingCarId, bool incomingIsOnMainRoad, VehiclePosition incomingVehPos, SegmentEnd targetEnd, SegmentEnd incomingEnd) {
			try {
#if DEBUG
				debug = targetEnd.NodeId == 16015;
				//debug = nodeId == 13531;
				if (debug) {
					Log._Debug($"HasVehiclePriority: Checking if {targetCarId} (main road = {targetIsOnMainRoad}) has priority over {incomingCarId} (main road = {incomingIsOnMainRoad}).");
                }
#endif

				if (targetEnd.NodeId != incomingEnd.NodeId) {
					Log.Error($"HasVehiclePriority: Incompatible SegmentEnds!");
					return true;
				}

				ushort nodeId = targetEnd.NodeId;

				// delete invalid target car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[targetCarId].m_flags & Vehicle.Flags.Created) == 0) {
					targetEnd.RequestCleanup();
					return true;
				}

				// delete invalid incoming car
				if ((Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingCarId].m_flags & Vehicle.Flags.Created) == 0) {
					incomingEnd.RequestCleanup();
					return true;
				}

				// check if incoming car has stopped
				float incomingVel = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[incomingCarId].GetLastFrameVelocity().magnitude;
				if (incomingVel <= maxStopVelocity) {
#if DEBUG
					Log._Debug($"HasVehiclePriority: incoming car {incomingCarId} is too slow");
#endif
					return true;
				}

				var targetVehState = VehicleStateManager.GetVehicleState(targetCarId);
				var incomingVehState = VehicleStateManager.GetVehicleState(incomingCarId);

				if (targetVehState == null) {
					targetEnd.RequestCleanup();
					return true;
				}

				if ((targetVehState.VehicleType & ExtVehicleType.Emergency) != ExtVehicleType.None) {
					// target vehicle is on emergency
					return true;
				}

				if (incomingVehState == null) {
					incomingEnd.RequestCleanup();
					return true;
				}

				if ((incomingVehState.VehicleType & ExtVehicleType.Emergency) != ExtVehicleType.None) {
					// incoming vehicle is on emergency
					return false;
				}

				var targetVehPos = targetVehState.GetCurrentPosition();

				if (targetVehPos == null) {
					targetEnd.RequestCleanup();
					return true;
				}

				if (targetVehPos.SourceSegmentId != targetEnd.SegmentId || targetVehPos.TransitNodeId != targetEnd.NodeId) {
					targetEnd.RequestCleanup();
					return true;
				}

				// check if target is on main road and incoming is on low-priority road
				if (targetIsOnMainRoad && !incomingIsOnMainRoad)
					return true;

#if DEBUG
				/*if (debug) {
					Log._Debug($"HasVehiclePriority: Distance between target car {targetCarId} and incoming car {incomingCarId}: {dist}. Incoming speed: {incomingVel}. Speed * 20 time units = {incomingVel*20}");
				}*/
#endif
				// check incoming car position
				/*if (incomingVel * 20f < dist) {
#if DEBUG
					if (debug) {
						Log.Message($"HasVehiclePriority: Target car {targetCarId} can make it against {incomingCarId}! Ha!");
					}
#endif
					return true;
				}*/

				//         TOP
				//          |
				//          |
				// LEFT --- + --- RIGHT
				//          |
				//          |
				//        BOTTOM

				// We assume the target car is coming from BOTTOM.

				SegmentGeometry targetGeometry = SegmentGeometry.Get(targetVehPos.SourceSegmentId);
				SegmentGeometry incomingGeometry = SegmentGeometry.Get(incomingVehPos.SourceSegmentId);
				bool isTargetStartNode = targetGeometry.StartNodeId() == nodeId;
				Direction targetToDir = targetGeometry.GetDirection(targetVehPos.TargetSegmentId, isTargetStartNode);
				Direction incomingRelDir = targetGeometry.GetDirection(incomingVehPos.SourceSegmentId, isTargetStartNode);
				Direction incomingToDir = incomingGeometry.GetDirection(incomingVehPos.TargetSegmentId, incomingGeometry.StartNodeId() == nodeId);
#if DEBUG
				if (debug) {
					Log._Debug($"HasVehiclePriority: targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
                }
#endif

				if (IsLeftHandDrive()) {
					// mirror situation for left-hand traffic systems
					targetToDir = InvertLeftRight(targetToDir);
					incomingRelDir = InvertLeftRight(incomingRelDir);
					incomingToDir = InvertLeftRight(incomingToDir);
#if DEBUG
					if (debug) {
						Log._Debug($"HasVehiclePriority: LHD! targetToDir: {targetToDir.ToString()}, incomingRelDir: {incomingRelDir.ToString()}, incomingToDir: {incomingToDir.ToString()}");
					}
#endif
				}

				bool sameTargets = false;
				bool laneOrderCorrect = false;
				if (targetVehPos.TargetSegmentId == incomingVehPos.TargetSegmentId) {
					// target and incoming are both going to same segment
					sameTargets = true;
					if (targetVehPos.TargetLaneIndex == incomingVehPos.TargetLaneIndex && targetVehPos.SourceSegmentId != incomingVehPos.SourceSegmentId)
						laneOrderCorrect = false;
					else {
						switch (targetToDir) {
							case Direction.Left:
								laneOrderCorrect = IsLaneOrderConflictFree(targetVehPos.TargetSegmentId, targetVehPos.TargetLaneIndex, incomingVehPos.TargetLaneIndex); // stay left
								break;
							case Direction.Forward:
							default:
								switch (incomingRelDir) {
									case Direction.Left:
									case Direction.Forward:
										laneOrderCorrect = IsLaneOrderConflictFree(targetVehPos.TargetSegmentId, incomingVehPos.TargetLaneIndex, targetVehPos.TargetLaneIndex); // stay right
										break;
									case Direction.Right:
										laneOrderCorrect = IsLaneOrderConflictFree(targetVehPos.TargetSegmentId, targetVehPos.TargetLaneIndex, incomingVehPos.TargetLaneIndex); // stay left
										break;
									case Direction.Turn:
									default:
										laneOrderCorrect = true;
										break;
								}
								break;
							case Direction.Right:
								laneOrderCorrect = IsLaneOrderConflictFree(targetVehPos.TargetSegmentId, incomingVehPos.TargetLaneIndex, targetVehPos.TargetLaneIndex); // stay right
								break;
						}
						laneOrderCorrect = IsLaneOrderConflictFree(targetVehPos.TargetSegmentId, targetVehPos.TargetLaneIndex, incomingVehPos.TargetLaneIndex);
#if DEBUG
						if (debug) {
							Log._Debug($"HasVehiclePriority: target {targetCarId} (going to lane {targetVehPos.TargetLaneIndex}) and incoming {incomingCarId} (going to lane {incomingVehPos.TargetLaneIndex}) are going to the same segment. Lane order correct? {laneOrderCorrect}");
						}
#endif
					}
				}

				if (sameTargets && laneOrderCorrect) {
#if DEBUG
					if (debug) {
						Log._Debug($"Lane order between car {targetCarId} and {incomingCarId} is correct.");
					}
#endif
					return true;
				}

				bool incomingCrossingStreet = incomingToDir == Direction.Forward || incomingToDir == Direction.Left;

				switch (targetToDir) {
					case Direction.Right:
						// target: BOTTOM->RIGHT
#if DEBUG
						if (debug) {
							Log._Debug($"Car {targetCarId} (vs. {incomingVehPos.SourceSegmentId}->{incomingVehPos.TargetSegmentId}) is going right without conflict!");
                        }
#endif

						if (!targetIsOnMainRoad && incomingIsOnMainRoad && sameTargets && !laneOrderCorrect) {
#if DEBUG
							if (debug) {
								Log._Debug($"Car {targetCarId} (vs. {incomingVehPos.SourceSegmentId}->{incomingVehPos.TargetSegmentId}) is on low-priority road turning right. the other vehicle is on a priority road.");
							}
#endif
							return false; // vehicle must wait for incoming vehicle on priority road
						}

						return true;
					case Direction.Forward:
					default:
#if DEBUG
						if (debug) {
							Log._Debug($"Car {targetCarId} (vs. {incomingVehPos.SourceSegmentId}->{incomingVehPos.TargetSegmentId}) is going forward: {incomingRelDir}, {targetIsOnMainRoad}, {incomingCrossingStreet}!");
						}
#endif
						// target: BOTTOM->TOP
						switch (incomingRelDir) {
							case Direction.Right:
								return !incomingIsOnMainRoad && !incomingCrossingStreet;
							case Direction.Left:
								return targetIsOnMainRoad || !incomingCrossingStreet;
							case Direction.Forward:
							default:
								return true;
						}
					case Direction.Left:
#if DEBUG
						if (debug) {
							Log._Debug($"Car {targetCarId} (vs. {incomingVehPos.SourceSegmentId}->{incomingVehPos.TargetSegmentId}) is going left: {incomingRelDir}, {targetIsOnMainRoad}, {incomingIsOnMainRoad}, {incomingCrossingStreet}, {incomingToDir}!");
						}
#endif
						// target: BOTTOM->LEFT
						switch (incomingRelDir) {
							case Direction.Right:
								return !incomingCrossingStreet;
							case Direction.Left:
								if (targetIsOnMainRoad && incomingIsOnMainRoad) // bent priority road
									return true;
								return !incomingCrossingStreet;
							case Direction.Forward:
							default:
								return incomingToDir == Direction.Left || incomingToDir == Direction.Turn;
						}
				}
			} catch (Exception e) {
				Log.Error("Error occured: " + e.ToString());
			}

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
			TrafficLightSimulation.LightSimulationByNodeId.Clear();
			priorityNodes.Clear();
			for (int i = 0; i < TrafficSegments.Length; ++i)
				TrafficSegments[i] = null;
		}

		public static bool IsLaneOrderConflictFree(ushort segmentId, uint leftLaneIndex, uint rightLaneIndex) { // TODO I think this is incorrect. See TrafficLightTool._guiLaneChangeWindow
			try {
				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
				NetInfo.Direction normDirection = IsLeftHandDrive() ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
				NetInfo.Lane leftLane = segmentInfo.m_lanes[leftLaneIndex];
				NetInfo.Lane rightLane = segmentInfo.m_lanes[rightLaneIndex];

				// forward (right-hand traffic system): left < right
				// backward (right-hand traffic system): left > right
				if ((byte)(leftLane.m_direction & normDirection) != 0) {
					return leftLane.m_position < rightLane.m_position; 
				} else {
					return rightLane.m_position < leftLane.m_position;
				}
			} catch (Exception e) {
				Log.Error($"IsLaneOrderConflictFree({segmentId}, {leftLaneIndex}, {rightLaneIndex}): Error: {e.ToString()}");
            }
			return true;
		}

		/// <summary>
		/// rebuilds the implicitly defined set of priority nodes (traffic light nodes & nodes with priority signs)
		/// </summary>
		private static void rebuildPriorityNodes() {
			priorityNodes.Clear();

			for (ushort i = 0; i < TrafficSegments.Length; ++i) {
				var trafficSeg = TrafficSegments[i];
				if (trafficSeg == null)
					continue;
				if (trafficSeg.Node1 != 0)
					priorityNodes.Add(trafficSeg.Node1);
				if (trafficSeg.Node2 != 0)
					priorityNodes.Add(trafficSeg.Node2);
			}
		}

		/// <summary>
		/// Determines if the map uses a left-hand traffic system
		/// </summary>
		/// <returns></returns>
		public static bool IsLeftHandDrive() {
			return Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
		}

		private static List<ushort> vehicleIdsToDelete = new List<ushort>();

		//private static ushort nextValidityCheckedSegment = 0;

		public static void SegmentSimulationStep(ushort segmentId) {
			if (ClearTrafficRequested) {
				TrafficPriority.ClearTraffic();
				ClearTrafficRequested = false;
			}

			/*SegmentGeometry.Get(nextValidityCheckedSegment)?.VerifyCreated();
			if (nextValidityCheckedSegment != segmentId)
				SegmentGeometry.Get(segmentId)?.VerifyByNodes();
			nextValidityCheckedSegment = (ushort)(((int)nextValidityCheckedSegment + 1) % NetManager.MAX_SEGMENT_COUNT);*/

			// simulate segment-ends
			TrafficSegment trafficSegment = TrafficSegments[segmentId];
			if (trafficSegment == null)
				return;
			trafficSegment.Instance1?.SimulationStep();
			trafficSegment.Instance2?.SimulationStep();
		}

		/*public static void nodeHousekeeping(ushort nodeId) {
			try {
				uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

				NetManager netManager = Singleton<NetManager>.instance;
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				Flags.applyNodeTrafficLightFlag(nodeId);

				if (IsPriorityNode(nodeId)) {
					NodeValidityState nodeState = NodeValidityState.Valid;
					if (!isValidPriorityNode(nodeId, out nodeState)) {
						if (nodeState != NodeValidityState.SimWithoutLight) {
							Log.Warning("Housekeeping: Deleting node " + nodeId);
							RemovePrioritySegments(nodeId);
						}

						switch (nodeState) {
							case NodeValidityState.SimWithoutLight:
								Log.Warning("Housekeeping: Re-adding traffic light at node " + nodeId);
								Flags.setNodeTrafficLight(nodeId, true);
								break;
							case NodeValidityState.Unused:
							case NodeValidityState.IllegalSim:
								// delete traffic light simulation
								Log.Warning("Housekeeping: RemoveNodeFromSimulation " + nodeId);
								TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, false, true);
								break;
							default:
								break;
						}
					}
				}

				// add newly created segments to timed traffic lights
				TrafficLightSimulation lightSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (lightSim != null)
					lightSim.handleNewSegments();
			} catch (Exception e) {
				Log.Warning($"Housekeeping failed: {e.ToString()}");
			}
		}

		private enum NodeValidityState {
			Valid,
			/// <summary>
			/// the node is currently not used (no traffic junction exists for the node id)
			/// </summary>
			Unused,
			/// <summary>
			/// a traffic light simulation is running for this node but the node does not have a traffic light
			/// </summary>
			SimWithoutLight,
			/// <summary>
			/// a traffic light simulation is running at a node that does not allow traffic lights
			/// </summary>
			IllegalSim,
			/// <summary>
			/// none of the node's possible priority signs is set
			/// </summary>
			NoValidSegments,
			/// <summary>
			/// Invalid node id given
			/// </summary>
			Invalid
		}

		private static bool isValidPriorityNode(ushort nodeId, out NodeValidityState nodeState) {
			nodeState = NodeValidityState.Valid;

			if (nodeId <= 0) {
				nodeState = NodeValidityState.Invalid;
				Log.Warning($"Housekeeping: Node {nodeId} is invalid!");
				return false;
			}

			NetManager netManager = Singleton<NetManager>.instance;

			if ((netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				nodeState = NodeValidityState.Unused;
				Log.Warning($"Housekeeping: Node {nodeId} is unused!");
				return false; // node is unused
			}

			bool hasTrafficLight = (netManager.m_nodes.m_buffer[nodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
			var nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim != null) {
				if (! Flags.mayHaveTrafficLight(nodeId)) {
					nodeState = NodeValidityState.IllegalSim;
					Log.Warning($"Housekeeping: Node {nodeId} has traffic light simulation but must not have a traffic light!");
					return false;
				}

				if (!hasTrafficLight) {
					// traffic light simulation is active but node does not have a traffic light
					nodeState = NodeValidityState.SimWithoutLight;
					Log.Warning($"Housekeeping: Node {nodeId} has traffic light simulation but no traffic light!");
					return false;
				} else {
					// check if all timed step segments are valid
					if (nodeSim.IsTimedLightActive()) {
						TimedTrafficLights timedLight = nodeSim.TimedLight;
						if (timedLight == null || timedLight.Steps.Count <= 0) {
							Log.Warning("Housekeeping: Timed light is null or no steps for node {nodeId}!");
							TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, false, false);
							return false;
						}

						//foreach (var segmentId in timedLight.Steps[0].segmentIds) {
						//	if (! IsPrioritySegment(nodeId, segmentId)) {
						//		Log.Warning("Housekeeping: Timed light - Priority segment has gone away!");
						//		RemoveNodeFromSimulation(nodeId);
						//		return false;
						//	}
						//}
					}
					return true;
				}
			} else {
				byte numSegmentsWithSigns = 0;
				for (var s = 0; s < 8; s++) {
					var segmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					if (netManager.m_segments.m_buffer[segmentId].m_startNode != nodeId && netManager.m_segments.m_buffer[segmentId].m_endNode != nodeId)
						continue;

					SegmentEnd prioritySegment = GetPrioritySegment(nodeId, segmentId);
					if (prioritySegment == null) {
						continue;
					}

					// if node is a traffic light, it must not have priority signs
					if (hasTrafficLight && prioritySegment.Type != SegmentEnd.PriorityType.None) {
						Log.Warning($"Housekeeping: Node {nodeId}, Segment {segmentId} is a priority sign but node has a traffic light!");
						prioritySegment.Type = SegmentEnd.PriorityType.None;
					}

					// if a priority sign is set, everything is ok
					if (prioritySegment.Type != SegmentEnd.PriorityType.None) {
						++numSegmentsWithSigns;
					}
				}

				if (numSegmentsWithSigns > 0) {
					// add priority segments for newly created segments
					numSegmentsWithSigns += AddPriorityNode(nodeId);
				}

				bool ok = numSegmentsWithSigns >= 2;
				if (!ok) {
					Log.Warning($"Housekeeping: Node {nodeId} does not have valid priority segments!");
					nodeState = NodeValidityState.NoValidSegments;
				}
				return ok;
			}
		}*/
	}
}

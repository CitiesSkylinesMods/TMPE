using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using UnityEngine;
using TrafficManager.State;

namespace TrafficManager.Traffic {
	class TrafficPriority {
		public enum Direction {
			Left,
			Forward,
			Right,
			Turn
		}

		private static uint[] segmentsCheckLoadBalanceMod = new uint[] { 15, 31, 63, 127, 255 };
		private static uint checkMod = 0;

		public static bool LeftHandDrive;

		/// <summary>
		/// Dictionary of segments that are connected to roads with timed traffic lights or priority signs
		/// </summary>
		public static Dictionary<ushort, TrafficSegment> PrioritySegments = new Dictionary<ushort, TrafficSegment>();

		/// <summary>
		/// Dictionary of relevant vehicles
		/// </summary>
		public static Dictionary<ushort, VehiclePosition> Vehicles = new Dictionary<ushort, VehiclePosition>();

		/// <summary>
		/// Nodes that have timed traffic lights or priority signs
		/// </summary>
		private static HashSet<ushort> priorityNodes = new HashSet<ushort>();

		public static void AddPrioritySegment(ushort nodeId, ushort segmentId, PrioritySegment.PriorityType type) {
			if (nodeId <= 0 || segmentId <= 0)
				return;

			Log.Message("adding PrioritySegment @ node " + nodeId + ", seg. " + segmentId + ", type " + type);
			if (PrioritySegments.ContainsKey(segmentId)) { // do not replace with IsPrioritySegment!
				var prioritySegment = PrioritySegments[segmentId];
				prioritySegment.Segment = segmentId;

				Log.Message("Priority segment already exists. Node1=" + prioritySegment.Node1 + " Node2=" + prioritySegment.Node2);

				if (prioritySegment.Node1 == nodeId || prioritySegment.Node1 == 0) {
					Log.Message("Updating Node1");
					prioritySegment.Node1 = nodeId;
					PrioritySegments[segmentId].Instance1 = new PrioritySegment(nodeId, segmentId, type);
					return;
				}

				if (prioritySegment.Node2 != 0) {
					// overwrite Node2
					Log.Warning("Overwriting priority segment for node " + nodeId + ", seg. " + segmentId + ", type " + type);
					prioritySegment.Node2 = nodeId;
					prioritySegment.Instance2.Nodeid = nodeId;
					prioritySegment.Instance2.Segmentid = segmentId;
					prioritySegment.Instance2.Type = type;
					rebuildPriorityNodes();
				} else {
					// add Node2
					Log.Message("Adding as Node2");
					prioritySegment.Node2 = nodeId;
					prioritySegment.Instance2 = new PrioritySegment(nodeId, segmentId, type);
				}
			} else {
				// add Node1
				Log.Message("Adding as Node1");
				PrioritySegments.Add(segmentId, new TrafficSegment());
				PrioritySegments[segmentId].Segment = segmentId;
				PrioritySegments[segmentId].Node1 = nodeId;
				PrioritySegments[segmentId].Instance1 = new PrioritySegment(nodeId, segmentId, type);
			}
			priorityNodes.Add(nodeId);
		}

		public static void RemovePrioritySegments(ushort nodeId) {
			if (nodeId <= 0)
				return;

			var node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);
				if (segmentId <= 0)
					continue;

				if (IsPrioritySegment(nodeId, segmentId)) {
					//Log.Message("Housekeeping: node " + nodeId + " contains prio seg. " + segmentId);
					var prioritySegment = PrioritySegments[segmentId];
					if (prioritySegment.Node1 == nodeId) {
						prioritySegment.Node1 = 0;
						prioritySegment.Instance1 = null;
					} else {
						prioritySegment.Node2 = 0;
						prioritySegment.Instance2 = null;
					}

					if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0) {
						PrioritySegments.Remove(segmentId);
					}
				}
			}
			priorityNodes.Remove(nodeId);
		}

		public static List<ushort> GetPrioritySegmentIds(ushort nodeId) {
			List<ushort> ret = new List<ushort>();
			if (nodeId <= 0)
				return ret;

			var node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);
				if (segmentId <= 0)
					continue;

				if (IsPrioritySegment(nodeId, segmentId)) {
					ret.Add(segmentId);
				}
			}

			return ret;
		}

		internal static void HandleAllVehicles() {
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			for (ushort i = 0; i < vehicleManager.m_vehicles.m_size; ++i) {
				if (vehicleManager.m_vehicles.m_buffer[i].m_flags != Vehicle.Flags.None) {
					try {
						CustomCarAI.HandleVehicle(i, ref vehicleManager.m_vehicles.m_buffer[i], true);
					} catch (Exception e) {
						Log.Error("TrafficPriority HandleAllVehicles Error: " + e.ToString());
					}
				}
			}
		}

		public static bool IsPrioritySegment(ushort nodeId, ushort segmentId) {
			if (nodeId <= 0 || segmentId <= 0)
				return false;

			if (PrioritySegments.ContainsKey(segmentId)) {
				var prioritySegment = PrioritySegments[segmentId];

				NetManager netManager = Singleton<NetManager>.instance;
				if (netManager.m_segments.m_buffer[segmentId].m_flags == NetSegment.Flags.None) {
					RemovePrioritySegment(nodeId, segmentId);
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

		public static HashSet<ushort> getPriorityNodes() {
			return priorityNodes;
		}

		public static PrioritySegment GetPrioritySegment(ushort nodeId, ushort segmentId) {
			if (!IsPrioritySegment(nodeId, segmentId)) return null;

			var prioritySegment = PrioritySegments[segmentId];

			if (prioritySegment.Node1 == nodeId) {
				return prioritySegment.Instance1;
			}

			return prioritySegment.Node2 == nodeId ?
				prioritySegment.Instance2 : null;
		}

		internal static void RemovePrioritySegment(ushort nodeId, ushort segmentId) {
			if (!PrioritySegments.ContainsKey(segmentId))
				return;
			var prioritySegment = PrioritySegments[segmentId];

			if (prioritySegment.Node1 == nodeId) {
				prioritySegment.Node1 = 0;
				prioritySegment.Instance1 = null;
			}
			if (prioritySegment.Node2 == nodeId) {
				prioritySegment.Node2 = 0;
				prioritySegment.Instance2 = null;
			}

			if (prioritySegment.Node1 == 0 && prioritySegment.Node2 == 0)
				PrioritySegments.Remove(segmentId);
			rebuildPriorityNodes();
		}

		internal static void ClearTraffic() {
			try {
				var vehicleList = TrafficPriority.Vehicles.Keys.ToList();

				lock (Singleton<VehicleManager>.instance) {
					foreach (var vehicle in
						from vehicle in vehicleList
						let vehicleData = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicle]
						where vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car
						select vehicle) {
						Singleton<VehicleManager>.instance.ReleaseVehicle(vehicle);
					}
				}
			} catch (Exception ex) {
				Log.Error($"Error occured when trying to clear traffic: {ex.ToString()}");
            }
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
				if (TrafficLightsManual.SegmentIsOutgoingOneWay(segmentId, nodeId))
					continue;

				TrafficPriority.AddPrioritySegment(nodeId, segmentId, PrioritySegment.PriorityType.None);
				++ret;
			}
			return ret;
		}

		public static bool HasIncomingVehicles(ushort targetCar, ushort nodeId) {
#if DEBUG
			bool debug = nodeId == 6090;
#else
			bool debug = false;
#endif

			if (!Vehicles.ContainsKey(targetCar)) {
				Log.Warning($"HasIncomingVehicles: {targetCar} @ {nodeId}, fromSegment: {Vehicles[targetCar].FromSegment}, toSegment: {Vehicles[targetCar].ToSegment}. Car does not exist!");
				return false;
			}


			if (debug) {
				Log.Message($"HasIncomingVehicles: {targetCar} @ {nodeId}, fromSegment: {Vehicles[targetCar].FromSegment}, toSegment: {Vehicles[targetCar].ToSegment}");
			}

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			var frame = currentFrameIndex >> 4;
			var node = TrafficLightTool.GetNetNode(nodeId);

			var targetFromPrioritySegment = GetPrioritySegment(nodeId, Vehicles[targetCar].FromSegment);
			if (targetFromPrioritySegment == null) {
				if (debug) {
					Log.Message($"source priority segment not found.");
				}
				return false;
			}

			var targetToPrioritySegment = GetPrioritySegment(nodeId, Vehicles[targetCar].ToSegment);
			if (targetToPrioritySegment == null) {
				if (debug) {
					Log.Message($"target priority segment not found.");
				}
				return false;
			}

			Direction targetToDir = GetDirection(Vehicles[targetCar].FromSegment, Vehicles[targetCar].ToSegment, nodeId);

			var numCars = 0;

			// get all cars
			for (var s = 0; s < 8; s++) {
				var segment = node.GetSegment(s);

				if (segment == 0 || segment == Vehicles[targetCar].FromSegment) continue;
				if (!IsPrioritySegment(nodeId, segment)) {
					if (debug) {
						Log.Message($"Segment {segment} @ {nodeId} is not a priority segment (1).");
					}
					continue;
				}

				var incomingFromPrioritySegment = GetPrioritySegment(nodeId, segment);
				if (incomingFromPrioritySegment == null) {
					if (debug) {
						Log.Message($"Segment {segment} @ {nodeId} is not a priority segment (2).");
					}
					continue; // should not happen
				}

				if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					if (targetFromPrioritySegment.Type == PrioritySegment.PriorityType.Main) {
						// target is on a main segment
						if (incomingFromPrioritySegment.Type != PrioritySegment.PriorityType.Main)
							continue; // ignore cars coming from low priority segments (yield/stop)
						// count incoming cars from other main segment

						numCars += incomingFromPrioritySegment.getNumApproachingVehicles();

						foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.getApproachingVehicles()) {
							var car = e.Key;
							if (!Vehicles.ContainsKey(car)) {
								--numCars;
								continue;
							}

							if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].GetLastFrameVelocity().magnitude > 0.25f) {
								if (HasVehiclePriority(debug, targetCar, true, car, true, nodeId))
									--numCars;
								else {
									/*if (debug) {
										Log.Message($"Vehicle {targetCar} on segment {Vehicles[targetCar].FromSegment} has to wait for vehicle {car} on segment {Vehicles[car].FromSegment}");
									}*/
									return true;
								}
							} else {
								numCars--;
							}
						}
					} else {
						// target car is on a low-priority segment

						// Main - Yield/Stop
						numCars += incomingFromPrioritySegment.getNumApproachingVehicles();

						foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.getApproachingVehicles()) {
							var car = e.Key;
							if (!Vehicles.ContainsKey(car)) {
								--numCars;
								continue;
							}

							if (incomingFromPrioritySegment.Type == PrioritySegment.PriorityType.Main) {
								if (!Vehicles[car].Stopped && Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].GetLastFrameVelocity().magnitude > 0.25f) {
									if (HasVehiclePriority(debug, targetCar, false, car, true, nodeId))
										--numCars;
									else
										return true;
								} else {
									numCars--;
								}
							} else {
								if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].GetLastFrameVelocity().magnitude > 0.25f) {
									if (HasVehiclePriority(debug, targetCar, false, car, false, nodeId))
										--numCars;
									else
										return true;
								} else {
									numCars--;
								}
							}
						}
					}
				} else {
					// Traffic lights
					if (!TrafficLightsManual.IsSegmentLight(nodeId, segment)) {
						if (debug) {
							Log.Message($"Segment {segment} @ {nodeId} does not have live traffic lights.");
						}
						continue;
					}

					var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segment);

					if (segmentLight.GetLightMain() != RoadBaseAI.TrafficLightState.Green) continue;

					numCars += incomingFromPrioritySegment.getNumApproachingVehicles();

					foreach (KeyValuePair<ushort, VehiclePosition> e in incomingFromPrioritySegment.getApproachingVehicles()) {
						var otherCar = e.Key;
						if (!Vehicles.ContainsKey(otherCar)) {
							--numCars;
							continue;
						}

						if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[otherCar].GetLastFrameVelocity().magnitude > 0.25f) {
							if (HasVehiclePriority(debug, targetCar, true, otherCar, true, nodeId))
								--numCars;
							else
								return true;
						} else {
							numCars--;
						}
					}
				}

				if (numCars > 0)
					return true;
			}

			return numCars > 0;
		}

		internal static void RemoveVehicle(ushort vehicleId) {
			foreach (KeyValuePair<ushort, TrafficSegment> e in PrioritySegments) {
				if (e.Value.Instance1 != null)
					e.Value.Instance1.RemoveCar(vehicleId);
				if (e.Value.Instance2 != null)
					e.Value.Instance2.RemoveCar(vehicleId);
			}
		}

		protected static bool HasVehiclePriority(bool debug, ushort targetCarId, bool targetIsOnMainRoad, ushort incomingCarId, bool incomingIsOnMainRoad, ushort nodeId) {
			try {
				var targetCar = Vehicles[targetCarId];
				var incomingCar = Vehicles[incomingCarId];

				//         TOP
				//          |
				//          |
				// LEFT --- + --- RIGHT
				//          |
				//          |
				//        BOTTOM

				// We assume the target car is coming from BOTTOM.

				Direction targetToDir = GetDirection(targetCar.FromSegment, targetCar.ToSegment, nodeId);
				Direction incomingRelDir = GetDirection(targetCar.FromSegment, incomingCar.FromSegment, nodeId);
				Direction incomingToDir = GetDirection(incomingCar.FromSegment, incomingCar.ToSegment, nodeId);

				if (LeftHandDrive) {
					// mirror situation for left-hand traffic systems
					targetToDir = InvertLeftRight(targetToDir);
					incomingRelDir = InvertLeftRight(incomingRelDir);
					incomingToDir = InvertLeftRight(incomingToDir);
				}

				bool sameTargets = false;
				bool laneOrderCorrect = false;
				if (targetCar.ToSegment == incomingCar.ToSegment) {
					// target and incoming are both going to same segment
					sameTargets = true;
					if (targetCar.ToLaneIndex == incomingCar.ToLaneIndex && targetCar.FromSegment != incomingCar.FromSegment)
						laneOrderCorrect = false;
					else {
						switch (targetToDir) {
							case Direction.Left:
								laneOrderCorrect = IsLaneOrderConflictFree(targetCar.ToSegment, targetCar.ToLaneIndex, incomingCar.ToLaneIndex); // stay left
								break;
							case Direction.Forward:
							default:
								switch (incomingRelDir) {
									case Direction.Left:
									case Direction.Forward:
										laneOrderCorrect = IsLaneOrderConflictFree(targetCar.ToSegment, incomingCar.ToLaneIndex, targetCar.ToLaneIndex); // stay right
										break;
									case Direction.Right:
										laneOrderCorrect = IsLaneOrderConflictFree(targetCar.ToSegment, targetCar.ToLaneIndex, incomingCar.ToLaneIndex); // stay left
										break;
									case Direction.Turn:
									default:
										laneOrderCorrect = true;
										break;
								}
								break;
							case Direction.Right:
								laneOrderCorrect = IsLaneOrderConflictFree(targetCar.ToSegment, incomingCar.ToLaneIndex, targetCar.ToLaneIndex); // stay right
								break;
						}
						laneOrderCorrect = IsLaneOrderConflictFree(targetCar.ToSegment, targetCar.ToLaneIndex, incomingCar.ToLaneIndex);
					}
				}

				if (sameTargets && laneOrderCorrect) {
					if (debug) {
						Log.Message($"Lane order between car {targetCarId} and {incomingCarId} is correct!");
					}
					return true;
				}

				bool incomingCrossingStreet = incomingToDir == Direction.Forward || incomingToDir == Direction.Left;

				switch (targetToDir) {
					case Direction.Right:
						// target: BOTTOM->RIGHT
						if (debug) {
							Log.Message($"Car {targetCarId} (vs. {incomingCar.FromSegment}->{incomingCar.ToSegment}) is going right without conflict!");
                        }
						return true;
					case Direction.Forward:
					default:
						if (debug) {
							Log.Message($"Car {targetCarId} (vs. {incomingCar.FromSegment}->{incomingCar.ToSegment}) is going forward: {incomingRelDir}, {targetIsOnMainRoad}, {incomingCrossingStreet}!");
						}
						// target: BOTTOM->TOP
						switch (incomingRelDir) {
							case Direction.Right:
							case Direction.Left:
								return targetIsOnMainRoad || !incomingCrossingStreet;
							case Direction.Forward:
							default:
								return true;
						}
					case Direction.Left:
						if (debug) {
							Log.Message($"Car {targetCarId} (vs. {incomingCar.FromSegment}->{incomingCar.ToSegment}) is going left: {incomingRelDir}, {targetIsOnMainRoad}, {incomingIsOnMainRoad}, {incomingCrossingStreet}, {incomingToDir}!");
						}
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

		internal static void OnLevelUnloading() {
			PrioritySegments.Clear();
			TrafficLightSimulation.LightSimulationByNodeId.Clear();
			Vehicles.Clear();
			priorityNodes.Clear();
		}

		public static bool IsLaneOrderConflictFree(ushort segmentId, uint leftLaneIndex, uint rightLaneIndex) {
			try {
				NetInfo segmentInfo = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
				NetInfo.Direction normDirection = TrafficPriority.LeftHandDrive ? NetInfo.Direction.Forward : NetInfo.Direction.Backward; // direction to normalize indices to
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

		/*public static bool LaneOrderCorrect(int segmentid, uint leftLane, uint rightLane) {
			if (leftLane == rightLane)
				return false;

			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentid];
			var info = segment.Info;

			var curLaneId = segment.m_lanes;
			var laneIndex = 0;

			var oneWaySegment = true;
			while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
				if (info.m_lanes[laneIndex].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[laneIndex].m_direction == NetInfo.Direction.Backward)) {
					oneWaySegment = false;
					break;
				}

				curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				laneIndex++;
			}

			laneIndex = 0;
			var leftLanePosition = 0f;
			var rightLanePosition = 0f;

			while (laneIndex < info.m_lanes.Length && curLaneId != 0u) {
				if (curLaneId == leftLane) {
					leftLanePosition = info.m_lanes[laneIndex].m_position;
				}

				if (curLaneId == rightLane) {
					rightLanePosition = info.m_lanes[laneIndex].m_position;
				}

				curLaneId = instance.m_lanes.m_buffer[(int)((UIntPtr)curLaneId)].m_nextLane;
				laneIndex++;
			}

			if (oneWaySegment) {
				if (leftLanePosition < rightLanePosition) {
					return true;
				}
			} else {
				if (leftLanePosition > rightLanePosition) {
					return true;
				}
			}

			return false;
		}*/

		/// <summary>
		/// Determines the direction vehicles are turning when changing from segment `fromSegment` to segment `toSegment` at node `nodeId`.
		/// </summary>
		/// <param name="fromSegment"></param>
		/// <param name="toSegment"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		public static Direction GetDirection(int fromSegment, int toSegment, ushort nodeId) {
			if (fromSegment == toSegment)
				return Direction.Turn;
			else if (IsRightSegment(fromSegment, toSegment, nodeId))
				return Direction.Right;
			else if (IsLeftSegment(fromSegment, toSegment, nodeId))
				return Direction.Left;
			else
				return Direction.Forward;
		}

		public static bool IsRightSegment(int fromSegment, int toSegment, ushort nodeid) {
			if (fromSegment <= 0 || toSegment <= 0)
				return false;

			return IsLeftSegment(toSegment, fromSegment, nodeid);
		}

		public static bool IsLeftSegment(int fromSegment, int toSegment, ushort nodeid) {
			if (fromSegment <= 0 || toSegment <= 0)
				return false;

			var fromDir = GetSegmentDir(fromSegment, nodeid);
			fromDir.y = 0;
			fromDir.Normalize();
			var toDir = GetSegmentDir(toSegment, nodeid);
			toDir.y = 0;
			toDir.Normalize();
			return Vector3.Cross(fromDir, toDir).y >= 0.5;
		}

		public static bool HasLeftSegment(int segmentId, ushort nodeId, bool debug = false) {
			var node = TrafficLightTool.GetNetNode(nodeId);

			for (var s = 0; s < 8; s++) {
				var segment = node.GetSegment(s);

				if (segment != 0 && segment != segmentId) {
					if (IsLeftSegment(segmentId, segment, nodeId)) {
						if (debug) {
							Log.Message("LEFT: " + segment + " " + GetSegmentDir(segment, nodeId));
						}
						return true;
					}
				}
			}

			return false;
		}

		public static bool HasRightSegment(int segmentId, ushort nodeId, bool debug = false) {
			if (segmentId <= 0)
				return false;

			var node = TrafficLightTool.GetNetNode(nodeId);

			for (var s = 0; s < 8; s++) {
				var segment = node.GetSegment(s);

				if (segment == 0 || segment == segmentId) continue;
				if (!IsRightSegment(segmentId, segment, nodeId)) continue;

				if (debug) {
					Log.Message("RIGHT: " + segment + " " + GetSegmentDir(segment, nodeId));
				}
				return true;
			}

			return false;
		}

		public static bool HasForwardSegment(int segmentId, ushort nodeId, bool debug = false) {
			if (segmentId <= 0)
				return false;

			var node = TrafficLightTool.GetNetNode(nodeId);

			for (var s = 0; s < 8; s++) {
				var segment = node.GetSegment(s);

				if (segment == 0 || segment == segmentId) continue;
				if (IsRightSegment(segmentId, segment, nodeId) || IsLeftSegment(segmentId, segment, nodeId)) continue;

				if (debug) {
					Log.Message("FORWARD: " + segment + " " + GetSegmentDir(segment, nodeId));
				}
				return true;
			}

			return false;
		}

		public static bool HasLeftLane(ushort nodeId, int segmentId) {
			var instance = Singleton<NetManager>.instance;
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
			var dir = NetInfo.Direction.Forward;
			if (segment.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

				if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.None) {
					return true;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			return false;
		}

		public static bool HasForwardLane(ushort nodeId, int segmentId) {
			var instance = Singleton<NetManager>.instance;
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
			var dir = NetInfo.Direction.Forward;
			if (segment.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

				if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Forward) {
					return true;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			return false;
		}

		public static bool HasRightLane(ushort nodeId, int segmentId) {
			var instance = Singleton<NetManager>.instance;
			var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
			var dir = NetInfo.Direction.Forward;
			if (segment.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				var flags = (NetLane.Flags)Singleton<NetManager>.instance.m_lanes.m_buffer[num2].m_flags;

				if (info.m_lanes[num3].m_direction == dir3 && (flags & NetLane.Flags.Left) != NetLane.Flags.Right) {
					return true;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			return false;
		}

		public static Vector3 GetSegmentDir(int segment, ushort nodeid) {
			var instance = Singleton<NetManager>.instance;

			Vector3 dir;

			dir = instance.m_segments.m_buffer[segment].m_startNode == nodeid ?
				instance.m_segments.m_buffer[segment].m_startDirection :
				instance.m_segments.m_buffer[segment].m_endDirection;

			return dir;
		}

		/// <summary>
		/// rebuilds the implicitly defined set of priority nodes (traffic light nodes & nodes with priority signs)
		/// </summary>
		private static void rebuildPriorityNodes() {
			priorityNodes.Clear();

			foreach (KeyValuePair<ushort, TrafficSegment> e in PrioritySegments) {
				if (e.Value.Node1 != 0)
					priorityNodes.Add(e.Value.Node1);
				if (e.Value.Node2 != 0)
					priorityNodes.Add(e.Value.Node2);
			}
		}

		internal static void fixJunctions() {
			for (ushort i = 0; i < Singleton<NetManager>.instance.m_nodes.m_size; ++i) {
				NetNode node = Singleton<NetManager>.instance.m_nodes.m_buffer[i];
				if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None)
					continue;
				if (node.CountSegments() > 2)
					Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags |= NetNode.Flags.Junction;
			}
		}

		public static void housekeeping(ushort nodeId) {
			try {
				uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
				checkMod = (checkMod + 1u) & segmentsCheckLoadBalanceMod[Options.simAccuracy];

				NetManager netManager = Singleton<NetManager>.instance;
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				Flags.applyNodeTrafficLightFlag(nodeId);

				// update lane arrows
				var node = netManager.m_nodes.m_buffer[nodeId];
				for (var s = 0; s < 8; s++) {
					var segmentId = node.GetSegment(s);
					if (segmentId <= 0)
						continue;
					NetSegment segment = netManager.m_segments.m_buffer[segmentId];

					uint laneId = segment.m_lanes;
					while (laneId != 0) {
						if (!Flags.applyLaneArrowFlags(laneId)) {
							Flags.removeLaneArrowFlags(laneId);
						}
						laneId = netManager.m_lanes.m_buffer[laneId].m_nextLane;
					}
				}

				// delete invalid segments & vehicles
				List<ushort> segmentIdsToDelete = new List<ushort>();
				HashSet<ushort> nodeIdsToCheck = new HashSet<ushort>(priorityNodes);
				if (IsPriorityNode(nodeId))
					nodeIdsToCheck.Add(nodeId);

				List<ushort> prioritySegmentIds = new List<ushort>(PrioritySegments.Keys);
				foreach (ushort segmentId in prioritySegmentIds) {
					if ((segmentId & checkMod) != checkMod)
						continue;

					if (segmentId <= 0) {
						segmentIdsToDelete.Add(segmentId);
						continue;
					}

					NetSegment segment = netManager.m_segments.m_buffer[segmentId];
					if (segment.m_flags == NetSegment.Flags.None) {
						segmentIdsToDelete.Add(segmentId);
						nodeIdsToCheck.Add(segment.m_startNode);
						nodeIdsToCheck.Add(segment.m_endNode);
						continue;
					}

					// segment is valid, check for invalid cars
					if (PrioritySegments[segmentId].Node1 != 0) {
						List<ushort> vehicleIdsToDelete = new List<ushort>();
						foreach (KeyValuePair<ushort, VehiclePosition> e in PrioritySegments[segmentId].Instance1.getCars()) {
							var vehicleId = e.Key;
							if (vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags == Vehicle.Flags.None) {
								vehicleIdsToDelete.Add(vehicleId);
							}
						}

						foreach (var vehicleId in vehicleIdsToDelete) {
							//Log.Warning("Housekeeping: Deleting vehicle " + vehicleId);
							PrioritySegments[segmentId].Instance1.RemoveCar(vehicleId);
							Vehicles.Remove(vehicleId);
						}
					}

					if (PrioritySegments[segmentId].Node2 != 0) {
						List<ushort> vehicleIdsToDelete = new List<ushort>();

						foreach (KeyValuePair<ushort, VehiclePosition> e in PrioritySegments[segmentId].Instance2.getCars()) {
							var vehicleId = e.Key;
							if (vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags == Vehicle.Flags.None) {
								vehicleIdsToDelete.Add(vehicleId);
							}
						}

						foreach (var vehicleId in vehicleIdsToDelete) {
							//Log.Warning("Housekeeping: Deleting vehicle " + vehicleId);
							PrioritySegments[segmentId].Instance2.RemoveCar(vehicleId);
							Vehicles.Remove(vehicleId);
						}
					}
				}

				foreach (var sId in segmentIdsToDelete) {
					Log.Warning("Housekeeping: Deleting segment " + sId);
					PrioritySegments.Remove(sId);
					TrafficLightsManual.RemoveSegmentLight(sId);
				}

				// validate nodes & delete invalid nodes
				foreach (ushort nId in nodeIdsToCheck) {
					NodeValidityState nodeState = NodeValidityState.Valid;
					if (!isValidPriorityNode(nId, out nodeState)) {
						if (nodeState != NodeValidityState.SimWithoutLight) {
							Log.Warning("Housekeeping: Deleting node " + nId);
							RemovePrioritySegments(nId);
						}

						switch (nodeState) {
							case NodeValidityState.SimWithoutLight:
								Log.Warning("Housekeeping: Re-adding traffic light at node " + nId);
								Flags.setNodeTrafficLight(nId, true);
								break;
							case NodeValidityState.Unused:
								// delete traffic light simulation
								Log.Warning("Housekeeping: RemoveNodeFromSimulation " + nId);
								TrafficLightSimulation.RemoveNodeFromSimulation(nId, false);
								break;
							default:
								break;
						}
					}
				}

				// add newly created segments to timed traffic lights
				if (TrafficLightsTimed.TimedScripts.ContainsKey(nodeId)) {
					TrafficLightsTimed.TimedScripts[nodeId].handleNewSegments();
				}
			} catch (Exception e) {
				Log.Warning($"Housekeeping failed: {e.Message}");
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

			Flags.applyNodeTrafficLightFlag(nodeId);
			var node = netManager.m_nodes.m_buffer[nodeId];
			if ((node.m_flags & NetNode.Flags.Created) == NetNode.Flags.None) {
				nodeState = NodeValidityState.Unused;
				Log.Warning($"Housekeeping: Node {nodeId} is unused!");
				return false; // node is unused
			}

			bool hasTrafficLight = (node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
			var nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim != null) {
				if (!hasTrafficLight) {
					// traffic light simulation is active but node does not have a traffic light
					nodeState = NodeValidityState.SimWithoutLight;
					Log.Warning($"Housekeeping: Node {nodeId} has traffic light simulation but no traffic light!");
					return false;
				} else {
					// check if all timed step segments are valid
					if (nodeSim.TimedTrafficLights && nodeSim.TimedTrafficLightsActive) {
						TrafficLightsTimed timedLight = TrafficLightsTimed.GetTimedLight(nodeId);
						if (timedLight == null || timedLight.Steps.Count <= 0) {
							Log.Warning("Housekeeping: Timed light is null or no steps for node {nodeId}!");
							TrafficLightSimulation.RemoveNodeFromSimulation(nodeId, false);
							return false;
						}

						/*foreach (var segmentId in timedLight.Steps[0].segmentIds) {
							if (! IsPrioritySegment(nodeId, segmentId)) {
								Log.Warning("Housekeeping: Timed light - Priority segment has gone away!");
								RemoveNodeFromSimulation(nodeId);
								return false;
							}
						}*/
					}
					return true;
				}
			} else {
				byte numSegmentsWithSigns = 0;
				for (var s = 0; s < 8; s++) {
					var segmentId = node.GetSegment(s);
					if (segmentId <= 0)
						continue;
					NetSegment segment = netManager.m_segments.m_buffer[segmentId];
					if (segment.m_startNode != nodeId && segment.m_endNode != nodeId)
						continue;

					PrioritySegment prioritySegment = GetPrioritySegment(nodeId, segmentId);
					if (prioritySegment == null) {
						continue;
					}

					// if node is a traffic light, it must not have priority signs
					if (hasTrafficLight && prioritySegment.Type != PrioritySegment.PriorityType.None) {
						Log.Warning($"Housekeeping: Node {nodeId}, Segment {segmentId} is a priority sign but node has a traffic light!");
						prioritySegment.Type = PrioritySegment.PriorityType.None;
					}

					// if a priority sign is set, everything is ok
					if (prioritySegment.Type != PrioritySegment.PriorityType.None) {
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
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.AI;
using UnityEngine;

namespace TrafficManager.Traffic {
	class TrafficPriority {
		public enum Direction {
			Left,
			Forward,
			Right
		}

		private static uint[] loadBalanceMod = new uint[] {0, 127, 511, 1023, 2047};

		public static bool LeftHandDrive;

		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		public static Dictionary<ushort, TrafficLightSimulation> LightSimByNodeId = new Dictionary<ushort, TrafficLightSimulation>();

		public static Dictionary<ushort, TrafficSegment> PrioritySegments = new Dictionary<ushort, TrafficSegment>();

		public static Dictionary<ushort, PriorityCar> VehicleList = new Dictionary<ushort, PriorityCar>();

		private static HashSet<ushort> priorityNodes = new HashSet<ushort>();

		public static void AddPrioritySegment(ushort nodeId, ushort segmentId, PrioritySegment.PriorityType type) {
			if (nodeId <= 0 || segmentId <= 0)
				return;

			Log.Message("adding PrioritySegment @ node " + nodeId + ", seg. " + segmentId + ", type " + type);
			if (PrioritySegments.ContainsKey(segmentId)) {
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

				if (PrioritySegments.ContainsKey(segmentId)) {
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
				} else {
					//Log.Message("Housekeeping: node " + nodeId + " contains NO prio seg. " + segmentId);
				}
			}
			priorityNodes.Remove(nodeId);
		}

		public static bool IsPrioritySegment(ushort nodeId, ushort segmentId) {
			if (nodeId <= 0 || segmentId <= 0)
				return false;

			if (PrioritySegments.ContainsKey(segmentId)) {
				var prioritySegment = PrioritySegments[segmentId];

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
			if (! IsPrioritySegment(nodeId, segmentId)) return null;

			var prioritySegment = PrioritySegments[segmentId];

			if (prioritySegment.Node1 == nodeId) {
				return prioritySegment.Instance1;
			}

			return prioritySegment.Node2 == nodeId ?
				prioritySegment.Instance2 : null;
		}

		public static bool HasIncomingVehicles(ushort targetCar, ushort nodeId) {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			var frame = currentFrameIndex >> 4;
			var node = TrafficLightTool.GetNetNode(nodeId);

			var fromPrioritySegment = GetPrioritySegment(nodeId, VehicleList[targetCar].FromSegment);
			if (fromPrioritySegment == null)
				return false;

			var numCars = 0;

			bool debug = nodeId == 32538;

			// get all cars
			for (var s = 0; s < 8; s++) {
				var segment = node.GetSegment(s);

				if (segment == 0 || segment == VehicleList[targetCar].FromSegment) continue;
				if (!IsPrioritySegment(nodeId, segment)) continue;

				var toPrioritySegment = GetPrioritySegment(nodeId, segment);
				if (toPrioritySegment == null)
					continue; // should not happen

				if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					if (fromPrioritySegment.Type == PrioritySegment.PriorityType.Main) {
						if (toPrioritySegment.Type != PrioritySegment.PriorityType.Main) continue;
						// Main - Main

						numCars += toPrioritySegment.NumCars;

						foreach (KeyValuePair<ushort, float> e in toPrioritySegment.Cars) {
							var car = e.Key;
							if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].m_frame0.m_velocity.magnitude > 0.1f) {
								if (CheckSameRoadIncomingCar(targetCar, car, nodeId))
									--numCars;
								else {
									if (debug) {
										Log.Message($"Vehicle {targetCar} on segment {VehicleList[targetCar].FromSegment} has to wait for vehicle {car} on segment {VehicleList[car].FromSegment}");
									}
									return true;
								}
							} else {
								numCars--;
							}
						}
					} else {
						// Main - Yield/Stop
						numCars += toPrioritySegment.NumCars;

						foreach (KeyValuePair<ushort, float> e in toPrioritySegment.Cars) {
							var car = e.Key;
							if (toPrioritySegment.Type == PrioritySegment.PriorityType.Main) {
								if (!VehicleList[car].Stopped && Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].m_frame0.m_velocity.magnitude > 0.1f) {
									if (CheckPriorityRoadIncomingCar(targetCar, car, nodeId))
										--numCars;
									else
										return true;
								} else {
									numCars--;
								}
							} else {
								if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].m_frame0.m_velocity.magnitude > 0.1f) {
									if (CheckSameRoadIncomingCar(targetCar, car, nodeId))
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
					// Yield/Stop - Main/Yield/Stop
					if (!TrafficLightsManual.IsSegmentLight(nodeId, segment)) continue;

					var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segment);

					if (segmentLight.GetLightMain() != RoadBaseAI.TrafficLightState.Green) continue;

					numCars += toPrioritySegment.NumCars;

					foreach (KeyValuePair<ushort, float> e in toPrioritySegment.Cars) {
						var car = e.Key;
						if (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[car].m_frame0.m_velocity.magnitude > 1f) {
							if (CheckSameRoadIncomingCar(targetCar, car, nodeId))
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

		public static bool CheckSameRoadIncomingCar(ushort targetCarId, ushort incomingCarId, ushort nodeId) {
			return LeftHandDrive ? _checkSameRoadIncomingCarLeftHandDrive(targetCarId, incomingCarId, nodeId) :
				_checkSameRoadIncomingCarRightHandDrive(targetCarId, incomingCarId, nodeId);
		}

		protected static bool _checkSameRoadIncomingCarLeftHandDrive(ushort targetCarId, ushort incomingCarId,
			ushort nodeId) {
			var targetCar = VehicleList[targetCarId];
			var incomingCar = VehicleList[incomingCarId];

			if (IsRightSegment(targetCar.FromSegment, incomingCar.FromSegment, nodeId)) {
				if (IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId)) {
					if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) {
						return true;
					}
				} else if (targetCar.ToSegment == incomingCar.ToSegment && targetCar.ToLaneId != incomingCar.ToLaneId) {
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
				}
			} else if (IsLeftSegment(targetCar.FromSegment, incomingCar.FromSegment, nodeId))
			  // incoming is on the left
			  {
				return true;
			} else // incoming is in front or elsewhere
			  {
				if (!IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car not going left
				{
					return true;
				}
				if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) {
					if (targetCar.ToLaneId != incomingCar.ToLaneId) {
						return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
					}
				} else if (IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId) && IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // both left turns
				  {
					return true;
				}
			}

			return false;
		}

		protected static bool _checkSameRoadIncomingCarRightHandDrive(ushort targetCarId, ushort incomingCarId,
			ushort nodeId) {
			var targetCar = VehicleList[targetCarId];
			var incomingCar = VehicleList[incomingCarId];

			if (IsRightSegment(targetCar.FromSegment, incomingCar.FromSegment, nodeId)) {
				if (IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId)) {
					return true;
				}
				if (targetCar.ToSegment == incomingCar.ToSegment && targetCar.ToLaneId != incomingCar.ToLaneId) {
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
				}
			} else if (IsLeftSegment(targetCar.FromSegment, incomingCar.FromSegment, nodeId))
			  // incoming is on the left
			  {
				return true;
			} else // incoming is in front or elsewhere
			  {
				if (!IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car not going left
				{
					return true;
				}
				if (IsRightSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) {
					if (targetCar.ToLaneId != incomingCar.ToLaneId) {
						return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
					}
				} else if (IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId) && IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // both left turns
				  {
					return true;
				}
			}

			return false;
		}


		public static bool CheckPriorityRoadIncomingCar(ushort targetCarId, ushort incomingCarId, ushort nodeId) {
			if (LeftHandDrive) {
				return _checkPriorityRoadIncomingCarLeftHandDrive(targetCarId, incomingCarId, nodeId);
			}
			return _checkPriorityRoadIncomingCarRightHandDrive(targetCarId, incomingCarId, nodeId);
		}

		protected static bool _checkPriorityRoadIncomingCarLeftHandDrive(ushort targetCarId, ushort incomingCarId,
			ushort nodeId) {
			var targetCar = VehicleList[targetCarId];
			var incomingCar = VehicleList[incomingCarId];

			if (incomingCar.ToSegment == targetCar.ToSegment) {
				if (incomingCar.ToLaneId == targetCar.ToLaneId) return false;

				if (IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car goes right
				{
					// go if incoming car is in the left lane
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
				}
				if (IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car goes left
				{
					// go if incoming car is in the right lane
					return LaneOrderCorrect(targetCar.ToSegment, incomingCar.ToLaneId, targetCar.ToLaneId);
				}
				if (IsRightSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car goes right
				{
					// go if incoming car is in the left lane
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, targetCar.ToLaneId);
				}
				if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car goes left
				{
					// go if incoming car is in the right lane
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId,
						incomingCar.ToLaneId);
				}
			} else if (incomingCar.ToSegment == targetCar.FromSegment) {
				if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) {
					return true;
				}
				if (targetCar.ToSegment == incomingCar.FromSegment) {
					return true;
				}
			} else // if no segment match
			  {
				// target car turning right
				if (IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId)) {
					return true;
				}
				if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car turning right
				{
					return true;
				}
			}

			return false;
		}

		internal static void OnLevelUnloading() {
			PrioritySegments.Clear();
			LightSimByNodeId.Clear();
			VehicleList.Clear();
			priorityNodes.Clear();
		}

		protected static bool _checkPriorityRoadIncomingCarRightHandDrive(ushort targetCarId, ushort incomingCarId,
			ushort nodeId) {
			var targetCar = VehicleList[targetCarId];
			var incomingCar = VehicleList[incomingCarId];

			if (incomingCar.ToSegment == targetCar.ToSegment) {
				if (incomingCar.ToLaneId == targetCar.ToLaneId) return false;

				if (IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car goes right
				{
					// go if incoming car is in the left lane
					return LaneOrderCorrect(targetCar.ToSegment, incomingCar.ToLaneId, targetCar.ToLaneId);
				}
				if (IsLeftSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId))
				// target car goes left
				{
					// go if incoming car is in the right lane
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
				}
				if (IsRightSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car goes right
				{
					// go if incoming car is in the left lane
					return LaneOrderCorrect(targetCar.ToSegment, targetCar.ToLaneId, incomingCar.ToLaneId);
				}
				if (IsLeftSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car goes left
				{
					// go if incoming car is in the right lane
					return LaneOrderCorrect(targetCar.ToSegment, incomingCar.ToLaneId,
						targetCar.ToLaneId);
				}
			} else if (incomingCar.ToSegment == targetCar.FromSegment) {
				if (IsRightSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) {
					return true;
				}
				if (targetCar.ToSegment == incomingCar.FromSegment) {
					return true;
				}
			} else // if no segment match
			  {
				// target car turning right
				if (IsRightSegment(targetCar.FromSegment, targetCar.ToSegment, nodeId)) {
					return true;
				}
				if (IsRightSegment(incomingCar.FromSegment, incomingCar.ToSegment, nodeId)) // incoming car turning right
				{
					return true;
				}
			}

			return false;
		}

		public static bool LaneOrderCorrect(int segmentid, uint leftLane, uint rightLane) {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentid];
			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			var oneWaySegment = true;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[num3].m_direction == NetInfo.Direction.Backward)) {
					oneWaySegment = false;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			num3 = 0;
			var leftLanePosition = 0f;
			var rightLanePosition = 0f;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				if (num2 == leftLane) {
					leftLanePosition = info.m_lanes[num3].m_position;
				}

				if (num2 == rightLane) {
					rightLanePosition = info.m_lanes[num3].m_position;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
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
		}

		/// <summary>
		/// Determines the direction vehicles are turning when changing from segment `fromSegment` to segment `toSegment` at node `nodeId`.
		/// </summary>
		/// <param name="fromSegment"></param>
		/// <param name="toSegment"></param>
		/// <param name="nodeId"></param>
		/// <returns></returns>
		public static Direction GetDirection(int fromSegment, int toSegment, ushort nodeId) {
			if (IsRightSegment(fromSegment, toSegment, nodeId))
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

		public static void housekeeping() {
			try {
				uint frameMod = Singleton<SimulationManager>.instance.m_currentFrameIndex & loadBalanceMod[Options.simAccuracy]; // load balancing: we don't do everything on every frame

				NetManager netManager = Singleton<NetManager>.instance;
				VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

				// delete invalid segments & vehicles
				List<ushort> segmentIdsToDelete = new List<ushort>();
				foreach (KeyValuePair<ushort, TrafficSegment> e in PrioritySegments) {
					var segmentId = e.Key;
					if (segmentId <= 0) {
						segmentIdsToDelete.Add(segmentId);
						continue;
					}

					NetSegment segment = netManager.m_segments.m_buffer[segmentId];
					if (segment.m_flags == NetSegment.Flags.None || (!priorityNodes.Contains(segment.m_startNode) && !priorityNodes.Contains(segment.m_endNode))) {
						segmentIdsToDelete.Add(segmentId);
						continue;
					}

					if ((segmentId & loadBalanceMod[Options.simAccuracy]) == frameMod) {
						//Log.Warning("Housekeeping cars of segment " + segmentId);

						// segment is valid, check for invalid cars
						if (e.Value.Instance1 != null) {
							List<ushort> vehicleIdsToDelete = new List<ushort>();
							foreach (KeyValuePair<ushort, float> e2 in e.Value.Instance1.Cars) {
								var vehicleId = e2.Key;
								if (vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags == Vehicle.Flags.None) {
									vehicleIdsToDelete.Add(vehicleId);
								}
							}

							foreach (var vehicleId in vehicleIdsToDelete) {
								Log.Warning("Housekeeping: Deleting vehicle " + vehicleId);
								e.Value.Instance1.RemoveCar(vehicleId);
								VehicleList.Remove(vehicleId);
							}
						}

						if (e.Value.Instance2 != null) {
							List<ushort> vehicleIdsToDelete = new List<ushort>();
							foreach (KeyValuePair<ushort, float> e2 in e.Value.Instance2.Cars) {
								var vehicleId = e2.Key;
								if (vehicleManager.m_vehicles.m_buffer[vehicleId].m_flags == Vehicle.Flags.None)
									vehicleIdsToDelete.Add(vehicleId);
							}

							foreach (var vehicleId in vehicleIdsToDelete) {
								Log.Warning("Housekeeping: Deleting vehicle " + vehicleId);
								e.Value.Instance2.RemoveCar(vehicleId);
								VehicleList.Remove(vehicleId);
							}
						}
					}
				}

				foreach (var sId in segmentIdsToDelete) {
					Log.Warning("Housekeeping: Deleting segment " + sId);
					PrioritySegments.Remove(sId);
					TrafficLightsManual.RemoveSegmentLight(sId);
				}

				// delete invalid nodes
				List<ushort> nodeIdsToDelete = new List<ushort>();
				foreach (ushort nodeId in priorityNodes) {
					NodeValidityState nodeState = NodeValidityState.Valid;
					if (!isValidPriorityNode(nodeId, out nodeState)) {
						if (nodeState != NodeValidityState.SimWithoutLight)
							nodeIdsToDelete.Add(nodeId);

						switch (nodeState) {
							case NodeValidityState.SimWithoutLight:
								Log.Warning("Housekeeping: Re-adding traffic light at node " + nodeId);
								netManager.m_nodes.m_buffer[nodeId].m_flags |= NetNode.Flags.TrafficLights;
								break;
							case NodeValidityState.Unused:
								// delete traffic light simulation
								Log.Warning("Housekeeping: RemoveNodeFromSimulation " + nodeId);
								RemoveNodeFromSimulation(nodeId);
								break;
							default:
								break;
						}
					}
				}

				foreach (var nId in nodeIdsToDelete) {
					Log.Warning("Housekeeping: Deleting node " + nId);
					RemovePrioritySegments(nId);
				}

				// add newly created segments to timed traffic lights
				foreach (KeyValuePair<ushort, TrafficLightsTimed> e in TrafficLightsTimed.TimedScripts) {
					TrafficLightsTimed timedLights = e.Value;
					ushort nodeId = e.Key;

					timedLights.handleNewSegments();
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
				return false;
			}

			var node = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
			if (node.m_flags == NetNode.Flags.None) {
				nodeState = NodeValidityState.Unused;
				return false; // node is unused
			}

			var nodeSim = GetNodeSimulation(nodeId);
			if (nodeSim != null) {
				if ((node.m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
					// traffic light simulation is active but node does not have a traffic light
					nodeState = NodeValidityState.SimWithoutLight;
					return false;
				} else {
					// check if all timed step segments are valid
					if (nodeSim.FlagTimedTrafficLights && nodeSim.TimedTrafficLightsActive) {
						TrafficLightsTimed timedLight = TrafficLightsTimed.GetTimedLight(nodeId);
						if (timedLight == null || timedLight.Steps.Count <= 0) {
							Log.Warning("Housekeeping: Timed light is null or no steps!");
							RemoveNodeFromSimulation(nodeId);
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
					NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
					if (segment.m_startNode != nodeId && segment.m_endNode != nodeId)
						continue;
					PrioritySegment prioritySegment = GetPrioritySegment(nodeId, segmentId);
					if (prioritySegment == null) {
						continue;
					}

					// if node is a traffic light, it must not have priority signs
					if ((node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
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

				bool ok = numSegmentsWithSigns > 1;
				if (! ok) {
					nodeState = NodeValidityState.NoValidSegments;
				}
				return ok;
			}
		}

		private static readonly object simLock = new object(); // TODO rework

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public static void AddNodeToSimulation(ushort nodeId) {
			LightSimByNodeId.Add(nodeId, new TrafficLightSimulation(nodeId));
		}

		public static void RemoveNodeFromSimulation(ushort nodeId) {
			//lock (simLock) {
			if (!LightSimByNodeId.ContainsKey(nodeId))
				return;
			var nodeSim = LightSimByNodeId[nodeId];
			var isTimedLight = nodeSim.TimedTrafficLights;
			TrafficLightsTimed timedLights = null;
			if (isTimedLight)
				timedLights = TrafficLightsTimed.GetTimedLight(nodeId);
			nodeSim.Destroy();
			if (isTimedLight && timedLights != null) {
				foreach (ushort otherNodeId in timedLights.NodeGroup) {
					Log.Message($"Removing simulation @ node {otherNodeId} (group)");
					LightSimByNodeId.Remove(otherNodeId);
				}
			}
			LightSimByNodeId.Remove(nodeId);
			//}
		}

		public static TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
			//lock (simLock) {
				if (LightSimByNodeId.ContainsKey(nodeId)) {
					return LightSimByNodeId[nodeId];
				}

				return null;
			//}
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
	}
}

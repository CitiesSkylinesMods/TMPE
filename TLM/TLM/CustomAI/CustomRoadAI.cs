using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.CustomAI {
	class CustomRoadAI : RoadBaseAI {
		public static Dictionary<ushort, TrafficLightSimulation> NodeDictionary = new Dictionary<ushort, TrafficLightSimulation>();

		private uint _lastFrame;

		public void Awake() {

		}

		public void Update() {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;

			if (_lastFrame < currentFrameIndex) {
				_lastFrame = currentFrameIndex;

				var clearedNodes = new List<ushort>();

				foreach (var nodeId in NodeDictionary.Keys) {
					var nodeData = TrafficLightTool.GetNetNode(nodeId);
					if (nodeData.m_flags == NetNode.Flags.None) {
						// node does not exist anymore
						clearedNodes.Add(nodeId);
						continue;
					}

					var hasTrafficLight = (nodeData.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
					if (!hasTrafficLight && (NodeDictionary[nodeId].FlagTimedTrafficLights || NodeDictionary[nodeId].FlagManualTrafficLights)) {
						// traffic light simulation exists but node does not have a traffic light
						clearedNodes.Add(nodeId);
						continue;
					}

					try {
						for (var i = 0; i < 8; i++) {
							var sgmid = nodeData.GetSegment(i);

							if (sgmid <= 0)
								continue;

							if (!TrafficLightsManual.IsSegmentLight(nodeId, sgmid)) {
								if (NodeDictionary[nodeId].FlagTimedTrafficLights) {
									var timedNode = TrafficLightsTimed.GetTimedLight(nodeId);

									if (timedNode != null)
										foreach (var timedNodeItem in timedNode.NodeGroup) {
											var nodeSim = GetNodeSimulation(timedNodeItem);

											nodeSim.TimedTrafficLightsActive = false;

											clearedNodes.Add(timedNodeItem);
											TrafficLightsTimed.RemoveTimedLight(timedNodeItem);
										}
								}
							}
						}
					} catch (Exception) {
					//Log.Warning("Error on Update: \n" + e.Message + "\n\nStacktrace:\n\n" + e.StackTrace);
				}
			}

			if (clearedNodes.Count > 0) {
				foreach (var clearedNode in clearedNodes) {
					RemoveNodeFromSimulation(clearedNode);
				}
			}

			foreach (var nodeId in NodeDictionary.Keys) {
				var node = GetNodeSimulation(nodeId);

				if (node.FlagManualTrafficLights || (node.FlagTimedTrafficLights && node.TimedTrafficLightsActive)) {
					var data = TrafficLightTool.GetNetNode(nodeId);

					node.SimulationStep(ref data);
					TrafficLightTool.SetNetNode(nodeId, data);

					if (clearedNodes.Count > 0) {
						break;
					}
				}
			}
		}
	}

	public void CustomSimulationStep(ushort nodeId, ref NetNode data) {
		var node = GetNodeSimulation(nodeId);

		if (node == null || (node.FlagTimedTrafficLights && !node.TimedTrafficLightsActive)) {
			OriginalSimulationStep(nodeId, ref data);
		}
	}

	public void OriginalSimulationStep(ushort nodeId, ref NetNode data) {
		var instance = Singleton<NetManager>.instance;
		if ((data.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			var num = data.m_maxWaitTime & 3;
			var num2 = data.m_maxWaitTime >> 2 & 7;
			var num3 = data.m_maxWaitTime >> 5;
			var num4 = -1;
			var num5 = -1;
			var num6 = -1;
			var num7 = -1;
			var num8 = -1;
			var num9 = -1;
			var num10 = 0;
			var num11 = 0;
			var num12 = 0;
			var num13 = 0;
			var num14 = 0;
			for (var i = 0; i < 8; i++) {
				var segment = data.GetSegment(i);
				if (segment != 0) {
					var num15 = 0;
					var num16 = 0;
					instance.m_segments.m_buffer[segment].CountLanes(segment, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, ref num15, ref num16);
					var flag = instance.m_segments.m_buffer[segment].m_startNode == nodeId;
					var flag2 = (!flag) ? (num15 != 0) : (num16 != 0);
					var flag3 = (!flag) ? (num16 != 0) : (num15 != 0);
					if (flag2) {
						num10 |= 1 << i;
					}
					if (flag3) {
						num11 |= 1 << i;
						num13++;
					}
					TrafficLightState trafficLightState;
					TrafficLightState trafficLightState2;
					bool flag4;
					bool flag5;
					GetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segment], currentFrameIndex - 256u, out trafficLightState, out trafficLightState2, out flag4, out flag5);
					if ((trafficLightState2 & TrafficLightState.Red) != TrafficLightState.Green && flag5) {
						if (num7 == -1) {
							num7 = i;
						}
						if (num9 == -1 && num14 >= num3) {
							num9 = i;
						}
					}
					num14++;
					if (flag2 || flag4) {
						if ((trafficLightState & TrafficLightState.Red) == TrafficLightState.Green) {
							num5 = i;
							if (flag4) {
								num4 = i;
							}
						} else if (flag4) {
							if (num6 == -1) {
								num6 = i;
							}
							if (num8 == -1 && num12 >= num2) {
								num8 = i;
							}
						}
						num12++;
					}
				}
			}
			if (num8 == -1) {
				num8 = num6;
			}
			if (num9 == -1) {
				num9 = num7;
			}
			if (num5 != -1 && num4 != -1 && num <= 1) {
				num8 = -1;
				num9 = -1;
				num++;
			}
			if (num9 != -1 && num8 != -1 && Singleton<SimulationManager>.instance.m_randomizer.Int32(3u) != 0) {
				num9 = -1;
			}
			if (num8 != -1) {
				num5 = num8;
			}
			if (num9 == num5) {
				num5 = -1;
			}
			var vector = Vector3.zero;
			if (num9 != -1) {
				ushort segment2 = data.GetSegment(num9);
				vector = instance.m_segments.m_buffer[segment2].GetDirection(nodeId);
				if (num5 != -1) {
					segment2 = data.GetSegment(num5);
					var direction = instance.m_segments.m_buffer[segment2].GetDirection(nodeId);
					if (direction.x * vector.x + direction.z * vector.z < -0.5f) {
						num5 = -1;
					}
				}
				if (num5 == -1) {
					for (int j = 0; j < 8; j++) {
						if (j != num9 && (num10 & 1 << j) != 0) {
							segment2 = data.GetSegment(j);
							if (segment2 != 0) {
								var direction2 = instance.m_segments.m_buffer[segment2].GetDirection(nodeId);
								if (direction2.x * vector.x + direction2.z * vector.z >= -0.5f) {
									num5 = j;
									break;
								}
							}
						}
					}
				}
			}
			var num17 = -1;
			var vector2 = Vector3.zero;
			var vector3 = Vector3.zero;
			if (num5 != -1) {
				var segment3 = data.GetSegment(num5);
				vector2 = instance.m_segments.m_buffer[segment3].GetDirection(nodeId);
				if ((num10 & num11 & 1 << num5) != 0) {
					for (var k = 0; k < 8; k++) {
						if (k != num5 && k != num9 && (num10 & num11 & 1 << k) != 0) {
							segment3 = data.GetSegment(k);
							if (segment3 != 0) {
								vector3 = instance.m_segments.m_buffer[segment3].GetDirection(nodeId);
								if (num9 == -1 || vector3.x * vector.x + vector3.z * vector.z >= -0.5f) {
									if (num13 == 2) {
										num17 = k;
										break;
									}
									if (vector3.x * vector2.x + vector3.z * vector2.z < -0.9396926f) {
										num17 = k;
										break;
									}
								}
							}
						}
					}
				}
			}
			for (int l = 0; l < 8; l++) {
				ushort segment4 = data.GetSegment(l);
				if (segment4 != 0) {
					TrafficLightState trafficLightState3;
					TrafficLightState trafficLightState4;
					GetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segment4], currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4);
					trafficLightState3 &= ~TrafficLightState.RedToGreen;
					trafficLightState4 &= ~TrafficLightState.RedToGreen;
					if (num5 == l || num17 == l) {
						if ((trafficLightState3 & TrafficLightState.Red) != TrafficLightState.Green) {
							trafficLightState3 = TrafficLightState.RedToGreen;
							num = 0;
							if (++num2 >= num12) {
								num2 = 0;
							}
						}
						if ((trafficLightState4 & TrafficLightState.Red) == TrafficLightState.Green) {
							trafficLightState4 = TrafficLightState.GreenToRed;
						}
					} else {
						if ((trafficLightState3 & TrafficLightState.Red) == TrafficLightState.Green) {
							trafficLightState3 = TrafficLightState.GreenToRed;
						}
						var direction3 = instance.m_segments.m_buffer[segment4].GetDirection(nodeId);
						if ((num11 & 1 << l) != 0 && num9 != l && ((num5 != -1 && direction3.x * vector2.x + direction3.z * vector2.z < -0.5f) || (num17 != -1 && direction3.x * vector3.x + direction3.z * vector3.z < -0.5f))) {
							if ((trafficLightState4 & TrafficLightState.Red) == TrafficLightState.Green) {
								trafficLightState4 = TrafficLightState.GreenToRed;
							}
						} else if ((trafficLightState4 & TrafficLightState.Red) != TrafficLightState.Green) {
							trafficLightState4 = TrafficLightState.RedToGreen;
							if (++num3 >= num14) {
								num3 = 0;
							}
						}
					}
					SetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segment4], currentFrameIndex, trafficLightState3, trafficLightState4, false, false);
				}
			}
			data.m_maxWaitTime = (byte)(num3 << 5 | num2 << 2 | num);
		}
		int num18 = 0;
		if (m_noiseAccumulation != 0) {
			var num19 = 0;
			for (var m = 0; m < 8; m++) {
				var segment5 = data.GetSegment(m);
				if (segment5 != 0) {
					num18 += instance.m_segments.m_buffer[segment5].m_trafficDensity;
					num19++;
				}
			}
			if (num19 != 0) {
				num18 /= num19;
			}
		}
		int num20 = 100 - (num18 - 100) * (num18 - 100) / 100;
		int num21 = m_noiseAccumulation * num20 / 100;
		if (num21 != 0) {
			Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num21, data.m_position, m_noiseRadius);
		}
		if ((data.m_problems & Notification.Problem.RoadNotConnected) != Notification.Problem.None && (data.m_flags & NetNode.Flags.Original) != NetNode.Flags.None) {
			var properties = Singleton<GuideManager>.instance.m_properties;
			if (properties != null) {
				instance.m_outsideNodeNotConnected.Activate(properties.m_outsideNotConnected, nodeId, Notification.Problem.RoadNotConnected);
			}
		}
	}

	public static void AddNodeToSimulation(ushort nodeId) {
		NodeDictionary.Add(nodeId, new TrafficLightSimulation(nodeId));
	}

	public static void RemoveNodeFromSimulation(ushort nodeId) {
		NodeDictionary.Remove(nodeId);
	}

	public static TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
		if (NodeDictionary.ContainsKey(nodeId)) {
			return NodeDictionary[nodeId];
		}

		return null;
	}
}
}

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Traffic;
using UnityEngine;
using ColossalFramework.Math;
using System.Threading;
using TrafficManager.UI;
using TrafficManager.State;

namespace TrafficManager.Custom.AI {
	class CustomRoadAI : RoadBaseAI {
		private static ushort[] nodeHousekeepingMask = { 3, 7, 15, 31, 63 };
		private static ushort[] segmentHousekeepingMask = { 15, 31, 63, 127, 255 };

		private static SegmentGeometry[] segmentGeometries;
		public static ushort[] currentLaneTrafficBuffer;
		public static uint[] currentLaneSpeeds;
		public static uint[] currentLaneDensities;

		public static byte[] laneMeanSpeeds;
		//public static byte[] laneMeanDensities;

		public static bool initDone = false;
		public static uint simStartFrame = 0;

		public static bool InStartupPhase = true;

		public void Awake() {

		}

		// this implements the Update method of MonoBehaviour
		public void Update() {
			
		}

		public void CustomNodeSimulationStep(ushort nodeId, ref NetNode data) {
			if (simStartFrame == 0)
				simStartFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			try {
				if (TrafficLightTool.getToolMode() != ToolMode.AddPrioritySigns) {
					try {
						TrafficPriority.nodeHousekeeping(nodeId);
					} catch (Exception e) {
						Log.Error($"Error occured while housekeeping node {nodeId}: " + e.ToString());
					}
				}

				TrafficPriority.TrafficLightSimulationStep();

				var nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (nodeSim == null || !nodeSim.IsSimulationActive()) {
					OriginalSimulationStep(nodeId, ref data);
				}
			} catch (Exception e) {
				Log.Warning($"CustomNodeSimulationStep: An error occurred: {e.ToString()}");
			}
		}

		public void CustomSegmentSimulationStep(ushort segmentID, ref NetSegment data) {
			if (initDone) {
				try {
					TrafficPriority.segmentHousekeeping(segmentID);
				} catch (Exception e) {
					Log.Error($"Error occured while housekeeping segment {segmentID}: " + e.ToString());
				}

				if (!Options.isStockLaneChangerUsed()) {
					try {
						InStartupPhase = simStartFrame == 0 || simStartFrame >> 14 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 14; // approx. 3 minutes

						// calculate traffic density
						uint curLaneId = data.m_lanes;
						int nextNumLanes = data.Info.m_lanes.Length;
						uint laneIndex = 0;
						bool firstWithTraffic = true;
						bool resetDensity = false;
						while (laneIndex < nextNumLanes && curLaneId != 0u) {
							uint buf = currentLaneTrafficBuffer[curLaneId];
							uint currentDensities = currentLaneDensities[curLaneId];

							//currentMeanDensity = (byte)Math.Min(100u, (uint)((currentDensities * 100u) / Math.Max(1u, maxDens))); // 0 .. 100

							byte currentMeanSpeed = 25;
							// we use integer division here because it's faster
							if (buf > 0) {
								uint currentSpeeds = currentLaneSpeeds[curLaneId];

								if (!InStartupPhase) {
									currentMeanSpeed = (byte)Math.Min(100u, ((currentSpeeds * 100u) / buf) / ((uint)(Math.Max(SpeedLimitManager.GetLockFreeGameSpeedLimit(segmentID, laneIndex, curLaneId, data.Info.m_lanes[laneIndex]) * 8f, 1f)))); // 0 .. 100, m_speedLimit of highway is 2, actual max. vehicle speed on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different units for velocity)
								}
							} else {
								if (!InStartupPhase) {
									currentMeanSpeed = 100;
								}
							}

							/*if (segmentID == 22980) {
								Log._Debug($"Lane {curLaneId}: currentMeanSpeed={currentMeanSpeed} currentMeanDensity={currentMeanDensity}");
							}*/

							if (currentMeanSpeed >= laneMeanSpeeds[curLaneId])
								laneMeanSpeeds[curLaneId] = (byte)Math.Min((int)laneMeanSpeeds[curLaneId] + 5, currentMeanSpeed);
							else
								laneMeanSpeeds[curLaneId] = (byte)Math.Max((int)laneMeanSpeeds[curLaneId] - 5, 0);

							//laneMeanDensities[curLaneId] = currentMeanDensity;

							currentLaneTrafficBuffer[curLaneId] = 0;
							currentLaneSpeeds[curLaneId] = 0;

							if (currentLaneDensities[curLaneId] > 0 && firstWithTraffic) {
								resetDensity = (currentLaneDensities[curLaneId] > 1000000);
								firstWithTraffic = false;
							}

							if (resetDensity) {
								currentLaneDensities[curLaneId] /= 2u;
							}

							laneIndex++;
							curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
						}
					} catch (Exception e) {
						Log.Error("Error occured while calculating lane traffic density: " + e.ToString());
					}
				}
			}
			try {
				OriginalSimulationStep(segmentID, ref data);
			} catch (Exception ex) {
				Log.Error("Error in CustomRoadAI.SimulationStep: " + ex.ToString());
			}
		}

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState) {
			TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim == null || !nodeSim.IsSimulationActive()) {
				RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState);
			} else {
				GetCustomTrafficLightState(vehicleId, ref vehicleData, nodeId, fromSegmentId, toSegmentId, out vehicleLightState, out pedestrianLightState, nodeSim);
			}
		}

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, out bool vehicles, out bool pedestrians) {
			TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim == null || !nodeSim.IsSimulationActive()) {
				RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
			} else {
				GetCustomTrafficLightState(vehicleId, ref vehicleData, nodeId, fromSegmentId, toSegmentId, out vehicleLightState, out pedestrianLightState, nodeSim);
				vehicles = false;
				pedestrians = false;
			}
		}

		private static void GetCustomTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, ushort toSegmentId, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, TrafficLightSimulation nodeSim = null) {
			if (nodeSim == null) {
				nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
				if (nodeSim == null) {
					Log.Error($"GetCustomTrafficLightState: node traffic light simulation not found at node {nodeId}! Vehicle {vehicleId} comes from segment {fromSegmentId} and goes to node {nodeId}");
					throw new ApplicationException($"GetCustomTrafficLightState: node traffic light simulation not found at node {nodeId}! Vehicle {vehicleId} comes from segment {fromSegmentId} and goes to node {nodeId}");
				}
			}

			// get vehicle position
			/*VehiclePosition vehiclePos = TrafficPriority.GetVehiclePosition(vehicleId);
			if (!vehiclePos.Valid || vehiclePos.FromSegment != fromSegmentId || vehiclePos.ToNode != nodeId) {
				Log._Debug($"GetTrafficLightState: Recalculating position for vehicle {vehicleId}! FromSegment={vehiclePos.FromSegment} Valid={vehiclePos.Valid}");
				try {
					HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], false, false);
				} catch (Exception e) {
					Log.Error("VehicleAI GetTrafficLightState Error: " + e.ToString());
				}
			}

			if (!vehiclePos.Valid || vehiclePos.FromSegment != fromSegmentId || vehiclePos.ToNode != nodeId) {
				Log.Warning($"GetTrafficLightState: Vehicle {vehicleId} is not moving at segment {fromSegmentId} to node {nodeId}! FromSegment={vehiclePos.FromSegment} ToNode={vehiclePos.ToNode} Valid={vehiclePos.Valid}");
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
				pedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}*/

			// get vehicle type
			ExtVehicleType? vehicleType = CustomVehicleAI.DetermineVehicleTypeFromVehicle(vehicleId, ref vehicleData);
			if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram && vehicleType != ExtVehicleType.Tram)
				Log.Warning($"vehicleType={vehicleType} ({(int)vehicleType}) for Tram");
			//Log._Debug($"GetCustomTrafficLightState: Vehicle {vehicleId} is a {vehicleType}");
			if (vehicleType == null) {
				Log.Warning($"GetTrafficLightState: Could not determine vehicle type of vehicle {vehicleId}!");
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
				pedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}

			// get responsible traffic light
			CustomSegmentLights lights = CustomTrafficLights.GetSegmentLights(nodeId, fromSegmentId);
			CustomSegmentLight light = lights == null ? null : lights.GetCustomLight((ExtVehicleType)vehicleType);
			if (lights == null || light == null) {
				Log.Warning($"GetTrafficLightState: No custom light for vehicleType {vehicleType} @ node {nodeId}, segment {fromSegmentId} found. lights null? {lights == null} light null? {light == null}");
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
				pedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}

			SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(fromSegmentId);

			// get traffic light state from responsible traffic light
			if (toSegmentId == fromSegmentId) {
				vehicleLightState = TrafficPriority.IsLeftHandDrive() ? light.GetLightRight() : light.GetLightLeft();
			} else if (geometry.IsLeftSegment(toSegmentId, nodeId)) {
				vehicleLightState = light.GetLightLeft();
			} else if (geometry.IsRightSegment(toSegmentId, nodeId)) {
				vehicleLightState = light.GetLightRight();
			} else {
				vehicleLightState = light.GetLightMain();
			}

			// get traffic lights state for pedestrians
			pedestrianLightState = (lights.PedestrianLightState != null) ? (RoadBaseAI.TrafficLightState)lights.PedestrianLightState : RoadBaseAI.TrafficLightState.Red;
		}

		public static void CustomSetTrafficLightState(ushort nodeID, ref NetSegment segmentData, uint frame, RoadBaseAI.TrafficLightState vehicleLightState, RoadBaseAI.TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
			OriginalSetTrafficLightState(false, nodeID, ref segmentData, frame, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
		}

		public static void OriginalSetTrafficLightState(bool customCall, ushort nodeID, ref NetSegment segmentData, uint frame, RoadBaseAI.TrafficLightState vehicleLightState, RoadBaseAI.TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
			/// NON-STOCK CODE START ///
			TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeID);
			if (nodeSim == null || !nodeSim.IsSimulationActive() || customCall) {
			/// NON-STOCK CODE END ///
				int num = (int)pedestrianLightState << 2 | (int)vehicleLightState;
				if (segmentData.m_startNode == nodeID) {
					if ((frame >> 8 & 1u) == 0u) {
						segmentData.m_trafficLightState0 = (byte)((int)(segmentData.m_trafficLightState0 & 240) | num);
					} else {
						segmentData.m_trafficLightState1 = (byte)((int)(segmentData.m_trafficLightState1 & 240) | num);
					}
					if (vehicles) {
						segmentData.m_flags |= NetSegment.Flags.TrafficStart;
					} else {
						segmentData.m_flags &= ~NetSegment.Flags.TrafficStart;
					}
					if (pedestrians) {
						segmentData.m_flags |= NetSegment.Flags.CrossingStart;
					} else {
						segmentData.m_flags &= ~NetSegment.Flags.CrossingStart;
					}
				} else {
					if ((frame >> 8 & 1u) == 0u) {
						segmentData.m_trafficLightState0 = (byte)((int)(segmentData.m_trafficLightState0 & 15) | num << 4);
					} else {
						segmentData.m_trafficLightState1 = (byte)((int)(segmentData.m_trafficLightState1 & 15) | num << 4);
					}
					if (vehicles) {
						segmentData.m_flags |= NetSegment.Flags.TrafficEnd;
					} else {
						segmentData.m_flags &= ~NetSegment.Flags.TrafficEnd;
					}
					if (pedestrians) {
						segmentData.m_flags |= NetSegment.Flags.CrossingEnd;
					} else {
						segmentData.m_flags &= ~NetSegment.Flags.CrossingEnd;
					}
				}
			} // NON-STOCK CODE
		}

		#region stock code
		public void OriginalSimulationStep(ushort nodeID, ref NetNode data) {
			NetManager instance = Singleton<NetManager>.instance;
			if ((data.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
				int num = (int)(data.m_maxWaitTime & 3);
				int num2 = data.m_maxWaitTime >> 2 & 7;
				int num3 = data.m_maxWaitTime >> 5;
				int num4 = -1;
				int num5 = -1;
				int num6 = -1;
				int num7 = -1;
				int num8 = -1;
				int num9 = -1;
				int num10 = 0;
				int num11 = 0;
				int num12 = 0;
				int num13 = 0;
				int num14 = 0;
				for (int i = 0; i < 8; i++) {
					ushort segment = data.GetSegment(i);
					if (segment != 0) {
						int num15 = 0;
						int num16 = 0;
						instance.m_segments.m_buffer[(int)segment].CountLanes(segment, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram, ref num15, ref num16);
						bool flag = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
						bool flag2 = (!flag) ? (num15 != 0) : (num16 != 0);
						bool flag3 = (!flag) ? (num16 != 0) : (num15 != 0);
						if (flag2) {
							num10 |= 1 << i;
						}
						if (flag3) {
							num11 |= 1 << i;
							num13++;
						}
						RoadBaseAI.TrafficLightState trafficLightState;
						RoadBaseAI.TrafficLightState trafficLightState2;
						bool flag4;
						bool flag5;
						RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment], currentFrameIndex - 256u, out trafficLightState, out trafficLightState2, out flag4, out flag5);
						if ((trafficLightState2 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green && flag5) {
							if (num7 == -1) {
								num7 = i;
							}
							if (num9 == -1 && num14 >= num3) {
								num9 = i;
							}
						}
						num14++;
						if (flag2 || flag4) {
							if ((trafficLightState & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green) {
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
				Vector3 vector = Vector3.zero;
				if (num9 != -1) {
					ushort segment2 = data.GetSegment(num9);
					vector = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
					if (num5 != -1) {
						segment2 = data.GetSegment(num5);
						Vector3 direction = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
						if (direction.x * vector.x + direction.z * vector.z < -0.5f) {
							num5 = -1;
						}
					}
					if (num5 == -1) {
						for (int j = 0; j < 8; j++) {
							if (j != num9 && (num10 & 1 << j) != 0) {
								segment2 = data.GetSegment(j);
								if (segment2 != 0) {
									Vector3 direction2 = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
									if (direction2.x * vector.x + direction2.z * vector.z >= -0.5f) {
										num5 = j;
										break;
									}
								}
							}
						}
					}
				}
				int num17 = -1;
				Vector3 vector2 = Vector3.zero;
				Vector3 vector3 = Vector3.zero;
				if (num5 != -1) {
					ushort segment3 = data.GetSegment(num5);
					vector2 = instance.m_segments.m_buffer[(int)segment3].GetDirection(nodeID);
					if ((num10 & num11 & 1 << num5) != 0) {
						for (int k = 0; k < 8; k++) {
							if (k != num5 && k != num9 && (num10 & num11 & 1 << k) != 0) {
								segment3 = data.GetSegment(k);
								if (segment3 != 0) {
									vector3 = instance.m_segments.m_buffer[(int)segment3].GetDirection(nodeID);
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
						RoadBaseAI.TrafficLightState trafficLightState3;
						RoadBaseAI.TrafficLightState trafficLightState4;
						RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment4], currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4);
						trafficLightState3 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
						trafficLightState4 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
						if (num5 == l || num17 == l) {
							if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green) {
								trafficLightState3 = RoadBaseAI.TrafficLightState.RedToGreen;
								num = 0;
								if (++num2 >= num12) {
									num2 = 0;
								}
							}
							if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green) {
								trafficLightState4 = RoadBaseAI.TrafficLightState.GreenToRed;
							}
						} else {
							if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green) {
								trafficLightState3 = RoadBaseAI.TrafficLightState.GreenToRed;
							}
							Vector3 direction3 = instance.m_segments.m_buffer[(int)segment4].GetDirection(nodeID);
							if ((num11 & 1 << l) != 0 && num9 != l && ((num5 != -1 && direction3.x * vector2.x + direction3.z * vector2.z < -0.5f) || (num17 != -1 && direction3.x * vector3.x + direction3.z * vector3.z < -0.5f))) {
								if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green) {
									trafficLightState4 = RoadBaseAI.TrafficLightState.GreenToRed;
								}
							} else if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green) {
								trafficLightState4 = RoadBaseAI.TrafficLightState.RedToGreen;
								if (++num3 >= num14) {
									num3 = 0;
								}
							}
						}
						RoadBaseAI.SetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment4], currentFrameIndex, trafficLightState3, trafficLightState4, false, false);
					}
				}
				data.m_maxWaitTime = (byte)(num3 << 5 | num2 << 2 | num);
			}
			int num18 = 0;
			if (this.m_noiseAccumulation != 0) {
				int num19 = 0;
				for (int m = 0; m < 8; m++) {
					ushort segment5 = data.GetSegment(m);
					if (segment5 != 0) {
						num18 += (int)instance.m_segments.m_buffer[(int)segment5].m_trafficDensity;
						num19++;
					}
				}
				if (num19 != 0) {
					num18 /= num19;
				}
			}
			int num20 = 100 - (num18 - 100) * (num18 - 100) / 100;
			int num21 = this.m_noiseAccumulation * num20 / 100;
			if (num21 != 0) {
				Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num21, data.m_position, this.m_noiseRadius);
			}
			if ((data.m_problems & Notification.Problem.RoadNotConnected) != Notification.Problem.None && (data.m_flags & NetNode.Flags.Original) != NetNode.Flags.None) {
				GuideController properties = Singleton<GuideManager>.instance.m_properties;
				if (properties != null) {
					instance.m_outsideNodeNotConnected.Activate(properties.m_outsideNotConnected, nodeID, Notification.Problem.RoadNotConnected);
				}
			}
		}

		public void OriginalSimulationStep(ushort segmentID, ref NetSegment data) {
			if ((data.m_flags & NetSegment.Flags.Original) == NetSegment.Flags.None) {
				NetManager netManager = Singleton<NetManager>.instance;
				Vector3 pos = netManager.m_nodes.m_buffer[(int)data.m_startNode].m_position;
				Vector3 pos2 = netManager.m_nodes.m_buffer[(int)data.m_endNode].m_position;
				int n = this.GetMaintenanceCost(pos, pos2);
				bool f = (ulong)(Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8 & 15u) == (ulong)((long)(segmentID & 15));
				if (n != 0) {
					if (f) {
						n = n * 16 / 100 - n / 100 * 15;
					} else {
						n /= 100;
					}
					Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.Maintenance, n, this.m_info.m_class);
				}
				if (f) {
					float n2 = (float)netManager.m_nodes.m_buffer[(int)data.m_startNode].m_elevation;
					float n3 = (float)netManager.m_nodes.m_buffer[(int)data.m_endNode].m_elevation;
					if (this.IsUnderground()) {
						n2 = -n2;
						n3 = -n3;
					}
					int constructionCost = this.GetConstructionCost(pos, pos2, n2, n3);
					if (constructionCost != 0) {
						StatisticBase statisticBase = Singleton<StatisticsManager>.instance.Acquire<StatisticInt64>(StatisticType.CityValue);
						if (statisticBase != null) {
							statisticBase.Add(constructionCost);
						}
					}
				}
			}

			SimulationManager instance = Singleton<SimulationManager>.instance;
			NetManager instance2 = Singleton<NetManager>.instance;
			Notification.Problem problem = Notification.RemoveProblems(data.m_problems, Notification.Problem.Flood | Notification.Problem.Snow);
			float num = 0f;
			uint num2 = data.m_lanes;
			int num3 = 0;
			while (num3 < this.m_info.m_lanes.Length && num2 != 0u) {
				NetInfo.Lane lane = this.m_info.m_lanes[num3];
				if ((byte)(lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0 && (lane.m_vehicleType & ~VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None) {
					num += instance2.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_length;
				}
				num2 = instance2.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}
			int num4 = 0;
			if (data.m_trafficBuffer == 65535) {
				if ((data.m_flags & NetSegment.Flags.Blocked) == NetSegment.Flags.None) {
					data.m_flags |= NetSegment.Flags.Blocked;
					data.m_modifiedIndex = instance.m_currentBuildIndex++;
				}
			} else {
				data.m_flags &= ~NetSegment.Flags.Blocked;
				int num5 = Mathf.RoundToInt(num) << 4;
				if (num5 != 0) {
					num4 = (int)((byte)Mathf.Min((int)(data.m_trafficBuffer * 100) / num5, 100));
				}
			}
			data.m_trafficBuffer = 0;
			if (num4 > (int)data.m_trafficDensity) {
				data.m_trafficDensity = (byte)Mathf.Min((int)(data.m_trafficDensity + 5), num4);
			} else if (num4 < (int)data.m_trafficDensity) {
				data.m_trafficDensity = (byte)Mathf.Max((int)(data.m_trafficDensity - 5), num4);
			}
			Vector3 position = instance2.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			Vector3 position2 = instance2.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			Vector3 vector = (position + position2) * 0.5f;
			bool flag = false;
			if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == Vehicle.Flags.None) {
				float num6 = Singleton<TerrainManager>.instance.WaterLevel(VectorUtils.XZ(vector));
				if (num6 > vector.y + 1f) {
					flag = true;
					data.m_flags |= NetSegment.Flags.Flooded;
					problem = Notification.AddProblems(problem, Notification.Problem.Flood | Notification.Problem.MajorProblem);
				} else {
					data.m_flags &= ~NetSegment.Flags.Flooded;
					if (num6 > vector.y) {
						flag = true;
						problem = Notification.AddProblems(problem, Notification.Problem.Flood);
					}
				}
			}
			DistrictManager instance3 = Singleton<DistrictManager>.instance;
			byte district = instance3.GetDistrict(vector);
			DistrictPolicies.CityPlanning cityPlanningPolicies = instance3.m_districts.m_buffer[(int)district].m_cityPlanningPolicies;
			int num7 = (int)(100 - (data.m_trafficDensity - 100) * (data.m_trafficDensity - 100) / 100);
			if ((this.m_info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
				if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == Vehicle.Flags.None) {
					int num8 = (int)data.m_wetness;
					if (!instance2.m_treatWetAsSnow) {
						if (flag) {
							num8 = 255;
						} else {
							int num9 = -(num8 + 63 >> 5);
							float num10 = Singleton<WeatherManager>.instance.SampleRainIntensity(vector, false);
							if (num10 != 0f) {
								int num11 = Mathf.RoundToInt(Mathf.Min(num10 * 4000f, 1000f));
								num9 += instance.m_randomizer.Int32(num11, num11 + 99) / 100;
							}
							num8 = Mathf.Clamp(num8 + num9, 0, 255);
						}
					} else if (this.m_accumulateSnow) {
						if (flag) {
							num8 = 128;
						} else {
							float num12 = Singleton<WeatherManager>.instance.SampleRainIntensity(vector, false);
							if (num12 != 0f) {
								int num13 = Mathf.RoundToInt(num12 * 400f);
								int num14 = instance.m_randomizer.Int32(num13, num13 + 99) / 100;
								if (Singleton<UnlockManager>.instance.Unlocked(UnlockManager.Feature.Snowplow)) {
									num8 = Mathf.Min(num8 + num14, 255);
								} else {
									num8 = Mathf.Min(num8 + num14, 128);
								}
							} else if (Singleton<SimulationManager>.instance.m_randomizer.Int32(4u) == 0) {
								num8 = Mathf.Max(num8 - 1, 0);
							}
							if (num8 >= 64 && (data.m_flags & (NetSegment.Flags.Blocked | NetSegment.Flags.Flooded)) == NetSegment.Flags.None && instance.m_randomizer.Int32(10u) == 0) {
								TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
								offer.Priority = num8 / 50;
								offer.NetSegment = segmentID;
								offer.Position = vector;
								offer.Amount = 1;
								Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Snow, offer);
							}
							if (num8 >= 192) {
								problem = Notification.AddProblems(problem, Notification.Problem.Snow);
							}
							District[] expr_4E2_cp_0_cp_0 = instance3.m_districts.m_buffer;
							byte expr_4E2_cp_0_cp_1 = district;
							expr_4E2_cp_0_cp_0[(int)expr_4E2_cp_0_cp_1].m_productionData.m_tempSnowCover = expr_4E2_cp_0_cp_0[(int)expr_4E2_cp_0_cp_1].m_productionData.m_tempSnowCover + (uint)num8;
						}
					}
					if (num8 != (int)data.m_wetness) {
						if (Mathf.Abs((int)data.m_wetness - num8) > 10) {
							data.m_wetness = (byte)num8;
							InstanceID empty = InstanceID.Empty;
							empty.NetSegment = segmentID;
							instance2.AddSmoothColor(empty);
							empty.NetNode = data.m_startNode;
							instance2.AddSmoothColor(empty);
							empty.NetNode = data.m_endNode;
							instance2.AddSmoothColor(empty);
						} else {
							data.m_wetness = (byte)num8;
							instance2.m_wetnessChanged = 256;
						}
					}
				}
				int num15;
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) != DistrictPolicies.CityPlanning.None) {
					num7 = num7 * 3 + 1 >> 1;
					num15 = Mathf.Min(700, (int)(50 + data.m_trafficDensity * 6));
				} else {
					num15 = Mathf.Min(500, (int)(50 + data.m_trafficDensity * 4));
				}
				if (!this.m_highwayRules) {
					int num16 = instance.m_randomizer.Int32(num15, num15 + 99) / 100;
					data.m_condition = (byte)Mathf.Max((int)data.m_condition - num16, 0);
					if (data.m_condition < 192 && (data.m_flags & (NetSegment.Flags.Blocked | NetSegment.Flags.Flooded)) == NetSegment.Flags.None && instance.m_randomizer.Int32(20u) == 0) {
						TransferManager.TransferOffer offer2 = default(TransferManager.TransferOffer);
						offer2.Priority = (int)((255 - data.m_condition) / 50);
						offer2.NetSegment = segmentID;
						offer2.Position = vector;
						offer2.Amount = 1;
						Singleton<TransferManager>.instance.AddIncomingOffer(TransferManager.TransferReason.RoadMaintenance, offer2);
					}
				}
			}
			if (!this.m_highwayRules) {
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.HeavyTrafficBan) != DistrictPolicies.CityPlanning.None) {
					data.m_flags |= NetSegment.Flags.HeavyBan;
				} else {
					data.m_flags &= ~NetSegment.Flags.HeavyBan;
				}
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.BikeBan) != DistrictPolicies.CityPlanning.None) {
					data.m_flags |= NetSegment.Flags.BikeBan;
				} else {
					data.m_flags &= ~NetSegment.Flags.BikeBan;
				}
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.OldTown) != DistrictPolicies.CityPlanning.None) {
					data.m_flags |= NetSegment.Flags.CarBan;
				} else {
					data.m_flags &= ~NetSegment.Flags.CarBan;
				}
			}
			int num17 = this.m_noiseAccumulation * num7 / 100;
			if (num17 != 0) {
				float num18 = Vector3.Distance(position, position2);
				int num19 = Mathf.FloorToInt(num18 / this.m_noiseRadius);
				for (int i = 0; i < num19; i++) {
					Vector3 position3 = Vector3.Lerp(position, position2, (float)(i + 1) / (float)(num19 + 1));
					Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num17, position3, this.m_noiseRadius);
				}
			}
			if (data.m_trafficDensity >= 50 && data.m_averageLength < 25f && (instance2.m_nodes.m_buffer[(int)data.m_startNode].m_flags & (NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights)) == NetNode.Flags.TrafficLights && (instance2.m_nodes.m_buffer[(int)data.m_endNode].m_flags & (NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights)) == NetNode.Flags.TrafficLights) {
				GuideController properties = Singleton<GuideManager>.instance.m_properties;
				if (properties != null) {
					Singleton<NetManager>.instance.m_shortRoadTraffic.Activate(properties.m_shortRoadTraffic, segmentID);
				}
			}
			data.m_problems = problem;
		}
		#endregion

		internal static void OnLevelUnloading() {
			initDone = false;	
		}

		internal static void OnBeforeLoadData() {
			if (!initDone) {
				segmentGeometries = new SegmentGeometry[Singleton<NetManager>.instance.m_segments.m_size];
				Log._Debug($"Building {segmentGeometries.Length} segment geometries...");
				for (ushort i = 0; i < segmentGeometries.Length; ++i) {
					segmentGeometries[i] = new SegmentGeometry(i);
				}
				Log._Debug($"Calculated segment geometries.");

				currentLaneTrafficBuffer = new ushort[Singleton<NetManager>.instance.m_lanes.m_size];
				currentLaneSpeeds = new uint[Singleton<NetManager>.instance.m_lanes.m_size];
				currentLaneDensities = new uint[Singleton<NetManager>.instance.m_lanes.m_size];
				laneMeanSpeeds = new byte[Singleton<NetManager>.instance.m_lanes.m_size];
				//laneMeanDensities = new byte[Singleton<NetManager>.instance.m_lanes.m_size];
				resetTrafficStats();
				initDone = true;
			}
		}

		internal static void resetTrafficStats() {
			for (uint i = 0; i < laneMeanSpeeds.Length; ++i) {
				laneMeanSpeeds[i] = 25;
				//laneMeanDensities[i] = 50;
				currentLaneTrafficBuffer[i] = 0;
			}
			simStartFrame = 0;
		}

		internal static void AddTraffic(uint laneID, ushort vehicleLength, ushort speed, bool realTraffic) {
			if (!initDone)
				return;
			currentLaneTrafficBuffer[laneID] = (ushort)Math.Min(65535u, (uint)currentLaneTrafficBuffer[laneID] + 1u);
			currentLaneSpeeds[laneID] += speed;
			currentLaneDensities[laneID] += vehicleLength;
		}

		internal static SegmentGeometry GetSegmentGeometry(ushort segmentId) {
			return segmentGeometries[segmentId];
		}

		internal static SegmentGeometry GetSegmentGeometry(ushort segmentId, ushort nodeId) {
			SegmentGeometry ret = segmentGeometries[segmentId];
			ret.VerifySegmentsByCount(nodeId);
			return ret;
		}
	}
}

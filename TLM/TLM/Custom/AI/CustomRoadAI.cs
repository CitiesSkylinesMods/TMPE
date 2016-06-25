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

		// TODO create accessor method for these arrays
		public static ushort[][] currentLaneTrafficBuffer;
		public static uint[][] currentLaneSpeeds;
		public static uint[][] currentLaneDensities;

		public static byte[][] laneMeanSpeeds;
		public static byte[][] laneMeanDensities;

		public static bool initDone = false;
		public static uint simStartFrame = 0;

		public static bool InStartupPhase = true;

		public void CustomNodeSimulationStep(ushort nodeId, ref NetNode data) {
			if (simStartFrame == 0)
				simStartFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			try {
				TrafficLightSimulation.SimulationStep();

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
					TrafficPriority.SegmentSimulationStep(segmentID);
				} catch (Exception e) {
					Log.Error($"Error occured while housekeeping segment {segmentID}: " + e.ToString());
				}

				if (!Options.isStockLaneChangerUsed()) {
					try {
						InStartupPhase = simStartFrame == 0 || simStartFrame >> 14 >= Singleton<SimulationManager>.instance.m_currentFrameIndex >> 14; // approx. 3 minutes

						// calculate traffic density
						uint curLaneId = data.m_lanes;
						int numLanes = data.Info.m_lanes.Length;
						uint laneIndex = 0;
						bool resetDensity = false;
						uint maxDensity = 0u;
						uint densitySum = 0u;

						if (currentLaneTrafficBuffer[segmentID] == null || currentLaneTrafficBuffer[segmentID].Length < numLanes) {
							currentLaneTrafficBuffer[segmentID] = new ushort[numLanes];
							currentLaneSpeeds[segmentID] = new uint[numLanes];
							currentLaneDensities[segmentID] = new uint[numLanes];
							laneMeanSpeeds[segmentID] = new byte[numLanes];
							laneMeanDensities[segmentID] = new byte[numLanes];
						}

						while (laneIndex < numLanes && curLaneId != 0u) {
							uint currentDensity = currentLaneDensities[segmentID][laneIndex];
							if (maxDensity == 0 || currentDensity > maxDensity)
								maxDensity = currentDensity;
							densitySum += currentDensity;
							
							laneIndex++;
							curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
						}
						if (maxDensity > 250)
							resetDensity = true;

						curLaneId = data.m_lanes;
						laneIndex = 0;
						while (laneIndex < numLanes && curLaneId != 0u) {
							uint buf = currentLaneTrafficBuffer[segmentID][laneIndex];
							uint currentDensity = currentLaneDensities[segmentID][laneIndex];

							//currentMeanDensity = (byte)Math.Min(100u, (uint)((currentDensities * 100u) / Math.Max(1u, maxDens))); // 0 .. 100

							byte currentMeanSpeed = 25;
							// we use integer division here because it's faster
							if (buf > 0) {
								uint currentSpeeds = currentLaneSpeeds[segmentID][laneIndex];

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

							if (currentMeanSpeed >= laneMeanSpeeds[segmentID][laneIndex])
								laneMeanSpeeds[segmentID][laneIndex] = (byte)Math.Min((int)laneMeanSpeeds[segmentID][laneIndex] + 10, currentMeanSpeed);
							else
								laneMeanSpeeds[segmentID][laneIndex] = (byte)Math.Max((int)laneMeanSpeeds[segmentID][laneIndex] - 10, 0);

							if (densitySum > 0)
								laneMeanDensities[segmentID][laneIndex] = (byte)Math.Min(100u, (currentDensity * 100u) / densitySum);
							else
								laneMeanDensities[segmentID][laneIndex] = (byte)0;
							currentLaneTrafficBuffer[segmentID][laneIndex] = 0;
							currentLaneSpeeds[segmentID][laneIndex] = 0;

							if (resetDensity) {
								currentLaneDensities[segmentID][laneIndex] /= 10u;
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

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState) {
			TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim == null || !nodeSim.IsSimulationActive()) {
				RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState);
			} else {
				GetCustomTrafficLightState(vehicleId, ref vehicleData, nodeId, fromSegmentId, fromLaneIndex, toSegmentId, out vehicleLightState, out pedestrianLightState, nodeSim);
			}
		}

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, out bool vehicles, out bool pedestrians) {
			TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(nodeId);
			if (nodeSim == null || !nodeSim.IsSimulationActive()) {
				RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
			} else {
				GetCustomTrafficLightState(vehicleId, ref vehicleData, nodeId, fromSegmentId, fromLaneIndex, toSegmentId, out vehicleLightState, out pedestrianLightState, nodeSim);
				vehicles = false;
				pedestrians = false;
			}
		}

		private static void GetCustomTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, TrafficLightSimulation nodeSim = null) {
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
			//ExtVehicleType? vehicleType = VehicleStateManager.GetVehicleState(vehicleId)?.VehicleType;
			/*if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram && vehicleType != ExtVehicleType.Tram)
				Log.Warning($"vehicleType={vehicleType} ({(int)vehicleType}) for Tram");*/
			//Log._Debug($"GetCustomTrafficLightState: Vehicle {vehicleId} is a {vehicleType}");
			/*if (vehicleType == null) {
				Log.Warning($"GetTrafficLightState: Could not determine vehicle type of vehicle {vehicleId}!");
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
				pedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}*/

			// get responsible traffic light
			//Log._Debug($"GetTrafficLightState: Getting custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}.");
			CustomSegmentLights lights = CustomTrafficLights.GetSegmentLights(nodeId, fromSegmentId);
			CustomSegmentLight light = lights == null ? null : lights.GetCustomLight(fromLaneIndex);
			if (lights == null || light == null) {
				Log.Warning($"GetTrafficLightState: No custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex} found. lights null? {lights == null} light null? {light == null}");
				vehicleLightState = RoadBaseAI.TrafficLightState.Red;
				pedestrianLightState = RoadBaseAI.TrafficLightState.Red;
				return;
			}

			SegmentGeometry geometry = SegmentGeometry.Get(fromSegmentId);

			// determine node position at `toSegment` (start/end)
			bool isStartNode = geometry.StartNodeId() == nodeId;

			// get traffic light state from responsible traffic light
			if (toSegmentId == fromSegmentId) {
				vehicleLightState = TrafficPriority.IsLeftHandDrive() ? light.GetLightRight() : light.GetLightLeft();
			} else if (geometry.IsLeftSegment(toSegmentId, isStartNode)) {
				vehicleLightState = light.GetLightLeft();
			} else if (geometry.IsRightSegment(toSegmentId, isStartNode)) {
				vehicleLightState = light.GetLightRight();
			} else {
				vehicleLightState = light.GetLightMain();
			}

			// get traffic lights state for pedestrians
			pedestrianLightState = (lights.PedestrianLightState != null) ? (RoadBaseAI.TrafficLightState)lights.PedestrianLightState : RoadBaseAI.TrafficLightState.Green;
#if DEBUG
			//Log._Debug($"GetTrafficLightState: Getting light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}. vehicleLightState={vehicleLightState}, pedestrianLightState={pedestrianLightState}");
#endif
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

		public void CustomUpdateLanes(ushort segmentID, ref NetSegment data, bool loading) {
			OriginalUpdateLanes(segmentID, ref data, loading);

			try {
				NetManager netManager = Singleton<NetManager>.instance;

				// update lane arrows
				uint laneId = netManager.m_segments.m_buffer[segmentID].m_lanes;
				while (laneId != 0) {
					if (!Flags.applyLaneArrowFlags(laneId)) {
						Flags.removeLaneArrowFlags(laneId);
					}
					laneId = netManager.m_lanes.m_buffer[laneId].m_nextLane;
				}
			} catch (Exception e) {
				Log.Error($"Error occured in CustomRoadAI.CustomUpdateLanes @ seg. {segmentID}: " + e.ToString());
			}
		}

		#region stock code
		public void OriginalUpdateLanes(ushort segmentID, ref NetSegment data, bool loading) {
			NetManager instance = Singleton<NetManager>.instance;
			bool flag = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
			Vector3 vector;
			Vector3 from;
			bool smoothStart;
			data.CalculateCorner(segmentID, true, true, true, out vector, out from, out smoothStart);
			Vector3 a;
			Vector3 to;
			bool smoothEnd;
			data.CalculateCorner(segmentID, true, false, true, out a, out to, out smoothEnd);
			Vector3 a2;
			Vector3 to2;
			data.CalculateCorner(segmentID, true, true, false, out a2, out to2, out smoothStart);
			Vector3 vector2;
			Vector3 from2;
			data.CalculateCorner(segmentID, true, false, false, out vector2, out from2, out smoothEnd);
			if ((data.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
				data.m_cornerAngleStart = (byte)(Mathf.RoundToInt(Mathf.Atan2(a2.z - vector.z, a2.x - vector.x) * 40.7436638f) & 255);
				data.m_cornerAngleEnd = (byte)(Mathf.RoundToInt(Mathf.Atan2(a.z - vector2.z, a.x - vector2.x) * 40.7436638f) & 255);
			} else {
				data.m_cornerAngleStart = (byte)(Mathf.RoundToInt(Mathf.Atan2(vector.z - a2.z, vector.x - a2.x) * 40.7436638f) & 255);
				data.m_cornerAngleEnd = (byte)(Mathf.RoundToInt(Mathf.Atan2(vector2.z - a.z, vector2.x - a.x) * 40.7436638f) & 255);
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			bool flag2 = false;
			bool flag3 = false;
			instance.m_nodes.m_buffer[(int)data.m_endNode].CountLanes(data.m_endNode, segmentID, NetInfo.Direction.Forward, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, -data.m_endDirection, ref num, ref num2, ref num3, ref num4, ref num5, ref num6);
			if ((instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & (NetNode.Flags.End | NetNode.Flags.Middle | NetNode.Flags.Bend | NetNode.Flags.Outside)) != NetNode.Flags.None) {
				if (num + num2 + num3 == 0) {
					flag3 = true;
				} else {
					flag2 = true;
				}
			}
			int num7 = 0;
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			int num11 = 0;
			int num12 = 0;
			bool flag4 = false;
			bool flag5 = false;
			instance.m_nodes.m_buffer[(int)data.m_startNode].CountLanes(data.m_startNode, segmentID, NetInfo.Direction.Forward, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, -data.m_startDirection, ref num7, ref num8, ref num9, ref num10, ref num11, ref num12);
			if ((instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & (NetNode.Flags.End | NetNode.Flags.Middle | NetNode.Flags.Bend | NetNode.Flags.Outside)) != NetNode.Flags.None) {
				if (num7 + num8 + num9 == 0) {
					flag5 = true;
				} else {
					flag4 = true;
				}
			}
			NetLane.Flags flags = NetLane.Flags.None;
			if (num4 != 0 && num == 0) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.EndOneWayLeft : NetLane.Flags.StartOneWayLeft);
			}
			if (num6 != 0 && num3 == 0) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.EndOneWayRight : NetLane.Flags.StartOneWayRight);
			}
			if (num10 != 0 && num7 == 0) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.StartOneWayLeft : NetLane.Flags.EndOneWayLeft);
			}
			if (num12 != 0 && num9 == 0) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.StartOneWayRight : NetLane.Flags.EndOneWayRight);
			}
			float num13 = 0f;
			float num14 = 0f;
			uint num15 = 0u;
			uint num16 = data.m_lanes;
			for (int i = 0; i < this.m_info.m_lanes.Length; i++) {
				if (num16 == 0u) {
					if (!Singleton<NetManager>.instance.CreateLanes(out num16, ref Singleton<SimulationManager>.instance.m_randomizer, segmentID, 1)) {
						break;
					}
					instance.m_lanes.m_buffer[(int)((UIntPtr)num15)].m_nextLane = num16;
				}
				NetInfo.Lane lane = this.m_info.m_lanes[i];
				float num17 = lane.m_position / (this.m_info.m_halfWidth * 2f) + 0.5f;
				if ((data.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					num17 = 1f - num17;
				}
				Vector3 vector3 = vector + (a2 - vector) * num17;
				Vector3 startDir = Vector3.Lerp(from, to2, num17);
				Vector3 vector4 = vector2 + (a - vector2) * num17;
				Vector3 endDir = Vector3.Lerp(from2, to, num17);
				vector3.y += lane.m_verticalOffset;
				vector4.y += lane.m_verticalOffset;
				Vector3 b;
				Vector3 c;
				NetSegment.CalculateMiddlePoints(vector3, startDir, vector4, endDir, smoothStart, smoothEnd, out b, out c);
				NetLane.Flags flags2 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags & ~(NetLane.Flags.Forward | NetLane.Flags.Left | NetLane.Flags.Right);
				flags2 &= ~(NetLane.Flags.StartOneWayLeft | NetLane.Flags.StartOneWayRight | NetLane.Flags.EndOneWayLeft | NetLane.Flags.EndOneWayRight);
				flags2 |= flags;
				if (flag) {
					flags2 |= NetLane.Flags.Inverted;
				} else {
					flags2 &= ~NetLane.Flags.Inverted;
				}
				int num18 = 0;
				int num19 = 255;
				if ((byte)(lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
					bool flag6 = (byte)(lane.m_finalDirection & NetInfo.Direction.Forward) != 0 == ((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None);
					int num20;
					int num21;
					int num22;
					if (flag6) {
						num20 = num;
						num21 = num2;
						num22 = num3;
					} else {
						num20 = num7;
						num21 = num8;
						num22 = num9;
					}
					int num23;
					int num24;
					if ((byte)(lane.m_finalDirection & NetInfo.Direction.Forward) != 0) {
						num23 = lane.m_similarLaneIndex;
						num24 = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
					} else {
						num23 = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
						num24 = lane.m_similarLaneIndex;
					}
					int num25 = num20 + num21 + num22;
					num18 = 255;
					num19 = 0;
					if (num25 != 0) {
						int num26;
						int num27;
						if (lane.m_similarLaneCount >= num25) {
							num26 = num20;
							num27 = num22;
						} else {
							num26 = num20 * lane.m_similarLaneCount / (num25 + (num21 >> 1));
							num27 = num22 * lane.m_similarLaneCount / (num25 + (num21 >> 1));
						}
						int num28 = num26;
						int num29 = lane.m_similarLaneCount - num26 - num27;
						int num30 = num27;
						if (num29 > 0) {
							if (num20 > num26) {
								num28++;
							}
							if (num22 > num27) {
								num30++;
							}
						}
						if (num23 < num28) {
							int num31 = (num23 * num20 + num28 - 1) / num28;
							int num32 = ((num23 + 1) * num20 + num28 - 1) / num28;
							if (num32 > num31) {
								flags2 |= NetLane.Flags.Left;
								num18 = Mathf.Min(num18, num31);
								num19 = Mathf.Max(num19, num32);
							}
						}
						if (num23 >= num26 && num24 >= num27 && num21 != 0) {
							if (lane.m_similarLaneCount > num25) {
								num26++;
							}
							int num33 = num20 + ((num23 - num26) * num21 + num29 - 1) / num29;
							int num34 = num20 + ((num23 + 1 - num26) * num21 + num29 - 1) / num29;
							if (num34 > num33) {
								flags2 |= NetLane.Flags.Forward;
								num18 = Mathf.Min(num18, num33);
								num19 = Mathf.Max(num19, num34);
							}
						}
						if (num24 < num30) {
							int num35 = num25 - ((num24 + 1) * num22 + num30 - 1) / num30;
							int num36 = num25 - (num24 * num22 + num30 - 1) / num30;
							if (num36 > num35) {
								flags2 |= NetLane.Flags.Right;
								num18 = Mathf.Min(num18, num35);
								num19 = Mathf.Max(num19, num36);
							}
						}
						if (this.m_highwayRules) {
							if ((flags2 & NetLane.Flags.LeftRight) == NetLane.Flags.Left) {
								if ((flags2 & NetLane.Flags.Forward) == NetLane.Flags.None || (num21 >= 2 && num20 == 1)) {
									num19 = Mathf.Min(num19, num18 + 1);
								}
							} else if ((flags2 & NetLane.Flags.LeftRight) == NetLane.Flags.Right && ((flags2 & NetLane.Flags.Forward) == NetLane.Flags.None || (num21 >= 2 && num22 == 1))) {
								num18 = Mathf.Max(num18, num19 - 1);
							}
						}
					}
					if (flag6) {
						if (flag2) {
							flags2 &= ~(NetLane.Flags.Forward | NetLane.Flags.Left | NetLane.Flags.Right);
						} else if (flag3) {
							flags2 |= NetLane.Flags.Forward;
						}
					} else if (flag4) {
						flags2 &= ~(NetLane.Flags.Forward | NetLane.Flags.Left | NetLane.Flags.Right);
					} else if (flag5) {
						flags2 |= NetLane.Flags.Forward;
					}
				}
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_bezier = new Bezier3(vector3, b, c, vector4);
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_segment = segmentID;
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags = (ushort)flags2;
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_firstTarget = (byte)num18;
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_lastTarget = (byte)num19;
				num13 += instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].UpdateLength();
				num14 += 1f;
				num15 = num16;
				num16 = instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_nextLane;
			}
			if (num14 != 0f) {
				data.m_averageLength = num13 / num14;
			} else {
				data.m_averageLength = 0f;
			}
			bool flag7 = false;
			if (data.m_averageLength < 11f && (instance.m_nodes.m_buffer[(int)data.m_startNode].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None && (instance.m_nodes.m_buffer[(int)data.m_endNode].m_flags & NetNode.Flags.Junction) != NetNode.Flags.None) {
				flag7 = true;
			}
			num16 = data.m_lanes;
			int num37 = 0;
			while (num37 < this.m_info.m_lanes.Length && num16 != 0u) {
				NetLane.Flags flags3 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags & ~NetLane.Flags.JoinedJunction;
				if (flag7) {
					flags3 |= NetLane.Flags.JoinedJunction;
				}
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags = (ushort)flags3;
				num16 = instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_nextLane;
				num37++;
			}
			if (!loading) {
				int num38 = Mathf.Max((int)((data.m_bounds.min.x - 16f) / 64f + 135f), 0);
				int num39 = Mathf.Max((int)((data.m_bounds.min.z - 16f) / 64f + 135f), 0);
				int num40 = Mathf.Min((int)((data.m_bounds.max.x + 16f) / 64f + 135f), 269);
				int num41 = Mathf.Min((int)((data.m_bounds.max.z + 16f) / 64f + 135f), 269);
				for (int j = num39; j <= num41; j++) {
					for (int k = num38; k <= num40; k++) {
						ushort num42 = instance.m_nodeGrid[j * 270 + k];
						int num43 = 0;
						while (num42 != 0) {
							NetInfo info = instance.m_nodes.m_buffer[(int)num42].Info;
							Vector3 position = instance.m_nodes.m_buffer[(int)num42].m_position;
							float num44 = Mathf.Max(Mathf.Max(data.m_bounds.min.x - 16f - position.x, data.m_bounds.min.z - 16f - position.z), Mathf.Max(position.x - data.m_bounds.max.x - 16f, position.z - data.m_bounds.max.z - 16f));
							if (num44 < 0f) {
								info.m_netAI.NearbyLanesUpdated(num42, ref instance.m_nodes.m_buffer[(int)num42]);
							}
							num42 = instance.m_nodes.m_buffer[(int)num42].m_nextGridNode;
							if (++num43 >= 32768) {
								CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
								break;
							}
						}
					}
				}
				if (this.m_info.m_hasPedestrianLanes && (this.m_info.m_hasForwardVehicleLanes || this.m_info.m_hasBackwardVehicleLanes)) {
					this.CheckBuildings(segmentID, ref data);
				}
			}
		}

		protected void CheckBuildings(ushort segmentID, ref NetSegment data) {
			Log.Error("CustomRoadAI.CheckBuildings called.");
		}

		public void OriginalSimulationStep(ushort nodeID, ref NetNode data) {
			if ((data.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				if ((data.m_flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None) {
					TrainTrackBaseAI.LevelCrossingSimulationStep(nodeID, ref data);
				} else {
					RoadBaseAI.TrafficLightSimulationStep(nodeID, ref data);
				}
			}
			NetManager instance = Singleton<NetManager>.instance;
			int num = 0;
			if (this.m_noiseAccumulation != 0) {
				int num2 = 0;
				for (int i = 0; i < 8; i++) {
					ushort segment = data.GetSegment(i);
					if (segment != 0) {
						num += (int)instance.m_segments.m_buffer[(int)segment].m_trafficDensity;
						num2++;
					}
				}
				if (num2 != 0) {
					num /= num2;
				}
			}
			int num3 = 100 - (num - 100) * (num - 100) / 100;
			int num4 = this.m_noiseAccumulation * num3 / 100;
			if (num4 != 0) {
				Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num4, data.m_position, this.m_noiseRadius);
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
			if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == 0) {
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
				if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == 0) {
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
				currentLaneTrafficBuffer = new ushort[NetManager.MAX_SEGMENT_COUNT][];
				currentLaneSpeeds = new uint[NetManager.MAX_SEGMENT_COUNT][];
				currentLaneDensities = new uint[NetManager.MAX_SEGMENT_COUNT][];
				laneMeanSpeeds = new byte[NetManager.MAX_SEGMENT_COUNT][];
				laneMeanDensities = new byte[NetManager.MAX_SEGMENT_COUNT][];
				resetTrafficStats();
				initDone = true;
			}
		}

		internal static void resetTrafficStats() {
			for (uint i = 0; i < laneMeanSpeeds.Length; ++i) {
				if (laneMeanDensities[i] != null) {
					for (int k = 0; k < laneMeanDensities[i].Length; ++k) {
						laneMeanSpeeds[i][k] = 50;
						currentLaneTrafficBuffer[i][k] = 0;
					}
				}
			}
			simStartFrame = 0;
		}

		internal static void AddTraffic(ushort segmentId, byte laneIndex, ushort vehicleLength, ushort? speed) {
			if (!initDone)
				return;
			if (laneMeanDensities[segmentId] == null || laneIndex >= laneMeanDensities[segmentId].Length)
				return;

			if (speed != null) {
				currentLaneTrafficBuffer[segmentId][laneIndex] = (ushort)Math.Min(65535u, (uint)currentLaneTrafficBuffer[segmentId][laneIndex] + 1u);
				currentLaneSpeeds[segmentId][laneIndex] += (uint)speed;
			}
			currentLaneDensities[segmentId][laneIndex] += vehicleLength;
		}

		/*internal static SegmentGeometry GetSegmentGeometry(ushort segmentId, ushort nodeId) {
			SegmentGeometry ret = segmentGeometries[segmentId];
			ret.VerifySegmentsByCount(nodeId);
			return ret;
		}*/
	}
}

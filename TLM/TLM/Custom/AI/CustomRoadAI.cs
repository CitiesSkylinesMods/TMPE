using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;
using TrafficManager.Geometry;
using UnityEngine;
using ColossalFramework.Math;
using System.Threading;
using TrafficManager.UI;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.UI.SubTools;
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	public class CustomRoadAI : RoadBaseAI {
		private static ushort lastSimulatedSegmentId = 0;
		private static byte trafficMeasurementMod = 0;
		private static TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
		private static TrafficLightManager tlm = TrafficLightManager.Instance;

		public void CustomNodeSimulationStep(ushort nodeId, ref NetNode data) {
			if (Options.timedLightsEnabled) {
				try {
					//tlsMan.SimulationStep();

					var nodeSim = tlsMan.GetNodeSimulation(nodeId);
					if (nodeSim == null || !nodeSim.IsSimulationActive()) {
						OriginalSimulationStep(nodeId, ref data);
					}
				} catch (Exception e) {
					Log.Warning($"CustomNodeSimulationStep: An error occurred: {e.ToString()}");
				}
			} else {
				OriginalSimulationStep(nodeId, ref data);
			}
		}

		public void CustomSegmentSimulationStep(ushort segmentID, ref NetSegment data) {
			try {
				uint curLaneId = data.m_lanes;
				int numLanes = data.Info.m_lanes.Length;
				uint laneIndex = 0;

				/*while (laneIndex < numLanes && curLaneId != 0u) {
					Flags.applyLaneArrowFlags(curLaneId);

					laneIndex++;
					curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				}*/

				SegmentEndManager.Instance.SegmentSimulationStep(segmentID);
			} catch (Exception e) {
				Log.Error($"Error occured while housekeeping segment {segmentID}: " + e.ToString());
			}

			if (!Options.isStockLaneChangerUsed()) {
				if (segmentID < lastSimulatedSegmentId) {
					// segment simulation restart
					++trafficMeasurementMod;
					if (trafficMeasurementMod >= 4)
						trafficMeasurementMod = 0;
				}
				lastSimulatedSegmentId = segmentID;

				bool doTrafficMeasurement = true;
				if (Options.simAccuracy == 1 || Options.simAccuracy == 2) {
					doTrafficMeasurement = (segmentID & 1) == trafficMeasurementMod;
				} else if (Options.simAccuracy >= 3) {
					doTrafficMeasurement = (segmentID & 3) == trafficMeasurementMod;
				}

				if (doTrafficMeasurement) {
					try {
						TrafficMeasurementManager.Instance.SimulationStep(segmentID, ref data);
					} catch (Exception e) {
						Log.Error("Error occured while calculating lane traffic density: " + e.ToString());
					}
				}
			}
			
			try {
				OriginalSimulationStep(segmentID, ref data);
			} catch (Exception ex) {
				Log.Error($"Error in CustomRoadAI.SimulationStep for segment {segmentID}: " + ex.ToString());
			}
		}

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState) {
			TrafficLightSimulation nodeSim = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(nodeId) : null;
			if (nodeSim == null || !nodeSim.IsSimulationActive()) {
				RoadBaseAI.GetTrafficLightState(nodeId, ref segmentData, frame, out vehicleLightState, out pedestrianLightState);
			} else {
				GetCustomTrafficLightState(vehicleId, ref vehicleData, nodeId, fromSegmentId, fromLaneIndex, toSegmentId, out vehicleLightState, out pedestrianLightState, nodeSim);
			}
		}

		public static void GetTrafficLightState(ushort vehicleId, ref Vehicle vehicleData, ushort nodeId, ushort fromSegmentId, byte fromLaneIndex, ushort toSegmentId, ref NetSegment segmentData, uint frame, out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState, out bool vehicles, out bool pedestrians) {
			TrafficLightSimulation nodeSim = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(nodeId) : null;
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
				nodeSim = TrafficLightSimulationManager.Instance.GetNodeSimulation(nodeId);
				if (nodeSim == null) {
					Log.Error($"GetCustomTrafficLightState: node traffic light simulation not found at node {nodeId}! Vehicle {vehicleId} comes from segment {fromSegmentId} and goes to node {nodeId}");
					vehicleLightState = TrafficLightState.Green;
					pedestrianLightState = TrafficLightState.Green;
					return;
					//throw new ApplicationException($"GetCustomTrafficLightState: node traffic light simulation not found at node {nodeId}! Vehicle {vehicleId} comes from segment {fromSegmentId} and goes to node {nodeId}");
				}
			}

			// get responsible traffic light
			//Log._Debug($"GetTrafficLightState: Getting custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}.");
			SegmentGeometry geometry = SegmentGeometry.Get(fromSegmentId);
			if (geometry == null) {
				pedestrianLightState = TrafficLightState.Green;
				Log.Error($"GetTrafficLightState: No geometry information @ node {nodeId}, segment {fromSegmentId}.");
				vehicleLightState = TrafficLightState.Green;
				pedestrianLightState = TrafficLightState.Green;
				return;
			}

			// determine node position at `fromSegment` (start/end)
			bool isStartNode = geometry.StartNodeId() == nodeId;

			CustomSegmentLights lights = CustomSegmentLightsManager.Instance.GetSegmentLights(fromSegmentId, isStartNode, false);

			if (lights != null) {
				// get traffic lights state for pedestrians
				pedestrianLightState = (lights.PedestrianLightState != null) ? (RoadBaseAI.TrafficLightState)lights.PedestrianLightState : RoadBaseAI.TrafficLightState.Green;
			} else {
				pedestrianLightState = TrafficLightState.Green;
				Log._Debug($"GetTrafficLightState: No pedestrian light @ node {nodeId}, segment {fromSegmentId} found.");
			}

			CustomSegmentLight light = lights == null ? null : lights.GetCustomLight(fromLaneIndex);
			if (lights == null || light == null) {
				//Log.Warning($"GetTrafficLightState: No custom light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex} found. lights null? {lights == null} light null? {light == null}");
				vehicleLightState = RoadBaseAI.TrafficLightState.Green;
				return;
			}

			// get traffic light state from responsible traffic light
			if (toSegmentId == fromSegmentId) {
				vehicleLightState = Constants.ServiceFactory.SimulationService.LeftHandDrive ? light.LightRight : light.LightLeft;
			} else if (geometry.IsLeftSegment(toSegmentId, isStartNode)) {
				vehicleLightState = light.LightLeft;
			} else if (geometry.IsRightSegment(toSegmentId, isStartNode)) {
				vehicleLightState = light.LightRight;
			} else {
				vehicleLightState = light.LightMain;
			}
#if DEBUG
			//Log._Debug($"GetTrafficLightState: Getting light for vehicle {vehicleId} @ node {nodeId}, segment {fromSegmentId}, lane {fromLaneIndex}. vehicleLightState={vehicleLightState}, pedestrianLightState={pedestrianLightState}");
#endif
		}

		public static void CustomSetTrafficLightState(ushort nodeID, ref NetSegment segmentData, uint frame, RoadBaseAI.TrafficLightState vehicleLightState, RoadBaseAI.TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
			OriginalSetTrafficLightState(false, nodeID, ref segmentData, frame, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
		}

		public static void OriginalSetTrafficLightState(bool customCall, ushort nodeID, ref NetSegment segmentData, uint frame, RoadBaseAI.TrafficLightState vehicleLightState, RoadBaseAI.TrafficLightState pedestrianLightState, bool vehicles, bool pedestrians) {
			/// NON-STOCK CODE START ///
			TrafficLightSimulation nodeSim = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(nodeID) : null;
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

		public void CustomClickNodeButton(ushort nodeID, ref NetNode data, int index) {
			if ((data.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None &&
				Singleton<InfoManager>.instance.CurrentMode == InfoManager.InfoMode.TrafficRoutes &&
				Singleton<InfoManager>.instance.CurrentSubMode == InfoManager.SubInfoMode.WaterPower) {
				if (index == -1) {
					/*data.m_flags ^= NetNode.Flags.TrafficLights;
					data.m_flags |= NetNode.Flags.CustomTrafficLights;*/
					// NON-STOCK CODE START
					ToggleTrafficLightsTool toggleTool = (ToggleTrafficLightsTool)UIBase.GetTrafficManagerTool(true).GetSubTool(ToolMode.SwitchTrafficLight);
					toggleTool.ToggleTrafficLight(nodeID, ref data, false);
					// NON-STOCK CODE END
					this.UpdateNodeFlags(nodeID, ref data);
					Singleton<NetManager>.instance.m_yieldLights.Disable();
				} else if (index >= 1 && index <= 8 && (data.m_flags & (NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn)) == NetNode.Flags.None) {
					ushort segmentId = data.GetSegment(index - 1);
					if (segmentId != 0) {
						NetManager netManager = Singleton<NetManager>.instance;
						NetInfo info = netManager.m_segments.m_buffer[(int)segmentId].Info;
						if ((info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
							bool flag = netManager.m_segments.m_buffer[(int)segmentId].m_startNode == nodeID;
							NetSegment.Flags flags = (!flag) ? NetSegment.Flags.YieldEnd : NetSegment.Flags.YieldStart;
							netManager.m_segments.m_buffer[segmentId].m_flags ^= flags;
							netManager.m_segments.m_buffer[(int)segmentId].UpdateLanes(segmentId, true);
							Singleton<NetManager>.instance.m_yieldLights.Disable();
						}
					}
				}
			}
		}

		public void CustomUpdateLanes(ushort segmentID, ref NetSegment data, bool loading) {
			// stock code start
			NetManager instance = Singleton<NetManager>.instance;
			bool flag = Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
			Vector3 vector;
			Vector3 a;
			bool smoothStart;
			data.CalculateCorner(segmentID, true, true, true, out vector, out a, out smoothStart);
			Vector3 a2;
			Vector3 b;
			bool smoothEnd;
			data.CalculateCorner(segmentID, true, false, true, out a2, out b, out smoothEnd);
			Vector3 a3;
			Vector3 b2;
			data.CalculateCorner(segmentID, true, true, false, out a3, out b2, out smoothStart);
			Vector3 vector2;
			Vector3 a4;
			data.CalculateCorner(segmentID, true, false, false, out vector2, out a4, out smoothEnd);
			if ((data.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
				data.m_cornerAngleStart = (byte)(Mathf.RoundToInt(Mathf.Atan2(a3.z - vector.z, a3.x - vector.x) * 40.7436638f) & 255);
				data.m_cornerAngleEnd = (byte)(Mathf.RoundToInt(Mathf.Atan2(a2.z - vector2.z, a2.x - vector2.x) * 40.7436638f) & 255);
			} else {
				data.m_cornerAngleStart = (byte)(Mathf.RoundToInt(Mathf.Atan2(vector.z - a3.z, vector.x - a3.x) * 40.7436638f) & 255);
				data.m_cornerAngleEnd = (byte)(Mathf.RoundToInt(Mathf.Atan2(vector2.z - a2.z, vector2.x - a2.x) * 40.7436638f) & 255);
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
			if ((data.m_flags & NetSegment.Flags.YieldStart) != NetSegment.Flags.None) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.YieldStart : NetLane.Flags.YieldEnd);
			}
			if ((data.m_flags & NetSegment.Flags.YieldEnd) != NetSegment.Flags.None) {
				flags |= (((data.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? NetLane.Flags.YieldEnd : NetLane.Flags.YieldStart);
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
					if (num15 != 0u) {
						instance.m_lanes.m_buffer[(int)((UIntPtr)num15)].m_nextLane = num16;
					} else {
						data.m_lanes = num16;
					}
				}
				NetInfo.Lane lane = this.m_info.m_lanes[i];
				float num17 = lane.m_position / (this.m_info.m_halfWidth * 2f) + 0.5f;
				if ((data.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None) {
					num17 = 1f - num17;
				}
				Vector3 vector3 = vector + (a3 - vector) * num17;
				Vector3 startDir = Vector3.Lerp(a, b2, num17);
				Vector3 vector4 = vector2 + (a2 - vector2) * num17;
				Vector3 endDir = Vector3.Lerp(a4, b, num17);
				vector3.y += lane.m_verticalOffset;
				vector4.y += lane.m_verticalOffset;
				Vector3 b3;
				Vector3 c;
				NetSegment.CalculateMiddlePoints(vector3, startDir, vector4, endDir, smoothStart, smoothEnd, out b3, out c);
				NetLane.Flags flags2 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags;
				NetLane.Flags flags3 = flags;
				flags2 &= ~(NetLane.Flags.Forward | NetLane.Flags.Left | NetLane.Flags.Right | NetLane.Flags.YieldStart | NetLane.Flags.YieldEnd | NetLane.Flags.StartOneWayLeft | NetLane.Flags.StartOneWayRight | NetLane.Flags.EndOneWayLeft | NetLane.Flags.EndOneWayRight);
				if ((byte)(lane.m_finalDirection & NetInfo.Direction.Both) == 2) {
					flags3 &= ~NetLane.Flags.YieldEnd;
				}
				if ((byte)(lane.m_finalDirection & NetInfo.Direction.Both) == 1) {
					flags3 &= ~NetLane.Flags.YieldStart;
				}
				flags2 |= flags3;
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
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_bezier = new Bezier3(vector3, b3, c, vector4);
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
				NetLane.Flags flags4 = (NetLane.Flags)instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags & ~NetLane.Flags.JoinedJunction;
				if (flag7) {
					flags4 |= NetLane.Flags.JoinedJunction;
				}
				instance.m_lanes.m_buffer[(int)((UIntPtr)num16)].m_flags = (ushort)flags4;
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
			// stock code end

			// NON-STOCK CODE START
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
			// NON-STOCK CODE END
		}

		public static void CustomGetTrafficLightNodeState(ushort nodeID, ref NetNode nodeData, ushort segmentID, ref NetSegment segmentData, ref NetNode.Flags flags, ref Color color) {
			TrafficLightSimulation nodeSim = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(nodeID) : null;
			bool customSim = nodeSim != null && nodeSim.IsSimulationActive();

			uint num = Singleton<SimulationManager>.instance.m_referenceFrameIndex - 15u;
			uint num2 = (uint)(((int)nodeID << 8) / 32768);
			uint num3 = num - num2 & 255u;
			RoadBaseAI.TrafficLightState trafficLightState;
			RoadBaseAI.TrafficLightState trafficLightState2;
			RoadBaseAI.GetTrafficLightState(nodeID, ref segmentData, num - num2, out trafficLightState, out trafficLightState2);
			color.a = 0.5f;
			switch (trafficLightState) {
				case RoadBaseAI.TrafficLightState.Green:
					color.g = 1f;
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (customSim) {
						color.r = 1f;
					} else {
						if (num3 < 45u) {
							color.g = 0f;
						} else if (num3 < 60u) {
							color.r = 1f;
						} else {
							color.g = 1f;
						}
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
					color.g = 0f;
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					if (customSim) {
						color.r = 1f;
					} else {
						if (num3 < 45u) {
							color.r = 1f;
						} else {
							color.g = 0f;
						}
					}
					break;
			}
			switch (trafficLightState2) {
				case RoadBaseAI.TrafficLightState.Green:
					color.b = 1f;
					break;
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (customSim) {
						color.b = 0f;
					} else {
						if (num3 < 45u) {
							color.b = 0f;
						} else {
							color.b = 1f;
						}
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
					color.b = 0f;
					break;
				case RoadBaseAI.TrafficLightState.GreenToRed:
					if (customSim) {
						color.b = 0f;
					} else {
						if (num3 < 45u) {
							if ((num3 / 8u & 1u) == 1u) {
								color.b = 1f;
							}
						} else {
							color.b = 0f;
						}
					}
					break;
			}
		}

		#region stock code

		protected void CheckBuildings(ushort segmentID, ref NetSegment data) {
			Log.Error("CustomRoadAI.CheckBuildings called.");
		}

		public void OriginalSimulationStep(ushort nodeID, ref NetNode data) { // pure stock code
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
					instance.m_outsideNodeNotConnected.Activate(properties.m_outsideNotConnected, nodeID, Notification.Problem.RoadNotConnected, false);
				}
			}
		}

		public void OriginalSimulationStep(ushort segmentID, ref NetSegment data) { // stock + custom code
			// base.SimulationStep START
			NetManager netManager = Singleton<NetManager>.instance;
			Vector3 startNodePos = netManager.m_nodes.m_buffer[data.m_startNode].m_position;
			Vector3 endNodePos = netManager.m_nodes.m_buffer[data.m_endNode].m_position;

			if (this.HasMaintenanceCost(segmentID, ref data)) {
				int maintenanceCost = this.GetMaintenanceCost(startNodePos, endNodePos);
				bool simulate = (ulong)(Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8 & 15u) == (ulong)((long)(segmentID & 15));
				if (maintenanceCost != 0) {
					if (simulate) {
						maintenanceCost = maintenanceCost * 16 / 100 - maintenanceCost / 100 * 15;
					} else {
						maintenanceCost /= 100;
					}
					Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.Maintenance, maintenanceCost, this.m_info.m_class);
				}
				if (simulate) {
					float startNodeElevation = (float)netManager.m_nodes.m_buffer[data.m_startNode].m_elevation;
					float endNodeElevation = (float)netManager.m_nodes.m_buffer[data.m_endNode].m_elevation;
					if (this.IsUnderground()) {
						startNodeElevation = -startNodeElevation;
						endNodeElevation = -endNodeElevation;
					}
					int constructionCost = this.GetConstructionCost(startNodePos, endNodePos, startNodeElevation, endNodeElevation);
					if (constructionCost != 0) {
						StatisticBase statisticBase = Singleton<StatisticsManager>.instance.Acquire<StatisticInt64>(StatisticType.CityValue);
						if (statisticBase != null) {
							statisticBase.Add(constructionCost);
						}
					}
				}
			}
			// base.SimulationStep END

			SimulationManager simManager = Singleton<SimulationManager>.instance;
			Notification.Problem problem = Notification.RemoveProblems(data.m_problems, Notification.Problem.Flood | Notification.Problem.Snow);
			if ((data.m_flags & NetSegment.Flags.AccessFailed) != NetSegment.Flags.None && Singleton<SimulationManager>.instance.m_randomizer.Int32(16u) == 0) {
				data.m_flags &= ~NetSegment.Flags.AccessFailed;
			}
			float totalLength = 0f;
			uint curLaneId = data.m_lanes;
			int laneIndex = 0;
			while (laneIndex < this.m_info.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane lane = this.m_info.m_lanes[laneIndex];
				if ((byte)(lane.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0 && (lane.m_vehicleType & ~VehicleInfo.VehicleType.Bicycle) != VehicleInfo.VehicleType.None) {
					totalLength += netManager.m_lanes.m_buffer[curLaneId].m_length;
				}
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				laneIndex++;
			}

			int trafficDensity = 0;
			if (data.m_trafficBuffer == 65535) {
				if ((data.m_flags & NetSegment.Flags.Blocked) == NetSegment.Flags.None) {
					data.m_flags |= NetSegment.Flags.Blocked;
					data.m_modifiedIndex = simManager.m_currentBuildIndex++;
				}
			} else {
				data.m_flags &= ~NetSegment.Flags.Blocked;
				int lengthDenominator = Mathf.RoundToInt(totalLength) << 4;
				if (lengthDenominator != 0) {
					trafficDensity = (int)((byte)Mathf.Min((int)(data.m_trafficBuffer * 100) / lengthDenominator, 100));
				}
			}
			data.m_trafficBuffer = 0;
			if (trafficDensity > (int)data.m_trafficDensity) {
				data.m_trafficDensity = (byte)Mathf.Min((int)(data.m_trafficDensity + 5), trafficDensity);
			} else if (trafficDensity < (int)data.m_trafficDensity) {
				data.m_trafficDensity = (byte)Mathf.Max((int)(data.m_trafficDensity - 5), trafficDensity);
			}
			//Vector3 startNodePos = netManager.m_nodes.m_buffer[(int)data.m_startNode].m_position;
			//Vector3 endNodePos = netManager.m_nodes.m_buffer[(int)data.m_endNode].m_position;
			Vector3 middlePoint = (startNodePos + endNodePos) * 0.5f;
			bool flooded = false;
			if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == 0) {
				float waterLevelAtMiddlePoint = Singleton<TerrainManager>.instance.WaterLevel(VectorUtils.XZ(middlePoint));
				// NON-STOCK CODE START
				// Rainfall compatibility
				float _roadwayFloodedTolerance = LoadingExtension.IsRainfallLoaded ? (float)PlayerPrefs.GetInt("RF_RoadwayFloodedTolerance", 100)/100f : 1f;
				if (waterLevelAtMiddlePoint > middlePoint.y + _roadwayFloodedTolerance && waterLevelAtMiddlePoint > 0f) {
					flooded = true;
					data.m_flags |= NetSegment.Flags.Flooded;
					problem = Notification.AddProblems(problem, Notification.Problem.Flood | Notification.Problem.MajorProblem);
					Vector3 min = data.m_bounds.min;
					Vector3 max = data.m_bounds.max;
					RoadBaseAI.FloodParkedCars(min.x, min.z, max.x, max.z);
				} else {
					data.m_flags &= ~NetSegment.Flags.Flooded;
					float _roadwayFloodingTolerance = LoadingExtension.IsRainfallLoaded ? (float)PlayerPrefs.GetInt("RF_RoadwayFloodingTolerance", 50)/100f : 0f;
					if (waterLevelAtMiddlePoint > middlePoint.y + _roadwayFloodingTolerance && waterLevelAtMiddlePoint > 0f) {
						flooded = true;
						problem = Notification.AddProblems(problem, Notification.Problem.Flood);
					}
				}
				// NON-STOCK CODE END
			}
			DistrictManager districtManager = Singleton<DistrictManager>.instance;
			byte districtId = districtManager.GetDistrict(middlePoint);
			DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[(int)districtId].m_cityPlanningPolicies;
			int noisePollution = (int)(100 - (data.m_trafficDensity - 100) * (data.m_trafficDensity - 100) / 100);
			if ((this.m_info.m_vehicleTypes & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
				if ((this.m_info.m_setVehicleFlags & Vehicle.Flags.Underground) == 0) {
					if (flooded && (data.m_flags & (NetSegment.Flags.AccessFailed | NetSegment.Flags.Blocked)) == NetSegment.Flags.None && simManager.m_randomizer.Int32(10u) == 0) {
						TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
						offer.Priority = 4;
						offer.NetSegment = segmentID;
						offer.Position = middlePoint;
						offer.Amount = 1;
						Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.FloodWater, offer);
					}

					int wetness = (int)data.m_wetness;
					if (!netManager.m_treatWetAsSnow) {
						if (flooded) {
							wetness = 255;
						} else {
							int wetnessDelta = -(wetness + 63 >> 5);
							float rainIntensity = Singleton<WeatherManager>.instance.SampleRainIntensity(middlePoint, false);
							if (rainIntensity != 0f) {
								int wetnessIncreaseDueToRain = Mathf.RoundToInt(Mathf.Min(rainIntensity * 4000f, 1000f));
								wetnessDelta += simManager.m_randomizer.Int32(wetnessIncreaseDueToRain, wetnessIncreaseDueToRain + 99) / 100;
							}
							wetness = Mathf.Clamp(wetness + wetnessDelta, 0, 255);
						}
					} else if (this.m_accumulateSnow) {
						if (flooded) {
							wetness = 128;
						} else {
							float rainIntensity = Singleton<WeatherManager>.instance.SampleRainIntensity(middlePoint, false);
							if (rainIntensity != 0f) {
								int minWetnessDelta = Mathf.RoundToInt(rainIntensity * 400f);
								int wetnessDelta = simManager.m_randomizer.Int32(minWetnessDelta, minWetnessDelta + 99) / 100;
								if (Singleton<UnlockManager>.instance.Unlocked(UnlockManager.Feature.Snowplow)) {
									wetness = Mathf.Min(wetness + wetnessDelta, 255);
								} else {
									wetness = Mathf.Min(wetness + wetnessDelta, 128);
								}
							} else if (Singleton<SimulationManager>.instance.m_randomizer.Int32(4u) == 0) {
								wetness = Mathf.Max(wetness - 1, 0);
							}
							if (wetness >= 64 && (data.m_flags & (NetSegment.Flags.AccessFailed | NetSegment.Flags.Blocked | NetSegment.Flags.Flooded)) == NetSegment.Flags.None && simManager.m_randomizer.Int32(10u) == 0) {
								TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
								offer.Priority = wetness / 50;
								offer.NetSegment = segmentID;
								offer.Position = middlePoint;
								offer.Amount = 1;
								Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Snow, offer);
							}
							if (wetness >= 192) {
								problem = Notification.AddProblems(problem, Notification.Problem.Snow);
							}
							districtManager.m_districts.m_buffer[districtId].m_productionData.m_tempSnowCover += (uint)wetness;
						}
					}
					if (wetness != (int)data.m_wetness) {
						if (Mathf.Abs((int)data.m_wetness - wetness) > 10) {
							data.m_wetness = (byte)wetness;
							InstanceID empty = InstanceID.Empty;
							empty.NetSegment = segmentID;
							netManager.AddSmoothColor(empty);
							empty.NetNode = data.m_startNode;
							netManager.AddSmoothColor(empty);
							empty.NetNode = data.m_endNode;
							netManager.AddSmoothColor(empty);
						} else {
							data.m_wetness = (byte)wetness;
							netManager.m_wetnessChanged = 256;
						}
					}
				}
				int minConditionDelta;
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) != DistrictPolicies.CityPlanning.None) {
					noisePollution = noisePollution * 3 + 1 >> 1;
					minConditionDelta = Mathf.Min(700, (int)(50 + data.m_trafficDensity * 6));
				} else {
					minConditionDelta = Mathf.Min(500, (int)(50 + data.m_trafficDensity * 4));
				}
				if (!this.m_highwayRules) {
					int conditionDelta = simManager.m_randomizer.Int32(minConditionDelta, minConditionDelta + 99) / 100;
					data.m_condition = (byte)Mathf.Max((int)data.m_condition - conditionDelta, 0);
					if (data.m_condition < 192 && (data.m_flags & (NetSegment.Flags.AccessFailed | NetSegment.Flags.Blocked | NetSegment.Flags.Flooded)) == NetSegment.Flags.None && simManager.m_randomizer.Int32(20u) == 0) {
						TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
						offer.Priority = (int)((255 - data.m_condition) / 50);
						offer.NetSegment = segmentID;
						offer.Position = middlePoint;
						offer.Amount = 1;
						Singleton<TransferManager>.instance.AddIncomingOffer(TransferManager.TransferReason.RoadMaintenance, offer);
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
			int noisePollutionResource = this.m_noiseAccumulation * noisePollution / 100;
			if (noisePollutionResource != 0) {
				float distance = Vector3.Distance(startNodePos, endNodePos);
				int relNoiseRadius = Mathf.FloorToInt(distance / this.m_noiseRadius);
				for (int i = 0; i < relNoiseRadius; i++) {
					Vector3 position3 = Vector3.Lerp(startNodePos, endNodePos, (float)(i + 1) / (float)(relNoiseRadius + 1));
					Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, noisePollutionResource, position3, this.m_noiseRadius);
				}
			}
			if (data.m_trafficDensity >= 50 && data.m_averageLength < 25f && (netManager.m_nodes.m_buffer[(int)data.m_startNode].m_flags & (NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights)) == NetNode.Flags.TrafficLights && (netManager.m_nodes.m_buffer[(int)data.m_endNode].m_flags & (NetNode.Flags.LevelCrossing | NetNode.Flags.TrafficLights)) == NetNode.Flags.TrafficLights) {
				GuideController guideCtrl = Singleton<GuideManager>.instance.m_properties;
				if (guideCtrl != null) {
					Singleton<NetManager>.instance.m_shortRoadTraffic.Activate(guideCtrl.m_shortRoadTraffic, segmentID, false);
				}
			}
			if ((data.m_flags & NetSegment.Flags.Collapsed) != NetSegment.Flags.None) {
				GuideController guideCtrl = Singleton<GuideManager>.instance.m_properties;
				if (guideCtrl != null) {
					Singleton<NetManager>.instance.m_roadDestroyed.Activate(guideCtrl.m_roadDestroyed, segmentID, false);
					Singleton<NetManager>.instance.m_roadDestroyed2.Activate(guideCtrl.m_roadDestroyed2, this.m_info.m_class.m_service);
				}
				if ((ulong)(simManager.m_currentFrameIndex >> 8 & 15u) == (ulong)((long)(segmentID & 15))) {
					int delta = Mathf.RoundToInt(data.m_averageLength);
					StatisticBase statisticBase = Singleton<StatisticsManager>.instance.Acquire<StatisticInt32>(StatisticType.DestroyedLength);
					statisticBase.Add(delta);
				}
			}
			data.m_problems = problem;
		}
#endregion
	}
}

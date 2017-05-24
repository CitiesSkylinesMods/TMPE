using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.ExtCitizenInstance;
using static TrafficManager.Traffic.PrioritySegment;

namespace TrafficManager.Manager {
	public class VehicleBehaviorManager : AbstractCustomManager {
		private static PathUnit.Position DUMMY_POS = default(PathUnit.Position);
		public const int MAX_PRIORITY_WAIT_TIME = 50;

		public static readonly VehicleBehaviorManager Instance = new VehicleBehaviorManager();

		private VehicleBehaviorManager() {

		}

		/// <summary>
		/// Checks for traffic lights and priority signs when changing segments (for rail vehicles).
		/// Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change is not allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="lastFrameData">last frame data of vehicle</param>
		/// <param name="isRecklessDriver">if true, this vehicle ignores red traffic lights and priority signs</param>
		/// <param name="prevPos">previous path position</param>
		/// <param name="prevTargetNodeId">previous target node</param>
		/// <param name="prevLaneID">previous lane</param>
		/// <param name="position">current path position</param>
		/// <param name="targetNodeId">transit node</param>
		/// <param name="laneID">current lane</param>
		/// <param name="maxSpeed">maximum allowed speed (only valid if method returns false)</param>
		/// <returns>true, if the vehicle may change segments, false otherwise.</returns>
		public bool MayChangeSegment(ushort vehicleId, ref Vehicle vehicleData, ref Vehicle.Frame lastFrameData, bool isRecklessDriver, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID, out float maxSpeed, bool debug = false) {
			return MayChangeSegment(vehicleId, ref vehicleData, ref lastFrameData, isRecklessDriver, ref prevPos, ref prevSegment, prevTargetNodeId, prevLaneID, ref position, targetNodeId, ref targetNode, laneID, ref DUMMY_POS, 0, out maxSpeed, debug);
		}

		/// <summary>
		/// Checks for traffic lights and priority signs when changing segments (for road & rail vehicles).
		/// Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change is not allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="lastFrameData">last frame data of vehicle</param>
		/// <param name="isRecklessDriver">if true, this vehicle ignores red traffic lights and priority signs</param>
		/// <param name="prevPos">previous path position</param>
		/// <param name="prevTargetNodeId">previous target node</param>
		/// <param name="prevLaneID">previous lane</param>
		/// <param name="position">current path position</param>
		/// <param name="targetNodeId">transit node</param>
		/// <param name="laneID">current lane</param>
		/// <param name="nextPosition">next path position</param>
		/// <param name="nextTargetNodeId">next target node</param>
		/// <param name="maxSpeed">maximum allowed speed (only valid if method returns false)</param>
		/// <returns>true, if the vehicle may change segments, false otherwise.</returns>
		public bool MayChangeSegment(ushort vehicleId, ref Vehicle vehicleData, ref Vehicle.Frame lastFrameData, bool isRecklessDriver, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID, ref PathUnit.Position nextPosition, ushort nextTargetNodeId, out float maxSpeed, bool debug = false) {
			debug = false;
			if (prevTargetNodeId != targetNodeId) {
				// method should only be called if targetNodeId == prevTargetNode
				maxSpeed = 0f;
				return true;
			}

			VehicleState vehicleState = null;
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				vehicleState = vehStateManager.GetVehicleState(vehicleId);
			}

			if (Options.simAccuracy >= 2 && (Options.prioritySignsEnabled || Options.timedLightsEnabled)) {
				try {
					vehStateManager.UpdateVehiclePos(vehicleId, ref vehicleData, ref prevPos, ref position);
				} catch (Exception e) {
					Log.Error("VehicleAI MayChangeSegment Error: " + e.ToString());
				}
			}

			var netManager = Singleton<NetManager>.instance;

			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint prevTargetNodeLower8Bits = (uint)((prevTargetNodeId << 8) / 32768);
			uint random = currentFrameIndex - prevTargetNodeLower8Bits & 255u;

			bool isRailVehicle = (vehicleData.Info.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
			bool isMonorail = vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail;

			NetNode.Flags targetNodeFlags = targetNode.m_flags;
			bool hasActiveTimedSimulation = (Options.timedLightsEnabled && TrafficLightSimulationManager.Instance.HasActiveTimedSimulation(targetNodeId));
			bool hasTrafficLightFlag = (targetNodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
			bool hasTrafficLight = hasTrafficLightFlag || hasActiveTimedSimulation;
			if (hasActiveTimedSimulation && ! hasTrafficLightFlag) {
				TrafficLightManager.Instance.AddTrafficLight(targetNodeId, ref targetNode);
			}
			bool hasStockYieldSign = false;
			float sqrSpeed = lastFrameData.m_velocity.sqrMagnitude;
			bool checkTrafficLights = hasActiveTimedSimulation;
			bool isTargetStartNode = prevSegment.m_startNode == targetNodeId;
			if (!isRailVehicle) {
				// check if to check space

#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} is not a train.");
#endif

				var prevLaneFlags = (NetLane.Flags)netManager.m_lanes.m_buffer[prevLaneID].m_flags;
				var hasCrossing = (targetNodeFlags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
				var isJoinedJunction = (prevLaneFlags & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
				hasStockYieldSign = (prevLaneFlags & (NetLane.Flags.YieldStart | NetLane.Flags.YieldEnd)) != NetLane.Flags.None && (targetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction;
				bool checkSpace = !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, isTargetStartNode) && !isRecklessDriver;

				//TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(destinationNodeId);
				//if (timedNode != null && timedNode.vehiclesMayEnterBlockedJunctions) {
				//	checkSpace = false;
				//}

				// stock priority signs
				if (/*!Options.prioritySignsEnabled &&*/ hasStockYieldSign && sqrSpeed > 0.01f && (vehicleData.m_flags & Vehicle.Flags.Emergency2) == (Vehicle.Flags)0) {
					maxSpeed = 0f;
					return false;
				}

				if (checkSpace) {
					// check if there is enough space
					if ((targetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction &&
						targetNode.CountSegments() != 2) {
						var len = vehicleData.CalculateTotalLength(vehicleId) + 2f;
						if (!netManager.m_lanes.m_buffer[laneID].CheckSpace(len)) {
							var sufficientSpace = false;
							if (nextPosition.m_segment != 0 && netManager.m_lanes.m_buffer[laneID].m_length < 30f) {
								NetNode.Flags nextTargetNodeFlags = netManager.m_nodes.m_buffer[nextTargetNodeId].m_flags;
								if ((nextTargetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction ||
									netManager.m_nodes.m_buffer[nextTargetNodeId].CountSegments() == 2) {
									uint nextLaneId = PathManager.GetLaneID(nextPosition);
									if (nextLaneId != 0u) {
										sufficientSpace = netManager.m_lanes.m_buffer[nextLaneId].CheckSpace(len);
									}
								}
							}
							if (!sufficientSpace) {
								maxSpeed = 0f;
								try {
									if (vehicleState != null) {
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to BLOCKED");
#endif

										vehicleState.JunctionTransitState = VehicleJunctionTransitState.Blocked;
									}
								} catch (Exception e) {
									Log.Error("VehicleAI MayChangeSegment error while setting junction state to BLOCKED: " + e.ToString());
								}
								return false;
							}
						}
					}
				}

				checkTrafficLights = checkTrafficLights || (!isJoinedJunction || hasCrossing);
			} else {
#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} is a train/monorail.");
#endif

				if (!isMonorail) {
					checkTrafficLights = true;
				}
			}

			try {
				if (vehicleState != null && vehicleState.JunctionTransitState == VehicleJunctionTransitState.Blocked) {
#if DEBUG
					if (debug)
						Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState from BLOCKED to ENTER");
#endif
					vehicleState.JunctionTransitState = VehicleJunctionTransitState.Enter;
				}

				if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == 0) {
					if (hasTrafficLight && checkTrafficLights) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomVehicleAI.MayChangeSegment: Node {targetNodeId} has a traffic light.");
#endif

						var destinationInfo = targetNode.Info;

						if (vehicleState != null && vehicleState.JunctionTransitState == VehicleJunctionTransitState.None) {
#if DEBUG
							if (debug)
								Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to ENTER (1)");
#endif
							vehicleState.JunctionTransitState = VehicleJunctionTransitState.Enter;
						}

						RoadBaseAI.TrafficLightState vehicleLightState;
						RoadBaseAI.TrafficLightState pedestrianLightState;
						bool vehicles;
						bool pedestrians;
						CustomRoadAI.GetTrafficLightState(vehicleId, ref vehicleData, targetNodeId, prevPos.m_segment, prevPos.m_lane, position.m_segment, ref prevSegment, currentFrameIndex - prevTargetNodeLower8Bits, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);

						if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car && isRecklessDriver) { // TODO no reckless driving at railroad crossings
							vehicleLightState = RoadBaseAI.TrafficLightState.Green;
						}

#if DEBUG
						if (debug)
							Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} has {vehicleLightState} at node {targetNodeId}");
#endif

						if (!vehicles && random >= 196u) {
							vehicles = true;
							RoadBaseAI.SetTrafficLightState(targetNodeId, ref prevSegment, currentFrameIndex - prevTargetNodeLower8Bits, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
						}

						var stopCar = false;
						switch (vehicleLightState) {
							case RoadBaseAI.TrafficLightState.RedToGreen:
								if (random < 60u) {
									stopCar = true;
								} else {
#if DEBUG
									if (debug)
										Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (RedToGreen)");
#endif
									if (vehicleState != null)
										vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
								}
								break;
							case RoadBaseAI.TrafficLightState.Red:
								stopCar = true;
								break;
							case RoadBaseAI.TrafficLightState.GreenToRed:
								if (random >= 30u) {
									stopCar = true;
								} else if (vehicleState != null) {
#if DEBUG
									if (debug)
										Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (GreenToRed)");
#endif
									vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
								}
								break;
						}

						/*if ((vehicleLightState == RoadBaseAI.TrafficLightState.Green || vehicleLightState == RoadBaseAI.TrafficLightState.RedToGreen) && !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, prevSegment.m_startNode == targetNodeId)) {
							var hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, targetNodeId);

							if (hasIncomingCars) {
								// green light but other cars are incoming and they have priority: stop
								stopCar = true;
							}
						}*/

						if (stopCar) {
							if (vehicleState != null) {
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP");
#endif
								vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;
							}
							maxSpeed = 0f;
							return false;
						}
					} else if (!isMonorail && vehicleState != null && Options.prioritySignsEnabled) {
						TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

#if DEBUG
						//bool debug = destinationNodeId == 10864;
						//bool debug = destinationNodeId == 13531;
						//bool debug = false;// targetNodeId == 5027;
#endif
						//bool debug = false;
#if DEBUG
						if (debug)
							Log._Debug($"Vehicle {vehicleId} is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {targetNodeId} which is not a traffic light.");
#endif

						var sign = prioMan.GetPrioritySign(prevPos.m_segment, isTargetStartNode);
						if (sign != PrioritySegment.PriorityType.None) {
#if DEBUG
							if (debug)
								Log._Debug($"Vehicle {vehicleId} is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {targetNodeId} which is not a traffic light and is a priority segment.");
#endif
							//if (prioritySegment.HasVehicle(vehicleId)) {
#if DEBUG
							if (debug)
								Log._Debug($"Vehicle {vehicleId}: segment target position found");
#endif
#if DEBUG
							if (debug)
								Log._Debug($"Vehicle {vehicleId}: global target position found. carState = {vehicleState.JunctionTransitState.ToString()}");
#endif
							var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
							var frame = currentFrameIndex2 >> 4;

							if (vehicleState.JunctionTransitState == VehicleJunctionTransitState.None) {
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to ENTER (prio)");
#endif
								vehicleState.JunctionTransitState = VehicleJunctionTransitState.Enter;
							}

							if (vehicleState.JunctionTransitState != VehicleJunctionTransitState.Leave) {
								bool hasIncomingCars;
								switch (sign) {
									case PriorityType.Stop:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: STOP sign. waittime={vehicleState.WaitTime}, sqrSpeed={sqrSpeed}");
#endif

										if (Options.simAccuracy <= 2 || (Options.simAccuracy >= 3 && vehicleState.WaitTime < MAX_PRIORITY_WAIT_TIME)) {
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											if (sqrSpeed <= TrafficPriorityManager.MAX_SQR_STOP_VELOCITY) {
												vehicleState.WaitTime++;

												float minStopWaitTime = UnityEngine.Random.Range(0f, 3f);
												if (vehicleState.WaitTime >= minStopWaitTime) {
													if (Options.simAccuracy >= 4) {
														vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
													} else {
														hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevSegment, targetNodeId, ref targetNode, ref prevPos, ref position);
#if DEBUG
														if (debug)
															Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

														if (hasIncomingCars) {
															maxSpeed = 0f;
															return false;
														}
#if DEBUG
														if (debug)
															Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (min wait timeout)");
#endif
														vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
													}
												} else {
													maxSpeed = 0;
													return false;
												}
											} else {
												vehicleState.WaitTime = 0;
												maxSpeed = 0f;
												return false;
											}
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (max wait timeout)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
										}
										break;
									case PriorityType.Yield:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: YIELD sign. waittime={vehicleState.WaitTime}");
#endif

										if (Options.simAccuracy <= 2 || (Options.simAccuracy >= 3 && vehicleState.WaitTime < MAX_PRIORITY_WAIT_TIME)) {
											vehicleState.WaitTime++;
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											if (sqrSpeed <= TrafficPriorityManager.MAX_SQR_YIELD_VELOCITY || Options.simAccuracy <= 2) {
												if (Options.simAccuracy >= 4) {
													vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
												} else {
													hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevSegment, targetNodeId, ref targetNode, ref prevPos, ref position);
#if DEBUG
													if (debug)
														Log._Debug($"Vehicle {vehicleId}: hasIncomingCars: {hasIncomingCars}");
#endif

													if (hasIncomingCars) {
														maxSpeed = 0f;
														return false;
													} else {
#if DEBUG
														if (debug)
															Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (no incoming cars)");
#endif
														vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
													}
												}
											} else {
#if DEBUG
												if (debug)
													Log._Debug($"Vehicle {vehicleId}: Vehicle has not yet reached yield speed (reduce {sqrSpeed} by {vehicleState.ReduceSqrSpeedByValueToYield})");
#endif

												// vehicle has not yet reached yield speed
												maxSpeed = TrafficPriorityManager.MAX_YIELD_VELOCITY;
												return false;
											}
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (max wait timeout)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
										}
										break;
									case PriorityType.Main:
									default:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: MAIN sign. waittime={vehicleState.WaitTime}");
#endif
										maxSpeed = 0f;

										if (Options.simAccuracy == 4)
											return true;

										if (Options.simAccuracy <= 2 || (Options.simAccuracy == 3 && vehicleState.WaitTime < MAX_PRIORITY_WAIT_TIME)) {
											vehicleState.WaitTime++;
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevSegment, targetNodeId, ref targetNode, ref prevPos, ref position);
#if DEBUG
											if (debug)
												Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

											if (hasIncomingCars) {
												return false;
											}
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to LEAVE (no conflicting car)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
										}
										return true;
								}
							} else if (sqrSpeed <= TrafficPriorityManager.MAX_SQR_STOP_VELOCITY) {
								// vehicle is not moving. reset allowance to leave junction
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState from LEAVE to BLOCKED (speed to low)");
#endif
								vehicleState.JunctionTransitState = VehicleJunctionTransitState.Blocked;

								maxSpeed = 0f;
								return false;
							}
						}
					}
				}
			} catch (Exception e) {
				Log.Error($"Error occured in MayChangeSegment: {e.ToString()}");
			}
			maxSpeed = 0f; // maxSpeed should be set by caller
			return true;
		}

		public float CalcMaxSpeed(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, Vector3 pos, float maxSpeed, bool isRecklessDriver) {
			var netManager = Singleton<NetManager>.instance;
			NetInfo segmentInfo = netManager.m_segments.m_buffer[(int)position.m_segment].Info;
			bool highwayRules = (segmentInfo.m_netAI is RoadBaseAI && ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules);
			VehicleState state = VehicleStateManager.Instance.GetVehicleState(vehicleId);

			if (!highwayRules) {
				if (netManager.m_treatWetAsSnow) {
					DistrictManager districtManager = Singleton<DistrictManager>.instance;
					byte district = districtManager.GetDistrict(pos);
					DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPolicies;
					if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) != DistrictPolicies.CityPlanning.None) {
						// NON-STOCK CODE START
						if (Options.strongerRoadConditionEffects) {
							if (maxSpeed > VehicleStateManager.ICY_ROADS_STUDDED_MIN_SPEED)
								maxSpeed = VehicleStateManager.ICY_ROADS_STUDDED_MIN_SPEED + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - VehicleStateManager.ICY_ROADS_STUDDED_MIN_SPEED);
						} else {
							// NON-STOCK CODE END
							maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
																																	   // NON-STOCK CODE START
						}
						// NON-STOCK CODE END
						districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPoliciesEffect |= DistrictPolicies.CityPlanning.StuddedTires;
					} else {
						// NON-STOCK CODE START
						if (Options.strongerRoadConditionEffects) {
							if (maxSpeed > VehicleStateManager.ICY_ROADS_MIN_SPEED)
								maxSpeed = VehicleStateManager.ICY_ROADS_MIN_SPEED + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - VehicleStateManager.ICY_ROADS_MIN_SPEED);
						} else {
							// NON-STOCK CODE END
							maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.00117647066f; // vanilla: -30% .. ±0%
																																		// NON-STOCK CODE START
						}
						// NON-STOCK CODE END
					}
				} else {
					// NON-STOCK CODE START
					if (Options.strongerRoadConditionEffects) {
						float minSpeed = Math.Min(maxSpeed * VehicleStateManager.WET_ROADS_FACTOR, VehicleStateManager.WET_ROADS_MAX_SPEED);
						if (maxSpeed > minSpeed)
							maxSpeed = minSpeed + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - minSpeed);
					} else {
						// NON-STOCK CODE END
						maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
																																   // NON-STOCK CODE START
					}
					// NON-STOCK CODE END
				}

				// NON-STOCK CODE START
				if (Options.strongerRoadConditionEffects) {
					float minSpeed = Math.Min(maxSpeed * VehicleStateManager.BROKEN_ROADS_FACTOR, VehicleStateManager.BROKEN_ROADS_MAX_SPEED);
					if (maxSpeed > minSpeed) {
						maxSpeed = minSpeed + (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_condition * 0.0039215686f * (maxSpeed - minSpeed);
					}
				} else {
					// NON-STOCK CODE END
					maxSpeed *= 1f + (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_condition * 0.0005882353f; // vanilla: ±0% .. +15 %
																																 // NON-STOCK CODE START
				}
				// NON-STOCK CODE END
			}

			// NON-STOCK CODE START
			if (Options.realisticSpeeds) {
				float vehicleRand = Math.Min(1f, (float)(vehicleId % 100) * 0.01f);
				if (state != null && state.HeavyVehicle)
					maxSpeed *= 0.9f + vehicleRand * 0.1f; // a little variance, 0.85 .. 1
				else if (isRecklessDriver)
					maxSpeed *= 1.3f + vehicleRand * 0.7f; // woohooo, 1.3 .. 2
				else
					maxSpeed *= 0.7f + vehicleRand * 0.6f; // a little variance, 0.7 .. 1.3
			} else {
				if (isRecklessDriver)
					maxSpeed *= 1.4f;
			}

			maxSpeed = Math.Max(VehicleStateManager.MIN_SPEED, maxSpeed); // at least 10 km/h
																		  // NON-STOCK CODE END

			return maxSpeed;
		}
	}
}

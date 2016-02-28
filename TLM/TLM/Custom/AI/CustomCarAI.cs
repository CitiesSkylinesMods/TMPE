#define DEBUGVx

using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;

namespace TrafficManager.Custom.AI {
	internal class CustomCarAI : CarAI { // correct would be to inherit from VehicleAI (in order to keep the correct references to `base`)
		private const float FarLod = 1210000f;
		private const float CloseLod = 250000f;

		private static int[] closeLodUpdateMod = new int[] { 1, 2, 4, 6, 8 };
		private static int[] farLodUpdateMod = new int[] { 4, 6, 8, 10, 12 };
		private static int[] veryFarLodUpdateMod = new int[] { 8, 10, 12, 14, 16 };

		public static readonly int MaxPriorityWaitTime = 80;

		public void Awake() {
			
		}

		internal static void OnLevelUnloading() {

		}

		/// <summary>
		/// Lightweight simulation step method.
		/// This method is occasionally being called for different cars.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <param name="physicsLodRefPos"></param>
		public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None) {
				PathManager instance = Singleton<PathManager>.instance;
				byte pathFindFlags = instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
				if ((pathFindFlags & 4) != 0) {
					vehicleData.m_pathPositionIndex = 255;
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					vehicleData.m_flags &= ~Vehicle.Flags.Arriving;
					this.PathfindSuccess(vehicleId, ref vehicleData);
					this.TrySpawn(vehicleId, ref vehicleData);
				} else if ((pathFindFlags & 8) != 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					this.PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
			} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
				this.TrySpawn(vehicleId, ref vehicleData);
			}

			try {
				CustomVehicleAI.HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], true, true);
			} catch (Exception e) {
				Log.Error("CarAI TrafficManagerSimulationStep Error: " + e.ToString());
			}

			Vector3 lastFramePosition = vehicleData.GetLastFramePosition();
			int lodPhysics;
			if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1210000f) {
				lodPhysics = 2;
			} else if (Vector3.SqrMagnitude(Singleton<SimulationManager>.instance.m_simulationView.m_position - lastFramePosition) >= 250000f) {
				lodPhysics = 1;
			} else {
				lodPhysics = 0;
			}
			this.SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);
			if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
				VehicleManager instance2 = Singleton<VehicleManager>.instance;
				ushort num = vehicleData.m_trailingVehicle;
				int num2 = 0;
				while (num != 0) {
					ushort trailingVehicle = instance2.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
					VehicleInfo info = instance2.m_vehicles.m_buffer[(int)num].Info;
					info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, lodPhysics);
					num = trailingVehicle;
					if (++num2 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
			int privateServiceIndex = ItemClass.GetPrivateServiceIndex(this.m_info.m_class.m_service);
			int maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) == Vehicle.Flags.None && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if ((int)vehicleData.m_blockCounter == maxBlockCounter && Options.enableDespawning) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if (vehicleData.m_leadingVehicle == 0 && CustomVehicleAI.ShouldRecalculatePath(vehicleId, ref vehicleData, maxBlockCounter)) {
				CustomVehicleAI.MarkPathRecalculation(vehicleId);
				InvalidPath(vehicleId, ref vehicleData, vehicleId, ref vehicleData);
			}
		}

		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
			PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID,
			byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			//var vehicleManager = Singleton<VehicleManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
			bool isRecklessDriver = IsRecklessDriver(vehicleId, ref vehicleData);

			var lastFrameData = vehicleData.GetLastFrameData();
			var lastFrameVehiclePos = lastFrameData.m_position;

			var camPos = Camera.main.transform.position;
			bool simulatePrioritySigns = (lastFrameVehiclePos - camPos).sqrMagnitude < FarLod && !isRecklessDriver;

			if (Options.simAccuracy <= 2) {
				if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
					VehiclePosition vehiclePos = TrafficPriority.GetVehiclePosition(vehicleId);
					if (vehiclePos.Valid && simulatePrioritySigns) { // TODO check if this should be !vehiclePos.Valid
						try {
							CustomVehicleAI.HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], false, false);
						} catch (Exception e) {
							Log.Error("CarAI TmCalculateSegmentPosition Error: " + e.ToString());
						}
					}
				} else {
					//Log._Debug($"TmCalculateSegmentPosition does not handle vehicles of type {vehicleData.Info.m_vehicleType}");
				}
			}

			// I think this is supposed to be the lane position?
			// [VN, 12/23/2015] It's the 3D car position on the Bezier curve of the lane.
			// This crazy 0.003921569f equals to 1f/255 and prevOffset is the byte value (0..255) of the car position.
			var vehiclePosOnBezier = netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].CalculatePosition(prevOffset * 0.003921569f);
			//ushort currentSegmentId = netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_segment;

			ushort destinationNodeId;
			ushort sourceNodeId;
			if (offset < position.m_offset) {
				destinationNodeId = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
				sourceNodeId = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
			} else {
				destinationNodeId = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
				sourceNodeId = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
			}
			var previousDestinationNode = prevOffset == 0 ? netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode : netManager.m_segments.m_buffer[prevPos.m_segment].m_endNode;

			// this seems to be like the required braking force in order to stop the vehicle within its half length.
			var crazyValue = 0.5f * lastFrameData.m_velocity.sqrMagnitude / m_info.m_braking + m_info.m_generatedInfo.m_size.z * 0.5f;

			// Essentially, this is true if the car has enough time and space to brake (e.g. for a red traffic light)
			if (destinationNodeId == previousDestinationNode) {
				if (Vector3.Distance(lastFrameVehiclePos, vehiclePosOnBezier) >= crazyValue - 1f) {
					var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
					var num5 = (uint)((previousDestinationNode << 8) / 32768);
					var num6 = currentFrameIndex - num5 & 255u;

					var nodeFlags = netManager.m_nodes.m_buffer[destinationNodeId].m_flags;
					var prevLaneFlags = (NetLane.Flags)netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_flags;
					var hasTrafficLight = (nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
					var hasCrossing = (nodeFlags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
					var isJoinedJunction = (prevLaneFlags & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
					bool checkSpace = !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode == destinationNodeId) && !isRecklessDriver;
					//TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(destinationNodeId);
					/*if (timedNode != null && timedNode.vehiclesMayEnterBlockedJunctions) {
						checkSpace = false;
					}*/

					if (checkSpace) {
						// check if there is enough space
						if ((nodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction &&
							netManager.m_nodes.m_buffer[destinationNodeId].CountSegments() != 2) {
							var len = vehicleData.CalculateTotalLength(vehicleId) + 2f;
							if (!netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(len)) {
								var sufficientSpace = false;
								if (nextPosition.m_segment != 0 &&
									netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_length < 30f) {
									var flags3 = netManager.m_nodes.m_buffer[sourceNodeId].m_flags;
									if ((flags3 &
										 (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) !=
										NetNode.Flags.Junction || netManager.m_nodes.m_buffer[sourceNodeId].CountSegments() == 2) {
										var laneId2 = PathManager.GetLaneID(nextPosition);
										if (laneId2 != 0u) {
											sufficientSpace = netManager.m_lanes.m_buffer[(int)((UIntPtr)laneId2)].CheckSpace(len);
										}
									}
								}
								if (!sufficientSpace) {
									maxSpeed = 0f;
									return;
								}
							}
						}
					}

					try {
						VehiclePosition globalTargetPos = TrafficPriority.GetVehiclePosition(vehicleId);

						if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None) {
							if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
								if (hasTrafficLight && (!isJoinedJunction || hasCrossing)) {
									var destinationInfo = netManager.m_nodes.m_buffer[destinationNodeId].Info;

									if (globalTargetPos.CarState == VehicleJunctionTransitState.None) {
										globalTargetPos.CarState = VehicleJunctionTransitState.Enter;
									}

									RoadBaseAI.TrafficLightState vehicleLightState;
									RoadBaseAI.TrafficLightState pedestrianLightState;
									bool vehicles;
									bool pedestrians;
									CustomRoadAI.GetTrafficLightState(vehicleId, ref vehicleData, destinationNodeId, prevPos.m_segment, position.m_segment, ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);

									if (isRecklessDriver && (destinationInfo.GetConnectionClass().m_service & ItemClass.Service.PublicTransport) == ItemClass.Service.None) { // no reckless driving at railroad crossings
										vehicleLightState = RoadBaseAI.TrafficLightState.Green;
									}

									if (!vehicles && num6 >= 196u) {
										vehicles = true;
										RoadBaseAI.SetTrafficLightState(destinationNodeId, ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
									}

									var stopCar = false;
									switch (vehicleLightState) {
										case RoadBaseAI.TrafficLightState.RedToGreen:
											if (num6 < 60u) {
												stopCar = true;
											} else {
												globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
											}
											break;
										case RoadBaseAI.TrafficLightState.Red:
											stopCar = true;
											break;
										case RoadBaseAI.TrafficLightState.GreenToRed:
											if (num6 >= 30u) {
												stopCar = true;
											} else {
												globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
											}
											break;
									}

									if ((vehicleLightState == RoadBaseAI.TrafficLightState.Green || vehicleLightState == RoadBaseAI.TrafficLightState.RedToGreen) && !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode == destinationNodeId)) {
										var hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);

										if (hasIncomingCars) {
											// green light but other cars are incoming and they have priority: stop
											stopCar = true;
										}
									}

									if (stopCar) {
										globalTargetPos.CarState = VehicleJunctionTransitState.Stop;
										maxSpeed = 0f;
										return;
									}
								} else if (simulatePrioritySigns) {
#if DEBUG
									//bool debug = destinationNodeId == 10864;
									//bool debug = destinationNodeId == 13531;
									bool debug = false;
#endif
									//bool debug = false;
#if DEBUG
									if (debug)
										Log._Debug($"Vehicle {vehicleId} is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {destinationNodeId} which is not a traffic light.");
#endif

									var prioritySegment = TrafficPriority.GetPrioritySegment(destinationNodeId, prevPos.m_segment);
									if (prioritySegment != null) {
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId} is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {destinationNodeId} which is not a traffic light and is a priority segment.");
#endif
										if (prioritySegment.HasVehicle(vehicleId)) {
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: segment target position found");
#endif
											if (globalTargetPos.Valid) {
#if DEBUG
												if (debug)
													Log._Debug($"Vehicle {vehicleId}: global target position found. carState = {globalTargetPos.CarState.ToString()}");
#endif
												var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
												var frame = currentFrameIndex2 >> 4;

												if (globalTargetPos.CarState == VehicleJunctionTransitState.None) {
													globalTargetPos.CarState = VehicleJunctionTransitState.Enter;
												}

												if (globalTargetPos.CarState != VehicleJunctionTransitState.Leave) {
													bool hasIncomingCars;
													switch (prioritySegment.Type) {
														case SegmentEnd.PriorityType.Stop:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: STOP sign. waittime={globalTargetPos.WaitTime}, vel={lastFrameData.m_velocity.magnitude}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.CarState = VehicleJunctionTransitState.Stop;

																if (lastFrameData.m_velocity.magnitude < 0.5f ||
																	globalTargetPos.Stopped) {
																	globalTargetPos.Stopped = true;
																	globalTargetPos.WaitTime++;

																	float minStopWaitTime = Random.Range(0f, 3f);
																	if (globalTargetPos.WaitTime >= minStopWaitTime) {
																		hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
#if DEBUG
																		if (debug)
																			Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

																		if (hasIncomingCars) {
																			maxSpeed = 0f;
																			return;
																		}
																		globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
																	} else {
																		maxSpeed = 0;
																		return;
																	}
																} else {
																	maxSpeed = 0f;
																	return;
																}
															} else {
																globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
															}
															break;
														case SegmentEnd.PriorityType.Yield:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: YIELD sign. waittime={globalTargetPos.WaitTime}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.WaitTime++;
																globalTargetPos.CarState = VehicleJunctionTransitState.Stop;
																hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
#if DEBUG
																if (debug)
																	Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif
																if (hasIncomingCars) {
																	if (lastFrameData.m_velocity.magnitude > 0) {
																		maxSpeed = Math.Max(0f, lastFrameData.m_velocity.magnitude - globalTargetPos.ReduceSpeedByValueToYield);
																	} else {
																		maxSpeed = 0;
																	}
#if DEBUG
																	/*if (TrafficPriority.Vehicles[vehicleId].ToNode == 8621)
																		Log.Message($"Vehicle {vehicleId} is yielding at node {destinationNodeId}. Speed: {maxSpeed}, Waiting time: {TrafficPriority.Vehicles[vehicleId].WaitTime}");*/
#endif
																	return;
																} else {
#if DEBUG
																	/*if (TrafficPriority.Vehicles[vehicleId].ToNode == 8621)
																		Log.Message($"Vehicle {vehicleId} is NOT yielding at node {destinationNodeId}.");*/
#endif
																	if (lastFrameData.m_velocity.magnitude > 0) {
																		maxSpeed = Math.Max(1f, lastFrameData.m_velocity.magnitude - globalTargetPos.ReduceSpeedByValueToYield * 0.5f);
																	}
																}
																globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
															} else {
																globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
															}
															break;
														case SegmentEnd.PriorityType.Main:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: MAIN sign. waittime={globalTargetPos.WaitTime}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.WaitTime++;
																globalTargetPos.CarState = VehicleJunctionTransitState.Stop;
																maxSpeed = 0f;

																hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
#if DEBUG
																if (debug)
																	Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

																if (hasIncomingCars) {
																	globalTargetPos.Stopped = true;
																	return;
																}
																globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
																globalTargetPos.Stopped = false;
															}

															var info3 = netManager.m_segments.m_buffer[position.m_segment].Info;
															if (info3.m_lanes != null && info3.m_lanes.Length > position.m_lane) {
																//maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, info3.m_lanes[position.m_lane].m_speedLimit, netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve) * 0.8f;
																maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info3.m_lanes[position.m_lane]), netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
															} else {
																maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
															}
															return;
													}
												} else {
													globalTargetPos.CarState = VehicleJunctionTransitState.Leave;
												}
											} else {
#if DEBUG
												if (debug)
													Log._Debug($"globalTargetPos is null! {vehicleId} @ seg. {prevPos.m_segment} @ node {destinationNodeId}");
#endif
											}
										} else {
#if DEBUG
											if (debug)
												Log._Debug($"targetPos is null! {vehicleId} @ seg. {prevPos.m_segment} @ node {destinationNodeId}");
#endif
										}
									}
								}
							}
						}
					} catch (Exception e) {
						Log.Error($"Error occured in TmCalculateSegmentPosition: {e.ToString()}");
					}
				}
			}

			var info2 = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info2.m_lanes != null && info2.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info2.m_lanes[position.m_lane]); // info2.m_lanes[position.m_lane].m_speedLimit;

#if DEBUG
				/*if (position.m_segment == 275) {
					Log._Debug($"Applying lane speed limit of {laneSpeedLimit} to lane {laneID} @ seg. {position.m_segment}");
                }*/
#endif

				/*if (TrafficRoadRestrictions.IsSegment(position.m_segment)) {
					var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

					if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f) {
						laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
					}
				}*/

				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			maxSpeed = CalcMaxSpeed(vehicleId, ref vehicleData, position, pos, maxSpeed, isRecklessDriver);
		}

		internal static readonly float MIN_SPEED = 8f * 0.2f; // 10 km/h
		internal static readonly float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h
		internal static readonly float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h
		internal static readonly float WET_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		internal static readonly float WET_ROADS_FACTOR = 0.75f;
		internal static readonly float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		internal static readonly float BROKEN_ROADS_FACTOR = 0.75f;

		internal static float CalcMaxSpeed(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, Vector3 pos, float maxSpeed, bool isRecklessDriver) {
			var netManager = Singleton<NetManager>.instance;
			NetInfo segmentInfo = netManager.m_segments.m_buffer[(int)position.m_segment].Info;
			bool highwayRules = (segmentInfo.m_netAI is RoadBaseAI && ((RoadBaseAI)segmentInfo.m_netAI).m_highwayRules);

			if (!highwayRules) {
				if (netManager.m_treatWetAsSnow) {
					DistrictManager districtManager = Singleton<DistrictManager>.instance;
					byte district = districtManager.GetDistrict(pos);
					DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPolicies;
					if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) != DistrictPolicies.CityPlanning.None) {
						if (Options.strongerRoadConditionEffects) {
							if (maxSpeed > ICY_ROADS_STUDDED_MIN_SPEED)
								maxSpeed = ICY_ROADS_STUDDED_MIN_SPEED + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - ICY_ROADS_STUDDED_MIN_SPEED);
						} else {
							maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
						}
						districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPoliciesEffect |= DistrictPolicies.CityPlanning.StuddedTires;
					} else {
						if (Options.strongerRoadConditionEffects) {
							if (maxSpeed > ICY_ROADS_MIN_SPEED)
								maxSpeed = ICY_ROADS_MIN_SPEED + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - ICY_ROADS_MIN_SPEED);
						} else {
							maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.00117647066f; // vanilla: -30% .. ±0%
						}
					}
				} else {
					if (Options.strongerRoadConditionEffects) {
						float minSpeed = Math.Min(maxSpeed * WET_ROADS_FACTOR, WET_ROADS_MAX_SPEED);
						if (maxSpeed > minSpeed)
							maxSpeed = minSpeed + (float)(255 - netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness) * 0.0039215686f * (maxSpeed - minSpeed);
					} else {
						maxSpeed *= 1f - (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
					}
				}

				if (Options.strongerRoadConditionEffects) {
					float minSpeed = Math.Min(maxSpeed * BROKEN_ROADS_FACTOR, BROKEN_ROADS_MAX_SPEED);
					if (maxSpeed > minSpeed) {
						maxSpeed = minSpeed + (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_condition * 0.0039215686f * (maxSpeed - minSpeed);
					}
				} else {
					maxSpeed *= 1f + (float)netManager.m_segments.m_buffer[(int)position.m_segment].m_condition * 0.0005882353f; // vanilla: ±0% .. +15 %
				}
			}

			ExtVehicleType? vehicleType = CustomVehicleAI.DetermineVehicleTypeFromVehicle(vehicleId, ref vehicleData);
			float vehicleRand = Math.Min(1f, (float)(vehicleId % 101) * 0.01f); // we choose 101 because it's a prime number
			if (isRecklessDriver)
				maxSpeed *= 1.5f + vehicleRand * 1.5f; // woohooo, 1.5 .. 3
			else if ((vehicleType & ExtVehicleType.PassengerCar) != ExtVehicleType.None)
				maxSpeed *= 0.8f + vehicleRand * 0.3f; // a little variance, 0.8 .. 1.1
			else if ((vehicleType & ExtVehicleType.Taxi) != ExtVehicleType.None)
				maxSpeed *= 0.9f + vehicleRand * 0.4f; // a little variance, 0.9 .. 1.3

			maxSpeed = Math.Max(MIN_SPEED, maxSpeed); // at least 10 km/h

			return maxSpeed;
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f,
				out pos, out dir);
			var info = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneId, info.m_lanes[position.m_lane]); //info.m_lanes[position.m_lane].m_speedLimit;
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,	netManager.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			maxSpeed = CalcMaxSpeed(vehicleId, ref vehicleData, position, pos, maxSpeed, IsRecklessDriver(vehicleId, ref vehicleData));
		}

		internal static bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None)
				return true;

			return ((vehicleData.Info.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) && (uint)vehicleId % (Options.getRecklessDriverModulo()) == 0;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			ExtVehicleType? vehicleType = CustomVehicleAI.DetermineVehicleTypeFromVehicle(vehicleID, ref vehicleData);
			/*if (vehicleType == null) {
				Log._Debug($"CustomCarAI.CustomStartPathFind: Could not determine ExtVehicleType from class type. typeof this={this.GetType().ToString()}");
			} else {
				Log._Debug($"CustomCarAI.CustomStartPathFind: vehicleType={vehicleType}. typeof this={this.GetType().ToString()}");
			}*/

			VehicleInfo info = this.m_info;
			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, undergroundTarget, false, 32f, out endPosA, out endPosB, out num3, out num4)) {
				if (!startBothWays || num < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num3 < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				bool res = false;
				if (vehicleType == null)
					res = Singleton<CustomPathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false);
				else
					res = Singleton<CustomPathManager>.instance.CreatePath((ExtVehicleType)vehicleType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false);
				if (res) {
					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}
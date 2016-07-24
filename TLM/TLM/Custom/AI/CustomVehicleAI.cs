#define EXTRAPFx
#define QUEUEDSTATSx
#define PATHRECALCx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomVehicleAI : VehicleAI {
		public static readonly int MaxPriorityWaitTime = 50;

		//private static readonly int MIN_BLOCK_COUNTER_PATH_RECALC_VALUE = 3;

		private static PathUnit.Position DUMMY_POS = default(PathUnit.Position);

		public void CustomReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			VehicleStateManager.OnReleaseVehicle(vehicleId, ref vehicleData);
		}

		public void CustomCalculateSegmentPosition(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			CalculateSegPos(vehicleID, ref vehicleData, position, laneID, offset, out pos, out dir, out maxSpeed);
		}

		protected virtual void CalculateSegPos(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]);
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

#if PATHRECALC
		public static bool ShouldRecalculatePath(ushort vehicleId, ref Vehicle vehicleData, int maxBlockCounter, out ushort segmentId) {
			segmentId = 0;
			if (! Options.IsDynamicPathRecalculationActive())
				return false;
			if (Options.simAccuracy > 1)
				return false;
			if (!CustomPathManager.InitDone
#if EXTRAPF
				|| CustomPathManager.ExtraQueuedPathFinds > Options.someValue8
#elif QUEUEDSTATS
				|| CustomPathManager.TotalQueuedPathFinds > Options.someValue8
#endif
				)
				return false;
			if (vehicleData.m_leadingVehicle != 0)
				return false;
			if (vehicleData.m_path == 0)
				return false;
			/*if (vehicleData.GetLastFrameVelocity().magnitude > Options.someValue9)
				return false;*/
			/*if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == 0)
				return false;*/
			VehicleState state = VehicleStateManager.GetVehicleState(vehicleId);
			if (state == null || state.LastPathRecalculation >= GetVehiclePathRecalculationFrame())
				return false;
			if (vehicleData.GetLastFrameVelocity().magnitude > Options.someValue9 * state.CurrentMaxSpeed)
				return false;
			NetManager netManager = Singleton<NetManager>.instance;
			bool recalc = false;
			ushort outSegmentId = 0;
			state.ProcessCurrentAndNextPathPosition(ref vehicleData, delegate (ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
				if (curPos.m_segment == 0 || nextPos.m_segment == 0)
					return;
				if (state.LastPathRecalculationSegmentId == curPos.m_segment)
					return;
				if (curPos.m_lane >= netManager.m_segments.m_buffer[curPos.m_segment].Info.m_lanes.Length)
					return;
				if (nextPos.m_lane >= netManager.m_segments.m_buffer[nextPos.m_segment].Info.m_lanes.Length)
					return;

				if (CustomRoadAI.laneMeanSpeeds[nextPos.m_segment] == null || nextPos.m_lane >= CustomRoadAI.laneMeanSpeeds[nextPos.m_segment].Length) {
					return;
				}

				if (CustomRoadAI.laneMeanSpeeds[nextPos.m_segment][nextPos.m_lane] >= 60) {
					return;
				}

				/*if (CustomRoadAI.laneMeanDensities[nextPos.m_segment] == null || nextPos.m_lane >= CustomRoadAI.laneMeanDensities[nextPos.m_segment].Length)
					return;
				byte nextDensity = CustomRoadAI.laneMeanDensities[nextPos.m_segment][nextPos.m_lane];

				if (nextDensity < 0.5) // TODO incorporate number of lanes (density is measured relatively)
					return;*/

				recalc = true;
				outSegmentId = curPos.m_segment;
			});

			segmentId = outSegmentId;
			return recalc;
		}
#endif

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
		internal static bool MayChangeSegment(ushort vehicleId, ref Vehicle vehicleData, ref Vehicle.Frame lastFrameData, bool isRecklessDriver, ref PathUnit.Position prevPos, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, uint laneID, out float maxSpeed, bool debug=false) {
			return MayChangeSegment(vehicleId, ref vehicleData, ref lastFrameData, isRecklessDriver, ref prevPos, prevTargetNodeId, prevLaneID, ref position, targetNodeId, laneID, ref DUMMY_POS, 0, out maxSpeed, debug);
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
		internal static bool MayChangeSegment(ushort vehicleId, ref Vehicle vehicleData, ref Vehicle.Frame lastFrameData, bool isRecklessDriver, ref PathUnit.Position prevPos, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, uint laneID, ref PathUnit.Position nextPosition, ushort nextTargetNodeId, out float maxSpeed, bool debug=false) {
			debug = false;
			if (prevTargetNodeId != targetNodeId) {
				// method should only be called if targetNodeId == prevTargetNode
				maxSpeed = 0f;
				return true;
			}

			bool forceUpdatePos = false;
			VehicleState vehicleState = null;
			try {
				vehicleState = VehicleStateManager.GetVehicleState(vehicleId);

				if (vehicleState == null) {
					VehicleStateManager.OnPathFindReady(vehicleId, ref vehicleData);
					vehicleState = VehicleStateManager.GetVehicleState(vehicleId);

					if (vehicleState == null) {
#if DEBUG
						Log._Debug($"Could not get vehicle state of {vehicleId}!");
#endif
					} else {
						forceUpdatePos = true;
					}
				}
			} catch (Exception e) {
				Log.Error("VehicleAI MayChangeSegment vehicle state error: " + e.ToString());
			}

			if (forceUpdatePos || Options.simAccuracy >= 2) {
				try {
					VehicleStateManager.UpdateVehiclePos(vehicleId, ref vehicleData, ref prevPos, ref position);
				} catch (Exception e) {
					Log.Error("VehicleAI MayChangeSegment Error: " + e.ToString());
				}
			}

			var netManager = Singleton<NetManager>.instance;

			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint prevTargetNodeLower8Bits = (uint)((prevTargetNodeId << 8) / 32768);
			uint random = currentFrameIndex - prevTargetNodeLower8Bits & 255u;

			bool isRailVehicle = (vehicleData.Info.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro)) != VehicleInfo.VehicleType.None;

			NetNode.Flags targetNodeFlags = netManager.m_nodes.m_buffer[targetNodeId].m_flags;
			bool hasTrafficLight = (targetNodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
			bool checkTrafficLights = false;
			if (!isRailVehicle) {
				// check if to check space

#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} is not a train.");
#endif

				var prevLaneFlags = (NetLane.Flags)netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_flags;
				var hasCrossing = (targetNodeFlags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
				var isJoinedJunction = (prevLaneFlags & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
				bool checkSpace = !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode == targetNodeId) && !isRecklessDriver;
				//TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(destinationNodeId);
				//if (timedNode != null && timedNode.vehiclesMayEnterBlockedJunctions) {
				//	checkSpace = false;
				//}

				if (checkSpace) {
					// check if there is enough space
					if ((targetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction &&
						netManager.m_nodes.m_buffer[targetNodeId].CountSegments() != 2) {
						var len = vehicleData.CalculateTotalLength(vehicleId) + 2f;
						if (!netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CheckSpace(len)) {
							var sufficientSpace = false;
							if (nextPosition.m_segment != 0 && netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_length < 30f) {
								NetNode.Flags nextTargetNodeFlags = netManager.m_nodes.m_buffer[nextTargetNodeId].m_flags;
								if ((nextTargetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction ||
									netManager.m_nodes.m_buffer[nextTargetNodeId].CountSegments() == 2) {
									uint nextLaneId = PathManager.GetLaneID(nextPosition);
									if (nextLaneId != 0u) {
										sufficientSpace = netManager.m_lanes.m_buffer[(int)((UIntPtr)nextLaneId)].CheckSpace(len);
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

				checkTrafficLights = (!isJoinedJunction || hasCrossing);
			} else {
#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} is a train.");
#endif

				checkTrafficLights = true;
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

						var destinationInfo = netManager.m_nodes.m_buffer[targetNodeId].Info;

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
						CustomRoadAI.GetTrafficLightState(vehicleId, ref vehicleData, targetNodeId, prevPos.m_segment, prevPos.m_lane, position.m_segment, ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - prevTargetNodeLower8Bits, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);

						if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car && isRecklessDriver) { // no reckless driving at railroad crossings
							vehicleLightState = RoadBaseAI.TrafficLightState.Green;
						}

#if DEBUG
						if (debug)
							Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {vehicleId} has {vehicleLightState} at node {targetNodeId}");
#endif

						if (!vehicles && random >= 196u) {
							vehicles = true;
							RoadBaseAI.SetTrafficLightState(targetNodeId, ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - prevTargetNodeLower8Bits, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
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

						/*if ((vehicleLightState == RoadBaseAI.TrafficLightState.Green || vehicleLightState == RoadBaseAI.TrafficLightState.RedToGreen) && !Flags.getEnterWhenBlockedAllowed(prevPos.m_segment, netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode == targetNodeId)) {
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
					} else if (vehicleState != null) {
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

						var prioritySegment = TrafficPriority.GetPrioritySegment(targetNodeId, prevPos.m_segment);
						if (prioritySegment != null) {
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
							float speed = lastFrameData.m_velocity.magnitude;

							if (vehicleState.JunctionTransitState == VehicleJunctionTransitState.None) {
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to ENTER (prio)");
#endif
								vehicleState.JunctionTransitState = VehicleJunctionTransitState.Enter;
							}

							if (vehicleState.JunctionTransitState != VehicleJunctionTransitState.Leave) {
								bool hasIncomingCars;
								switch (prioritySegment.Type) {
									case SegmentEnd.PriorityType.Stop:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: STOP sign. waittime={vehicleState.WaitTime}, vel={speed}");
#endif

										if (Options.simAccuracy <= 2 || (Options.simAccuracy >= 3 && vehicleState.WaitTime < MaxPriorityWaitTime)) {
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											if (speed <= TrafficPriority.maxStopVelocity) {
												vehicleState.WaitTime++;

												float minStopWaitTime = UnityEngine.Random.Range(0f, 3f);
												if (vehicleState.WaitTime >= minStopWaitTime) {
													if (Options.simAccuracy >= 4) {
														vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
													} else {
														hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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
									case SegmentEnd.PriorityType.Yield:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: YIELD sign. waittime={vehicleState.WaitTime}");
#endif

										if (Options.simAccuracy <= 2 || (Options.simAccuracy >= 3 && vehicleState.WaitTime < MaxPriorityWaitTime)) {
											vehicleState.WaitTime++;
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											if (speed <= TrafficPriority.maxYieldVelocity || Options.simAccuracy <= 2) {
												if (Options.simAccuracy >= 4) {
													vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
												} else {
													hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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
													Log._Debug($"Vehicle {vehicleId}: Vehicle has not yet reached yield speed (reduce {speed} by {vehicleState.ReduceSpeedByValueToYield})");
#endif

												// vehicle has not yet reached yield speed
												maxSpeed = TrafficPriority.maxYieldVelocity;
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
									case SegmentEnd.PriorityType.Main:
									case SegmentEnd.PriorityType.None:
#if DEBUG
										if (debug)
											Log._Debug($"Vehicle {vehicleId}: MAIN sign. waittime={vehicleState.WaitTime}");
#endif
										maxSpeed = 0f;

										if (Options.simAccuracy == 4)
											return true;

										if (Options.simAccuracy <= 2 || (Options.simAccuracy == 3 && vehicleState.WaitTime < MaxPriorityWaitTime)) {
											vehicleState.WaitTime++;
#if DEBUG
											if (debug)
												Log._Debug($"Vehicle {vehicleId}: Setting JunctionTransitState to STOP (wait)");
#endif
											vehicleState.JunctionTransitState = VehicleJunctionTransitState.Stop;

											hasIncomingCars = TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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
							} else if (speed <= TrafficPriority.maxStopVelocity) {
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

		/// <summary>
		/// Retrieves the current (shifted) frame index that is used to store dynamic path recalculation data.
		/// </summary>
		/// <returns></returns>
		private static uint GetVehiclePathRecalculationFrame() {
			return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8;
		}

		/// <summary>
		/// Stores that the given vehicle's path has been dynamically recalculated at the current frame.
		/// </summary>
		/// <param name="vehicleId"></param>
#if PATHRECALC
		internal static void MarkPathRecalculation(ushort vehicleId, ushort segmentId) {
			VehicleState state = VehicleStateManager.GetVehicleState(vehicleId);
			if (state == null)
				return;
			state.LastPathRecalculation = GetVehiclePathRecalculationFrame();
			state.LastPathRecalculationSegmentId = segmentId;
			state.PathRecalculationRequested = true;
		}
#endif
	}
}

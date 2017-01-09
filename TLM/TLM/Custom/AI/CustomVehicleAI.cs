#define EXTRAPFx
#define QUEUEDSTATSx
#define PATHRECALCx

using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;

namespace TrafficManager.Custom.AI {
	class CustomVehicleAI : VehicleAI { // TODO inherit from PrefabAI (in order to keep the correct references to `base`)
		public static readonly int MaxPriorityWaitTime = 50;

		//private static readonly int MIN_BLOCK_COUNTER_PATH_RECALC_VALUE = 3;

		private static PathUnit.Position DUMMY_POS = default(PathUnit.Position);

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
				var laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
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

			VehicleStateManager vehStateManager = VehicleStateManager.Instance;

			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				vehicleState = vehStateManager.GetVehicleState(vehicleId);
			}

			if ((Options.prioritySignsEnabled || Options.timedLightsEnabled) && (forceUpdatePos || Options.simAccuracy >= 2)) {
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
					} else if (vehicleState != null && Options.prioritySignsEnabled) {
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

						var prioritySegment = prioMan.GetPrioritySegment(targetNodeId, prevPos.m_segment);
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
							float sqrSpeed = lastFrameData.m_velocity.sqrMagnitude;

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
											Log._Debug($"Vehicle {vehicleId}: STOP sign. waittime={vehicleState.WaitTime}, sqrSpeed={sqrSpeed}");
#endif

										if (Options.simAccuracy <= 2 || (Options.simAccuracy >= 3 && vehicleState.WaitTime < MaxPriorityWaitTime)) {
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
														hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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

											if (sqrSpeed <= TrafficPriorityManager.MAX_SQR_YIELD_VELOCITY || Options.simAccuracy <= 2) {
												if (Options.simAccuracy >= 4) {
													vehicleState.JunctionTransitState = VehicleJunctionTransitState.Leave;
												} else {
													hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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

											hasIncomingCars = prioMan.HasIncomingVehiclesWithHigherPriority(vehicleId, ref vehicleData, ref prevPos, ref position);
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

		/*protected void UpdatePathTargetPositions(ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos, ref int index, int max, float minSqrDistanceA, float minSqrDistanceB) {
			PathManager instance = Singleton<PathManager>.instance;
			NetManager instance2 = Singleton<NetManager>.instance;
			Vector4 vector = vehicleData.m_targetPos0;
			vector.w = 1000f;
			float num = minSqrDistanceA;
			uint num2 = vehicleData.m_path;
			byte b = vehicleData.m_pathPositionIndex;
			byte b2 = vehicleData.m_lastPathOffset;
			if (b == 255) {
				b = 0;
				if (index <= 0) {
					vehicleData.m_pathPositionIndex = 0;
				}
				if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].CalculatePathPositionOffset(b >> 1, vector, out b2)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
			}
			PathUnit.Position position;
			if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].GetPosition(b >> 1, out position)) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}
			NetInfo info = instance2.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes.Length <= (int)position.m_lane) {
				this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
				return;
			}
			uint num3 = PathManager.GetLaneID(position);
			NetInfo.Lane lane = info.m_lanes[(int)position.m_lane];
			Bezier3 bezier;
			while (true) {
				if ((b & 1) == 0) {
					if (lane.m_laneType != NetInfo.LaneType.CargoVehicle) {
						bool flag = true;
						while (b2 != position.m_offset) {
							if (flag) {
								flag = false;
							} else {
								float num4 = Mathf.Sqrt(num) - Vector3.Distance(vector, refPos);
								int num5;
								if (num4 < 0f) {
									num5 = 4;
								} else {
									num5 = 4 + Mathf.Max(0, Mathf.CeilToInt(num4 * 256f / (instance2.m_lanes.m_buffer[(int)((UIntPtr)num3)].m_length + 1f)));
								}
								if (b2 > position.m_offset) {
									b2 = (byte)Mathf.Max((int)b2 - num5, (int)position.m_offset);
								} else if (b2 < position.m_offset) {
									b2 = (byte)Mathf.Min((int)b2 + num5, (int)position.m_offset);
								}
							}
							Vector3 a;
							Vector3 vector2;
							float b3;
							this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, b2, out a, out vector2, out b3);
							vector.Set(a.x, a.y, a.z, Mathf.Min(vector.w, b3));
							float sqrMagnitude = (a - refPos).sqrMagnitude;
							if (sqrMagnitude >= num) {
								if (index <= 0) {
									vehicleData.m_lastPathOffset = b2;
								}
								vehicleData.SetTargetPos(index++, vector);
								num = minSqrDistanceB;
								refPos = vector;
								vector.w = 1000f;
								if (index == max) {
									return;
								}
							}
						}
					}
					b += 1;
					b2 = 0;
					if (index <= 0) {
						vehicleData.m_pathPositionIndex = b;
						vehicleData.m_lastPathOffset = b2;
					}
				}
				int num6 = (b >> 1) + 1;
				uint num7 = num2;
				if (num6 >= (int)instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_positionCount) {
					num6 = 0;
					num7 = instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].m_nextPathUnit;
					if (num7 == 0u) {
						if (index <= 0) {
							Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
							vehicleData.m_path = 0u;
						}
						vector.w = 1f;
						vehicleData.SetTargetPos(index++, vector);
						return;
					}
				}
				PathUnit.Position position2;
				if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].GetPosition(num6, out position2)) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				NetInfo info2 = instance2.m_segments.m_buffer[(int)position2.m_segment].Info;
				if (info2.m_lanes.Length <= (int)position2.m_lane) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				uint laneID = PathManager.GetLaneID(position2);
				NetInfo.Lane lane2 = info2.m_lanes[(int)position2.m_lane];
				ushort startNode = instance2.m_segments.m_buffer[(int)position.m_segment].m_startNode;
				ushort endNode = instance2.m_segments.m_buffer[(int)position.m_segment].m_endNode;
				ushort startNode2 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_startNode;
				ushort endNode2 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_endNode;
				if (startNode2 != startNode && startNode2 != endNode && endNode2 != startNode && endNode2 != endNode && ((instance2.m_nodes.m_buffer[(int)startNode].m_flags | instance2.m_nodes.m_buffer[(int)endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None && ((instance2.m_nodes.m_buffer[(int)startNode2].m_flags | instance2.m_nodes.m_buffer[(int)endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				if (lane2.m_laneType == NetInfo.LaneType.Pedestrian) {
					if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == (Vehicle.Flags)0) {
						byte offset = position.m_offset;
						byte offset2 = position.m_offset;
						if (this.ParkVehicle(vehicleID, ref vehicleData, position, num7, num6 << 1, out offset2)) {
							if (offset2 != offset) {
								if (index <= 0) {
									vehicleData.m_pathPositionIndex = (byte)((int)vehicleData.m_pathPositionIndex & -2);
									vehicleData.m_lastPathOffset = offset;
								}
								position.m_offset = offset2;
								instance.m_pathUnits.m_buffer[(int)((UIntPtr)num2)].SetPosition(b >> 1, position);
							}
							vehicleData.m_flags |= Vehicle.Flags.Parking;
						} else {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					}
					return;
				}
				if ((byte)(lane2.m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle | NetInfo.LaneType.TransportVehicle)) == 0) {
					this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
					return;
				}
				if (lane2.m_vehicleType != this.m_info.m_vehicleType && this.NeedChangeVehicleType(vehicleID, ref vehicleData, position2, laneID, lane2.m_vehicleType, ref vector)) {
					float sqrMagnitude3 = (vector - (Vector4)refPos).sqrMagnitude;
					if (sqrMagnitude3 >= num) {
						vehicleData.SetTargetPos(index++, vector);
					}
					if (index <= 0) {
						while (index < max) {
							vehicleData.SetTargetPos(index++, vector);
						}
						if (num7 != vehicleData.m_path) {
							Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
						}
						vehicleData.m_pathPositionIndex = (byte)(num6 << 1);
						PathUnit.CalculatePathPositionOffset(laneID, vector, out vehicleData.m_lastPathOffset);
						if (vehicleID != 0 && !this.ChangeVehicleType(vehicleID, ref vehicleData, position2, laneID)) {
							this.InvalidPath(vehicleID, ref vehicleData, vehicleID, ref vehicleData);
						}
					} else {
						while (index < max) {
							vehicleData.SetTargetPos(index++, vector);
						}
					}
					return;
				}
				if (position2.m_segment != position.m_segment && vehicleID != 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
				}
				byte b4 = 0;
				if ((vehicleData.m_flags & Vehicle.Flags.Flying) != (Vehicle.Flags)0) {
					b4 = (byte) ((position2.m_offset < 128) ? 255 : 0);
				} else if (num3 != laneID && lane.m_laneType != NetInfo.LaneType.CargoVehicle) {
					PathUnit.CalculatePathPositionOffset(laneID, vector, out b4);
					bezier = default(Bezier3);
					Vector3 vector3;
					float num8;
					this.CalculateSegmentPosition(vehicleID, ref vehicleData, position, num3, position.m_offset, out bezier.a, out vector3, out num8);
					bool flag2 = b2 == 0;
					if (flag2) {
						if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0) {
							flag2 = (vehicleData.m_trailingVehicle == 0);
						} else {
							flag2 = (vehicleData.m_leadingVehicle == 0);
						}
					}
					Vector3 vector4;
					float num9;
					if (flag2) {
						PathUnit.Position nextPosition;
						if (!instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].GetNextPosition(num6, out nextPosition)) {
							nextPosition = default(PathUnit.Position);
						}
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, nextPosition, position2, laneID, b4, position, num3, position.m_offset, index, out bezier.d, out vector4, out num9);
					} else {
						this.CalculateSegmentPosition(vehicleID, ref vehicleData, position2, laneID, b4, out bezier.d, out vector4, out num9);
					}
					if (num9 < 0.01f || (instance2.m_segments.m_buffer[(int)position2.m_segment].m_flags & NetSegment.Flags.Flooded) != NetSegment.Flags.None) {
						if (index <= 0) {
							vehicleData.m_lastPathOffset = b2;
						}
						vector = bezier.a;
						vector.w = 0f;
						while (index < max) {
							vehicleData.SetTargetPos(index++, vector);
						}
					}
					if (position.m_offset == 0) {
						vector3 = -vector3;
					}
					if (b4 < position2.m_offset) {
						vector4 = -vector4;
					}
					vector3.Normalize();
					vector4.Normalize();
					float num10;
					NetSegment.CalculateMiddlePoints(bezier.a, vector3, bezier.d, vector4, true, true, out bezier.b, out bezier.c, out num10);
					if (num10 > 1f) {
						ushort num11;
						if (b4 == 0) {
							num11 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_startNode;
						} else if (b4 == 255) {
							num11 = instance2.m_segments.m_buffer[(int)position2.m_segment].m_endNode;
						} else {
							num11 = 0;
						}
						float num12 = 1.57079637f * (1f + Vector3.Dot(vector3, vector4));
						if (num10 > 1f) {
							num12 /= num10;
						}
						num9 = Mathf.Min(num9, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, num12));
						while (b2 < 255) {
							float num13 = Mathf.Sqrt(num) - Vector3.Distance(vector, refPos);
							int num14;
							if (num13 < 0f) {
								num14 = 8;
							} else {
								num14 = 8 + Mathf.Max(0, Mathf.CeilToInt(num13 * 256f / (num10 + 1f)));
							}
							b2 = (byte)Mathf.Min((int)b2 + num14, 255);
							Vector3 a2 = bezier.Position((float)b2 * 0.003921569f);
							vector.Set(a2.x, a2.y, a2.z, Mathf.Min(vector.w, num9));
							float sqrMagnitude2 = (a2 - refPos).sqrMagnitude;
							if (sqrMagnitude2 >= num) {
								if (index <= 0) {
									vehicleData.m_lastPathOffset = b2;
								}
								if (num11 != 0) {
									this.UpdateNodeTargetPos(vehicleID, ref vehicleData, num11, ref instance2.m_nodes.m_buffer[(int)num11], ref vector, index);
								}
								vehicleData.SetTargetPos(index++, vector);
								num = minSqrDistanceB;
								refPos = vector;
								vector.w = 1000f;
								if (index == max) {
									return;
								}
							}
						}
					}
				} else {
					PathUnit.CalculatePathPositionOffset(laneID, vector, out b4);
				}
				if (index <= 0) {
					if (num6 == 0) {
						Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
					}
					if (num6 >= (int)(instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].m_positionCount - 1) && instance.m_pathUnits.m_buffer[(int)((UIntPtr)num7)].m_nextPathUnit == 0u && vehicleID != 0) {
						this.ArrivingToDestination(vehicleID, ref vehicleData);
					}
				}
				num2 = num7;
				b = (byte)(num6 << 1);
				b2 = b4;
				if (index <= 0) {
					vehicleData.m_pathPositionIndex = b;
					vehicleData.m_lastPathOffset = b2;
					vehicleData.m_flags = ((vehicleData.m_flags & ~(Vehicle.Flags.OnGravel | Vehicle.Flags.Underground | Vehicle.Flags.Transition)) | info2.m_setVehicleFlags);
					if (this.LeftHandDrive(lane2)) {
						vehicleData.m_flags |= Vehicle.Flags.LeftHandDrive;
					} else {
						vehicleData.m_flags &= (Vehicle.Flags.Created | Vehicle.Flags.Deleted | Vehicle.Flags.Spawned | Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget | Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped | Vehicle.Flags.Leaving | Vehicle.Flags.Arriving | Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff | Vehicle.Flags.Flying | Vehicle.Flags.Landing | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo | Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget | Vehicle.Flags.Importing | Vehicle.Flags.Exporting | Vehicle.Flags.Parking | Vehicle.Flags.CustomName | Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading | Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic | Vehicle.Flags.Underground | Vehicle.Flags.Transition | Vehicle.Flags.InsideBuilding);
					}
				}
				position = position2;
				num3 = laneID;
				lane = lane2;
			}
			return;
		}*/
	}
}

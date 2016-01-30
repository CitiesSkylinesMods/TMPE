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
	internal class CustomCarAI : CarAI {
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

		internal static void HandleVehicle(ushort vehicleId, ref Vehicle vehicleData, bool addTraffic, bool realTraffic) {
			HandleVehicle(vehicleId, ref vehicleData, addTraffic, realTraffic, 2);
		}

		/// <summary>
		/// Handles vehicle path information in order to manage special nodes (nodes with priority signs or traffic lights).
		/// Data like "vehicle X is on segment S0 and is going to segment S1" is collected.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		internal static void HandleVehicle(ushort vehicleId, ref Vehicle vehicleData, bool addTraffic, bool realTraffic, byte maxUpcomingPathPositions, bool debug = false) {
			if (maxUpcomingPathPositions <= 0)
				maxUpcomingPathPositions = 1; // we need at least one upcoming path position

			var netManager = Singleton<NetManager>.instance;
			var lastFrameData = vehicleData.GetLastFrameData();
			var lastFrameVehiclePos = lastFrameData.m_position;
#if DEBUGV
			var camPos = Camera.main.transform.position;
			//debug = (lastFrameVehiclePos - camPos).sqrMagnitude < CloseLod;
			debug = false;
			List<String> logBuffer = new List<String>();
			bool logme = false;
#endif
			if ((vehicleData.m_flags & Vehicle.Flags.Created) == Vehicle.Flags.None)
				return;

			if (vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Car && vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Train) {
				//Log._Debug($"HandleVehicle does not handle vehicles of type {vehicleData.Info.m_vehicleType}");
                return;
			}
#if DEBUGV
			logBuffer.Add("Calculating prio info for vehicleId " + vehicleId);
#endif

			// add vehicle to our vehicle list
			VehiclePosition vehiclePos = TrafficPriority.GetVehiclePosition(vehicleId);

			// we extract the segment information directly from the vehicle
			var currentPathUnitId = vehicleData.m_path;
			List<ushort> realTimeDestinationNodes = new List<ushort>(); // current and upcoming node ids
			List<PathUnit.Position> realTimePositions = new List<PathUnit.Position>(); // current and upcoming vehicle positions

#if DEBUGV
			logBuffer.Add("* vehicleId " + vehicleId + ". currentPathId: " + currentPathUnitId + " pathPositionIndex: " + vehicleData.m_pathPositionIndex);
#endif

			if (currentPathUnitId > 0) {
				// vehicle has a path...
				if ((Singleton<PathManager>.instance.m_pathUnits.m_buffer[currentPathUnitId].m_pathFindFlags & PathUnit.FLAG_READY) != 0) {
					// The path(unit) is established and is ready for use: get the vehicle's current position in terms of segment and lane
					realTimePositions.Add(Singleton<PathManager>.instance.m_pathUnits.m_buffer[currentPathUnitId].GetPosition(vehicleData.m_pathPositionIndex >> 1));
					if (realTimePositions[0].m_offset == 0) {
						realTimeDestinationNodes.Add(netManager.m_segments.m_buffer[realTimePositions[0].m_segment].m_startNode);
					} else {
						realTimeDestinationNodes.Add(netManager.m_segments.m_buffer[realTimePositions[0].m_segment].m_endNode);
					}

					if (maxUpcomingPathPositions > 0) {
						// evaluate upcoming path units
						byte i = 0;
						uint pathUnitId = currentPathUnitId;
						int pathPos = (byte)((vehicleData.m_pathPositionIndex >> 1) + 1);
						while (true) {
							if (pathPos > 11) {
								// go to next path unit
								pathPos = 0;
								pathUnitId = Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit;
#if DEBUGV
								logBuffer.Add("* vehicleId " + vehicleId + ". Going to next path unit (1). pathUnitId=" + pathUnitId);
#endif
								if (pathUnitId <= 0)
									break;
							}

							PathUnit.Position nextRealTimePosition = default(PathUnit.Position);
							if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathUnitId].GetPosition(pathPos, out nextRealTimePosition)) { // if this returns false, there is no next path unit
#if DEBUGV
								logBuffer.Add("* vehicleId " + vehicleId + ". No next path unit! pathPos=" + pathPos + ", pathUnitId=" + pathUnitId);
#endif
								break;
							}

							ushort destNodeId = 0;
							if (nextRealTimePosition.m_segment > 0) {
								if (nextRealTimePosition.m_offset == 0) {
									destNodeId = netManager.m_segments.m_buffer[nextRealTimePosition.m_segment].m_startNode;
								} else {
									destNodeId = netManager.m_segments.m_buffer[nextRealTimePosition.m_segment].m_endNode;
								}
							}

#if DEBUGV
							logBuffer.Add("* vehicleId " + vehicleId + ". Next path unit! node " + destNodeId + ", seg. " + nextRealTimePosition.m_segment + ", pathUnitId=" + pathUnitId + ", pathPos: " + pathPos);
#endif

							realTimePositions.Add(nextRealTimePosition);
							realTimeDestinationNodes.Add(destNodeId);

							if (i >= maxUpcomingPathPositions - 1)
								break; // we calculate up to 2 upcoming path units at the moment

							++pathPos;
							++i;
						}
					}

					// please don't ask why we use "m_pathPositionIndex >> 1" (which equals to "m_pathPositionIndex / 2") here (Though it would
					// be interesting to know why they used such an ugly indexing scheme!!). I assume the oddness of m_pathPositionIndex relates
					// to the car's position on the segment. If it is even the car might be in the segment's first half and if it is odd, it might
					// be in the segment's second half.
#if DEBUGV
					logBuffer.Add("* vehicleId " + vehicleId + ". *INFO* rtPos.seg=" + realTimePositions[0].m_segment + " nrtPos.seg=" + (realTimePositions.Count > 1 ? ""+realTimePositions[1].m_segment : "n/a"));
#endif
				}
			}

			// we have seen the car!
			vehiclePos.LastFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

#if DEBUGV
			logBuffer.Add("* vehicleId " + vehicleId + ". ToNode: " + vehiclePos.ToNode + ". FromSegment: " + vehiclePos.FromSegment/* + ". FromLaneId: " + TrafficPriority.Vehicles[vehicleId].FromLaneId*/);
#endif
			if (addTraffic && vehicleData.m_leadingVehicle == 0 && realTimePositions.Count > 0) {
				// add traffic to lane
				uint laneId = PathManager.GetLaneID(realTimePositions[0]);
				CustomRoadAI.AddTraffic(laneId, (ushort)Mathf.RoundToInt(vehicleData.CalculateTotalLength(vehicleId)), (ushort)Mathf.RoundToInt(lastFrameData.m_velocity.magnitude), realTraffic);
			}

#if DEBUGV
			logBuffer.Add("* vehicleId " + vehicleId + ". Real time positions: " + realTimePositions.Count + ", Destination nodes: " + realTimeDestinationNodes.Count);
#endif
			if (realTimePositions.Count >= 2) {
				// we found a valid path unit
				var sourceLaneIndex = realTimePositions[0].m_lane;

				if (
					!vehiclePos.Valid ||
					vehiclePos.ToNode != realTimeDestinationNodes[0] ||
					vehiclePos.FromSegment != realTimePositions[0].m_segment ||
					vehiclePos.FromLaneIndex != sourceLaneIndex) {
					// vehicle information is not up-to-date. remove the car from old priority segments (if existing)...
					TrafficPriority.RemoveVehicleFromSegments(vehicleId);

					// save vehicle information for priority rule handling
					vehiclePos.Valid = true;
					vehiclePos.CarState = CarState.None;
					vehiclePos.WaitTime = 0;
					vehiclePos.Stopped = false;
					vehiclePos.ToNode = realTimeDestinationNodes[0];
					vehiclePos.FromSegment = realTimePositions[0].m_segment;
					vehiclePos.FromLaneIndex = realTimePositions[0].m_lane;
					vehiclePos.ToSegment = realTimePositions[1].m_segment;
					vehiclePos.ToLaneIndex = realTimePositions[1].m_lane;
					vehiclePos.ReduceSpeedByValueToYield = Random.Range(16f, 28f);
					vehiclePos.OnEmergency = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None;

#if DEBUGV
					logBuffer.Add($"* vehicleId {vehicleId}. Setting current position to: from {vehiclePos.FromSegment} (lane {vehiclePos.FromLaneIndex}), going over {vehiclePos.ToNode}, to {vehiclePos.ToSegment} (lane {vehiclePos.ToLaneIndex})");
#endif

					//if (!Options.disableSomething) {
						// add the vehicle to upcoming priority segments that have timed traffic lights
						for (int i = 0; i < realTimePositions.Count - 1; ++i) {
							var prioritySegment = TrafficPriority.GetPrioritySegment(realTimeDestinationNodes[i], realTimePositions[i].m_segment);
							if (prioritySegment == null)
								continue;

							// add upcoming segments only if there is a timed traffic light
							TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(realTimeDestinationNodes[i]);
							if (i > 0 && (nodeSim == null || !nodeSim.IsTimedLight() || !nodeSim.IsTimedLightActive()))
								continue;

							VehiclePosition upcomingVehiclePos = new VehiclePosition();
							upcomingVehiclePos.Valid = true;
							upcomingVehiclePos.CarState = CarState.None;
							upcomingVehiclePos.LastFrame = vehiclePos.LastFrame;
							upcomingVehiclePos.ToNode = realTimeDestinationNodes[i];
							upcomingVehiclePos.FromSegment = realTimePositions[i].m_segment;
							upcomingVehiclePos.FromLaneIndex = realTimePositions[i].m_lane;
							upcomingVehiclePos.ToSegment = realTimePositions[i + 1].m_segment;
							upcomingVehiclePos.ToLaneIndex = realTimePositions[i + 1].m_lane;
							upcomingVehiclePos.ReduceSpeedByValueToYield = Random.Range(16f, 28f);
							upcomingVehiclePos.OnEmergency = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None;
#if DEBUGV
							logBuffer.Add($"* vehicleId {vehicleId}. Adding future position: from {upcomingVehiclePos.FromSegment}  (lane {upcomingVehiclePos.FromLaneIndex}), going over {upcomingVehiclePos.ToNode}, to {upcomingVehiclePos.ToSegment} (lane {upcomingVehiclePos.ToLaneIndex})");
#endif

							prioritySegment.AddVehicle(vehicleId, upcomingVehiclePos);
						}
					//}
				} else {
#if DEBUGV
					logBuffer.Add($"* vehicleId {vehicleId}. Nothing has changed. from {vehiclePos.FromSegment} (lane {vehiclePos.FromLaneIndex}), going over {vehiclePos.ToNode}, to {vehiclePos.ToSegment} (lane {vehiclePos.ToLaneIndex})");
					logme = false;
#endif
				}
			} else {
#if DEBUGV
				logBuffer.Add($"* vehicleId {vehicleId}. Insufficient path unit positions.");
#endif
			}
			
#if DEBUGV
			if (logme) {
				Log._Debug("vehicleId: " + vehicleId + " ============================================");
				foreach (String logBuf in logBuffer) {
					Log._Debug(logBuf);
				}
				Log._Debug("vehicleId: " + vehicleId + " ============================================");
			}
#endif
		}

		/// <summary>
		/// Lightweight simulation step method.
		/// This method is occasionally being called for different cars.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <param name="physicsLodRefPos"></param>
		public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			try {
				FindPathIfNeeded(vehicleId, ref vehicleData);
			} catch (InvalidOperationException) {
				return;
			}

			SpawnVehicleIfWaiting(vehicleId, ref vehicleData);

			try {
				HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], true, true);
			} catch (Exception e) {
				Log.Error("CarAI TrafficManagerSimulationStep Error: " + e.ToString());
			}

			SimulateVehicleAndTrailers(vehicleId, ref vehicleData, physicsLodRefPos);

			DespawnVehicles(vehicleId, ref vehicleData);
		}

		private void FindPathIfNeeded(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) == Vehicle.Flags.None) return;

			if (!CanFindPath(vehicleId, ref vehicleData))
				throw new InvalidOperationException("Path Not Available for Vehicle");
		}

		private void SpawnVehicleIfWaiting(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
				TrySpawn(vehicleId, ref vehicleData);
			}
		}

		private void SimulateVehicleAndTrailers(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			var lastFramePosition = vehicleData.GetLastFramePosition();

			var lodPhysics = CalculateLod(physicsLodRefPos, lastFramePosition);

			SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);

			if (vehicleData.m_leadingVehicle != 0 || vehicleData.m_trailingVehicle == 0)
				return;

			var vehicleManager = Singleton<VehicleManager>.instance;
			var trailingVehicleId = vehicleData.m_trailingVehicle;
			SimulateTrailingVehicles(vehicleId, ref vehicleData, lodPhysics, trailingVehicleId, ref vehicleManager, 0);
		}

		private void DespawnVehicles(ushort vehicleId, ref Vehicle vehicleData) {
			int privateServiceIndex = ItemClass.GetPrivateServiceIndex(this.m_info.m_class.m_service);
			int num3 = (privateServiceIndex == -1) ? 150 : 100;
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) == Vehicle.Flags.None && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if ((int)vehicleData.m_blockCounter == num3 && LoadingExtension.Instance.DespawnEnabled) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
		}

		private void SimulateTrailingVehicles(ushort leaderId, ref Vehicle leaderData, int lodPhysics,
			ushort trailingVehicleId, ref VehicleManager vehicleManager, int numberOfIterations) {
			if (trailingVehicleId == 0) {
				return;
			}

			var trailingTrailingVehicleId = vehicleManager.m_vehicles.m_buffer[trailingVehicleId].m_trailingVehicle;
			var trailingVehicleInfo = vehicleManager.m_vehicles.m_buffer[trailingVehicleId].Info;

			trailingVehicleInfo.m_vehicleAI.SimulationStep(trailingVehicleId,
				ref vehicleManager.m_vehicles.m_buffer[trailingVehicleId], leaderId,
				ref leaderData, lodPhysics);

			if (++numberOfIterations > 16384) {
				CODebugBase<LogChannel>.Error(LogChannel.Core,
					"Invalid list detected!\n" + Environment.StackTrace);
				return;
			}
			SimulateTrailingVehicles(leaderId, ref leaderData, lodPhysics, trailingTrailingVehicleId, ref vehicleManager, numberOfIterations);
		}

		private static int CalculateLod(Vector3 physicsLodRefPos, Vector3 lastFramePosition) {
			int lodPhysics;
			if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= FarLod) {
				lodPhysics = 2;
			} else if (Vector3.SqrMagnitude(Singleton<SimulationManager>.
				  instance.m_simulationView.m_position - lastFramePosition) >= CloseLod) {
				lodPhysics = 1;
			} else {
				lodPhysics = 0;
			}
			return lodPhysics;
		}

		private bool CanFindPath(ushort vehicleId, ref Vehicle data) {
			var pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_pathFindFlags;
			if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
				data.m_pathPositionIndex = 255;
				data.m_flags &= ~Vehicle.Flags.WaitingPath;
				data.m_flags &= ~Vehicle.Flags.Arriving;
				PathfindSuccess(vehicleId, ref data);
				TrySpawn(vehicleId, ref data);
			} else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0) {
				data.m_flags &= ~Vehicle.Flags.WaitingPath;
				Singleton<PathManager>.instance.ReleasePath(data.m_path);
				data.m_path = 0u;
				PathfindFailure(vehicleId, ref data);
				return false;
			}
			return true;
		}

		public void TmCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
			PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID,
			byte prevOffset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			//var vehicleManager = Singleton<VehicleManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
			bool isRecklessDriver = (uint)vehicleId % (Options.getRecklessDriverModulo()) == 0;

			var lastFrameData = vehicleData.GetLastFrameData();
			var lastFrameVehiclePos = lastFrameData.m_position;

			var camPos = Camera.main.transform.position;
			bool simulatePrioritySigns = (lastFrameVehiclePos - camPos).sqrMagnitude < FarLod && !isRecklessDriver;

			if (Options.simAccuracy <= 0) {
				if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
					VehiclePosition vehiclePos = TrafficPriority.GetVehiclePosition(vehicleId);
					if (vehiclePos.Valid && simulatePrioritySigns) {
						try {
							HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], false, false);
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
					bool checkSpace = !Options.mayEnterBlockedJunctions;
					TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(destinationNodeId);
					if (nodeSim != null && nodeSim.IsTimedLightActive()) {
						TimedTrafficLights timedNode = nodeSim.TimedLight;
						if (timedNode != null && timedNode.vehiclesMayEnterBlockedJunctions) {
							checkSpace = false;
						}
					}
					if (checkSpace && (uint)vehicleId % (Options.getRecklessDriverModulo() / 2) == 0) {
						checkSpace = false;
					}
					if (checkSpace) {
						// check if there is enough space
						if ((nodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) ==
							NetNode.Flags.Junction && netManager.m_nodes.m_buffer[destinationNodeId].CountSegments() != 2) {
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
						if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None) {
							if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
								if (hasTrafficLight && (!isJoinedJunction || hasCrossing)) {
									var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(previousDestinationNode);

									var destinationInfo = netManager.m_nodes.m_buffer[destinationNodeId].Info;
									RoadBaseAI.TrafficLightState vehicleLightState;
									ManualSegmentLight light = ManualTrafficLights.GetSegmentLight(previousDestinationNode, prevPos.m_segment); // TODO rework

									if (light == null || nodeSimulation == null || !nodeSimulation.IsTimedLightActive()) {
										/*if (destinationNodeId == 20164) {
											Log.Warning($"No sim @ node {destinationNodeId}");
                                        }*/

										RoadBaseAI.TrafficLightState pedestrianLightState;
										bool flag5;
										bool pedestrians;
										RoadBaseAI.GetTrafficLightState(previousDestinationNode,
											ref netManager.m_segments.m_buffer[prevPos.m_segment],
											currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5,
											out pedestrians);
										if (!flag5 && num6 >= 196u) {
											flag5 = true;
											RoadBaseAI.SetTrafficLightState(previousDestinationNode,
												ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5,
												vehicleLightState, pedestrianLightState, flag5, pedestrians);
										}

										switch (vehicleLightState) {
											case RoadBaseAI.TrafficLightState.RedToGreen:
												if (num6 < 60u) {
													maxSpeed = 0f;
													return;
												}
												break;
											case RoadBaseAI.TrafficLightState.Red:
												maxSpeed = 0f;
												return;
											case RoadBaseAI.TrafficLightState.GreenToRed:
												if (num6 >= 30u) {
													maxSpeed = 0f;
													return;
												}
												break;
										}
									} else {
										//Log._Debug($"CarAI: Handling vehicle {vehicleId} @ {destinationNodeId} (timed traffic light) going from seg. {prevPos.m_segment} to {position.m_segment}, {nextPosition.m_segment}");
										// traffic light simulation is active
										var stopCar = false;

										if (isRecklessDriver)
											vehicleLightState = RoadBaseAI.TrafficLightState.Green;
										else {
											SegmentGeometry geometry = CustomRoadAI.GetSegmentGeometry(prevPos.m_segment);

											// determine responsible traffic light (left, right or main)
											if (geometry.IsLeftSegment(position.m_segment, destinationNodeId)) {
												vehicleLightState = light.GetLightLeft();
											} else if (geometry.IsRightSegment(position.m_segment, destinationNodeId)) {
												vehicleLightState = light.GetLightRight();
											} else {
												vehicleLightState = light.GetLightMain();
											}
										}

										switch (vehicleLightState) {
											case RoadBaseAI.TrafficLightState.RedToGreen:
												if (num6 < 60u) {
													stopCar = true;
												}
												break;
											case RoadBaseAI.TrafficLightState.Red:
												stopCar = true;
												break;
											case RoadBaseAI.TrafficLightState.GreenToRed:
												if (num6 >= 30u) {
													stopCar = true;
												}
												break;
										}

										if (vehicleLightState == RoadBaseAI.TrafficLightState.Green && !Options.mayEnterBlockedJunctions) {
											var hasIncomingCars = Options.disableSomething3 ? false : TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);

											if (hasIncomingCars) {
												// green light but other cars are incoming and they have priority: stop
												stopCar = true;
											}
										}

										if (stopCar) {
											maxSpeed = 0f;
											return;
										}
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
											VehiclePosition globalTargetPos = TrafficPriority.GetVehiclePosition(vehicleId);
											if (globalTargetPos.Valid) {
#if DEBUG
												if (debug)
													Log._Debug($"Vehicle {vehicleId}: global target position found. carState = {globalTargetPos.CarState.ToString()}");
#endif
												var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
												var frame = currentFrameIndex2 >> 4;

												if (globalTargetPos.CarState == CarState.None) {
													globalTargetPos.CarState = CarState.Enter;
												}

												if (globalTargetPos.CarState != CarState.Leave) {
													bool hasIncomingCars;
													switch (prioritySegment.Type) {
														case PrioritySegment.PriorityType.Stop:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: STOP sign. waittime={globalTargetPos.WaitTime}, vel={lastFrameData.m_velocity.magnitude}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.CarState = CarState.Stop;

																if (lastFrameData.m_velocity.magnitude < 0.5f ||
																	globalTargetPos.Stopped) {
																	globalTargetPos.Stopped = true;
																	globalTargetPos.WaitTime++;

																	float minStopWaitTime = Random.Range(0f, 3f);
																	if (globalTargetPos.WaitTime >= minStopWaitTime) {
																		hasIncomingCars = Options.disableSomething3 ? false : TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
#if DEBUG
																		if (debug)
																			Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

																		if (hasIncomingCars) {
																			maxSpeed = 0f;
																			return;
																		}
																		globalTargetPos.CarState = CarState.Leave;
																	} else {
																		maxSpeed = 0;
																		return;
																	}
																} else {
																	maxSpeed = 0f;
																	return;
																}
															} else {
																globalTargetPos.CarState = CarState.Leave;
															}
															break;
														case PrioritySegment.PriorityType.Yield:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: YIELD sign. waittime={globalTargetPos.WaitTime}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.WaitTime++;
																globalTargetPos.CarState = CarState.Stop;
																hasIncomingCars = Options.disableSomething3 ? false : TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
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
																globalTargetPos.CarState = CarState.Leave;
															} else {
																globalTargetPos.CarState = CarState.Leave;
															}
															break;
														case PrioritySegment.PriorityType.Main:
#if DEBUG
															if (debug)
																Log._Debug($"Vehicle {vehicleId}: MAIN sign. waittime={globalTargetPos.WaitTime}");
#endif
															if (globalTargetPos.WaitTime < MaxPriorityWaitTime) {
																globalTargetPos.WaitTime++;
																globalTargetPos.CarState = CarState.Stop;
																maxSpeed = 0f;

																hasIncomingCars = Options.disableSomething3 ? false : TrafficPriority.HasIncomingVehiclesWithHigherPriority(vehicleId, destinationNodeId);
#if DEBUG
																if (debug)
																	Log._Debug($"hasIncomingCars: {hasIncomingCars}");
#endif

																if (hasIncomingCars) {
																	globalTargetPos.Stopped = true;
																	return;
																}
																globalTargetPos.CarState = CarState.Leave;
																globalTargetPos.Stopped = false;
															}

															var info3 = netManager.m_segments.m_buffer[position.m_segment].Info;
															if (info3.m_lanes != null && info3.m_lanes.Length > position.m_lane) {
																//maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, info3.m_lanes[position.m_lane].m_speedLimit, netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve) * 0.8f;
																maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, ref info3.m_lanes[position.m_lane]), netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve) * 0.8f;
															} else {
																maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f) * 0.8f;
															}
															return;
													}
												} else {
													globalTargetPos.CarState = CarState.Leave;
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
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, ref info2.m_lanes[position.m_lane]); // info2.m_lanes[position.m_lane].m_speedLimit;

				/*if (TrafficRoadRestrictions.IsSegment(position.m_segment)) {
					var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

					if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f) {
						laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
					}
				}*/

				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,	netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			if (isRecklessDriver)
				maxSpeed *= Random.Range(1.5f, 3f);
		}

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData,
			PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f,
				out pos, out dir);
			var info = instance.m_segments.m_buffer[position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneId, ref info.m_lanes[position.m_lane]); //info.m_lanes[position.m_lane].m_speedLimit;

				/*if (TrafficRoadRestrictions.IsSegment(position.m_segment)) {
					var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

					if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f) {
						laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
					}
				}*/

				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,
					instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}
		}

		protected override bool StartPathFind(ushort vehicleId, ref Vehicle vehicleData, Vector3 startPos,
			Vector3 endPos, bool startBothWays, bool endBothWays) {
			var info = m_info;
			var allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) !=
								   Vehicle.Flags.None;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			const bool requireConnect = false;
			const float maxDistance = 32f;
			if (!PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle,
				info.m_vehicleType, allowUnderground, requireConnect, maxDistance, out startPosA, out startPosB,
				out num, out num2) ||
				!PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle,
					info.m_vehicleType, false, requireConnect, 32f, out endPosA, out endPosB, out num3, out num4))
				return false;
			if (!startBothWays || num < 10f) {
				startPosB = default(PathUnit.Position);
			}
			if (!endBothWays || num3 < 10f) {
				endPosB = default(PathUnit.Position);
			}
			uint path;

			if (!Singleton<CustomPathManager>.instance.CreatePath(out path,
				ref Singleton<SimulationManager>.instance.m_randomizer,
				Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB,
				default(PathUnit.Position), NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, IsHeavyVehicle(),
				IgnoreBlocked(vehicleId, ref vehicleData), false, false))
				return false;
			if (vehicleData.m_path != 0u) {
				Singleton<CustomPathManager>.instance.ReleasePath(vehicleData.m_path);
			}
			vehicleData.m_path = path;
			vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
			return true;
		}
	}
}
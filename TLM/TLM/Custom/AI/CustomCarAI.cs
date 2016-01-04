using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using TrafficManager.Custom.Manager;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TrafficManager.Custom.AI {
	internal class CustomCarAI : CarAI {
		private const float FarLod = 1210000f;
		private const float CloseLod = 250000f;
		public static HashSet<ushort> watchedVehicleIds = new HashSet<ushort>();

		private static int[] closeLodUpdateMod = new int[] { 0, 1, 2, 4, 6 };
		private static int[] farLodUpdateMod = new int[] { 4, 8, 10, 12, 16 };
		private static int[] veryFarLodUpdateMod = new int[] { 8, 10, 12, 16, 20 };

		internal static void OnLevelUnloading() {
			watchedVehicleIds.Clear();
		}

		/// <summary>
		/// Handles vehicle path information in order to manage special nodes (nodes with priority signs or traffic lights).
		/// Data like "vehicle X is on segment S0 and is going to segment S1" is collected.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		internal static void HandleVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			var netManager = Singleton<NetManager>.instance;
			var lastFrameData = vehicleData.GetLastFrameData();

#if DEBUG
			List<String> logBuffer = new List<String>();
			bool logme = (vehicleId == 16310);
#endif

			if (vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Car)
				return;
#if DEBUG
			logBuffer.Add("Calculating prio info for vehicleId " + vehicleId);
#endif

			// add vehicle to our vehicle list
			if (!TrafficPriority.Vehicles.ContainsKey(vehicleId)) {
				TrafficPriority.Vehicles.Add(vehicleId, new VehiclePosition());
			}

			// we extract the segment information directly from the vehicle
			var currentPathUnitId = vehicleData.m_path;
			List<ushort> realTimeDestinationNodes = new List<ushort>(); // current and upcoming node ids
			List<PathUnit.Position> realTimePositions = new List<PathUnit.Position>(); // current and upcoming vehicle positions

#if DEBUG
			logBuffer.Add("* vehicleId " + vehicleId + ". currentPathId: " + currentPathUnitId);
#endif

			if (currentPathUnitId > 0) {
				// vehicle has a path...
				var vehiclePathUnit = Singleton<PathManager>.instance.m_pathUnits.m_buffer[currentPathUnitId];
				if ((vehiclePathUnit.m_pathFindFlags & PathUnit.FLAG_READY) != 0) {
					// The path(unit) is established and is ready for use: get the vehicle's current position in terms of segment and lane
					realTimePositions.Add(vehiclePathUnit.GetPosition(vehicleData.m_pathPositionIndex >> 1));
					var currentSegment = netManager.m_segments.m_buffer[realTimePositions[0].m_segment];
					if (realTimePositions[0].m_offset == 0) {
						realTimeDestinationNodes.Add(currentSegment.m_startNode);
					} else {
						realTimeDestinationNodes.Add(currentSegment.m_endNode);
					}

					// evaluate upcoming path units
					byte i = 0;
					uint pathUnitId = currentPathUnitId;
					byte pathPos = (byte)((vehicleData.m_pathPositionIndex >> 1) + 1);
					var pathUnit = vehiclePathUnit;
					while (true) {
						if (pathPos > 11) {
							// go to next path unit
							pathPos = 0;
							pathUnitId = pathUnit.m_nextPathUnit;
							if (pathUnitId <= 0)
								break;
							pathUnit = Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathUnitId];
						}

						PathUnit.Position nextRealTimePosition = default(PathUnit.Position);
						if (!pathUnit.GetPosition(pathPos, out nextRealTimePosition)) // if this returns false, there is no next path unit
							break;

						ushort destNodeId = 0;
						if (nextRealTimePosition.m_segment > 0) {
							var nextSegment = netManager.m_segments.m_buffer[nextRealTimePosition.m_segment];
							if (nextRealTimePosition.m_offset == 0) {
								destNodeId = nextSegment.m_startNode;
							} else {
								destNodeId = nextSegment.m_endNode;
							}
						}

						realTimePositions.Add(nextRealTimePosition);
						realTimeDestinationNodes.Add(destNodeId);

						if (i >= 1)
							break; // we calculate up to 2 upcoming path units at the moment

						++pathPos;
						++i;
					}

					// please don't ask why we use "m_pathPositionIndex >> 1" (which equals to "m_pathPositionIndex / 2") here (Though it would
					// be interesting to know why they used such an ugly indexing scheme!!). I assume the oddness of m_pathPositionIndex relates
					// to the car's position on the segment. If it is even the car might be in the segment's first half and if it is odd, it might
					// be in the segment's second half.
#if DEBUG
					logBuffer.Add("* vehicleId " + vehicleId + ". *INFO* rtPos.seg=" + realTimePositions[0].m_segment + " nrtPos.seg=" + (realTimePositions.Count > 1 ? ""+realTimePositions[1].m_segment : "n/a"));
#endif
				}
			}

			// we have seen the car!
			TrafficPriority.Vehicles[vehicleId].LastFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex;

#if DEBUG
			logBuffer.Add("* vehicleId " + vehicleId + ". ToNode: " + TrafficPriority.Vehicles[vehicleId].ToNode + ". FromSegment: " + TrafficPriority.Vehicles[vehicleId].FromSegment + ". FromLaneId: " + TrafficPriority.Vehicles[vehicleId].FromLaneId);
#endif
			if (realTimePositions.Count >= 2) {
				// we found a valid path unit
				var sourceLaneId = PathManager.GetLaneID(realTimePositions[0]);
				var oldPriorityCar = TrafficPriority.Vehicles[vehicleId];
				if (oldPriorityCar.ToNode != realTimeDestinationNodes[0] ||
					oldPriorityCar.FromSegment != realTimePositions[0].m_segment ||
					oldPriorityCar.FromLaneId != sourceLaneId) {
					// vehicle information is not up-to-date. remove the car from an old priority segment (if existing)...
					var oldNode = oldPriorityCar.ToNode;
					var oldSegment = oldPriorityCar.FromSegment;

					var oldPrioritySegment = TrafficPriority.GetPrioritySegment(oldNode, oldSegment);
					if (oldPrioritySegment != null) {
						TrafficPriority.Vehicles[vehicleId].WaitTime = 0;
						TrafficPriority.Vehicles[vehicleId].Stopped = false;
					}

					// remove vehicle from all priority segments
					TrafficPriority.RemoveVehicle(vehicleId);

					// save vehicle information for priority rule handling
					TrafficPriority.Vehicles[vehicleId].ToNode = realTimeDestinationNodes[0];
					TrafficPriority.Vehicles[vehicleId].FromSegment = realTimePositions[0].m_segment;
					TrafficPriority.Vehicles[vehicleId].FromLaneId = PathManager.GetLaneID(realTimePositions[0]);
					TrafficPriority.Vehicles[vehicleId].ToSegment = realTimePositions[1].m_segment;
					if (realTimePositions[1].m_segment > 0)
						TrafficPriority.Vehicles[vehicleId].ToLaneId = PathManager.GetLaneID(realTimePositions[1]);
					else
						TrafficPriority.Vehicles[vehicleId].ToLaneId = 0;
					TrafficPriority.Vehicles[vehicleId].ReduceSpeedByValueToYield = Random.Range(13f, 18f);

					// add the vehicle to upcoming priority segments that have timed traffic lights
					for (int i = 0; i < realTimePositions.Count - 1; ++i) {
						var prioritySegment = TrafficPriority.GetPrioritySegment(realTimeDestinationNodes[i], realTimePositions[i].m_segment);
						if (prioritySegment == null)
							continue;

						// add upcoming segments only if there is a timed traffic light
						TrafficLightSimulation nodeSim = TrafficLightSimulation.GetNodeSimulation(realTimeDestinationNodes[i]);
						if (i > 0 && (nodeSim == null || !nodeSim.TimedTrafficLights || !nodeSim.TimedTrafficLightsActive))
							continue;

						VehiclePosition carPos = new VehiclePosition();
						carPos.LastFrame = TrafficPriority.Vehicles[vehicleId].LastFrame;
						carPos.ToNode = realTimeDestinationNodes[i];
						carPos.FromSegment = realTimePositions[i].m_segment;
						carPos.FromLaneId = PathManager.GetLaneID(realTimePositions[i]);
						carPos.ToSegment = realTimePositions[i+1].m_segment;
						if (realTimePositions[i+1].m_segment > 0)
							carPos.ToLaneId = PathManager.GetLaneID(realTimePositions[i+1]);
						else
							carPos.ToLaneId = 0;
						carPos.ReduceSpeedByValueToYield = Random.Range(13f, 18f);

						prioritySegment.AddCar(vehicleId, carPos);
					}
				}
			}
			
#if DEBUG
			if (false && logme && vehicleId % 5 == 0) {
				if (logme) {
					Log.Error("vehicleId: " + vehicleId + " ============================================");
				} else {
					Log.Warning("vehicleId: " + vehicleId + " ============================================");
				}
				foreach (String logBuf in logBuffer) {
					Log.Message(logBuf);
				}
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
				HandleVehicle(vehicleId, ref vehicleData);
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
			var pathManager = Singleton<PathManager>.instance;
			var pathFindFlags = pathManager.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_pathFindFlags;
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
			if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
				// add vehicle to our vehicle list
				if (!TrafficPriority.Vehicles.ContainsKey(vehicleId)) {
					TrafficPriority.Vehicles.Add(vehicleId, new VehiclePosition());
				}

				if (
					(
						(lastFrameVehiclePos - camPos).sqrMagnitude < CloseLod &&
						(TrafficPriority.Vehicles[vehicleId].LastFrame >> closeLodUpdateMod[Options.simAccuracy]) < (Singleton<SimulationManager>.instance.m_currentFrameIndex >> closeLodUpdateMod[Options.simAccuracy]) // very often
					) ||
					(
						(lastFrameVehiclePos - camPos).sqrMagnitude < FarLod && 
						(TrafficPriority.Vehicles[vehicleId].LastFrame >> farLodUpdateMod[Options.simAccuracy]) < (Singleton<SimulationManager>.instance.m_currentFrameIndex >> farLodUpdateMod[Options.simAccuracy]) // often
					) ||
						(TrafficPriority.Vehicles[vehicleId].LastFrame >> veryFarLodUpdateMod[Options.simAccuracy]) < (Singleton<SimulationManager>.instance.m_currentFrameIndex >> veryFarLodUpdateMod[Options.simAccuracy]) // less often
					) {
					//Log.Message("handle vehicle after threshold");
					try {
						HandleVehicle(vehicleId, ref vehicleData);
					} catch (Exception e) {
						Log.Error("CarAI TmCalculateSegmentPosition Error: " + e.ToString());
					}
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
			var interestingNodeId = prevOffset == 0 ? netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode :
				netManager.m_segments.m_buffer[prevPos.m_segment].m_endNode;
			
			// this seems to be like the required braking force in order to stop the vehicle within its half length.
			var crazyValue = 0.5f * lastFrameData.m_velocity.sqrMagnitude / m_info.m_braking + m_info.m_generatedInfo.m_size.z * 0.5f;

			// Essentially, this is true if the car has enough time and space to brake (e.g. for a red traffic light)
			if (destinationNodeId == interestingNodeId) {
				if (Vector3.Distance(lastFrameVehiclePos, vehiclePosOnBezier) >= crazyValue - 1f) {
					var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
					var num5 = (uint)((interestingNodeId << 8) / 32768);
					var num6 = currentFrameIndex - num5 & 255u;

					var nodeFlags = netManager.m_nodes.m_buffer[destinationNodeId].m_flags;
					var prevLaneFlags = (NetLane.Flags)netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_flags;
					var hasTrafficLight = (nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
					var hasCrossing = (nodeFlags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
					var isJoinedJunction = (prevLaneFlags & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
					if ((uint)vehicleId % (Options.getRecklessDriverModulo()/2) == 0) {
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

					if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
						if (hasTrafficLight && (!isJoinedJunction || hasCrossing)) {
							var nodeSimulation = TrafficLightSimulation.GetNodeSimulation(interestingNodeId);

							var destinationInfo = netManager.m_nodes.m_buffer[destinationNodeId].Info;
							RoadBaseAI.TrafficLightState vehicleLightState;
							ManualSegmentLight light = TrafficLightsManual.GetSegmentLight(interestingNodeId, prevPos.m_segment); // TODO rework

							if (light == null || nodeSimulation == null ||
								(nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive)) {
								RoadBaseAI.TrafficLightState pedestrianLightState;
								bool flag5;
								bool pedestrians;
								RoadBaseAI.GetTrafficLightState(interestingNodeId,
									ref netManager.m_segments.m_buffer[prevPos.m_segment],
									currentFrameIndex - num5, out vehicleLightState, out pedestrianLightState, out flag5,
									out pedestrians);
								if (!flag5 && num6 >= 196u) {
									flag5 = true;
									RoadBaseAI.SetTrafficLightState(interestingNodeId,
										ref netManager.m_segments.m_buffer[prevPos.m_segment], currentFrameIndex - num5,
										vehicleLightState, pedestrianLightState, flag5, pedestrians);
								}

								if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
									destinationInfo.m_class.m_service != ItemClass.Service.Road) {
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
								}
							} else {
								// traffic light simulation is active
								var stopCar = false;
								
								if (isRecklessDriver)
									vehicleLightState = RoadBaseAI.TrafficLightState.Green;
								else {
									// determine responsible traffic light (left, right or main)
									if (TrafficPriority.IsLeftSegment(prevPos.m_segment, position.m_segment, destinationNodeId)) {
										vehicleLightState = light.GetLightLeft();
									} else if (TrafficPriority.IsRightSegment(prevPos.m_segment, position.m_segment, destinationNodeId)) {
										vehicleLightState = light.GetLightRight();
									} else {
										vehicleLightState = light.GetLightMain();
									}
								}

								if (vehicleLightState == RoadBaseAI.TrafficLightState.Green) {
									var hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

									if (hasIncomingCars) {
										// green light but other cars are incoming: slow approach
										maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f) * 0.3f;
										//stopCar = true;
										return;
									}
								}

								if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None ||
									destinationInfo.m_class.m_service != ItemClass.Service.Road) {
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
								}

								if (stopCar) {
									maxSpeed = 0f;
									return;
								}
							}
						} else if ((lastFrameVehiclePos - camPos).sqrMagnitude < FarLod && !isRecklessDriver) {
							if (TrafficPriority.Vehicles.ContainsKey(vehicleId) &&
								TrafficPriority.IsPrioritySegment(destinationNodeId, prevPos.m_segment) &&
								TrafficPriority.Vehicles[vehicleId].ToNode == destinationNodeId &&
								TrafficPriority.Vehicles[vehicleId].FromSegment == prevPos.m_segment) {
								var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
								var frame = currentFrameIndex2 >> 4;

								var prioritySegment = TrafficPriority.GetPrioritySegment(destinationNodeId, prevPos.m_segment);

								if (TrafficPriority.Vehicles[vehicleId].CarState == CarState.None) {
									TrafficPriority.Vehicles[vehicleId].CarState = CarState.Enter;
								}

								if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None &&
									TrafficPriority.Vehicles[vehicleId].CarState != CarState.Leave) {
									bool hasIncomingCars;
									switch (prioritySegment.Type) {
										case PrioritySegment.PriorityType.Stop:
											if (TrafficPriority.Vehicles[vehicleId].WaitTime < 30) {
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Stop;

												if (lastFrameData.m_velocity.magnitude < 0.5f ||
													TrafficPriority.Vehicles[vehicleId].Stopped) {
													TrafficPriority.Vehicles[vehicleId].Stopped = true;
													TrafficPriority.Vehicles[vehicleId].WaitTime++;

													if (TrafficPriority.Vehicles[vehicleId].WaitTime > 1) {
														hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

														if (hasIncomingCars) {
															maxSpeed = 0f;
															return;
														}
														TrafficPriority.Vehicles[vehicleId].CarState =
															CarState.Leave;
													} else {
														maxSpeed = 0f;
														return;
													}
												} else {
													maxSpeed = 0f;
													return;
												}
											} else {
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Leave;
											}
											break;
										case PrioritySegment.PriorityType.Yield:
											if (TrafficPriority.Vehicles[vehicleId].WaitTime < 30) {
												TrafficPriority.Vehicles[vehicleId].WaitTime++;
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Stop;
												hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);
												if (hasIncomingCars) {
													if (lastFrameData.m_velocity.magnitude > 0) {
														maxSpeed = Math.Max(0f, lastFrameData.m_velocity.magnitude -
																   TrafficPriority.Vehicles[vehicleId].ReduceSpeedByValueToYield);
													} else {
														maxSpeed = 0f;
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
												}
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Leave;
											} else {
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Leave;
											}
											break;
										case PrioritySegment.PriorityType.Main:
											if (TrafficPriority.Vehicles[vehicleId].WaitTime < 30) {
												TrafficPriority.Vehicles[vehicleId].WaitTime++;
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Stop;
												maxSpeed = 0f;

												hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

												if (hasIncomingCars) {
													TrafficPriority.Vehicles[vehicleId].Stopped = true;
													return;
												}
												TrafficPriority.Vehicles[vehicleId].CarState = CarState.Leave;
												TrafficPriority.Vehicles[vehicleId].Stopped = false;
											}

											var info3 = netManager.m_segments.m_buffer[position.m_segment].Info;
											if (info3.m_lanes != null && info3.m_lanes.Length > position.m_lane) {
												maxSpeed =
													CalculateTargetSpeed(vehicleId, ref vehicleData,
														info3.m_lanes[position.m_lane].m_speedLimit,
														netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve) * 0.8f;
											} else {
												maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f) *
														   0.8f;
											}
											return;
									}
								} else {
									TrafficPriority.Vehicles[vehicleId].CarState = CarState.Transit;
								}
							}
						}
					}
				}
			}

			var info2 = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info2.m_lanes != null && info2.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = info2.m_lanes[position.m_lane].m_speedLimit;

				if (TrafficRoadRestrictions.IsSegment(position.m_segment)) {
					var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

					if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f) {
						laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
					}
				}

				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit,
					netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			if (isRecklessDriver)
				maxSpeed *= Random.Range(1.2f, 2.5f);
		}

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData,
			PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f,
				out pos, out dir);
			var info = instance.m_segments.m_buffer[position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = info.m_lanes[position.m_lane].m_speedLimit;

				if (TrafficRoadRestrictions.IsSegment(position.m_segment)) {
					var restrictionSegment = TrafficRoadRestrictions.GetSegment(position.m_segment);

					if (restrictionSegment.SpeedLimits[position.m_lane] > 0.1f) {
						laneSpeedLimit = restrictionSegment.SpeedLimits[position.m_lane];
					}
				}

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
				IgnoreBlocked(vehicleId, ref vehicleData), false, false, vehicleData.Info.m_class.m_service))
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
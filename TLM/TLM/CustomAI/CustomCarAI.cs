using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TrafficManager.CustomAI {
	internal class CustomCarAI : CarAI {
		private const int MaxTrailingVehicles = 16384;
		private const float FarLod = 1210000f;
		private const float CloseLod = 250000f;
		public static HashSet<ushort> watchedVehicleIds = new HashSet<ushort>();

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

			SimulateVehicleChain(vehicleId, ref vehicleData, physicsLodRefPos);

			DespawnVehicles(vehicleId, vehicleData);
		}

		/// <summary>
		/// Handles vehicle path information in order to manage special nodes (nodes with priority signs or traffic lights).
		/// Data like "vehicle X is on segment S0 and is going to segment S1" is collected.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		private void HandleVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			HandleVehicle(vehicleId, ref vehicleData, true);
		}

		private bool HandleVehicle(ushort vehicleId, ref Vehicle vehicleData, bool handleWatched) {
			bool handledVehicle = false;
			if (handleWatched) {
				var vehManager = Singleton<VehicleManager>.instance;
				// handle watched vehicles
				HashSet<ushort> toDelete = new HashSet<ushort>();
				foreach (ushort otherVehicleId in watchedVehicleIds) {
					Vehicle otherVehicle = vehManager.m_vehicles.m_buffer[otherVehicleId];
					if (otherVehicle.m_flags != Vehicle.Flags.None) {
						if (HandleVehicle(otherVehicleId, ref otherVehicle, false))
							toDelete.Add(otherVehicleId);
					} else {
						toDelete.Add(otherVehicleId);
					}
				}

				foreach (ushort vehicleIdToDelete in toDelete) {
					watchedVehicleIds.Remove(vehicleIdToDelete);
				}
			}

			var netManager = Singleton<NetManager>.instance;
			var lastFrameData = vehicleData.GetLastFrameData();

#if DEBUG
			List<String> logBuffer = new List<String>();
			bool logme = false;
#endif

			if (vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Car)
				return false;
#if DEBUG
			if (vehicleId % 5 == 0) {
				logBuffer.Add("Calculating prio info for vehicleId " + vehicleId);
			}
#endif

			// we extract the segment information directly from the vehicle
			var currentPathId = vehicleData.m_path;
			//ushort realTimeSourceNode = 0; // the car is currently moving from this node...
			ushort realTimeDestinationNode = 0; // ... to this node ...
			//ushort realTimeVeryNextDestinationNode = 0; // ... and then to this node ...
			PathUnit.Position? realTimePosition = null; // car position at current frame
			PathUnit.Position veryNextRealTimePosition = default(PathUnit.Position);
			List<PathUnit.Position> nextRealTimePositions = new List<PathUnit.Position>(); // car position after passing the next nodes
			List<ushort> nextRealTimeDestinationNodes = new List<ushort>(); // upcoming node ids

#if DEBUG
			if (vehicleId % 5 == 0) {
				logBuffer.Add("* vehicleId " + vehicleId + ". currentPathId: " + currentPathId);
			}
#endif

			if (currentPathId > 0) {
				// vehicle has a path...
				var vehiclePathUnit = Singleton<PathManager>.instance.m_pathUnits.m_buffer[currentPathId];
				if ((vehiclePathUnit.m_pathFindFlags & PathUnit.FLAG_READY) != 0) {
					// The path(unit) is established and is ready for use: get the vehicle's current position in terms of segment and lane
					realTimePosition = vehiclePathUnit.GetPosition(vehicleData.m_pathPositionIndex >> 1);
					var currentSegment = netManager.m_segments.m_buffer[realTimePosition.Value.m_segment];
					if (realTimePosition.Value.m_offset == 0) {
						realTimeDestinationNode = currentSegment.m_startNode;
						//realTimeSourceNode = currentSegment.m_endNode;
					} else {
						realTimeDestinationNode = currentSegment.m_endNode;
						//realTimeSourceNode = currentSegment.m_startNode;
					}

					// evaluate upcoming path units
					bool first = true;
					for (byte pathPos = (byte)((vehicleData.m_pathPositionIndex >> 1)+1); pathPos < vehiclePathUnit.m_positionCount; ++pathPos) {
						PathUnit.Position nextRealTimePosition = default(PathUnit.Position);
						if (!vehiclePathUnit.GetPosition(pathPos, out nextRealTimePosition)) // if this returns false, there is no next path unit
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

						// this is the very next path node, save it separately
						if (first) {
							veryNextRealTimePosition = nextRealTimePosition;
							//realTimeVeryNextDestinationNode = destNodeId;
							first = false;
							if (! handleWatched) {
								break;
								// we only need the first path unit
							}
						}

						nextRealTimePositions.Add(nextRealTimePosition);
						nextRealTimeDestinationNodes.Add(destNodeId);
					}

					// please don't ask why we use "m_pathPositionIndex >> 1" (which equals to "m_pathPositionIndex / 2") here (Though it would
					// be interesting to know why they used such an ugly indexing scheme!!). I assume m_pathPositionIndex relates to the car's position
					// on the segment. If it is even the car might be in the segement's first half and if it is odd, it might be in the segment's
					// second half.
#if DEBUG
					if (vehicleId % 5 == 0) {
						logBuffer.Add("* vehicleId " + vehicleId + ". *INFO* rtPos.seg=" + realTimePosition.Value.m_segment + " nrtPos.seg=" + veryNextRealTimePosition.m_segment);
					}
#endif
				}
			}

			// we have seen the car!
			TrafficPriority.VehicleList[vehicleId].LastFrame = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 4;
			TrafficPriority.VehicleList[vehicleId].LastSpeed = lastFrameData.m_velocity.sqrMagnitude;

#if DEBUG
			if (vehicleId % 5 == 0) {
				logBuffer.Add("* vehicleId " + vehicleId + ". ToNode: " + TrafficPriority.VehicleList[vehicleId].ToNode + ". FromSegment: " + TrafficPriority.VehicleList[vehicleId].FromSegment + ". FromLaneId: " + TrafficPriority.VehicleList[vehicleId].FromLaneId);
			}
#endif

			if (realTimePosition != null) {
				// we found a valid path unit
				var sourceLaneId = PathManager.GetLaneID(realTimePosition.Value);
				if (TrafficPriority.VehicleList[vehicleId].ToNode != realTimeDestinationNode ||
					TrafficPriority.VehicleList[vehicleId].FromSegment != realTimePosition.Value.m_segment ||
					TrafficPriority.VehicleList[vehicleId].FromLaneId != sourceLaneId) {
					// vehicle information is not up-to-date. remove the car from the old priority segment (if existing)...
					var oldNode = TrafficPriority.VehicleList[vehicleId].ToNode;
					var oldSegment = TrafficPriority.VehicleList[vehicleId].FromSegment;

					var oldPrioritySegment = TrafficPriority.GetPrioritySegment(oldNode, oldSegment);
					if (oldPrioritySegment != null) {
						TrafficPriority.VehicleList[vehicleId].WaitTime = 0;
						TrafficPriority.VehicleList[vehicleId].Stopped = false;
						if (oldPrioritySegment.RemoveCar(vehicleId)) {
#if DEBUG
							if (vehicleId % 5 == 0) {
								logBuffer.Add("### REMOVING vehicle " + vehicleId);
							}
#endif
						} else {
#if DEBUG
							if (vehicleId % 5 == 0) {
								logBuffer.Add("* vehicleId " + vehicleId + " was NOT REMOVED!");
							}
#endif
						}
					} else {
#if DEBUG
						if (vehicleId % 5 == 0) {
							logBuffer.Add("* vehicleId " + vehicleId + ". oldPrioSeg is null");
						}
#endif
					}

#if DEBUG
					if (vehicleId % 5 == 0) {
						logBuffer.Add("* vehicleId " + vehicleId + ". *VEHINFO* ToNode=" + realTimeDestinationNode + " FromSegment=" + realTimePosition.Value.m_segment + " FromLaneId=" + PathManager.GetLaneID(realTimePosition.Value) + " ToSegment=" + veryNextRealTimePosition.m_segment + " ToLaneId=" + (veryNextRealTimePosition.m_segment > 0 ? "" + PathManager.GetLaneID(veryNextRealTimePosition) : "n/a"));
					}
#endif

					if (handleWatched)
						watchedVehicleIds.Remove(vehicleId);
					handledVehicle = true;

					// ... and add it to the new priority segment
					var prioritySegment = TrafficPriority.GetPrioritySegment(realTimeDestinationNode, realTimePosition.Value.m_segment);
					if (prioritySegment != null) {
#if DEBUG
						logme = true;
#endif
						if (handleWatched)
							watchedVehicleIds.Add(vehicleId);

						TrafficPriority.VehicleList[vehicleId].ToNode = realTimeDestinationNode;
						TrafficPriority.VehicleList[vehicleId].FromSegment = realTimePosition.Value.m_segment;
						TrafficPriority.VehicleList[vehicleId].FromLaneId = PathManager.GetLaneID(realTimePosition.Value);
						TrafficPriority.VehicleList[vehicleId].ToSegment = veryNextRealTimePosition.m_segment;
						if (veryNextRealTimePosition.m_segment > 0)
							TrafficPriority.VehicleList[vehicleId].ToLaneId = PathManager.GetLaneID(veryNextRealTimePosition);
						else
							TrafficPriority.VehicleList[vehicleId].ToLaneId = 0;
						TrafficPriority.VehicleList[vehicleId].FromLaneFlags = netManager.m_lanes.m_buffer[TrafficPriority.VehicleList[vehicleId].FromLaneId].m_flags;
						TrafficPriority.VehicleList[vehicleId].ReduceSpeedByValueToYield = Random.Range(13f, 18f);

						if (prioritySegment.AddCar(vehicleId)) {
#if DEBUG
							if (vehicleId % 5 == 0) {
								logBuffer.Add("* vehicleId " + vehicleId + " was added!");
							}
#endif
							/*if (vehicleId % 16 == 0) {
								Log.Message("query: vehicleId: " + vehicleId + ", prevLaneID: " + prevLaneID + ", laneID: " + laneID + ", prevOffset: " + prevOffset + ", offset: " + offset);
								Log.Warning("*** ADDING vehicle " + vehicleId + " at (prev/cur/next) lane: (" + TrafficPriority.VehicleList[vehicleId].FromLaneId + "/" + TrafficPriority.VehicleList[vehicleId].ToLaneId + "/" + (nextPosition.m_segment > 0 ? "" + PathManager.GetLaneID(nextPosition) : "n/a") + "), (prev/cur/next) offset: (" + prevPos.m_offset + "/" + position.m_offset + "/" + nextPosition.m_offset + "), (prev/cur/next) segment: (" + prevPos.m_segment + "/" + position.m_segment + "/" + nextPosition.m_segment + ") at (src/dest/intrst) node (" + sourceNodeId + "/" + destinationNodeId + "/" + interestingNodeId + "), pathId: " + vehicleData.m_path + ", pathPosIndex: " + vehicleData.m_pathPositionIndex);
							}*/
						} else {
#if DEBUG
							if (vehicleId % 5 == 0) {
								logBuffer.Add("* vehicleId " + vehicleId + " was NOT added!");
							}
#endif
						}
					} else {
#if DEBUG
						if (vehicleId % 5 == 0) {
							logBuffer.Add("* vehicleId " + vehicleId + ". prioSeg is null");
						}
#endif
					}

					// add to watchlist if any upcoming node is a priority node
					if (handleWatched) {
						for (int i = 0; i < nextRealTimePositions.Count; ++i) {
							if (nextRealTimePositions[i].m_segment > 0) {
								var nextPrioritySegment = TrafficPriority.GetPrioritySegment(nextRealTimeDestinationNodes[i], nextRealTimePositions[i].m_segment);
								if (nextPrioritySegment != null)
									watchedVehicleIds.Add(vehicleId);
							}
						}
					}
				} else {
					var prioritySegment = TrafficPriority.GetPrioritySegment(realTimeDestinationNode, realTimePosition.Value.m_segment);
					if (prioritySegment != null) {
						// vehicle is still on priority segment
						if (handleWatched)
							watchedVehicleIds.Add(vehicleId);
					}
				}
			} else {
				if (TrafficPriority.VehicleList.ContainsKey(vehicleId)) {
					var oldNode = TrafficPriority.VehicleList[vehicleId].ToNode;
					var oldSegment = TrafficPriority.VehicleList[vehicleId].FromSegment;

					var oldPrioritySegment = TrafficPriority.GetPrioritySegment(oldNode, oldSegment);
					if (oldPrioritySegment != null) {
						oldPrioritySegment.RemoveCar(vehicleId);
					}
				}

				if (handleWatched)
					watchedVehicleIds.Remove(vehicleId);
				handledVehicle = true;
			}
			
#if DEBUG
			if (false && vehicleId == (ushort)12000/*logme || vehicleId % 100 == 0*/) {
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
			
			return handledVehicle;
		}

		private void SimulateVehicleChain(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			var lastFramePosition = vehicleData.GetLastFramePosition();

			var lodPhysics = CalculateLod(physicsLodRefPos, lastFramePosition);

			SimulationStep(vehicleId, ref vehicleData, vehicleId, ref vehicleData, lodPhysics);

			if (vehicleData.m_leadingVehicle != 0 || vehicleData.m_trailingVehicle == 0)
				return;

			var vehicleManager = Singleton<VehicleManager>.instance;
			var trailingVehicleId = vehicleData.m_trailingVehicle;
			SimulateTrailingVehicles(vehicleId, ref vehicleData, lodPhysics, trailingVehicleId, vehicleManager,
				0);
		}

		private void DespawnVehicles(ushort vehicleId, Vehicle vehicleData) {
			DespawnInvalidVehicles(vehicleId, vehicleData);

			DespawnVehicleIfOverBlockMax(vehicleId, vehicleData);
		}

		private void DespawnInvalidVehicles(ushort vehicleId, Vehicle vehicleData) {
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
				Vehicle.Flags.None && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
		}

		private void DespawnVehicleIfOverBlockMax(ushort vehicleId, Vehicle vehicleData) {
			var maxBlockingVehicles = CalculateMaxBlockingVehicleCount();
			if (vehicleData.m_blockCounter >= maxBlockingVehicles && LoadingExtension.Instance.DespawnEnabled) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
		}

		private int CalculateMaxBlockingVehicleCount() {
			return (m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
		}

		private void SimulateTrailingVehicles(ushort vehicleId, ref Vehicle vehicleData, int lodPhysics,
			ushort leadingVehicleId, VehicleManager vehicleManager, int numberOfIterations) {
			if (leadingVehicleId == 0) {
				return;
			}

			var trailingVehicleId = vehicleManager.m_vehicles.m_buffer[leadingVehicleId].m_trailingVehicle;
			var trailingVehicleInfo = vehicleManager.m_vehicles.m_buffer[trailingVehicleId].Info;

			trailingVehicleInfo.m_vehicleAI.SimulationStep(trailingVehicleId,
				ref vehicleManager.m_vehicles.m_buffer[trailingVehicleId], vehicleId,
				ref vehicleData, lodPhysics);

			if (++numberOfIterations > MaxTrailingVehicles) {
				CODebugBase<LogChannel>.Error(LogChannel.Core,
					"Invalid list detected!\n" + Environment.StackTrace);
				return;
			}
			SimulateTrailingVehicles(trailingVehicleId, ref vehicleData, lodPhysics, trailingVehicleId, vehicleManager,
				numberOfIterations);
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

		private void SpawnVehicleIfWaiting(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
				TrySpawn(vehicleId, ref vehicleData);
			}
		}

		private void FindPathIfNeeded(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) == Vehicle.Flags.None) return;

			if (!CanFindPath(vehicleId, ref vehicleData))
				throw new InvalidOperationException("Path Not Available for Vehicle");
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

			var lastFrameData = vehicleData.GetLastFrameData();
			var lastFrameVehiclePos = lastFrameData.m_position;

			if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
				// add vehicle to our vehicle list
				if (!TrafficPriority.VehicleList.ContainsKey(vehicleId)) {
					TrafficPriority.VehicleList.Add(vehicleId, new PriorityCar());
				}
			}

			HandleVehicle(vehicleId, ref vehicleData);

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

					if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
						if (hasTrafficLight && (!isJoinedJunction || hasCrossing)) {
							var nodeSimulation = TrafficPriority.GetNodeSimulation(interestingNodeId);

							var destinationInfo = netManager.m_nodes.m_buffer[destinationNodeId].Info;
							RoadBaseAI.TrafficLightState vehicleLightState;

							if (nodeSimulation == null ||
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

								// determine responsible traffic light (left, right or main)
								if (TrafficPriority.IsLeftSegment(prevPos.m_segment, position.m_segment, destinationNodeId)) {
									vehicleLightState = TrafficLightsManual.GetSegmentLight(interestingNodeId, prevPos.m_segment).GetLightLeft();
								} else if (TrafficPriority.IsRightSegment(prevPos.m_segment, position.m_segment, destinationNodeId)) {
									vehicleLightState = TrafficLightsManual.GetSegmentLight(interestingNodeId, prevPos.m_segment).GetLightRight();
								} else {
									vehicleLightState = TrafficLightsManual.GetSegmentLight(interestingNodeId, prevPos.m_segment).GetLightMain();
								}

								if (vehicleLightState == RoadBaseAI.TrafficLightState.Green) {
									var hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

									if (hasIncomingCars) {
										// green light but other cars are incoming: slow approach
										maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f) * 0.01f;
										//stopCar = true;
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
						} else {
							if (TrafficPriority.VehicleList.ContainsKey(vehicleId) &&
								TrafficPriority.IsPrioritySegment(destinationNodeId, prevPos.m_segment)) {
								var currentFrameIndex2 = Singleton<SimulationManager>.instance.m_currentFrameIndex;
								var frame = currentFrameIndex2 >> 4;

								var prioritySegment = TrafficPriority.GetPrioritySegment(destinationNodeId, prevPos.m_segment);

								if (TrafficPriority.VehicleList[vehicleId].CarState == CarState.None) {
									TrafficPriority.VehicleList[vehicleId].CarState = CarState.Enter;
								}

								if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == Vehicle.Flags.None &&
									TrafficPriority.VehicleList[vehicleId].CarState != CarState.Leave) {
									bool hasIncomingCars;
									switch (prioritySegment.Type) {
										case PrioritySegment.PriorityType.Stop:
											if (TrafficPriority.VehicleList[vehicleId].WaitTime < 75) {
												TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;

												if (lastFrameData.m_velocity.sqrMagnitude < 0.1f ||
													TrafficPriority.VehicleList[vehicleId].Stopped) {
													TrafficPriority.VehicleList[vehicleId].Stopped = true;
													TrafficPriority.VehicleList[vehicleId].WaitTime++;

													if (TrafficPriority.VehicleList[vehicleId].WaitTime > 2) {
														hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

														if (hasIncomingCars) {
															maxSpeed = 0f;
															return;
														}
														TrafficPriority.VehicleList[vehicleId].CarState =
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
												TrafficPriority.VehicleList[vehicleId].CarState = CarState.Leave;
											}
											break;
										case PrioritySegment.PriorityType.Yield:
											if (TrafficPriority.VehicleList[vehicleId].WaitTime < 75) {
												TrafficPriority.VehicleList[vehicleId].WaitTime++;
												TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;
												maxSpeed = 0f;

												if (lastFrameData.m_velocity.sqrMagnitude <
													TrafficPriority.VehicleList[vehicleId].ReduceSpeedByValueToYield) {
													hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

													if (hasIncomingCars) {
														return;
													}
												} else {
													maxSpeed = lastFrameData.m_velocity.sqrMagnitude -
															   TrafficPriority.VehicleList[vehicleId]
																   .ReduceSpeedByValueToYield;
													return;
												}
											} else {
												TrafficPriority.VehicleList[vehicleId].CarState = CarState.Leave;
											}
											break;
										case PrioritySegment.PriorityType.Main:
											TrafficPriority.VehicleList[vehicleId].WaitTime++;
											TrafficPriority.VehicleList[vehicleId].CarState = CarState.Stop;
											maxSpeed = 0f;

											hasIncomingCars = TrafficPriority.HasIncomingVehicles(vehicleId, destinationNodeId);

											if (hasIncomingCars) {
												TrafficPriority.VehicleList[vehicleId].Stopped = true;
												return;
											}
											TrafficPriority.VehicleList[vehicleId].Stopped = false;

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
									TrafficPriority.VehicleList[vehicleId].CarState = CarState.Transit;
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
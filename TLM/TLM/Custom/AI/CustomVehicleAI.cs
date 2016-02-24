using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.TrafficLight;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomVehicleAI : VehicleAI {
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
			if ((vehicleData.m_flags & Vehicle.Flags.Created) == Vehicle.Flags.None) {
				TrafficPriority.RemoveVehicleFromSegments(vehicleId);
				return;
			}

			if (vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Car &&
				vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Train &&
				vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Tram) {
				//Log._Debug($"HandleVehicle does not handle vehicles of type {vehicleData.Info.m_vehicleType}");
				return;
			}
#if DEBUGV
			logBuffer.Add("Calculating prio info for vehicleId " + vehicleId);
#endif

			ExtVehicleType? vehicleType = CustomVehicleAI.DetermineVehicleTypeFromVehicle(vehicleId, ref vehicleData);
			if (vehicleType == null) {
				Log.Warning($"Could not determine vehicle type of vehicle {vehicleId}!");
			}

			if (vehicleType == null || vehicleType == ExtVehicleType.None) {
				return;
			}

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
			if (realTimePositions.Count >= 1) {
				// we found a valid path unit
				var sourceLaneIndex = realTimePositions[0].m_lane;

				if (
					!vehiclePos.Valid ||
					vehiclePos.ToNode != realTimeDestinationNodes[0] ||
					vehiclePos.FromSegment != realTimePositions[0].m_segment ||
					vehiclePos.FromLaneIndex != sourceLaneIndex) {
					// vehicle information is not up-to-date. remove the car from old priority segments (if existing)...
					TrafficPriority.RemoveVehicleFromSegments(vehicleId);

					if (realTimePositions.Count >= 2) {
						// save vehicle information for priority rule handling
						vehiclePos.Valid = true;
						vehiclePos.CarState = VehicleJunctionTransitState.None;
						vehiclePos.WaitTime = 0;
						vehiclePos.Stopped = false;
						vehiclePos.ToNode = realTimeDestinationNodes[0];
						vehiclePos.FromSegment = realTimePositions[0].m_segment;
						vehiclePos.FromLaneIndex = realTimePositions[0].m_lane;
						vehiclePos.ToSegment = realTimePositions[1].m_segment;
						vehiclePos.ToLaneIndex = realTimePositions[1].m_lane;
						vehiclePos.ReduceSpeedByValueToYield = UnityEngine.Random.Range(16f, 28f);
						vehiclePos.OnEmergency = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None;
						vehiclePos.VehicleType = (ExtVehicleType)vehicleType;

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
							upcomingVehiclePos.CarState = VehicleJunctionTransitState.None;
							upcomingVehiclePos.LastFrame = vehiclePos.LastFrame;
							upcomingVehiclePos.ToNode = realTimeDestinationNodes[i];
							upcomingVehiclePos.FromSegment = realTimePositions[i].m_segment;
							upcomingVehiclePos.FromLaneIndex = realTimePositions[i].m_lane;
							upcomingVehiclePos.ToSegment = realTimePositions[i + 1].m_segment;
							upcomingVehiclePos.ToLaneIndex = realTimePositions[i + 1].m_lane;
							upcomingVehiclePos.ReduceSpeedByValueToYield = UnityEngine.Random.Range(16f, 28f);
							upcomingVehiclePos.OnEmergency = (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None;
							upcomingVehiclePos.VehicleType = (ExtVehicleType)vehicleType;
#if DEBUGV
							logBuffer.Add($"* vehicleId {vehicleId}. Adding future position: from {upcomingVehiclePos.FromSegment}  (lane {upcomingVehiclePos.FromLaneIndex}), going over {upcomingVehiclePos.ToNode}, to {upcomingVehiclePos.ToSegment} (lane {upcomingVehiclePos.ToLaneIndex})");
#endif

							prioritySegment.AddVehicle(vehicleId, upcomingVehiclePos);
						}
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
				TrafficPriority.RemoveVehicleFromSegments(vehicleId);
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

		internal static ExtVehicleType? DetermineVehicleTypeFromVehicle(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None)
				return ExtVehicleType.Emergency;
			/*else {
				VehiclePosition vehiclePos = TrafficPriority.GetVehiclePosition(vehicleId);
				if (vehiclePos != null && vehiclePos.Valid && vehiclePos.VehicleType != ExtVehicleType.Emergency)
					return vehiclePos.VehicleType;
			}*/

			VehicleAI ai = vehicleData.Info.m_vehicleAI;
			return DetermineVehicleTypeFromAIType(ai, (vehicleData.m_flags & Vehicle.Flags.Emergency2) != Vehicle.Flags.None);
		}

		internal static ExtVehicleType? DetermineVehicleTypeFromVehicleInfo(VehicleInfo vehicleInfo) {
			VehicleAI ai = vehicleInfo.m_vehicleAI;
			return DetermineVehicleTypeFromAIType(ai, false);
		}

		internal static ExtVehicleType? DetermineVehicleTypeFromAIType(VehicleAI ai, bool emergencyOnDuty) {
			switch (ai.m_info.m_vehicleType) {
				case VehicleInfo.VehicleType.Bicycle:
					return ExtVehicleType.Bicycle;
				case VehicleInfo.VehicleType.Car:
					if (ai is PassengerCarAI)
						return ExtVehicleType.PassengerCar;
					if (ai is AmbulanceAI || ai is FireTruckAI || ai is PoliceCarAI) {
						if (emergencyOnDuty)
							return ExtVehicleType.Emergency;
						return ExtVehicleType.Service;
					}
					if (ai is CarTrailerAI)
						return ExtVehicleType.None;
					if (ai is BusAI)
						return ExtVehicleType.Bus;
					if (ai is TaxiAI)
						return ExtVehicleType.Taxi;
					if (ai is CargoTruckAI)
						return ExtVehicleType.CargoTruck;
					if (ai is HearseAI || ai is GarbageTruckAI || ai is MaintenanceTruckAI || ai is SnowTruckAI)
						return ExtVehicleType.Service;
					break;
				case VehicleInfo.VehicleType.Metro:
				case VehicleInfo.VehicleType.Train:
					if (ai is PassengerTrainAI)
						return ExtVehicleType.PassengerTrain;
					if (ai is CargoTrainAI)
						return ExtVehicleType.CargoTrain;
					break;
				case VehicleInfo.VehicleType.Tram:
					return ExtVehicleType.Tram;
				case VehicleInfo.VehicleType.Ship:
					if (ai is PassengerShipAI)
						return ExtVehicleType.PassengerShip;
					//if (ai is CargoShipAI)
						return ExtVehicleType.CargoShip;
					//break;
				case VehicleInfo.VehicleType.Plane:
					//if (ai is PassengerPlaneAI)
						return ExtVehicleType.PassengerPlane;
					//break;
			}
			Log._Debug($"Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
			return null;
		}
	}
}

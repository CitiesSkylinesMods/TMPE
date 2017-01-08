#define DEBUGVx
#define USEPATHWAITCOUNTERx
#define PATHRECALCx

using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Math;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using Random = UnityEngine.Random;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using static TrafficManager.Traffic.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	public class CustomCarAI : CarAI { // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
		public void Awake() {

		}

		/// <summary>
		/// Lightweight simulation step method.
		/// This method is occasionally being called for different cars.
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <param name="physicsLodRefPos"></param>
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			PathManager pathMan = Singleton<PathManager>.instance;
#if DEBUG
			/*if (!GlobalConfig.Instance.DebugSwitches[0]) {
				Log._Debug($"CustomCarAI.CustomSimulationStep({vehicleId}) called. flags: {vehicleData.m_flags} pfFlags: {pathMan.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags}");
            }*/
#endif

			// NON-STOCK CODE START
			VehicleState state = null;
			ExtCitizenInstance driverExtInstance = null;
			bool prohibitPocketCars = Options.prohibitPocketCars;
			if (prohibitPocketCars) {
				// check for valid driver and update return path state
				state = VehicleStateManager.Instance._GetVehicleState(vehicleData.GetFirstVehicle(vehicleId));
				if (state.VehicleType == ExtVehicleType.PassengerCar) {
					driverExtInstance = state.GetDriverExtInstance();
					if (driverExtInstance == null) {
						prohibitPocketCars = false;
					} else {
						driverExtInstance.UpdateReturnPathState();
					}
				} else {
					prohibitPocketCars = false;
				}
			}
			// NON-STOCK CODE END

			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0 &&
				(! prohibitPocketCars || driverExtInstance.ReturnPathState != ExtCitizenInstance.ExtPathState.Calculating)) { // NON-STOCK CODE: Parking AI: wait for the return path to be calculated
				PathManager pathManager = Singleton<PathManager>.instance;
				byte pathFindFlags = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

				bool pathFindFailed = (pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0; // path == 0: non-stock code!
				bool pathFindSucceeded = (pathFindFlags & PathUnit.FLAG_READY) != 0;

#if USEPATHWAITCOUNTER
				if ((pathFindFlags & (PathUnit.FLAG_READY | PathUnit.FLAG_FAILED)) != 0) {
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleId);
					state.PathWaitCounter = 0; // NON-STOCK CODE
				}
#endif

				if (prohibitPocketCars) {
					if (driverExtInstance.ReturnPathState == ExtPathState.Failed) {
						// no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomCarAI.CustomSimulationStep: Return path {driverExtInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
#endif
						pathFindSucceeded = false;
						pathFindFailed = true;
					}

					driverExtInstance.ReleaseReturnPath();

					if (pathFindSucceeded) {
						CustomPassengerCarAI.OnPathFindSuccess(vehicleId, ref vehicleData, driverExtInstance);
					} else if (pathFindFailed) {
						CustomPassengerCarAI.OnPathFindFailure(driverExtInstance, vehicleId);
					}
				}

				if (pathFindSucceeded) {
					vehicleData.m_pathPositionIndex = 255;
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					vehicleData.m_flags &= ~Vehicle.Flags.Arriving;
					this.PathfindSuccess(vehicleId, ref vehicleData);
					this.TrySpawn(vehicleId, ref vehicleData);
				} else if (pathFindFailed) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					this.PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
#if USEPATHWAITCOUNTER
				else {
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleId);
					state.PathWaitCounter = (ushort)Math.Min(ushort.MaxValue, (int)state.PathWaitCounter+1); // NON-STOCK CODE
				}
#endif
			} else {
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
					this.TrySpawn(vehicleId, ref vehicleData);
				}
			}

			/// NON-STOCK CODE START ///
			VehicleStateManager vehStateManager = VehicleStateManager.Instance;
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				// update vehicle position for timed traffic lights and priority signs
				try {
					vehStateManager.UpdateVehiclePos(vehicleId, ref vehicleData);
				} catch (Exception e) {
					Log.Error("CarAI CustomSimulationStep Error: " + e.ToString());
				}
			}

			if (!Options.isStockLaneChangerUsed()) {
				// Advanced AI traffic measurement
				try {
					vehStateManager.LogTraffic(vehicleId, ref vehicleData, true);
				} catch (Exception e) {
					Log.Error("CarAI CustomSimulationStep Error: " + e.ToString());
				}
			}
			/// NON-STOCK CODE END ///

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
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				ushort num = vehicleData.m_trailingVehicle;
				int num2 = 0;
				while (num != 0) {
					ushort trailingVehicle = vehManager.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
					VehicleInfo info = vehManager.m_vehicles.m_buffer[(int)num].Info;
					info.m_vehicleAI.SimulationStep(num, ref vehManager.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, lodPhysics);
					num = trailingVehicle;
					if (++num2 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}
#if PATHRECALC
			ushort recalcSegmentId = 0;
#endif
			int privateServiceIndex = ItemClass.GetPrivateServiceIndex(this.m_info.m_class.m_service);
			int maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) == 0 && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if ((int)vehicleData.m_blockCounter >= maxBlockCounter && Options.enableDespawning) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			}
#if PATHRECALC
			else if (vehicleData.m_leadingVehicle == 0 && CustomVehicleAI.ShouldRecalculatePath(vehicleId, ref vehicleData, maxBlockCounter, out recalcSegmentId)) {
				CustomVehicleAI.MarkPathRecalculation(vehicleId, recalcSegmentId);
				InvalidPath(vehicleId, ref vehicleData, vehicleId, ref vehicleData);
			}
#endif
		}

		public override bool TrySpawn(ushort vehicleID, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0) {
				// NON-STOCK CODE START
				if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
					VehicleStateManager.Instance.OnVehicleSpawned(vehicleID, ref vehicleData);
				}
				// NON-STOCK CODE END

				return true;
			}
			if (CustomCarAI.CheckOverlap(vehicleData.m_segment, 0, 1000f)) {
				vehicleData.m_flags |= Vehicle.Flags.WaitingSpace;
				return false;
			}
			vehicleData.Spawn(vehicleID);
			vehicleData.m_flags &= ~Vehicle.Flags.WaitingSpace;

			// NON-STOCK CODE START
			if (Options.prioritySignsEnabled || Options.timedLightsEnabled) {
				VehicleStateManager.Instance.OnVehicleSpawned(vehicleID, ref vehicleData);
			}
			// NON-STOCK CODE END

			return true;
		}

		private static bool CheckOverlap(Segment3 segment, ushort ignoreVehicle, float maxVelocity) {
			Log.Error("CustomCarAI.CheckOverlap called");
			return false;
		}

		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
				PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID,
				byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			if ((Options.prioritySignsEnabled || Options.timedLightsEnabled) && Options.simAccuracy <= 1) {
				// update vehicle position for timed traffic lights and priority signs
				try {
					VehicleStateManager.Instance.UpdateVehiclePos(vehicleId, ref vehicleData, ref prevPos, ref position);
				} catch (Exception e) {
					Log.Error("CarAI CustomCalculateSegmentPosition Error: " + e.ToString());
				}
			}

			var netManager = Singleton<NetManager>.instance;
			//var vehicleManager = Singleton<VehicleManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);

			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 lastFrameVehiclePos = lastFrameData.m_position;

			var camPos = Camera.main.transform.position;

#if DEBUG
			//bool isEmergency = VehicleStateManager._GetVehicleState(vehicleId).VehicleType == ExtVehicleType.Emergency;
#endif

			// I think this is supposed to be the lane position?
			// [VN, 12/23/2015] It's the 3D car position on the Bezier curve of the lane.
			// This crazy 0.003921569f equals to 1f/255 and prevOffset is the byte value (0..255) of the car position.
			var vehiclePosOnBezier = netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].CalculatePosition(prevOffset * 0.003921569f);
			//ushort currentSegmentId = netManager.m_lanes.m_buffer[(int)((UIntPtr)prevLaneID)].m_segment;

			ushort targetNodeId;
			ushort nextTargetNodeId;
			if (offset < position.m_offset) {
				targetNodeId = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
				nextTargetNodeId = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
			} else {
				targetNodeId = netManager.m_segments.m_buffer[position.m_segment].m_endNode;
				nextTargetNodeId = netManager.m_segments.m_buffer[position.m_segment].m_startNode;
			}
			var prevTargetNodeId = prevOffset == 0 ? netManager.m_segments.m_buffer[prevPos.m_segment].m_startNode : netManager.m_segments.m_buffer[prevPos.m_segment].m_endNode;

			// this seems to be like the required braking force in order to stop the vehicle within its half length.
			var crazyValue = 0.5f * lastFrameData.m_velocity.sqrMagnitude / m_info.m_braking + m_info.m_generatedInfo.m_size.z * 0.5f;

			bool isRecklessDriver = IsRecklessDriver(vehicleId, ref vehicleData);
			if (targetNodeId == prevTargetNodeId) {
				if (Vector3.Distance(lastFrameVehiclePos, vehiclePosOnBezier) >= crazyValue - 1f) {
					if (!CustomVehicleAI.MayChangeSegment(vehicleId, ref vehicleData, ref lastFrameData, isRecklessDriver, ref prevPos, prevTargetNodeId, prevLaneID, ref position, targetNodeId, laneID, ref nextPosition, nextTargetNodeId, out maxSpeed))
						return;
				}
			}

			var info2 = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info2.m_lanes != null && info2.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info2.m_lanes[position.m_lane]) : info2.m_lanes[position.m_lane].m_speedLimit; // info2.m_lanes[position.m_lane].m_speedLimit;
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
			VehicleState state = VehicleStateManager.Instance.GetVehicleState(vehicleId);

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

			if (Options.realisticSpeeds) {
				float vehicleRand = Math.Min(1f, (float)(vehicleId % 101) * 0.01f); // we choose 101 because it's a prime number
				if (state != null && state.HeavyVehicle)
					maxSpeed *= 0.9f + vehicleRand * 0.1f; // a little variance, 0.85 .. 1
				else if (isRecklessDriver)
					maxSpeed *= 1.2f + vehicleRand * 0.8f; // woohooo, 1.2 .. 2
				else
					maxSpeed *= 0.8f + vehicleRand * 0.5f; // a little variance, 0.8 .. 1.3
			} else {
				if (isRecklessDriver)
					maxSpeed *= 1.4f;
			}

			maxSpeed = Math.Max(MIN_SPEED, maxSpeed); // at least 10 km/h

			return maxSpeed;
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[(int)((UIntPtr)laneId)].CalculatePositionAndDirection(offset * 0.003921569f,
				out pos, out dir);
			var info = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
				var laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneId, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[(int)((UIntPtr)laneId)].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			maxSpeed = CalcMaxSpeed(vehicleId, ref vehicleData, position, pos, maxSpeed, IsRecklessDriver(vehicleId, ref vehicleData)); // NON-STOCK CODE
		}

		/// <summary>
		/// Determines if the given vehicle is driven by a reckless driver
		/// </summary>
		/// <param name="vehicleId"></param>
		/// <param name="vehicleData"></param>
		/// <returns></returns>
		internal static bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0)
				return true;
			if (Options.evacBussesMayIgnoreRules && vehicleData.Info.GetService() == ItemClass.Service.Disaster)
				return true;
			if (Options.recklessDrivers == 3)
				return false;

			return ((vehicleData.Info.m_vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) && (uint)vehicleId % (Options.getRecklessDriverModulo()) == 0;
		}


		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif
#if DEBUG
			bool debug = GlobalConfig.Instance.DebugSwitches[8] && vehicleData.m_sourceBuilding == 23712;
			if (debug)
				Log._Debug($"CustomCarAI.CustomStartPathFind called for vehicle {vehicleID} which is a {VehicleStateManager.Instance._GetVehicleState(vehicleID).VehicleType} (AI: {this.GetType().Name}). going from {vehicleData.m_sourceBuilding} to {vehicleData.m_targetBuilding}");
#endif

			VehicleInfo info = this.m_info;
			bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startDistSqrA;
			float startDistSqrB;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endDistSqrA;
			float endDistSqrB;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out startDistSqrA, out startDistSqrB) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, undergroundTarget, false, 32f, out endPosA, out endPosB, out endDistSqrA, out endDistSqrB)) {
				if (!startBothWays || startDistSqrA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endDistSqrA < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				ExtVehicleType vehicleType = VehicleStateManager.Instance._GetVehicleState(vehicleID).VehicleType;
				if (vehicleType == ExtVehicleType.None) {
#if DEBUG
					Log.Warning($"CustomCarAI.CustomStartPathFind: Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
					vehicleType = ExtVehicleType.RoadVehicle;
				}

				if (CustomPathManager._instance.CreatePath(
#if PATHRECALC
					recalcRequested, 
#endif
					(ExtVehicleType)vehicleType, vehicleID, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false)) {

#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

#if DEBUG
					if (debug || GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"Path-finding starts for car {vehicleID}, path={path}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
#endif


					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
#if DEBUG
			if (debug)
				Log._Debug($"Path-finding failed for car {vehicleID} (path could not be created)");
#endif
			return false;
		}
	}
}
#define DEBUGVx

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
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using System.Runtime.CompilerServices;
using TrafficManager.Traffic.Data;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;
using TrafficManager.Traffic.Enums;
using TrafficManager.RedirectionFramework.Attributes;

namespace TrafficManager.Custom.AI {
	[TargetType(typeof(CarAI))]
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
		[RedirectMethod]
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				PathManager pathManager = Singleton<PathManager>.instance;
				byte pathFindFlags = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

				// NON-STOCK CODE START
				ExtPathState mainPathState = ExtPathState.Calculating;
				if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
					mainPathState = ExtPathState.Failed;
				} else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
					mainPathState = ExtPathState.Ready;
				}

				if (Options.parkingAI && VehicleStateManager.Instance.VehicleStates[vehicleId].vehicleType == ExtVehicleType.PassengerCar) {
					mainPathState = AdvancedParkingManager.Instance.UpdateCarPathState(vehicleId, ref vehicleData, ref ExtCitizenInstanceManager.Instance.ExtInstances[Constants.ManagerFactory.VehicleStateManager.GetDriverInstanceId(vehicleId, ref vehicleData)], mainPathState);
				}
				// NON-STOCK CODE END

				if (mainPathState == ExtPathState.Ready) {
					vehicleData.m_pathPositionIndex = 255;
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					vehicleData.m_flags &= ~Vehicle.Flags.Arriving;
					this.PathfindSuccess(vehicleId, ref vehicleData);
					this.TrySpawn(vehicleId, ref vehicleData);
				} else if (mainPathState == ExtPathState.Failed) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					this.PathfindFailure(vehicleId, ref vehicleData);
					return;
				}
			} else {
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
					this.TrySpawn(vehicleId, ref vehicleData);
				}
			}

			// NON-STOCK CODE START
			VehicleStateManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData);

			if (!Options.isStockLaneChangerUsed()) {
				// Advanced AI traffic measurement
				VehicleStateManager.Instance.LogTraffic(vehicleId);
			}
			// NON-STOCK CODE END

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
				ushort trailerId = vehicleData.m_trailingVehicle;
				int numIters = 0;
				while (trailerId != 0) {
					ushort trailingVehicle = vehManager.m_vehicles.m_buffer[(int)trailerId].m_trailingVehicle;
					VehicleInfo info = vehManager.m_vehicles.m_buffer[(int)trailerId].Info;
					info.m_vehicleAI.SimulationStep(trailerId, ref vehManager.m_vehicles.m_buffer[(int)trailerId], vehicleId, ref vehicleData, lodPhysics);
					trailerId = trailingVehicle;
					if (++numIters > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}

			int privateServiceIndex = ItemClass.GetPrivateServiceIndex(this.m_info.m_class.m_service);
			int maxBlockCounter = (privateServiceIndex == -1) ? 150 : 100;
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) == 0 && vehicleData.m_cargoParent == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if ((int)vehicleData.m_blockCounter >= maxBlockCounter) {
				// NON-STOCK CODE START
				if (VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
					// NON-STOCK CODE END
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} // NON-STOCK CODE
			}
		}

		[RedirectMethod]
		public bool CustomTrySpawn(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != (Vehicle.Flags)0) {
				return true;
			}
			if (CustomCarAI.CheckOverlap(vehicleData.m_segment, 0, 1000f)) {
				vehicleData.m_flags |= Vehicle.Flags.WaitingSpace;
				return false;
			}
			vehicleData.Spawn(vehicleId);
			vehicleData.m_flags &= ~Vehicle.Flags.WaitingSpace;
			return true;
		}

		[RedirectMethod]
		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
				PathUnit.Position prevPosition, uint prevLaneId, byte prevOffset, PathUnit.Position refPosition, uint refLaneId,
				byte refOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			ushort prevSourceNodeId;
			ushort prevTargetNodeId;
			if (prevOffset < prevPosition.m_offset) {
				prevSourceNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_startNode;
				prevTargetNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_endNode;
			} else {
				prevSourceNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_endNode;
				prevTargetNodeId = netManager.m_segments.m_buffer[prevPosition.m_segment].m_startNode;
			}

			ushort refTargetNodeId;
			if (refOffset == 0) {
				refTargetNodeId = netManager.m_segments.m_buffer[(int)refPosition.m_segment].m_startNode;
			} else {
				refTargetNodeId = netManager.m_segments.m_buffer[(int)refPosition.m_segment].m_endNode;
			}

#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[21] && (GlobalConfig.Instance.Debug.NodeId <= 0 || refTargetNodeId == GlobalConfig.Instance.Debug.NodeId) && (GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RoadVehicle) && (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId);

			if (debug) {
				Log._Debug($"CustomCarAI.CustomCalculateSegmentPosition({vehicleId}) called.\n" +
					$"\trefPosition.m_segment={refPosition.m_segment}, refPosition.m_offset={refPosition.m_offset}\n" +
					$"\tprevPosition.m_segment={prevPosition.m_segment}, prevPosition.m_offset={prevPosition.m_offset}\n" +
					$"\tnextPosition.m_segment={nextPosition.m_segment}, nextPosition.m_offset={nextPosition.m_offset}\n" +
					$"\trefLaneId={refLaneId}, refOffset={refOffset}\n" +
					$"\tprevLaneId={prevLaneId}, prevOffset={prevOffset}\n" +
					$"\tprevSourceNodeId={prevSourceNodeId}, prevTargetNodeId={prevTargetNodeId}\n" +
					$"\trefTargetNodeId={refTargetNodeId}, refTargetNodeId={refTargetNodeId}\n" +
					$"\tindex={index}");
			}
#endif

			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 lastFrameVehiclePos = lastFrameData.m_position;
			float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

			netManager.m_lanes.m_buffer[prevLaneId].CalculatePositionAndDirection(prevOffset * 0.003921569f, out pos, out dir);

			float braking = this.m_info.m_braking;
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != (Vehicle.Flags)0) {
				braking *= 2f;
			}

			// car position on the Bezier curve of the lane
			var refVehiclePosOnBezier = netManager.m_lanes.m_buffer[refLaneId].CalculatePosition(refOffset * 0.003921569f);
			//ushort currentSegmentId = netManager.m_lanes.m_buffer[prevLaneID].m_segment;

			// this seems to be like the required braking force in order to stop the vehicle within its half length.
			var crazyValue = 0.5f * sqrVelocity / braking + m_info.m_generatedInfo.m_size.z * 0.5f;
			bool withinBrakingDistance = Vector3.Distance(lastFrameVehiclePos, refVehiclePosOnBezier) >= crazyValue - 1f;

			if (prevSourceNodeId == refTargetNodeId && withinBrakingDistance) {
				// NON-STOCK CODE START (stock code replaced)
				if (!VehicleBehaviorManager.Instance.MayChangeSegment(vehicleId, ref vehicleData, sqrVelocity, ref refPosition, ref netManager.m_segments.m_buffer[refPosition.m_segment], refTargetNodeId, refLaneId, ref prevPosition, prevSourceNodeId, ref netManager.m_nodes.m_buffer[prevSourceNodeId], prevLaneId, ref nextPosition, prevTargetNodeId, out maxSpeed)) { // NON-STOCK CODE
					return;
				} else {
					VehicleStateManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData/*, lastFrameData.m_velocity.magnitude*/);
				}
				// NON-STOCK CODE END
			}

			var segmentInfo = netManager.m_segments.m_buffer[prevPosition.m_segment].Info;
			if (segmentInfo.m_lanes != null && segmentInfo.m_lanes.Length > prevPosition.m_lane) {
				// NON-STOCK CODE START
				float laneSpeedLimit = 1f;

				if (!Options.customSpeedLimitsEnabled) {
					laneSpeedLimit = segmentInfo.m_lanes[prevPosition.m_lane].m_speedLimit;
				} else {
					laneSpeedLimit = Constants.ManagerFactory.SpeedLimitManager.GetLockFreeGameSpeedLimit(prevPosition.m_segment, prevPosition.m_lane, prevLaneId, segmentInfo.m_lanes[prevPosition.m_lane]);
				}

				// NON-STOCK CODE END
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[prevLaneId].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			// NON-STOCK CODE START (stock code replaced)
			maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(vehicleId, ref vehicleData, this.m_info, prevPosition, ref netManager.m_segments.m_buffer[prevPosition.m_segment], pos, maxSpeed);
			// NON-STOCK CODE END
		}

		[RedirectMethod]
		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
			var segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (segmentInfo.m_lanes != null && segmentInfo.m_lanes.Length > position.m_lane) {
				// NON-STOCK CODE START
				float laneSpeedLimit = 1f;
				if (!Options.customSpeedLimitsEnabled) {
					laneSpeedLimit = segmentInfo.m_lanes[position.m_lane].m_speedLimit;
				} else {
					laneSpeedLimit = Constants.ManagerFactory.SpeedLimitManager.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneId, segmentInfo.m_lanes[position.m_lane]);
				}
				// NON-STOCK CODE END
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[laneId].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			// NON-STOCK CODE START
			maxSpeed = VehicleBehaviorManager.Instance.CalcMaxSpeed(vehicleId, ref vehicleData, this.m_info, position, ref netManager.m_segments.m_buffer[position.m_segment], pos, maxSpeed);
			// NON-STOCK CODE END
		}

		[RedirectMethod]
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			bool vehDebug = GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleID;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && vehDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && vehDebug;

			if (debug)
				Log.Warning($"CustomCarAI.CustomStartPathFind({vehicleID}): called for vehicle {vehicleID}, startPos={startPos}, endPos={endPos}, startBothWays={startBothWays}, endBothWays={endBothWays}, undergroundTarget={undergroundTarget}");
#endif

			ExtVehicleType vehicleType = ExtVehicleType.None;
#if BENCHMARK
			using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
				vehicleType = VehicleStateManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
				if (vehicleType == ExtVehicleType.None) {
#if DEBUG
					Log.Warning($"CustomCarAI.CustomStartPathFind({vehicleID}): Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
					vehicleType = ExtVehicleType.RoadVehicle;
				}
#if BENCHMARK
			}
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

				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = ExtPathType.None;
				args.extVehicleType = vehicleType;
				args.vehicleId = vehicleID;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = default(PathUnit.Position);
				args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
				args.vehicleTypes = info.m_vehicleType;
				args.maxLength = 20000f;
				args.isHeavyVehicle = this.IsHeavyVehicle();
				args.hasCombustionEngine = this.CombustionEngine();
				args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = false;
				args.stablePath = false;
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

				if (CustomPathManager._instance.CustomCreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomCarAI.CustomStartPathFind({vehicleID}): Path-finding starts for vehicle {vehicleID}, path={path}, extVehicleType={vehicleType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, info.m_vehicleType={info.m_vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
#endif

					// NON-STOCK CODE END
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

		[MethodImpl(MethodImplOptions.NoInlining)]
		[RedirectReverse]
		private static bool CheckOverlap(Segment3 segment, ushort ignoreVehicle, float maxVelocity) {
			Log.Error("CustomCarAI.CheckOverlap called");
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[RedirectReverse]
		private static ushort CheckOtherVehicle(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics) {
			Log.Error("CustomCarAI.CheckOtherVehicle called");
			return 0;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[RedirectReverse]
		private static ushort CheckCitizen(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, float lastLen, float nextLen, ref float maxSpeed, ref bool blocked, float maxBraking, ushort otherID, ref CitizenInstance otherData, Vector3 min, Vector3 max) {
			Log.Error("CustomCarAI.CheckCitizen called");
			return 0;
		}
	}
}
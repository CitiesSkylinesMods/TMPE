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

#if BENCHMARK
				using (var bm = new Benchmark(null, "UpdateCarPathState")) {
#endif
					if (Options.prohibitPocketCars && VehicleStateManager.Instance.VehicleStates[vehicleId].vehicleType == ExtVehicleType.PassengerCar) {
						mainPathState = AdvancedParkingManager.Instance.UpdateCarPathState(vehicleId, ref vehicleData, ref ExtCitizenInstanceManager.Instance.ExtInstances[CustomPassengerCarAI.GetDriverInstanceId(vehicleId, ref vehicleData)], mainPathState);
					}
#if BENCHMARK
				}
#endif
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
#if BENCHMARK
			using (var bm = new Benchmark(null, "UpdateVehiclePosition")) {
#endif
				VehicleStateManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData);
#if BENCHMARK
			}
#endif
			if (!Options.isStockLaneChangerUsed()) {
#if BENCHMARK
				using (var bm = new Benchmark(null, "LogTraffic")) {
#endif
					// Advanced AI traffic measurement
					VehicleStateManager.Instance.LogTraffic(vehicleId);
#if BENCHMARK
				}
#endif
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
				bool mayDespawn = true;
#if BENCHMARK
				using (var bm = new Benchmark(null, "MayDespawn")) {
#endif
					mayDespawn = VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData);
#if BENCHMARK
				}
#endif

				if (mayDespawn) {
					// NON-STOCK CODE END
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} // NON-STOCK CODE
			}
		}

		public override bool TrySpawn(ushort vehicleId, ref Vehicle vehicleData) {
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

		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition,
				PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID,
				byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
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

			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			Vector3 lastFrameVehiclePos = lastFrameData.m_position;
			float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

			netManager.m_lanes.m_buffer[laneID].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);

			float braking = this.m_info.m_braking;
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != (Vehicle.Flags)0) {
				braking *= 2f;
			}

			// car position on the Bezier curve of the lane
			var vehiclePosOnBezier = netManager.m_lanes.m_buffer[prevLaneID].CalculatePosition(prevOffset * 0.003921569f);
			//ushort currentSegmentId = netManager.m_lanes.m_buffer[prevLaneID].m_segment;

			// this seems to be like the required braking force in order to stop the vehicle within its half length.
			var crazyValue = 0.5f * sqrVelocity / braking + m_info.m_generatedInfo.m_size.z * 0.5f;
			bool withinBrakingDistance = Vector3.Distance(lastFrameVehiclePos, vehiclePosOnBezier) >= crazyValue - 1f;

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "UpdateVehiclePosition")) {
#endif
				VehicleStateManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData, lastFrameData.m_velocity.magnitude);
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			bool isRecklessDriver = VehicleStateManager.Instance.IsRecklessDriver(vehicleId, ref vehicleData); // NON-STOCK CODE
			if (targetNodeId == prevTargetNodeId && withinBrakingDistance) {
				// NON-STOCK CODE START (stock code replaced)
#if BENCHMARK
				using (var bm = new Benchmark(null, "MayChangeSegment")) {
#endif
					if (!VehicleBehaviorManager.Instance.MayChangeSegment(vehicleId, ref VehicleStateManager.Instance.VehicleStates[vehicleId], ref vehicleData, sqrVelocity, isRecklessDriver, ref prevPos, ref netManager.m_segments.m_buffer[prevPos.m_segment], prevTargetNodeId, prevLaneID, ref position, targetNodeId, ref netManager.m_nodes.m_buffer[targetNodeId], laneID, ref nextPosition, nextTargetNodeId, out maxSpeed)) // NON-STOCK CODE
						return;
#if BENCHMARK
				}
#endif
				// NON-STOCK CODE END
			}

			var segmentInfo = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (segmentInfo.m_lanes != null && segmentInfo.m_lanes.Length > position.m_lane) {
				// NON-STOCK CODE START
				// NON-STOCK CODE START
				float laneSpeedLimit = 1f;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, segmentInfo.m_lanes[position.m_lane]) : segmentInfo.m_lanes[position.m_lane].m_speedLimit; // info2.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
#if BENCHMARK
				}
#endif
				// NON-STOCK CODE END
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			// NON-STOCK CODE START (stock code replaced)
#if BENCHMARK
			using (var bm = new Benchmark(null, "CalcMaxSpeed")) {
#endif
				maxSpeed = VehicleBehaviorManager.Instance.CalcMaxSpeed(vehicleId, this.m_info, position, ref netManager.m_segments.m_buffer[position.m_segment], pos, maxSpeed, isRecklessDriver);
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position position, uint laneId, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			var netManager = Singleton<NetManager>.instance;
			netManager.m_lanes.m_buffer[laneId].CalculatePositionAndDirection(offset * 0.003921569f, out pos, out dir);
			var info = netManager.m_segments.m_buffer[position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
				// NON-STOCK CODE START
				float laneSpeedLimit = 1f;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneId, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit; // NON-STOCK CODE
#if BENCHMARK
				}
#endif
				// NON-STOCK CODE END
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, laneSpeedLimit, netManager.m_lanes.m_buffer[laneId].m_curve);
			} else {
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}

			// NON-STOCK CODE START (stock code replaced)
#if BENCHMARK
			using (var bm = new Benchmark(null, "CalcMaxSpeed")) {
#endif
				maxSpeed = VehicleBehaviorManager.Instance.CalcMaxSpeed(vehicleId, this.m_info, position, ref netManager.m_segments.m_buffer[position.m_segment], pos, maxSpeed, VehicleStateManager.Instance.IsRecklessDriver(vehicleId, ref vehicleData));
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[2])
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
				if (CustomPathManager._instance.CreatePath((ExtVehicleType)vehicleType, vehicleID, ExtCitizenInstance.ExtPathType.None, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0)) {
#if DEBUG
					if (GlobalConfig.Instance.Debug.Switches[2])
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
		private static bool CheckOverlap(Segment3 segment, ushort ignoreVehicle, float maxVelocity) {
			Log.Error("CustomCarAI.CheckOverlap called");
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ushort CheckOtherVehicle(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ref float maxSpeed, ref bool blocked, ref Vector3 collisionPush, float maxBraking, ushort otherID, ref Vehicle otherData, Vector3 min, Vector3 max, int lodPhysics) {
			Log.Error("CustomCarAI.CheckOtherVehicle called");
			return 0;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ushort CheckCitizen(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, float lastLen, float nextLen, ref float maxSpeed, ref bool blocked, float maxBraking, ushort otherID, ref CitizenInstance otherData, Vector3 min, Vector3 max) {
			Log.Error("CustomCarAI.CheckCitizen called");
			return 0;
		}
	}
}
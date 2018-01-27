using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using CSUtil.Commons;
using System.Runtime.CompilerServices;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	class CustomTramBaseAI : TramBaseAI { // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {

			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

				if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
					try {
						this.PathfindSuccess(vehicleId, ref vehicleData);
						this.PathFindReady(vehicleId, ref vehicleData);
					} catch (Exception e) {
						Log.Warning($"TramBaseAI.PathFindSuccess/PathFindReady({vehicleId}) threw an exception: {e.ToString()}");
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						vehicleData.m_path = 0u;
						this.PathfindFailure(vehicleId, ref vehicleData);
						return;
					}
				} else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
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

			VehicleManager instance = Singleton<VehicleManager>.instance;
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)vehicleId].Info;
			info.m_vehicleAI.SimulationStep(vehicleId, ref instance.m_vehicles.m_buffer[(int)vehicleId], vehicleId, ref vehicleData, 0);
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
				return;
			}
			ushort trailingVehicle = instance.m_vehicles.m_buffer[(int)vehicleId].m_trailingVehicle;
			int num = 0;
			while (trailingVehicle != 0) {
				info = instance.m_vehicles.m_buffer[(int)trailingVehicle].Info;
				info.m_vehicleAI.SimulationStep(trailingVehicle, ref instance.m_vehicles.m_buffer[(int)trailingVehicle], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				trailingVehicle = instance.m_vehicles.m_buffer[(int)trailingVehicle].m_trailingVehicle;
				if (++num > 16384) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == 0) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else if (vehicleData.m_blockCounter == 255) {
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

		private static void InitializePath(ushort vehicleID, ref Vehicle vehicleData) {
			Log.Error("CustomTrainAI.InitializePath called");
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
#if DEBUG
			//Log._Debug($"CustomTramBaseAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

#if BENCHMARK
			using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
			VehicleStateManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
#if BENCHMARK
			}
#endif

			VehicleInfo info = this.m_info;
			bool allowUnderground;
			bool allowUnderground2;
			if (info.m_vehicleType == VehicleInfo.VehicleType.Metro) {
				allowUnderground = true;
				allowUnderground2 = true;
			} else {
				allowUnderground = ((vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0);
				allowUnderground2 = false;
			}
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startSqrDistA;
			float startSqrDistB;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float endSqrDistA;
			float endSqrDistB;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB) &&
				CustomPathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground2, false, 32f, out endPosA, out endPosB, out endSqrDistA, out endSqrDistB)) {
				if (!startBothWays || startSqrDistB > startSqrDistA * 1.2f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || endSqrDistB > endSqrDistA * 1.2f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = ExtCitizenInstance.ExtPathType.None;
				args.extVehicleType = ExtVehicleType.Tram;
				args.vehicleId = vehicleID;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = default(PathUnit.Position);
				args.laneTypes = NetInfo.LaneType.Vehicle;
				args.vehicleTypes = info.m_vehicleType;
				args.maxLength = 20000f;
				args.isHeavyVehicle = false;
				args.hasCombustionEngine = false;
				args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
				args.ignoreFlooded = false;
				args.randomParking = false;
				args.stablePath = true;
				args.skipQueue = true;

				if (CustomPathManager._instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
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

		public void CustomCalculateSegmentPosition(ushort vehicleId, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position prevPosition, uint prevLaneId, byte prevOffset, PathUnit.Position refPosition, uint refLaneId, byte refOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager netManager = Singleton<NetManager>.instance;
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
			bool debug = GlobalConfig.Instance.Debug.Switches[21] && (GlobalConfig.Instance.Debug.NodeId <= 0 || refTargetNodeId == GlobalConfig.Instance.Debug.NodeId) && (GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.Tram) && (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId);

			if (debug) {
				Log._Debug($"CustomTramBaseAI.CustomCalculateSegmentPosition({vehicleId}) called.\n" +
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
			float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

			netManager.m_lanes.m_buffer[prevLaneId].CalculatePositionAndDirection((float)prevOffset * 0.003921569f, out pos, out dir);
			Vector3 b = netManager.m_lanes.m_buffer[refLaneId].CalculatePosition((float)refOffset * 0.003921569f);
			Vector3 a = lastFrameData.m_position;
			Vector3 a2 = lastFrameData.m_position;
			Vector3 b2 = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			a += b2;
			a2 -= b2;
			float crazyValue = 0.5f * sqrVelocity / this.m_info.m_braking;
			float a3 = Vector3.Distance(a, b);
			float b3 = Vector3.Distance(a2, b);
			if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
				// dead stock code
				/*Segment3 segment;
				segment.a = pos;
				if (prevOffset < prevPosition.m_offset) {
					segment.b = pos + dir.normalized * this.m_info.m_generatedInfo.m_size.z;
				} else {
					segment.b = pos - dir.normalized * this.m_info.m_generatedInfo.m_size.z;
				}*/
				if (prevSourceNodeId == refTargetNodeId) {
#if BENCHMARK
					using (var bm = new Benchmark(null, "MayChangeSegment")) {
#endif
					if (!VehicleBehaviorManager.Instance.MayChangeSegment(vehicleId, ref VehicleStateManager.Instance.VehicleStates[vehicleId], ref vehicleData, sqrVelocity, false, ref refPosition, ref netManager.m_segments.m_buffer[refPosition.m_segment], refTargetNodeId, refLaneId, ref prevPosition, prevSourceNodeId, ref netManager.m_nodes.m_buffer[prevSourceNodeId], prevLaneId, ref nextPosition, prevTargetNodeId, out maxSpeed)) {
						return;
					} else {
#if BENCHMARK	
						using (var bm = new Benchmark(null, "UpdateVehiclePosition")) {
#endif
						VehicleStateManager.Instance.UpdateVehiclePosition(vehicleId, ref vehicleData/*, lastFrameData.m_velocity.magnitude*/);
#if BENCHMARK
						}
#endif
					}
#if BENCHMARK
					}
#endif
				}
			}

			NetInfo info = netManager.m_segments.m_buffer[(int)prevPosition.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)prevPosition.m_lane) {
				float speedLimit = 1;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					speedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(prevPosition.m_segment, prevPosition.m_lane, prevLaneId, info.m_lanes[prevPosition.m_lane]) : info.m_lanes[prevPosition.m_lane].m_speedLimit;
#if BENCHMARK
				}
#endif
				maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, speedLimit, netManager.m_lanes.m_buffer[prevLaneId].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
			}
		}

		public void CustomCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[laneID].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				float speedLimit = 1;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					speedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
#if BENCHMARK
				}
#endif
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, speedLimit, instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		public void CustomSimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[16] && GlobalConfig.Instance.Debug.NodeId == vehicleID;
#endif

			ushort leadingVehicle = vehicleData.m_leadingVehicle;
			uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			VehicleInfo leaderInfo;
			if (leaderID != vehicleID) {
				leaderInfo = leaderData.Info;
			} else {
				leaderInfo = this.m_info;
			}
			TramBaseAI tramBaseAI = leaderInfo.m_vehicleAI as TramBaseAI;

			if (leadingVehicle != 0) {
				frameData.m_position += frameData.m_velocity * 0.4f;
			} else {
				frameData.m_position += frameData.m_velocity * 0.5f;
			}

			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
			Vector3 wheelBaseRot = frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			Vector3 posAfterWheelRot = frameData.m_position + wheelBaseRot;
			Vector3 posBeforeWheelRot = frameData.m_position - wheelBaseRot;

			float acceleration = this.m_info.m_acceleration;
			float braking = this.m_info.m_braking;
			float curSpeed = frameData.m_velocity.magnitude;

			Vector3 afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
			float afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;

			Quaternion curInvRot = Quaternion.Inverse(frameData.m_rotation);
			Vector3 curveTangent = curInvRot * frameData.m_velocity;

#if DEBUG
			if (debug) {
				Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): ================================================");
				Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): leadingVehicle={leadingVehicle} frameData.m_position={frameData.m_position} frameData.m_swayPosition={frameData.m_swayPosition} wheelBaseRot={wheelBaseRot} posAfterWheelRot={posAfterWheelRot} posBeforeWheelRot={posBeforeWheelRot} acceleration={acceleration} braking={braking} curSpeed={curSpeed} afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag} curInvRot={curInvRot} curveTangent={curveTangent} this.m_info.m_generatedInfo.m_wheelBase={this.m_info.m_generatedInfo.m_wheelBase}");
			}
#endif

			Vector3 forward = Vector3.forward;
			Vector3 targetMotion = Vector3.zero;
			float targetSpeed = 0f;
			float motionFactor = 0.5f;
			float turnAngle = 0f;
			if (leadingVehicle != 0) {
				VehicleManager vehMan = Singleton<VehicleManager>.instance;
				Vehicle.Frame leadingVehLastFrameData = vehMan.m_vehicles.m_buffer[(int)leadingVehicle].GetLastFrameData();
				VehicleInfo leadingVehInfo = vehMan.m_vehicles.m_buffer[(int)leadingVehicle].Info;

				float attachOffset;
				if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0) {
					attachOffset = this.m_info.m_attachOffsetBack - this.m_info.m_generatedInfo.m_size.z * 0.5f;
				} else {
					attachOffset = this.m_info.m_attachOffsetFront - this.m_info.m_generatedInfo.m_size.z * 0.5f;
				}

				float leadingAttachOffset;
				if ((vehMan.m_vehicles.m_buffer[(int)leadingVehicle].m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0) {
					leadingAttachOffset = leadingVehInfo.m_attachOffsetFront - leadingVehInfo.m_generatedInfo.m_size.z * 0.5f;
				} else {
					leadingAttachOffset = leadingVehInfo.m_attachOffsetBack - leadingVehInfo.m_generatedInfo.m_size.z * 0.5f;
				}

				Vector3 curPosMinusRotAttachOffset = frameData.m_position - frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
				Vector3 leadingPosPlusRotAttachOffset = leadingVehLastFrameData.m_position + leadingVehLastFrameData.m_rotation * new Vector3(0f, 0f, leadingAttachOffset);

				wheelBaseRot = leadingVehLastFrameData.m_rotation * new Vector3(0f, 0f, leadingVehInfo.m_generatedInfo.m_wheelBase * 0.5f);
				Vector3 leadingPosBeforeWheelRot = leadingVehLastFrameData.m_position - wheelBaseRot;

				if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, (Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) < 0f && vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
					int someIndex = -1;
					InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posBeforeWheelRot, 0, ref leaderData, ref someIndex, 0, 0, Vector3.SqrMagnitude(posBeforeWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
					afterRotToTargetPos1DiffSqrMag = 0f;
				}

				float attachRotDist = Mathf.Max(Vector3.Distance(curPosMinusRotAttachOffset, leadingPosPlusRotAttachOffset), 2f);

				float one = 1f;
				float attachRotSqrDist = attachRotDist * attachRotDist;
				float oneSqr = one * one;
				int i = 0;
				if (afterRotToTargetPos1DiffSqrMag < attachRotSqrDist) {
					if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
						InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, posBeforeWheelRot, posAfterWheelRot, 0, ref leaderData, ref i, 1, 2, attachRotSqrDist, oneSqr);
					}
					while (i < 4) {
						vehicleData.SetTargetPos(i, vehicleData.GetTargetPos(i - 1));
						i++;
					}
					afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
					afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;
				}
				afterRotToTargetPos1Diff = curInvRot * afterRotToTargetPos1Diff;

				float negTotalAttachLen = -((this.m_info.m_generatedInfo.m_wheelBase + leadingVehInfo.m_generatedInfo.m_wheelBase) * 0.5f + attachOffset + leadingAttachOffset);
				bool hasPath = false;
				if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
					float u1;
					float u2;
					if (Line3.Intersect(posAfterWheelRot, vehicleData.m_targetPos1, leadingPosBeforeWheelRot, negTotalAttachLen, out u1, out u2)) {
						targetMotion = afterRotToTargetPos1Diff * Mathf.Clamp(Mathf.Min(u1, u2) / 0.6f, 0f, 2f);
					} else {
						Line3.DistanceSqr(posAfterWheelRot, vehicleData.m_targetPos1, leadingPosBeforeWheelRot, out u1);
						targetMotion = afterRotToTargetPos1Diff * Mathf.Clamp(u1 / 0.6f, 0f, 2f);
					}
					hasPath = true;
				}

				if (hasPath) {
					if (Vector3.Dot(leadingPosBeforeWheelRot - posAfterWheelRot, posAfterWheelRot - posBeforeWheelRot) < 0f) {
						motionFactor = 0f;
					}
				} else {
					float leadingPosBeforeToAfterWheelRotDist = Vector3.Distance(leadingPosBeforeWheelRot, posAfterWheelRot);
					motionFactor = 0f;
					targetMotion = curInvRot * ((leadingPosBeforeWheelRot - posAfterWheelRot) * (Mathf.Max(0f, leadingPosBeforeToAfterWheelRotDist - negTotalAttachLen) / Mathf.Max(1f, leadingPosBeforeToAfterWheelRotDist * 0.6f)));
				}
			} else {
				float estimatedFrameDist = (curSpeed + acceleration) * (0.5f + 0.5f * (curSpeed + acceleration) / braking) + (this.m_info.m_generatedInfo.m_size.z - this.m_info.m_generatedInfo.m_wheelBase) * 0.5f;
				float maxSpeedAdd = Mathf.Max(curSpeed + acceleration, 2f);
				float meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 2f, 2f);
				float maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
				float meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;
				if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, (Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) < 0f && vehicleData.m_path != 0u && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0) {
					int someIndex = -1;
					InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posBeforeWheelRot, leaderID, ref leaderData, ref someIndex, 0, 0, Vector3.SqrMagnitude(posBeforeWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
					afterRotToTargetPos1DiffSqrMag = 0f;
#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): dot < 0");
					}
#endif
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Leading vehicle is 0. vehicleData.m_targetPos0={vehicleData.m_targetPos0} vehicleData.m_targetPos1={vehicleData.m_targetPos1} posBeforeWheelRot={posBeforeWheelRot} posBeforeWheelRot={posAfterWheelRot} estimatedFrameDist={estimatedFrameDist} maxSpeedAdd={maxSpeedAdd} meanSpeedAdd={meanSpeedAdd} maxSpeedAddSqr={maxSpeedAddSqr} meanSpeedAddSqr={meanSpeedAddSqr} afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag}");
				}
#endif

				int posIndex = 0;
				bool hasValidPathTargetPos = false;
				if ((afterRotToTargetPos1DiffSqrMag < maxSpeedAddSqr || vehicleData.m_targetPos3.w < 0.01f) && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0) {
					if (vehicleData.m_path != 0u) {
						InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, posBeforeWheelRot, posAfterWheelRot, leaderID, ref leaderData, ref posIndex, 1, 4, maxSpeedAddSqr, meanSpeedAddSqr);
					}
					if (posIndex < 4) {
						hasValidPathTargetPos = true;
						while (posIndex < 4) {
							vehicleData.SetTargetPos(posIndex, vehicleData.GetTargetPos(posIndex - 1));
							posIndex++;
						}
					}
					afterRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posAfterWheelRot;
					afterRotToTargetPos1DiffSqrMag = afterRotToTargetPos1Diff.sqrMagnitude;
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): posIndex={posIndex} hasValidPathTargetPos={hasValidPathTargetPos}");
				}
#endif

				if (leaderData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
					NetManager netMan = Singleton<NetManager>.instance;
					byte leaderPathPosIndex = leaderData.m_pathPositionIndex;
					byte leaderLastPathOffset = leaderData.m_lastPathOffset;
					if (leaderPathPosIndex == 255) {
						leaderPathPosIndex = 0;
					}
					int noise;
					float leaderLen = 1f + leaderData.CalculateTotalLength(leaderID, out noise);

#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): leaderPathPosIndex={leaderPathPosIndex} leaderLastPathOffset={leaderLastPathOffset} leaderPathPosIndex={leaderPathPosIndex} leaderLen={leaderLen}");
					}
#endif

					// reserve space / add traffic
					PathManager pathMan = Singleton<PathManager>.instance;
					PathUnit.Position pathPos;
					if (pathMan.m_pathUnits.m_buffer[leaderData.m_path].GetPosition(leaderPathPosIndex >> 1, out pathPos)) {
						netMan.m_segments.m_buffer[(int)pathPos.m_segment].AddTraffic(Mathf.RoundToInt(leaderLen * 2.5f), noise);
						bool reservedSpaceOnCurrentLane = false;
						if ((leaderPathPosIndex & 1) == 0 || leaderLastPathOffset == 0) {
							uint laneId = PathManager.GetLaneID(pathPos);
							if (laneId != 0u) {
								Vector3 curPathOffsetPos = netMan.m_lanes.m_buffer[laneId].CalculatePosition((float)pathPos.m_offset * 0.003921569f);
								float speedAdd = 0.5f * curSpeed * curSpeed / this.m_info.m_braking;
								float afterWheelRotCurPathOffsetDist = Vector3.Distance(posAfterWheelRot, curPathOffsetPos);
								float beforeWheelRotCurPathOffsetDist = Vector3.Distance(posBeforeWheelRot, curPathOffsetPos);
								if (Mathf.Min(afterWheelRotCurPathOffsetDist, beforeWheelRotCurPathOffsetDist) >= speedAdd - 1f) {
									netMan.m_lanes.m_buffer[laneId].ReserveSpace(leaderLen);
									reservedSpaceOnCurrentLane = true;
								}
							}
						}

						if (!reservedSpaceOnCurrentLane && pathMan.m_pathUnits.m_buffer[leaderData.m_path].GetNextPosition(leaderPathPosIndex >> 1, out pathPos)) {
							uint nextLaneId = PathManager.GetLaneID(pathPos);
							if (nextLaneId != 0u) {
								netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(leaderLen);
							}
						}
					}

					if ((ulong)(currentFrameIndex >> 4 & 15u) == (ulong)((long)(leaderID & 15))) {
						// check if vehicle can proceed to next path position

						bool canProceeed = false;
						uint curLeaderPathId = leaderData.m_path;
						int curLeaderPathPosIndex = leaderPathPosIndex >> 1;
						int k = 0;
						while (k < 5) {
							bool invalidPos;
							if (PathUnit.GetNextPosition(ref curLeaderPathId, ref curLeaderPathPosIndex, out pathPos, out invalidPos)) {
								uint laneId = PathManager.GetLaneID(pathPos);
								if (laneId != 0u && !netMan.m_lanes.m_buffer[laneId].CheckSpace(leaderLen)) {
									k++;
									continue;
								}
							}
							if (invalidPos) {
								this.InvalidPath(vehicleID, ref vehicleData, leaderID, ref leaderData);
							}
							canProceeed = true;
							break;
						}
						if (!canProceeed) {
							leaderData.m_flags |= Vehicle.Flags.Congestion;
						}
					}
				}

				float maxSpeed;
				if ((leaderData.m_flags & Vehicle.Flags.Stopped) != (Vehicle.Flags)0) {
					maxSpeed = 0f;
#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Vehicle is stopped. maxSpeed={maxSpeed}");
					}
#endif
				} else {
					maxSpeed = Mathf.Min(vehicleData.m_targetPos1.w, GetMaxSpeed(leaderID, ref leaderData));
#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Vehicle is not stopped. maxSpeed={maxSpeed}");
					}
#endif
				}

#if DEBUG
				if (debug) {
					Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Start of second part. curSpeed={curSpeed} curInvRot={curInvRot}");
				}
#endif

				afterRotToTargetPos1Diff = curInvRot * afterRotToTargetPos1Diff;
#if DEBUG
				if (debug) {
					Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} (old afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag})");
				}
#endif
				Vector3 zero = Vector3.zero;
				bool blocked = false;
				float forwardLen = 0f;
				if (afterRotToTargetPos1DiffSqrMag > 1f) { // TODO why is this not recalculated?
					forward = VectorUtils.NormalizeXZ(afterRotToTargetPos1Diff, out forwardLen);
#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): afterRotToTargetPos1DiffSqrMag > 1f. forward={forward} forwardLen={forwardLen}");
					}
#endif
					if (forwardLen > 1f) {
						Vector3 fwd = afterRotToTargetPos1Diff;
						maxSpeedAdd = Mathf.Max(curSpeed, 2f);
						maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): forwardLen > 1f. fwd={fwd} maxSpeedAdd={maxSpeedAdd} maxSpeedAddSqr={maxSpeedAddSqr}");
						}
#endif
						if (afterRotToTargetPos1DiffSqrMag > maxSpeedAddSqr) {
							float fwdLimiter = maxSpeedAdd / Mathf.Sqrt(afterRotToTargetPos1DiffSqrMag);
							fwd.x *= fwdLimiter;
							fwd.y *= fwdLimiter;

#if DEBUG
							if (debug) {
								Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): afterRotToTargetPos1DiffSqrMag > maxSpeedAddSqr. afterRotToTargetPos1DiffSqrMag={afterRotToTargetPos1DiffSqrMag} maxSpeedAddSqr={maxSpeedAddSqr} fwdLimiter={fwdLimiter} fwd={fwd}");
							}
#endif
						}

						if (fwd.z < -1f) { // !!!
#if DEBUG
							if (debug) {
								Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): fwd.z < -1f. fwd={fwd}");
							}
#endif

							if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
								Vector3 targetPos0TargetPos1Diff = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
								if ((curInvRot * targetPos0TargetPos1Diff).z < -0.01f) { // !!!
#if DEBUG
									if (debug) {
										Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): (curInvRot * targetPos0TargetPos1Diff).z < -0.01f. curInvRot={curInvRot} targetPos0TargetPos1Diff={targetPos0TargetPos1Diff}");
									}
#endif
									if (afterRotToTargetPos1Diff.z < Mathf.Abs(afterRotToTargetPos1Diff.x) * -10f) { // !!!
#if DEBUG
										if (debug) {
											Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): afterRotToTargetPos1Diff.z < Mathf.Abs(afterRotToTargetPos1Diff.x) * -10f. fwd={fwd} targetPos0TargetPos1Diff={targetPos0TargetPos1Diff} afterRotToTargetPos1Diff={afterRotToTargetPos1Diff}");
										}
#endif

										/*fwd.z = 0f;
										afterRotToTargetPos1Diff = Vector3.zero;*/
										maxSpeed = 0.5f; // NON-STOCK CODE

#if DEBUG
										if (debug) {
											Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): (1) set maxSpeed={maxSpeed}");
										}
#endif
									} else {
										posAfterWheelRot = posBeforeWheelRot + Vector3.Normalize(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) * this.m_info.m_generatedInfo.m_wheelBase;
										posIndex = -1;
										InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, vehicleData.m_targetPos1, leaderID, ref leaderData, ref posIndex, 0, 0, Vector3.SqrMagnitude(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) + 1f, 1f);

#if DEBUG
										if (debug) {
											Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): afterRotToTargetPos1Diff.z >= Mathf.Abs(afterRotToTargetPos1Diff.x) * -10f. Invoked UpdatePathTargetPositions. posAfterWheelRot={posAfterWheelRot} posBeforeWheelRot={posBeforeWheelRot} this.m_info.m_generatedInfo.m_wheelBase={this.m_info.m_generatedInfo.m_wheelBase}");
										}
#endif
									}
								} else {
									posIndex = -1;
									InvokeUpdatePathTargetPositions(tramBaseAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posBeforeWheelRot, leaderID, ref leaderData, ref posIndex, 0, 0, Vector3.SqrMagnitude(posBeforeWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
									vehicleData.m_targetPos1 = posAfterWheelRot;
									fwd.z = 0f;
									afterRotToTargetPos1Diff = Vector3.zero;
									maxSpeed = 0f;

#if DEBUG
									if (debug) {
										Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Vehicle is waiting for a path. posIndex={posIndex} vehicleData.m_targetPos1={vehicleData.m_targetPos1} fwd={fwd} afterRotToTargetPos1Diff={afterRotToTargetPos1Diff} maxSpeed={maxSpeed}");
									}
#endif
								}
							}
							motionFactor = 0f;
#if DEBUG
							if (debug) {
								Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Reset motion factor. motionFactor={motionFactor}");
							}
#endif
						}

						forward = VectorUtils.NormalizeXZ(fwd, out forwardLen);
						float curve = Mathf.PI/2f /* 1.57079637f*/ * (1f - forward.z); // <- constant: a bit inaccurate PI/2
						if (forwardLen > 1f) {
							curve /= forwardLen;
						}
						float targetDist = forwardLen;
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): targetDist={targetDist} fwd={fwd} curve={curve} maxSpeed={maxSpeed}");
						}
#endif

						if (vehicleData.m_targetPos1.w < 0.1f) {
							maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve);
							maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos1.w, braking * 0.9f));
						} else {
							maxSpeed = Mathf.Min(maxSpeed, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve));
						}
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): [1] maxSpeed={maxSpeed}");
						}
#endif
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos2.w, braking * 0.9f));
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): [2] maxSpeed={maxSpeed}");
						}
#endif
						targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos3.w, braking * 0.9f));
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): [3] maxSpeed={maxSpeed}");
						}
#endif
						targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
						if (vehicleData.m_targetPos3.w < 0.01f) {
							targetDist = Mathf.Max(0f, targetDist + (this.m_info.m_generatedInfo.m_wheelBase - this.m_info.m_generatedInfo.m_size.z) * 0.5f);
						}
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, 0f, braking * 0.9f));
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): [4] maxSpeed={maxSpeed}");
						}
#endif
						CarAI.CheckOtherVehicles(vehicleID, ref vehicleData, ref frameData, ref maxSpeed, ref blocked, ref zero, estimatedFrameDist, braking * 0.9f, lodPhysics);
#if DEBUG
						if (debug) {
							Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): CheckOtherVehicles finished. blocked={blocked}");
						}
#endif
						if (maxSpeed < curSpeed) {
							float brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
							targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
#if DEBUG
							if (debug) {
								Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): maxSpeed < curSpeed. maxSpeed={maxSpeed} curSpeed={curSpeed} brake={brake} targetSpeed={targetSpeed}");
							}
#endif
						} else {
							float accel = Mathf.Max(acceleration, Mathf.Min(braking, -curSpeed));
							targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
#if DEBUG
							if (debug) {
								Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): maxSpeed >= curSpeed. maxSpeed={maxSpeed} curSpeed={curSpeed} accel={accel} targetSpeed={targetSpeed}");
							}
#endif
						}
					}
				} else if (curSpeed < 0.1f && hasValidPathTargetPos && leaderInfo.m_vehicleAI.ArriveAtDestination(leaderID, ref leaderData)) {
					leaderData.Unspawn(leaderID);
					return;
				}
				if ((leaderData.m_flags & Vehicle.Flags.Stopped) == (Vehicle.Flags)0 && maxSpeed < 0.1f) {
#if DEBUG
					if (debug) {
						Log._Debug($"CustomTramBaseAI.SimulationStep({vehicleID}): Vehicle is not stopped but maxSpeed < 0.1. maxSpeed={maxSpeed}");
					}
#endif
					blocked = true;
				}
				if (blocked) {
					leaderData.m_blockCounter = (byte)Mathf.Min((int)(leaderData.m_blockCounter + 1), 255);
				} else {
					leaderData.m_blockCounter = 0;
				}
				if (forwardLen > 1f) {
					turnAngle = Mathf.Asin(forward.x) * Mathf.Sign(targetSpeed);
					targetMotion = forward * targetSpeed;
				} else {
					Vector3 vel = Vector3.ClampMagnitude(afterRotToTargetPos1Diff * 0.5f - curveTangent, braking);
					targetMotion = curveTangent + vel;
				}
			}
			bool mayBlink = (currentFrameIndex + (uint)leaderID & 16u) != 0u;
			Vector3 springs = targetMotion - curveTangent;
			Vector3 targetAfterWheelRotMotion = frameData.m_rotation * targetMotion;
			Vector3 targetBeforeWheelRotMotion = Vector3.Normalize((Vector3)vehicleData.m_targetPos0 - posBeforeWheelRot) * (targetMotion.magnitude * motionFactor);
			targetBeforeWheelRotMotion -= targetAfterWheelRotMotion * (Vector3.Dot(targetAfterWheelRotMotion, targetBeforeWheelRotMotion) / Mathf.Max(1f, targetAfterWheelRotMotion.sqrMagnitude));
			posAfterWheelRot += targetAfterWheelRotMotion;
			posBeforeWheelRot += targetBeforeWheelRotMotion;
			frameData.m_rotation = Quaternion.LookRotation(posAfterWheelRot - posBeforeWheelRot);
			Vector3 targetPos = posAfterWheelRot - frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			frameData.m_velocity = targetPos - frameData.m_position;
			if (leadingVehicle != 0) {
				frameData.m_position += frameData.m_velocity * 0.6f;
			} else {
				frameData.m_position += frameData.m_velocity * 0.5f;
			}
			frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - this.m_info.m_dampers) - springs * (1f - this.m_info.m_springs) - frameData.m_swayPosition * this.m_info.m_springs;
			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
			frameData.m_steerAngle = 0f;
			frameData.m_travelDistance += targetMotion.z;
			if (leadingVehicle != 0) {
				frameData.m_lightIntensity = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[(int)leaderID].GetLastFrameData().m_lightIntensity;
			} else {
				frameData.m_lightIntensity.x = 5f;
				frameData.m_lightIntensity.y = ((springs.z >= -0.1f) ? 0.5f : 5f);
				frameData.m_lightIntensity.z = ((turnAngle >= -0.1f || !mayBlink) ? 0f : 5f);
				frameData.m_lightIntensity.w = ((turnAngle <= 0.1f || !mayBlink) ? 0f : 5f);
			}
			frameData.m_underground = ((vehicleData.m_flags & Vehicle.Flags.Underground) != (Vehicle.Flags)0);
			frameData.m_transition = ((vehicleData.m_flags & Vehicle.Flags.Transition) != (Vehicle.Flags)0);
			//base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void InvokeUpdatePathTargetPositions(TramBaseAI tramBaseAI, ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos1, Vector3 refPos2, ushort leaderID, ref Vehicle leaderData, ref int index, int max1, int max2, float minSqrDistanceA, float minSqrDistanceB) {
			Log.Error($"CustomTramBaseAI.InvokeUpdatePathTargetPositions called! tramBaseAI={tramBaseAI}");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static float GetMaxSpeed(ushort leaderID, ref Vehicle leaderData) {
			Log.Error("CustomTrainAI.GetMaxSpeed called");
			return 0f;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static float CalculateMaxSpeed(float targetDist, float targetSpeed, float maxBraking) {
			Log.Error("CustomTrainAI.CalculateMaxSpeed called");
			return 0f;
		}
	}
}

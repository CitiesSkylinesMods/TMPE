using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using System.Runtime.CompilerServices;
using TrafficManager.Traffic.Data;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	public class CustomTrainAI : TrainAI { // TODO inherit from VehicleAI (in order to keep the correct references to `base`)
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
				byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

				if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
					try {
						this.PathFindReady(vehicleId, ref vehicleData);
					} catch (Exception e) {
						Log.Warning($"TrainAI.PathFindReady({vehicleId}) for vehicle {vehicleData.Info?.m_class?.name} threw an exception: {e.ToString()}");
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						vehicleData.m_path = 0u;
						vehicleData.Unspawn(vehicleId);
						return;
					}
				} else if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || vehicleData.m_path == 0) {
					vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
					Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					vehicleData.m_path = 0u;
					vehicleData.Unspawn(vehicleId);
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

			bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			ushort connectedVehicleId;
			if (reversed) {
				connectedVehicleId = vehicleData.GetLastVehicle(vehicleId);
			} else {
				connectedVehicleId = vehicleId;
			}

			VehicleManager instance = Singleton<VehicleManager>.instance;
			VehicleInfo info = instance.m_vehicles.m_buffer[(int)connectedVehicleId].Info;
			info.m_vehicleAI.SimulationStep(connectedVehicleId, ref instance.m_vehicles.m_buffer[(int)connectedVehicleId], vehicleId, ref vehicleData, 0);
			if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
				return;
			}
			bool newReversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
			if (newReversed != reversed) {
				reversed = newReversed;
				if (reversed) {
					connectedVehicleId = vehicleData.GetLastVehicle(vehicleId);
				} else {
					connectedVehicleId = vehicleId;
				}
				info = instance.m_vehicles.m_buffer[(int)connectedVehicleId].Info;
				info.m_vehicleAI.SimulationStep(connectedVehicleId, ref instance.m_vehicles.m_buffer[(int)connectedVehicleId], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				newReversed = ((vehicleData.m_flags & Vehicle.Flags.Reversed) != 0);
				if (newReversed != reversed) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
					return;
				}
			}
			if (reversed) {
				connectedVehicleId = instance.m_vehicles.m_buffer[(int)connectedVehicleId].m_leadingVehicle;
				int num2 = 0;
				while (connectedVehicleId != 0) {
					info = instance.m_vehicles.m_buffer[(int)connectedVehicleId].Info;
					info.m_vehicleAI.SimulationStep(connectedVehicleId, ref instance.m_vehicles.m_buffer[(int)connectedVehicleId], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					connectedVehicleId = instance.m_vehicles.m_buffer[(int)connectedVehicleId].m_leadingVehicle;
					if (++num2 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			} else {
				connectedVehicleId = instance.m_vehicles.m_buffer[(int)connectedVehicleId].m_trailingVehicle;
				int num3 = 0;
				while (connectedVehicleId != 0) {
					info = instance.m_vehicles.m_buffer[(int)connectedVehicleId].Info;
					info.m_vehicleAI.SimulationStep(connectedVehicleId, ref instance.m_vehicles.m_buffer[(int)connectedVehicleId], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					connectedVehicleId = instance.m_vehicles.m_buffer[(int)connectedVehicleId].m_trailingVehicle;
					if (++num3 > 16384) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
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

		public void CustomSimulationStep(ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics) {
			bool reversed = (leaderData.m_flags & Vehicle.Flags.Reversed) != (Vehicle.Flags)0;
			ushort frontVehicleId = (!reversed) ? vehicleData.m_leadingVehicle : vehicleData.m_trailingVehicle;
			VehicleInfo vehicleInfo;
			if (leaderID != vehicleID) {
				vehicleInfo = leaderData.Info;
			} else {
				vehicleInfo = this.m_info;
			}
			TrainAI trainAI = vehicleInfo.m_vehicleAI as TrainAI;
			if (frontVehicleId != 0) {
				frameData.m_position += frameData.m_velocity * 0.4f;
			} else {
				frameData.m_position += frameData.m_velocity * 0.5f;
			}
			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;

			Vector3 posBeforeWheelRot = frameData.m_position;
			Vector3 posAfterWheelRot = frameData.m_position;
			Vector3 wheelBaseRot = frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			if (reversed) {
				posBeforeWheelRot -= wheelBaseRot;
				posAfterWheelRot += wheelBaseRot;
			} else {
				posBeforeWheelRot += wheelBaseRot;
				posAfterWheelRot -= wheelBaseRot;
			}

			float acceleration = this.m_info.m_acceleration;
			float braking = this.m_info.m_braking;
			float curSpeed = frameData.m_velocity.magnitude;

			Vector3 beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
			float beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;

			Quaternion curInvRot = Quaternion.Inverse(frameData.m_rotation);
			Vector3 curveTangent = curInvRot * frameData.m_velocity;

			Vector3 forward = Vector3.forward;
			Vector3 targetMotion = Vector3.zero;
			float targetSpeed = 0f;
			float motionFactor = 0.5f;

			if (frontVehicleId != 0) {
				VehicleManager vehMan = Singleton<VehicleManager>.instance;
				Vehicle.Frame frontVehLastFrameData = vehMan.m_vehicles.m_buffer[(int)frontVehicleId].GetLastFrameData();
				VehicleInfo frontVehInfo = vehMan.m_vehicles.m_buffer[(int)frontVehicleId].Info;

				float attachOffset;
				if ((vehicleData.m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0 != reversed) {
					attachOffset = this.m_info.m_attachOffsetBack - this.m_info.m_generatedInfo.m_size.z * 0.5f;
				} else {
					attachOffset = this.m_info.m_attachOffsetFront - this.m_info.m_generatedInfo.m_size.z * 0.5f;
				}

				float frontAttachOffset;
				if ((vehMan.m_vehicles.m_buffer[(int)frontVehicleId].m_flags & Vehicle.Flags.Inverted) != (Vehicle.Flags)0 != reversed) {
					frontAttachOffset = frontVehInfo.m_attachOffsetFront - frontVehInfo.m_generatedInfo.m_size.z * 0.5f;
				} else {
					frontAttachOffset = frontVehInfo.m_attachOffsetBack - frontVehInfo.m_generatedInfo.m_size.z * 0.5f;
				}

				Vector3 posMinusAttachOffset = frameData.m_position;
				if (reversed) {
					posMinusAttachOffset += frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
				} else {
					posMinusAttachOffset -= frameData.m_rotation * new Vector3(0f, 0f, attachOffset);
				}

				Vector3 frontPosPlusAttachOffset = frontVehLastFrameData.m_position;
				if (reversed) {
					frontPosPlusAttachOffset -= frontVehLastFrameData.m_rotation * new Vector3(0f, 0f, frontAttachOffset);
				} else {
					frontPosPlusAttachOffset += frontVehLastFrameData.m_rotation * new Vector3(0f, 0f, frontAttachOffset);
				}

				Vector3 frontPosMinusWheelBaseRot = frontVehLastFrameData.m_position;
				wheelBaseRot = frontVehLastFrameData.m_rotation * new Vector3(0f, 0f, frontVehInfo.m_generatedInfo.m_wheelBase * 0.5f);
				if (reversed) {
					frontPosMinusWheelBaseRot += wheelBaseRot;
				} else {
					frontPosMinusWheelBaseRot -= wheelBaseRot;
				}

				if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, (Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) < 0f && vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
					int someIndex = -1;
					InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posAfterWheelRot, 0, ref leaderData, ref someIndex, 0, 0, Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
					beforeRotToTargetPos1DiffSqrMag = 0f;
				}

				float maxAttachDist = Mathf.Max(Vector3.Distance(posMinusAttachOffset, frontPosPlusAttachOffset), 2f);
				float one = 1f;
				float maxAttachSqrDist = maxAttachDist * maxAttachDist;
				float oneSqr = one * one;
				int i = 0;
				if (beforeRotToTargetPos1DiffSqrMag < maxAttachSqrDist) {
					if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
						InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, posAfterWheelRot, posBeforeWheelRot, 0, ref leaderData, ref i, 1, 2, maxAttachSqrDist, oneSqr);
					}
					while (i < 4) {
						vehicleData.SetTargetPos(i, vehicleData.GetTargetPos(i - 1));
						i++;
					}
					beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
					beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;
				}

				if (vehicleData.m_path != 0u) {
					NetManager netMan = Singleton<NetManager>.instance;
					byte pathPosIndex = vehicleData.m_pathPositionIndex;
					byte lastPathOffset = vehicleData.m_lastPathOffset;
					if (pathPosIndex == 255) {
						pathPosIndex = 0;
					}

					PathManager pathMan = Singleton<PathManager>.instance;
					PathUnit.Position curPathPos;
					if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(pathPosIndex >> 1, out curPathPos)) {
						netMan.m_segments.m_buffer[(int)curPathPos.m_segment].AddTraffic(Mathf.RoundToInt(this.m_info.m_generatedInfo.m_size.z * 3f), this.GetNoiseLevel());
						PathUnit.Position nextPathPos; // NON-STOCK CODE
						if ((pathPosIndex & 1) == 0 || lastPathOffset == 0 || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0) {
							uint laneId = PathManager.GetLaneID(curPathPos);
							if (laneId != 0u) {
								netMan.m_lanes.m_buffer[laneId].ReserveSpace(this.m_info.m_generatedInfo.m_size.z);
							}
						} else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(pathPosIndex >> 1, out nextPathPos)) {
							// NON-STOCK CODE START
							ushort transitNodeId;
							if (curPathPos.m_offset < 128) {
								transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_startNode;
							} else {
								transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_endNode;
							}

							bool spaceReservationAllowed = true;
#if BENCHMARK
							using (var bm = new Benchmark(null, "IsSpaceReservationAllowed")) {
#endif
								spaceReservationAllowed = VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(transitNodeId, curPathPos, nextPathPos);
#if BENCHMARK
							}
#endif
							if (spaceReservationAllowed) {
								// NON-STOCK CODE END

								uint nextLaneId = PathManager.GetLaneID(nextPathPos);
								if (nextLaneId != 0u) {
									netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(this.m_info.m_generatedInfo.m_size.z);
								}
							} // NON-STOCK CODE
						}
					}
				}

				beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
				float negTotalAttachLen = -((this.m_info.m_generatedInfo.m_wheelBase + frontVehInfo.m_generatedInfo.m_wheelBase) * 0.5f + attachOffset + frontAttachOffset);
				bool hasPath = false;
				if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
					float u1;
					float u2;
					if (Line3.Intersect(posBeforeWheelRot, vehicleData.m_targetPos1, frontPosMinusWheelBaseRot, negTotalAttachLen, out u1, out u2)) {
						targetMotion = beforeRotToTargetPos1Diff * Mathf.Clamp(Mathf.Min(u1, u2) / 0.6f, 0f, 2f);
					} else {
						Line3.DistanceSqr(posBeforeWheelRot, vehicleData.m_targetPos1, frontPosMinusWheelBaseRot, out u1);
						targetMotion = beforeRotToTargetPos1Diff * Mathf.Clamp(u1 / 0.6f, 0f, 2f);
					}
					hasPath = true;
				}

				if (hasPath) {
					if (Vector3.Dot(frontPosMinusWheelBaseRot - posBeforeWheelRot, posBeforeWheelRot - posAfterWheelRot) < 0f) {
						motionFactor = 0f;
					}
				} else {
					float frontPosBeforeToAfterWheelRotDist = Vector3.Distance(frontPosMinusWheelBaseRot, posBeforeWheelRot);
					motionFactor = 0f;
					targetMotion = curInvRot * ((frontPosMinusWheelBaseRot - posBeforeWheelRot) * (Mathf.Max(0f, frontPosBeforeToAfterWheelRotDist - negTotalAttachLen) / Mathf.Max(1f, frontPosBeforeToAfterWheelRotDist * 0.6f)));
				}
			} else {
				float estimatedFrameDist = (curSpeed + acceleration) * (0.5f + 0.5f * (curSpeed + acceleration) / braking);
				float maxSpeedAdd = Mathf.Max(curSpeed + acceleration, 2f);
				float meanSpeedAdd = Mathf.Max((estimatedFrameDist - maxSpeedAdd) / 2f, 1f);
				float maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
				float meanSpeedAddSqr = meanSpeedAdd * meanSpeedAdd;
				if (Vector3.Dot(vehicleData.m_targetPos1 - vehicleData.m_targetPos0, (Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) < 0f && vehicleData.m_path != 0u && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0) {
					int someIndex = -1;
					InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posAfterWheelRot, leaderID, ref leaderData, ref someIndex, 0, 0, Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
					beforeRotToTargetPos1DiffSqrMag = 0f;
				}

				int posIndex = 0;
				bool flag3 = false;
				if ((beforeRotToTargetPos1DiffSqrMag < maxSpeedAddSqr || vehicleData.m_targetPos3.w < 0.01f) && (leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0) {
					if (vehicleData.m_path != 0u) {
						InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, posAfterWheelRot, posBeforeWheelRot, leaderID, ref leaderData, ref posIndex, 1, 4, maxSpeedAddSqr, meanSpeedAddSqr);
					}
					if (posIndex < 4) {
						flag3 = true;
						while (posIndex < 4) {
							vehicleData.SetTargetPos(posIndex, vehicleData.GetTargetPos(posIndex - 1));
							posIndex++;
						}
					}
					beforeRotToTargetPos1Diff = (Vector3)vehicleData.m_targetPos1 - posBeforeWheelRot;
					beforeRotToTargetPos1DiffSqrMag = beforeRotToTargetPos1Diff.sqrMagnitude;
				}
				
				if ((leaderData.m_flags & (Vehicle.Flags.WaitingPath | Vehicle.Flags.Stopped)) == (Vehicle.Flags)0 && this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) {
					CustomForceTrafficLights(vehicleID, ref vehicleData, curSpeed > 0.1f); // NON-STOCK CODE
				}

				if (vehicleData.m_path != 0u) {
					NetManager netMan = Singleton<NetManager>.instance;
					byte pathPosIndex = vehicleData.m_pathPositionIndex;
					byte lastPathOffset = vehicleData.m_lastPathOffset;
					if (pathPosIndex == 255) {
						pathPosIndex = 0;
					}

					PathManager pathMan = Singleton<PathManager>.instance;
					PathUnit.Position curPathPos;
					if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(pathPosIndex >> 1, out curPathPos)) {
						netMan.m_segments.m_buffer[curPathPos.m_segment].AddTraffic(Mathf.RoundToInt(this.m_info.m_generatedInfo.m_size.z * 3f), this.GetNoiseLevel());
						PathUnit.Position nextPathPos;
						if ((pathPosIndex & 1) == 0 || lastPathOffset == 0 || (leaderData.m_flags & Vehicle.Flags.WaitingPath) != (Vehicle.Flags)0) {
							uint laneId = PathManager.GetLaneID(curPathPos);
							if (laneId != 0u) {
								netMan.m_lanes.m_buffer[laneId].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
							}
						} else if (pathMan.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(pathPosIndex >> 1, out nextPathPos)) {
							// NON-STOCK CODE START
							ushort transitNodeId;
							if (curPathPos.m_offset < 128) {
								transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_startNode;
							} else {
								transitNodeId = netMan.m_segments.m_buffer[curPathPos.m_segment].m_endNode;
							}

							bool spaceReservationAllowed = true;
#if BENCHMARK
							using (var bm = new Benchmark(null, "IsSpaceReservationAllowed")) {
#endif
								spaceReservationAllowed = VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(transitNodeId, curPathPos, nextPathPos);
#if BENCHMARK
							}
#endif
							if (spaceReservationAllowed) {
								// NON-STOCK CODE END

								uint nextLaneId = PathManager.GetLaneID(nextPathPos);
								if (nextLaneId != 0u) {
									netMan.m_lanes.m_buffer[nextLaneId].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
								}
							} // NON-STOCK CODE
						}
					}
				}

				float maxSpeed;
				if ((leaderData.m_flags & Vehicle.Flags.Stopped) != (Vehicle.Flags)0) {
					maxSpeed = 0f;
				} else {
					maxSpeed = Mathf.Min(vehicleData.m_targetPos1.w, GetMaxSpeed(leaderID, ref leaderData));
				}

				beforeRotToTargetPos1Diff = curInvRot * beforeRotToTargetPos1Diff;
				if (reversed) {
					beforeRotToTargetPos1Diff = -beforeRotToTargetPos1Diff;
				}

				bool blocked = false;
				float forwardLen = 0f;
				if (beforeRotToTargetPos1DiffSqrMag > 1f) {
					forward = VectorUtils.NormalizeXZ(beforeRotToTargetPos1Diff, out forwardLen);
					if (forwardLen > 1f) {
						Vector3 fwd = beforeRotToTargetPos1Diff;
						maxSpeedAdd = Mathf.Max(curSpeed, 2f);
						maxSpeedAddSqr = maxSpeedAdd * maxSpeedAdd;
						if (beforeRotToTargetPos1DiffSqrMag > maxSpeedAddSqr) {
							float num20 = maxSpeedAdd / Mathf.Sqrt(beforeRotToTargetPos1DiffSqrMag);
							fwd.x *= num20;
							fwd.y *= num20;
						}

						if (fwd.z < -1f) {
							if (vehicleData.m_path != 0u && (leaderData.m_flags & Vehicle.Flags.WaitingPath) == (Vehicle.Flags)0) {
								Vector3 targetPos0TargetPos1Diff = vehicleData.m_targetPos1 - vehicleData.m_targetPos0;
								targetPos0TargetPos1Diff = curInvRot * targetPos0TargetPos1Diff;
								if (reversed) {
									targetPos0TargetPos1Diff = -targetPos0TargetPos1Diff;
								}

								if (targetPos0TargetPos1Diff.z < -0.01f) {
									if (beforeRotToTargetPos1Diff.z < Mathf.Abs(beforeRotToTargetPos1Diff.x) * -10f) {
										if (curSpeed < 0.01f) {
											Reverse(leaderID, ref leaderData);
											return;
										}

										fwd.z = 0f;
										beforeRotToTargetPos1Diff = Vector3.zero;
										maxSpeed = 0f;
									} else {
										posBeforeWheelRot = posAfterWheelRot + Vector3.Normalize(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) * this.m_info.m_generatedInfo.m_wheelBase;
										posIndex = -1;
										InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, vehicleData.m_targetPos1, leaderID, ref leaderData, ref posIndex, 0, 0, Vector3.SqrMagnitude(vehicleData.m_targetPos1 - vehicleData.m_targetPos0) + 1f, 1f);
									}
								} else {
									posIndex = -1;
									InvokeUpdatePathTargetPositions(trainAI, vehicleID, ref vehicleData, vehicleData.m_targetPos0, posAfterWheelRot, leaderID, ref leaderData, ref posIndex, 0, 0, Vector3.SqrMagnitude(posAfterWheelRot - (Vector3)vehicleData.m_targetPos0) + 1f, 1f);
									vehicleData.m_targetPos1 = posBeforeWheelRot;
									fwd.z = 0f;
									beforeRotToTargetPos1Diff = Vector3.zero;
									maxSpeed = 0f;
								}
							}
							motionFactor = 0f;
						}

						forward = VectorUtils.NormalizeXZ(fwd, out forwardLen);
						float curve = Mathf.PI / 2f /* 1.57079637f*/ * (1f - forward.z); // <- constant: a bit inaccurate PI/2
						if (forwardLen > 1f) {
							curve /= forwardLen;
						}

						maxSpeed = Mathf.Min(maxSpeed, this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1000f, curve));
						float targetDist = forwardLen;
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos2.w, braking));
						targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos2 - vehicleData.m_targetPos1);
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, vehicleData.m_targetPos3.w, braking));
						targetDist += VectorUtils.LengthXZ(vehicleData.m_targetPos3 - vehicleData.m_targetPos2);
						maxSpeed = Mathf.Min(maxSpeed, CalculateMaxSpeed(targetDist, 0f, braking));
						if (maxSpeed < curSpeed) {
							float brake = Mathf.Max(acceleration, Mathf.Min(braking, curSpeed));
							targetSpeed = Mathf.Max(maxSpeed, curSpeed - brake);
						} else {
							float accel = Mathf.Max(acceleration, Mathf.Min(braking, -curSpeed));
							targetSpeed = Mathf.Min(maxSpeed, curSpeed + accel);
						}
					}
				} else if (curSpeed < 0.1f && flag3 && vehicleInfo.m_vehicleAI.ArriveAtDestination(leaderID, ref leaderData)) {
					leaderData.Unspawn(leaderID);
					return;
				}

				if ((leaderData.m_flags & Vehicle.Flags.Stopped) == (Vehicle.Flags)0 && maxSpeed < 0.1f) {
					blocked = true;
				}

				if (blocked) {
					leaderData.m_blockCounter = (byte)Mathf.Min((int)(leaderData.m_blockCounter + 1), 255);
				} else {
					leaderData.m_blockCounter = 0;
				}

				if (forwardLen > 1f) {
					if (reversed) {
						forward = -forward;
					}
					targetMotion = forward * targetSpeed;
				} else {
					if (reversed) {
						beforeRotToTargetPos1Diff = -beforeRotToTargetPos1Diff;
					}
					Vector3 vel = Vector3.ClampMagnitude(beforeRotToTargetPos1Diff * 0.5f - curveTangent, braking);
					targetMotion = curveTangent + vel;
				}
			}

			Vector3 springs = targetMotion - curveTangent;
			Vector3 targetAfterWheelRotMotion = frameData.m_rotation * targetMotion;
			Vector3 posAfterWheelRotToTargetDiff = Vector3.Normalize((Vector3)vehicleData.m_targetPos0 - posAfterWheelRot) * (targetMotion.magnitude * motionFactor);
			posBeforeWheelRot += targetAfterWheelRotMotion;
			posAfterWheelRot += posAfterWheelRotToTargetDiff;

			Vector3 targetPos;
			if (reversed) {
				frameData.m_rotation = Quaternion.LookRotation(posAfterWheelRot - posBeforeWheelRot);
				targetPos = posBeforeWheelRot + frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			} else {
				frameData.m_rotation = Quaternion.LookRotation(posBeforeWheelRot - posAfterWheelRot);
				targetPos = posBeforeWheelRot - frameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			}
			frameData.m_velocity = targetPos - frameData.m_position;

			if (frontVehicleId != 0) {
				frameData.m_position += frameData.m_velocity * 0.6f;
			} else {
				frameData.m_position += frameData.m_velocity * 0.5f;
			}
			frameData.m_swayVelocity = frameData.m_swayVelocity * (1f - this.m_info.m_dampers) - springs * (1f - this.m_info.m_springs) - frameData.m_swayPosition * this.m_info.m_springs;
			frameData.m_swayPosition += frameData.m_swayVelocity * 0.5f;
			frameData.m_steerAngle = 0f;
			frameData.m_travelDistance += targetMotion.z;
			frameData.m_lightIntensity.x = ((!reversed) ? 5f : 0f);
			frameData.m_lightIntensity.y = ((!reversed) ? 0f : 5f);
			frameData.m_lightIntensity.z = 0f;
			frameData.m_lightIntensity.w = 0f;
			frameData.m_underground = ((vehicleData.m_flags & Vehicle.Flags.Underground) != (Vehicle.Flags)0);
			frameData.m_transition = ((vehicleData.m_flags & Vehicle.Flags.Transition) != (Vehicle.Flags)0);
			//base.SimulationStep(vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
		}

		public void TmCalculateSegmentPositionPathFinder(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed) {
			NetManager instance = Singleton<NetManager>.instance;
			instance.m_lanes.m_buffer[laneID].CalculatePositionAndDirection((float)offset * 0.003921569f, out pos, out dir);
			NetInfo info = instance.m_segments.m_buffer[(int)position.m_segment].Info;
			if (info.m_lanes != null && info.m_lanes.Length > (int)position.m_lane) {
				float laneSpeedLimit = 1;
#if BENCHMARK
				using (var bm = new Benchmark(null, "GetLockFreeGameSpeedLimit")) {
#endif
					laneSpeedLimit = Options.customSpeedLimitsEnabled ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(position.m_segment, position.m_lane, laneID, info.m_lanes[position.m_lane]) : info.m_lanes[position.m_lane].m_speedLimit;
#if BENCHMARK
				}
#endif
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, laneSpeedLimit, instance.m_lanes.m_buffer[laneID].m_curve);
			} else {
				maxSpeed = this.CalculateTargetSpeed(vehicleID, ref vehicleData, 1f, 0f);
			}
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
#if DEBUG
			//Log._Debug($"CustomTrainAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

			/// NON-STOCK CODE START ///
			ExtVehicleType vehicleType = ExtVehicleType.None;
#if BENCHMARK
			using (var bm = new Benchmark(null, "OnStartPathFind")) {
#endif
				vehicleType = VehicleStateManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
				if (vehicleType == ExtVehicleType.None) {
#if DEBUG
					Log.Warning($"CustomTrainAI.CustomStartPathFind: Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
					vehicleType = ExtVehicleType.RailVehicle;
				} else if (vehicleType == ExtVehicleType.CargoTrain) {
					vehicleType = ExtVehicleType.CargoVehicle;
				}
#if BENCHMARK
			}
#endif
			/// NON-STOCK CODE END ///

			VehicleInfo info = this.m_info;
			if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == 0 && Vector3.Distance(startPos, endPos) < 100f) {
				startPos = endPos;
			}
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
			if (CustomPathManager.FindPathPosition(startPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, VehicleInfo.VehicleType.None, allowUnderground, false, 32f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB) &&
				CustomPathManager.FindPathPosition(endPos, this.m_transportInfo.m_netService, this.m_transportInfo.m_secondaryNetService, NetInfo.LaneType.Vehicle, info.m_vehicleType, VehicleInfo.VehicleType.None, allowUnderground2, false, 32f, out endPosA, out endPosB, out endSqrDistA, out endSqrDistB)) {
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
				args.extVehicleType = vehicleType;
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
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
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

		public void CustomCheckNextLane(ushort vehicleId, ref Vehicle vehicleData, ref float maxSpeed, PathUnit.Position nextPosition, uint nextLaneId, byte nextOffset, PathUnit.Position refPosition, uint refLaneId, byte refOffset, Bezier3 bezier) {
			NetManager netManager = Singleton<NetManager>.instance;

			ushort nextSourceNodeId;
			if (nextOffset < nextPosition.m_offset) {
				nextSourceNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_startNode;
			} else {
				nextSourceNodeId = netManager.m_segments.m_buffer[(int)nextPosition.m_segment].m_endNode;
			}

			ushort refTargetNodeId;
			if (refOffset == 0) {
				refTargetNodeId = netManager.m_segments.m_buffer[(int)refPosition.m_segment].m_startNode;
			} else {
				refTargetNodeId = netManager.m_segments.m_buffer[(int)refPosition.m_segment].m_endNode;
			}

#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[21] && (GlobalConfig.Instance.Debug.NodeId <= 0 || refTargetNodeId == GlobalConfig.Instance.Debug.NodeId) && (GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.None || GlobalConfig.Instance.Debug.ExtVehicleType == ExtVehicleType.RailVehicle) && (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId);

			if (debug) {
				Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}) called.\n" +
					$"\trefPosition.m_segment={refPosition.m_segment}, refPosition.m_offset={refPosition.m_offset}\n" +
					$"\tnextPosition.m_segment={nextPosition.m_segment}, nextPosition.m_offset={nextPosition.m_offset}\n" +
					$"\trefLaneId={refLaneId}, refOffset={refOffset}\n" +
					$"\tprevLaneId={nextLaneId}, prevOffset={nextOffset}\n" +
					$"\tnextSourceNodeId={nextSourceNodeId}\n" +
					$"\trefTargetNodeId={refTargetNodeId}, refTargetNodeId={refTargetNodeId}");
			}
#endif

			Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
			float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

			Vector3 lastPosPlusRot = lastFrameData.m_position;
			Vector3 lastPosMinusRot = lastFrameData.m_position;
			Vector3 rotationAdd = lastFrameData.m_rotation * new Vector3(0f, 0f, this.m_info.m_generatedInfo.m_wheelBase * 0.5f);
			lastPosPlusRot += rotationAdd;
			lastPosMinusRot -= rotationAdd;
			float breakingDist = 0.5f * sqrVelocity / this.m_info.m_braking;
			float distToTargetAfterRot = Vector3.Distance(lastPosPlusRot, bezier.a);
			float distToTargetBeforeRot = Vector3.Distance(lastPosMinusRot, bezier.a);

#if DEBUG
			if (debug)
				Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): lastPos={lastFrameData.m_position} lastPosMinusRot={lastPosMinusRot} lastPosPlusRot={lastPosPlusRot} rotationAdd={rotationAdd} breakingDist={breakingDist} distToTargetAfterRot={distToTargetAfterRot} distToTargetBeforeRot={distToTargetBeforeRot}");
#endif

			if (Mathf.Min(distToTargetAfterRot, distToTargetBeforeRot) >= breakingDist - 5f) {
				/*VehicleManager vehMan = Singleton<VehicleManager>.instance;
				ushort firstVehicleId = vehicleData.GetFirstVehicle(vehicleId);
				if (VehicleBehaviorManager.Instance.MayDespawn(ref vehMan.m_vehicles.m_buffer[firstVehicleId]) || vehMan.m_vehicles.m_buffer[firstVehicleId].m_blockCounter < 100) {*/ // NON-STOCK CODE
#if DEBUG
					if (debug)
						Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Checking for free space on lane {nextLaneId}.");
#endif

					if (!netManager.m_lanes.m_buffer[nextLaneId].CheckSpace(1000f, vehicleId)) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): No space available on lane {nextLaneId}. ABORT.");
#endif
						vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
						vehicleData.m_waitCounter = 0;
						maxSpeed = 0f;
						return;
					}

					Vector3 bezierMiddlePoint = bezier.Position(0.5f);
					Segment3 segment;
					if (Vector3.SqrMagnitude(vehicleData.m_segment.a - bezierMiddlePoint) < Vector3.SqrMagnitude(bezier.a - bezierMiddlePoint)) {
						segment = new Segment3(vehicleData.m_segment.a, bezierMiddlePoint);
					} else {
						segment = new Segment3(bezier.a, bezierMiddlePoint);
					}

#if DEBUG
					if (debug)
						Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Checking for overlap (1). segment.a={segment.a} segment.b={segment.b}");
#endif
					if (segment.LengthSqr() >= 3f) {
						segment.a += (segment.b - segment.a).normalized * 2.5f;
						if (CustomTrainAI.CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
#if DEBUG
							if (debug)
								Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Overlap detected (1). segment.LengthSqr()={segment.LengthSqr()} segment.a={segment.a} ABORT.");
#endif
							vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
							vehicleData.m_waitCounter = 0;
							maxSpeed = 0f;
							return;
						}
					}

					segment = new Segment3(bezierMiddlePoint, bezier.d);
#if DEBUG
					if (debug)
						Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Checking for overlap (2). segment.a={segment.a} segment.b={segment.b}");
#endif
					if (segment.LengthSqr() >= 1f && CustomTrainAI.CheckOverlap(vehicleId, ref vehicleData, segment, vehicleId)) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Overlap detected (2). ABORT.");
#endif
						vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
						vehicleData.m_waitCounter = 0;
						maxSpeed = 0f;
						return;
					}
				//} // NON-STOCK CODE

				//if (this.m_info.m_vehicleType != VehicleInfo.VehicleType.Monorail) { // NON-STOCK CODE
				if (nextSourceNodeId == refTargetNodeId) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Checking if vehicle is allowed to change segment.");
#endif
					float oldMaxSpeed = maxSpeed;
					bool mayChange = true;
#if BENCHMARK
					using (var bm = new Benchmark(null, "MayChangeSegment")) {
#endif
						mayChange = VehicleBehaviorManager.Instance.MayChangeSegment(vehicleId, ref VehicleStateManager.Instance.VehicleStates[vehicleId], ref vehicleData, sqrVelocity, false, ref refPosition, ref netManager.m_segments.m_buffer[refPosition.m_segment], refTargetNodeId, refLaneId, ref nextPosition, refTargetNodeId, ref netManager.m_nodes.m_buffer[refTargetNodeId], nextLaneId, out maxSpeed);
#if BENCHMARK
					}
#endif
					if (!mayChange) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomTrainAI.CustomCheckNextLane({vehicleId}): Vehicle is NOT allowed to change segment. ABORT.");
#endif
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
					maxSpeed = oldMaxSpeed;
				}
				//} // NON-STOCK CODE
			}
		}

		private void CustomForceTrafficLights(ushort vehicleID, ref Vehicle vehicleData, bool reserveSpace) {
			uint pathUnitId = vehicleData.m_path;
			if (pathUnitId != 0u) {
				NetManager netMan = Singleton<NetManager>.instance;
				PathManager pathMan = Singleton<PathManager>.instance;
				byte pathPosIndex = vehicleData.m_pathPositionIndex;
				if (pathPosIndex == 255) {
					pathPosIndex = 0;
				}
				pathPosIndex = (byte)(pathPosIndex >> 1);
				bool stopLoop = false; // NON-STOCK CODE
				for (int i = 0; i < 6; i++) {
					PathUnit.Position position;
					if (!pathMan.m_pathUnits.m_buffer[pathUnitId].GetPosition(pathPosIndex, out position)) {
						return;
					}

					// NON-STOCK CODE START
					ushort transitNodeId;
					if (position.m_offset < 128) {
						transitNodeId = netMan.m_segments.m_buffer[position.m_segment].m_startNode;
					} else {
						transitNodeId = netMan.m_segments.m_buffer[position.m_segment].m_endNode;
					}

#if BENCHMARK
					using (var bm = new Benchmark(null, "IsSpaceReservationAllowed")) {
#endif
						if (Options.timedLightsEnabled) {
							// when a TTL is active only reserve space if it shows green
							PathUnit.Position nextPos;
							if (pathMan.m_pathUnits.m_buffer[pathUnitId].GetNextPosition(pathPosIndex, out nextPos)) {
								if (!VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(transitNodeId, position, nextPos)) {
									stopLoop = true;
								}
							}
						}
#if BENCHMARK
					}
#endif
					// NON-STOCK CODE END

					if (reserveSpace && i >= 1 && i <= 2) {
						uint laneID = PathManager.GetLaneID(position);
						if (laneID != 0u) {
							reserveSpace = netMan.m_lanes.m_buffer[laneID].ReserveSpace(this.m_info.m_generatedInfo.m_size.z, vehicleID);
						}
					}
					ForceTrafficLights(transitNodeId, position); // NON-STOCK CODE

					// NON-STOCK CODE START
					if (stopLoop) {
						return;
					}
					// NON-STOCK CODE END

					if ((pathPosIndex += 1) >= pathMan.m_pathUnits.m_buffer[pathUnitId].m_positionCount) {
						pathUnitId = pathMan.m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit;
						pathPosIndex = 0;
						if (pathUnitId == 0u) {
							return;
						}
					}
				}
			}
		}

		// slightly modified version of TrainAI.ForceTrafficLights(PathUnit.Position)
		private static void ForceTrafficLights(ushort transitNodeId, PathUnit.Position position) {
			NetManager netMan = Singleton<NetManager>.instance;
			if ((netMan.m_nodes.m_buffer[(int)transitNodeId].m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None) {
				uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
				uint shiftedNodeId = (uint)(((int)transitNodeId << 8) / 32768);
				uint rand = frame - shiftedNodeId & 255u;
				RoadBaseAI.TrafficLightState vehicleLightState;
				RoadBaseAI.TrafficLightState pedestrianLightState;
				bool vehicles;
				bool pedestrians;
				RoadBaseAI.GetTrafficLightState(transitNodeId, ref netMan.m_segments.m_buffer[(int)position.m_segment], frame - shiftedNodeId, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if (!vehicles && rand >= 196u) {
					vehicles = true;
					RoadBaseAI.SetTrafficLightState(transitNodeId, ref netMan.m_segments.m_buffer[(int)position.m_segment], frame - shiftedNodeId, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
				}
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		protected static bool CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle) {
			Log.Error("CustomTrainAI.CheckOverlap (1) called.");
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		protected static ushort CheckOverlap(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle, ushort otherID, ref Vehicle otherData, ref bool overlap, Vector3 min, Vector3 max) {
			Log.Error("CustomTrainAI.CheckOverlap (2) called.");
			return 0;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void InitializePath(ushort vehicleID, ref Vehicle vehicleData) {
			Log.Error("CustomTrainAI.InitializePath called");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void InvokeUpdatePathTargetPositions(TrainAI trainAI, ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos1, Vector3 refPos2, ushort leaderID, ref Vehicle leaderData, ref int index, int max1, int max2, float minSqrDistanceA, float minSqrDistanceB) {
			Log.Error($"CustomTrainAI.InvokeUpdatePathTargetPositions called! trainAI={trainAI}");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Reverse(ushort leaderID, ref Vehicle leaderData) {
			Log.Error("CustomTrainAI.Reverse called");
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
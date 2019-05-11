﻿using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Manager.Impl {
	public class AdvancedParkingManager : AbstractFeatureManager, IAdvancedParkingManager {
		public static AdvancedParkingManager Instance { get; private set; } = null;

		static AdvancedParkingManager() {
			Instance = new AdvancedParkingManager();
		}

		protected override void OnDisableFeatureInternal() {
			for (int citizenInstanceId = 0; citizenInstanceId < ExtCitizenInstanceManager.Instance.ExtInstances.Length; ++citizenInstanceId) {
				ExtPathMode pathMode = ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode;
				switch (pathMode) {
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
					case ExtPathMode.ApproachingParkedCar:
						// citizen requires a path to their parked car: release instance to prevent it from floating
						Services.CitizenService.ReleaseCitizenInstance((ushort)citizenInstanceId);
						break;
					case ExtPathMode.RequiresCarPath:
					case ExtPathMode.RequiresMixedCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.DrivingToKnownParkPos:
					case ExtPathMode.DrivingToTarget:
						if (Services.CitizenService.CheckCitizenInstanceFlags((ushort)citizenInstanceId, CitizenInstance.Flags.Character)) {
							// citizen instance requires a car but is walking: release instance to prevent it from floating
							Services.CitizenService.ReleaseCitizenInstance((ushort)citizenInstanceId);
						}
						break;
				}
			}
			ExtCitizenManager.Instance.Reset();
			ExtCitizenInstanceManager.Instance.Reset();
		}

		protected override void OnEnableFeatureInternal() {
			
		}

		public ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId, ref CitizenInstance citizenInstance, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen, ref Citizen citizen, ExtPathState mainPathState) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == citizenInstanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenInstance.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == citizenInstance.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == citizenInstance.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
			if (fineDebug)
				Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}) called.");
#endif
			if (mainPathState == ExtPathState.Calculating) {
				// main path is still calculating, do not check return path
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): still calculating main path. returning CALCULATING.");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			//ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(citizenInstanceId);

			if (!extInstance.IsValid()) {
				// no citizen
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): no citizen found!");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
				// main path failed or non-existing
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): mainPathSate is {mainPathState}.");
#endif

				if (mainPathState == ExtPathState.Failed) {
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Checking if path-finding may be repeated.");
#endif
					return OnCitizenPathFindFailure(citizenInstanceId, ref citizenInstance, ref extInstance, ref extCitizen);
				} else {
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Resetting instance and returning FAILED.");
#endif

					extInstance.Reset();
					return ExtSoftPathState.FailedHard;
				}
			}

			// main path state is READY

			// main path calculation succeeded: update return path state and check its state if necessary
			extInstance.UpdateReturnPathState();

			bool success = true;
			switch (extInstance.returnPathState) {
				case ExtPathState.None:
				default:
					// no return path calculated: ignore
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): return path state is None. Ignoring and returning main path state.");
#endif
					break;
				case ExtPathState.Calculating: // OK
											   // return path not read yet: wait for it
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): return path state is still calculating.");
#endif
					return ExtSoftPathState.Calculating;
				case ExtPathState.Failed: // OK
										  // no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Return path FAILED.");
#endif
					success = false;
					break;
				case ExtPathState.Ready:
					// handle valid return path
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Path is READY.");
#endif
					break;
			}

			extInstance.ReleaseReturnPath();

			if (success) {
				// handle path find success
				return OnCitizenPathFindSuccess(citizenInstanceId, ref citizenInstance, ref extInstance, ref extCitizen, ref citizen);
			} else {
				// handle path find failure
				return OnCitizenPathFindFailure(citizenInstanceId, ref citizenInstance, ref extInstance, ref extCitizen);
			}
		}

		public ExtSoftPathState UpdateCarPathState(ushort vehicleId, ref Vehicle vehicleData, ref CitizenInstance driverInstance, ref ExtCitizenInstance driverExtInstance, ExtPathState mainPathState) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == driverExtInstance.instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverInstance.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == driverInstance.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == driverInstance.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
			if (fineDebug)
				Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}) called.");
#endif
			if (mainPathState == ExtPathState.Calculating) {
				// main path is still calculating, do not check return path
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): still calculating main path. returning CALCULATING.");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			//ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(CustomPassengerCarAI.GetDriverInstance(vehicleId, ref vehicleData));

			if (!driverExtInstance.IsValid()) {
				// no driver
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): no driver found!");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			if (VehicleStateManager.Instance.VehicleStates[vehicleId].vehicleType != ExtVehicleType.PassengerCar) {
				// non-passenger cars are not handled
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): not a passenger car!");
#endif
				driverExtInstance.Reset();
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
				// main path failed or non-existing: reset return path
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): mainPathSate is {mainPathState}.");
#endif

				if (mainPathState == ExtPathState.Failed) {
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Checking if path-finding may be repeated.");
#endif
					driverExtInstance.ReleaseReturnPath();
					return OnCarPathFindFailure(vehicleId, ref vehicleData, ref driverInstance, ref driverExtInstance);
				} else {
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Resetting instance and returning FAILED.");
#endif

					driverExtInstance.Reset();
					return ExtSoftPathState.FailedHard;
				}
			}

			// main path state is READY

			// main path calculation succeeded: update return path state and check its state
			driverExtInstance.UpdateReturnPathState();

			switch (driverExtInstance.returnPathState) {
				case ExtPathState.None:
				default:
					// no return path calculated: ignore
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): return path state is None. Setting pathMode=DrivingToTarget and returning main path state.");
#endif
					driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
					return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
				case ExtPathState.Calculating:
					// return path not read yet: wait for it
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): return path state is still calculating.");
#endif
					return ExtSoftPathState.Calculating;
				case ExtPathState.Failed:
					// no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Return path {driverExtInstance.returnPathId} FAILED. Forcing path-finding to fail.");
#endif
					driverExtInstance.Reset();
					return ExtSoftPathState.FailedHard;
				case ExtPathState.Ready:
					// handle valid return path
					driverExtInstance.ReleaseReturnPath();
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Path is ready for vehicle {vehicleId}, citizen instance {driverExtInstance.instanceId}! CurrentPathMode={driverExtInstance.pathMode}");
#endif
					byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[vehicleData.m_path].m_laneTypes;
					bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;

					if (usesPublicTransport && (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos || driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos)) {
						driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
						driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
						driverExtInstance.parkingSpaceLocationId = 0;
					}

					if (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
						driverExtInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
						driverExtInstance.parkingPathStartPosition = null;
#if DEBUG
						if (debug)
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Path to an alternative parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
#endif
					} else if (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget) {
						driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
#if DEBUG
						if (debug)
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Car path is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
#endif
					} else if (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) {
						driverExtInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
#if DEBUG
						if (debug)
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Car path to known parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
#endif
					}
					return ExtSoftPathState.Ready;
			}
		}

		public ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, Vector3 physicsLodRefPos, ref VehicleParked parkedCar) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == instanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == instanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CheckCitizenReachedParkedCar({instanceId}): citizen instance {instanceId} is waiting for path-finding to complete.");
#endif
				return ParkedCarApproachState.None;
			}

			//ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (extInstance.pathMode != ExtPathMode.ApproachingParkedCar && extInstance.pathMode != ExtPathMode.WalkingToParkedCar) {
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} is not reaching a parked car ({extInstance.pathMode})");
#endif
				return ParkedCarApproachState.None;
			}

			if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
#if DEBUG
				/*if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} is not spawned!");*/
#endif
				return ParkedCarApproachState.None;
			}

			Vector3 lastFramePos = instanceData.GetLastFramePosition();
			Vector3 doorPosition = parkedCar.GetClosestDoorPosition(parkedCar.m_position, VehicleInfo.DoorType.Enter);

			if (extInstance.pathMode == ExtPathMode.WalkingToParkedCar) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					extInstance.pathMode = ExtPathMode.ApproachingParkedCar;
					extInstance.lastDistanceToParkedCar = (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} was walking to parked car and reached final path position. Switched PathMode to {extInstance.pathMode}.");
#endif
				}
			}

			if (extInstance.pathMode == ExtPathMode.ApproachingParkedCar) {
				Vector3 doorTargetDir = doorPosition - lastFramePos;
				Vector3 doorWalkVector = doorPosition;
				float doorTargetDirMagnitude = doorTargetDir.magnitude;
				if (doorTargetDirMagnitude > 1f) {
					float speed = Mathf.Max(doorTargetDirMagnitude - 5f, doorTargetDirMagnitude * 0.5f);
					doorWalkVector = lastFramePos + (doorTargetDir * (speed / doorTargetDirMagnitude));
				}
				instanceData.m_targetPos = new Vector4(doorWalkVector.x, doorWalkVector.y, doorWalkVector.z, 0.5f);
				instanceData.m_targetDir = VectorUtils.XZ(doorTargetDir);

				CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, physicsLodRefPos);

				float doorSqrDist = (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;
				if (doorSqrDist > GlobalConfig.Instance.ParkingAI.MaxParkedCarInstanceSwitchSqrDistance) {
					// citizen is still too far away from the parked car
					ExtPathMode oldPathMode = extInstance.pathMode;
					if (doorSqrDist > extInstance.lastDistanceToParkedCar + 1024f) {
						// distance has increased dramatically since the last time

#if DEBUG
						if (debug)
							Log.Warning($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} is currently reaching their parked car but distance increased! dist={doorSqrDist}, LastDistanceToParkedCar={extInstance.lastDistanceToParkedCar}.");

						if (GlobalConfig.Instance.Debug.Switches[6]) {
							Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): FORCED PAUSE. Distance increased! Citizen instance {instanceId}. dist={doorSqrDist}");
							Singleton<SimulationManager>.instance.SimulationPaused = true;
						}
#endif

						CitizenInstance.Frame frameData = instanceData.GetLastFrameData();
						frameData.m_position = doorPosition;
						instanceData.SetLastFrameData(frameData);

						extInstance.pathMode = ExtCitizenInstance.ExtPathMode.RequiresCarPath;

						return ParkedCarApproachState.Approached;
					} else if (doorSqrDist < extInstance.lastDistanceToParkedCar) {
						extInstance.lastDistanceToParkedCar = doorSqrDist;
					}
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} is currently reaching their parked car (dist={doorSqrDist}, LastDistanceToParkedCar={extInstance.lastDistanceToParkedCar}). CurrentDepartureMode={extInstance.pathMode}");
#endif

					return ParkedCarApproachState.Approaching;
				} else {
					extInstance.pathMode = ExtCitizenInstance.ExtPathMode.RequiresCarPath;
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} reached parking position (dist={doorSqrDist}). Calculating remaining path now. CurrentDepartureMode={extInstance.pathMode}");
#endif
					return ParkedCarApproachState.Approached;
				}
			}

			return ParkedCarApproachState.None;
		}

		protected void CitizenApproachingParkedCarSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			if ((instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
				CitizenInstance.Frame lastFrameData = instanceData.GetLastFrameData();
				int oldGridX = Mathf.Clamp((int)(lastFrameData.m_position.x / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION - 1);
				int oldGridY = Mathf.Clamp((int)(lastFrameData.m_position.z / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION - 1);
				bool lodPhysics = Vector3.SqrMagnitude(physicsLodRefPos - lastFrameData.m_position) >= 62500f;
				CitizenApproachingParkedCarSimulationStep(instanceID, ref instanceData, ref lastFrameData, lodPhysics);
				int newGridX = Mathf.Clamp((int)(lastFrameData.m_position.x / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION - 1);
				int newGridY = Mathf.Clamp((int)(lastFrameData.m_position.z / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION - 1);
				if ((newGridX != oldGridX || newGridY != oldGridY) && (instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
					Singleton<CitizenManager>.instance.RemoveFromGrid(instanceID, ref instanceData, oldGridX, oldGridY);
					Singleton<CitizenManager>.instance.AddToGrid(instanceID, ref instanceData, newGridX, newGridY);
				}
				if (instanceData.m_flags != CitizenInstance.Flags.None) {
					instanceData.SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex, lastFrameData);
				}
			}
		}

		protected void CitizenApproachingParkedCarSimulationStep(ushort instanceID, ref CitizenInstance instanceData, ref CitizenInstance.Frame frameData, bool lodPhysics) {
			frameData.m_position = frameData.m_position + (frameData.m_velocity * 0.5f);

			Vector3 targetDiff = (Vector3)instanceData.m_targetPos - frameData.m_position;
			Vector3 targetVelDiff = targetDiff - frameData.m_velocity;
			float targetVelDiffMag = targetVelDiff.magnitude;

			targetVelDiff = targetVelDiff * (2f / Mathf.Max(targetVelDiffMag, 2f));
			frameData.m_velocity = frameData.m_velocity + targetVelDiff;
			frameData.m_velocity = frameData.m_velocity - (Mathf.Max(0f, Vector3.Dot((frameData.m_position + frameData.m_velocity) - (Vector3)instanceData.m_targetPos, frameData.m_velocity)) / Mathf.Max(0.01f, frameData.m_velocity.sqrMagnitude) * frameData.m_velocity);
			if (frameData.m_velocity.sqrMagnitude > 0.01f) {
				frameData.m_rotation = Quaternion.LookRotation(frameData.m_velocity);
			}
		}

		public bool CitizenApproachingTargetSimulationStep(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == instanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == instanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is waiting for path-finding to complete.");
#endif
				return false;
			}

			//ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (extInstance.pathMode != ExtCitizenInstance.ExtPathMode.WalkingToTarget &&
				extInstance.pathMode != ExtCitizenInstance.ExtPathMode.TaxiToTarget) {
#if DEBUG
				if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is not reaching target ({extInstance.pathMode})");
#endif
				return false;
			}

			if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
#if DEBUG
				/*if (fineDebug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is not spawned!");*/
#endif
				return false;
			}


			
			// check if path is complete
			PathUnit.Position pos;
			if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
				extInstance.Reset();
#if DEBUG
				if (debug)
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): Citizen instance {instanceId} reached target. CurrentDepartureMode={extInstance.pathMode}");
#endif
				return true;
			}

			return false;
		}

		/// <summary>
		/// Handles a path-finding success for activated Parking AI.
		/// </summary>
		/// <param name="instanceId">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="extInstance">Extended citizen instance data</param>
		/// <param name="extCitizen">Extended citizen data</param>
		/// <param name="citizenData">Citizen data</param>
		/// <returns>soft path state</returns>
		protected ExtSoftPathState OnCitizenPathFindSuccess(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen, ref Citizen citizenData) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == instanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == instanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path-finding succeeded for citizen instance {instanceId}. Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
#endif

			//ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (!extInstance.IsValid()) {
#if DEBUG
				if (fineDebug)
					Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Ext. citizen instance not found.");
#endif
				return ExtSoftPathState.FailedHard;
			}

			if (citizenData.m_vehicle == 0) {
				// citizen does not already have a vehicle assigned

				if (extInstance.pathMode == ExtPathMode.TaxiToTarget) {
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen uses a taxi. Decreasing public transport demand and returning READY.");
#endif

					// cim uses taxi
					if (instanceData.m_sourceBuilding != 0) {
						ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, true);
					}

					if (instanceData.m_targetBuilding != 0) {
						ExtBuildingManager.Instance.ExtBuildings[instanceData.m_targetBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, false);
					}

					extCitizen.transportMode |= ExtCitizen.ExtTransportMode.PublicTransport;
					return ExtSoftPathState.Ready;
				}

				ushort parkedVehicleId = citizenData.m_parkedVehicle;
				float sqrDistToParkedVehicle = 0f;
				if (parkedVehicleId != 0) {
					// calculate distance to parked vehicle
					VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
					Vector3 doorPosition = vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId].GetClosestDoorPosition(vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_position, VehicleInfo.DoorType.Enter);
					sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;
				}

				byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes;
				ushort vehicleTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_vehicleTypes;
				bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;
				bool usesCar = (laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0 && (vehicleTypes & (ushort)(VehicleInfo.VehicleType.Car)) != 0;

				if (usesPublicTransport && usesCar && (extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos || extInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos)) {
					/*
					 * when using public transport together with a car (assuming a "source -> walk -> drive -> walk -> use public transport -> walk -> target" path)
					 * discard parking space information since the cim has to park near the public transport stop
					 * (instead of parking in the vicinity of the target building).
					 * 
					 * TODO we could check if the path looks like "source -> walk -> use public transport -> walk -> drive -> [walk ->] target" (in this case parking space information would still be valid)
					*/
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen uses their car together with public transport. Discarding parking space information and setting path mode to CalculatingCarPathToTarget.");
#endif
					extInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
					extInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
					extInstance.parkingSpaceLocationId = 0;
				}

				switch (extInstance.pathMode) {
					case ExtPathMode.None: // citizen starts at source building
					default:
						if (extInstance.pathMode != ExtPathMode.None) {
#if DEBUG
							if (debug)
								Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Unexpected path mode {extInstance.pathMode}! {extInstance}");
#endif
						}

						if (usesCar) {
#if DEBUG
							if (debug)
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path for citizen instance {instanceId} contains passenger car section. Ensuring that citizen is allowed to use their car.");
#endif

							ushort sourceBuildingId = instanceData.m_sourceBuilding;
							ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;

							if (parkedVehicleId == 0) {
#if DEBUG
								if (debug)
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId} does not have a parked vehicle! CurrentPathMode={extInstance.pathMode}");
#endif

								// try to spawn parked vehicle in the vicinity of the starting point.
								VehicleInfo vehicleInfo = null;
								if (instanceData.Info.m_agePhase > Citizen.AgePhase.Child) {
									// get a random car info (due to the fact we are using a different randomizer, car assignment differs from the selection in ResidentAI.GetVehicleInfo/TouristAI.GetVehicleInfo method, but this should not matter since we are reusing parked vehicle infos there)
									vehicleInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref Singleton<SimulationManager>.instance.m_randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialLow, ItemClass.Level.Level1);
								}

								if (vehicleInfo != null) {
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId} is using their own passenger car. CurrentPathMode={extInstance.pathMode}");
#endif

									// determine current position vector
									Vector3 currentPos;
									ushort currentBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].GetBuildingByLocation();
									if (currentBuildingId != 0) {
										currentPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[currentBuildingId].m_position;
#if DEBUG
										if (debug)
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Taking current position from current building {currentBuildingId} for citizen {instanceData.m_citizen} (citizen instance {instanceId}): {currentPos} CurrentPathMode={extInstance.pathMode}");
#endif
									} else {
										currentBuildingId = sourceBuildingId;
										currentPos = instanceData.GetLastFramePosition();
#if DEBUG
										if (debug)
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Taking current position from last frame position for citizen {instanceData.m_citizen} (citizen instance {instanceId}): {currentPos}. Home {homeId} pos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position} CurrentPathMode={extInstance.pathMode}");
#endif
									}

									// spawn a passenger car near the current position
									Vector3 parkPos;
									ParkingUnableReason parkReason;
									if (AdvancedParkingManager.Instance.TrySpawnParkedPassengerCar(instanceData.m_citizen, homeId, currentPos, vehicleInfo, out parkPos, out parkReason)) {
										parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
#if DEBUG
										if (debug)
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Parked vehicle for citizen {instanceData.m_citizen} (instance {instanceId}) is {parkedVehicleId} now (parkPos={parkPos}).");
#endif

										if (currentBuildingId != 0) {
											ExtBuildingManager.Instance.ExtBuildings[currentBuildingId].ModifyParkingSpaceDemand(parkPos, GlobalConfig.Instance.ParkingAI.MinSpawnedCarParkingSpaceDemandDelta, GlobalConfig.Instance.ParkingAI.MaxSpawnedCarParkingSpaceDemandDelta);
										}
									} else {
#if DEBUG
										if (debug) {
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): >> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} (citizen instance {instanceId}). reason={parkReason}. homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
										}
#endif

										if (parkReason == ParkingUnableReason.NoSpaceFound && currentBuildingId != 0) {
											ExtBuildingManager.Instance.ExtBuildings[currentBuildingId].AddParkingSpaceDemand(GlobalConfig.Instance.ParkingAI.FailedSpawnParkingSpaceDemandIncrement);
										}
									}
								} else {
#if DEBUG
									if (debug) {
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId}, home {homeId} does not own a vehicle.");
									}
#endif
								}
							}

							if (parkedVehicleId != 0) {
								// citizen has to reach their parked vehicle first
#if DEBUG
								if (debug)
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceId}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif

								extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
								return ExtSoftPathState.FailedSoft;
							} else {
								// error! could not find/spawn parked car
#if DEBUG
								if (debug)
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} still does not have a parked vehicle! Retrying: Cim should walk to target");
#endif

								extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
								return ExtSoftPathState.FailedSoft;
							}
						} else {
							// path does not contain a car section: path can be reused for walking
#if DEBUG
							if (debug)
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car path OR initial path was queried that does not contain a car section. Switching path mode to walking.");
#endif

							if (usesPublicTransport) {
								// decrease public tranport demand
								if (instanceData.m_sourceBuilding != 0) {
									ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, true);
								}
								if (instanceData.m_targetBuilding != 0) {
									ExtBuildingManager.Instance.ExtBuildings[instanceData.m_targetBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, false);
								}
								extCitizen.transportMode |= ExtCitizen.ExtTransportMode.PublicTransport;
							}

							extInstance.pathMode = ExtPathMode.WalkingToTarget;
							return ExtSoftPathState.Ready;
						}
					case ExtPathMode.CalculatingCarPathToTarget: // citizen has not yet entered their car (but is close to do so) and tries to reach the target directly
					case ExtPathMode.CalculatingCarPathToKnownParkPos: // citizen has not yet entered their (but is close to do so) car and tries to reach a parking space in the vicinity of the target
					case ExtPathMode.CalculatingCarPathToAltParkPos: // citizen has not yet entered their car (but is close to do so) and tries to reach an alternative parking space in the vicinity of the target
						if (usesCar) {
							// parked car should be reached now
#if DEBUG
							if (debug)
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path for citizen instance {instanceId} contains passenger car section and citizen should stand in front of their car.");
#endif
							if (extInstance.atOutsideConnection) {
								// car path calculated starting at road outside connection: success
								if (extInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
									extInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
									extInstance.parkingPathStartPosition = null;
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path to an alternative parking position is READY! CurrentPathMode={extInstance.pathMode}");
#endif
								} else if (extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget) {
									extInstance.pathMode = ExtPathMode.DrivingToTarget;
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Car path is READY! CurrentPathMode={extInstance.pathMode}");
#endif
								} else if (extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) {
									extInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Car path to known parking position is READY! CurrentPathMode={extInstance.pathMode}");
#endif
								}
								extInstance.atOutsideConnection = false; // citizen leaves outside connection
								return ExtSoftPathState.Ready;
							} else if (parkedVehicleId == 0) {
								// error! could not find/spawn parked car
#if DEBUG
								if (debug)
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} still does not have a parked vehicle! Retrying: Cim should walk to target");
#endif

								extInstance.Reset();
								extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
								return ExtSoftPathState.FailedSoft;
							} else if (sqrDistToParkedVehicle > 4f * GlobalConfig.Instance.ParkingAI.MaxParkedCarInstanceSwitchSqrDistance) {
								// error! parked car is too far away
#if DEBUG
								if (debug)
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} cannot enter parked vehicle because it is too far away (sqrDistToParkedVehicle={sqrDistToParkedVehicle})! Retrying: Cim should walk to parked car");
#endif
								extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
								return ExtSoftPathState.FailedSoft;
							} else {
								// path using passenger car has been calculated
								ushort vehicleId;
								if (CustomHumanAI.EnterParkedCar(instanceId, ref instanceData, parkedVehicleId, out vehicleId)) { // TODO move method body here
									extInstance.pathMode = extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget ? ExtPathMode.DrivingToTarget : ExtPathMode.DrivingToKnownParkPos;
									extCitizen.transportMode |= ExtCitizen.ExtTransportMode.Car;

#if DEBUG
									if (fineDebug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} has entered their car and is now travelling by car (vehicleId={vehicleId}). CurrentDepartureMode={extInstance.pathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
									return ExtSoftPathState.Ignore;
								} else {
									// error! parked car could not be entered (reached vehicle limit?): try to walk to target
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceId}. Trying to walk to target. CurrentDepartureMode={extInstance.pathMode}");
#endif

									extInstance.Reset();
									extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
									return ExtSoftPathState.FailedSoft;
								}
							}
						} else {
							// citizen does not need a car for the calculated path...
							switch (extInstance.pathMode) {
								case ExtPathMode.CalculatingCarPathToTarget:
									// ... and the path can be reused for walking
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car path was queried that does not contain a car section. Switching path mode to walking.");
#endif
									extInstance.Reset();

									if (usesPublicTransport) {
										// decrease public tranport demand
										if (instanceData.m_sourceBuilding != 0) {
											ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, true);
										}
										if (instanceData.m_targetBuilding != 0) {
											ExtBuildingManager.Instance.ExtBuildings[instanceData.m_targetBuilding].RemovePublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement, false);
										}
										extCitizen.transportMode |= ExtCitizen.ExtTransportMode.PublicTransport;
									}

									extInstance.pathMode = ExtPathMode.WalkingToTarget;
									return ExtSoftPathState.Ready;
								case ExtPathMode.CalculatingCarPathToKnownParkPos:
								case ExtPathMode.CalculatingCarPathToAltParkPos:
								default:
									// ... and a path to a parking spot was calculated: dismiss path and restart path-finding for walking
#if DEBUG
									if (debug)
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A parking space car path was queried but it turned out that no car is needed. Retrying path-finding for walking.");
#endif
									extInstance.Reset();
									extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
									return ExtSoftPathState.FailedSoft;
							}
						}
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
						// path to parked vehicle has been calculated...
						if (parkedVehicleId == 0) {
							// ... but the parked vehicle has vanished
#if DEBUG
							if (debug)
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} shall walk to their parked vehicle but it disappeared. Retrying path-find for walking.");
#endif
							extInstance.Reset();
							extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
							return ExtSoftPathState.FailedSoft;
						} else {
							extInstance.pathMode = ExtPathMode.WalkingToParkedCar;
#if DEBUG
							if (debug)
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.pathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
							return ExtSoftPathState.Ready;
						}
					case ExtPathMode.CalculatingWalkingPathToTarget:
						// final walking path to target has been calculated
						extInstance.pathMode = ExtPathMode.WalkingToTarget;
#if DEBUG
						if (debug)
							Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} is now travelling by foot to their final target. CurrentDepartureMode={extInstance.pathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
						return ExtSoftPathState.Ready;
				}
			} else {
				// citizen has a vehicle assigned

#if DEBUG
				if (debug)
					Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen has a vehicle assigned but this method does not handle this situation. Forcing path-find to fail.");
#endif
				extInstance.Reset();
				return ExtSoftPathState.FailedHard;
			}
		}

		/// <summary>
		/// Handles a path-finding failure for citizen instances and activated Parking AI.
		/// </summary>
		/// <param name="instanceId">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="extInstance">extended citizen instance information</param>
		/// <param name="extCitizen">extended citizen information</param>
		/// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
		protected ExtSoftPathState OnCitizenPathFindFailure(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == instanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == instanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Path-finding failed for citizen instance {extInstance.instanceId}. CurrentPathMode={extInstance.pathMode}");
#endif

			// update public transport demands
			switch (extInstance.pathMode) {
				case ExtPathMode.None:
				case ExtPathMode.CalculatingWalkingPathToTarget:
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
				case ExtPathMode.TaxiToTarget:
					// could not reach target building by walking/driving/public transport: increase public transport demand
					if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
#if DEBUG
						if (fineDebug)
							Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Increasing public transport demand of target building {instanceData.m_targetBuilding} and source building {instanceData.m_sourceBuilding}");
#endif

						if (instanceData.m_targetBuilding != 0) {
							ExtBuildingManager.Instance.ExtBuildings[instanceData.m_targetBuilding].AddPublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandIncrement, false);
						}
						if (instanceData.m_sourceBuilding != 0) {
							ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].AddPublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandIncrement, true);
						}
					}
					break;
			}

			/*
			 * relocate parked car if abandoned
			 */
			if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
				/*
				 * parked car is unreachable
				 */
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
					/*
					 * parked car is present
					 */

					ushort homeId = 0;
					Services.CitizenService.ProcessCitizen(extCitizen.citizenId, delegate (uint citId, ref Citizen cit) {
						homeId = cit.m_homeBuilding;
						return true;
					});

					// calculate distance between citizen and parked car
					bool movedCar = false;
					Vector3 citizenPos = instanceData.GetLastFramePosition();
					float parkedToCitizen = 0f;
					Vector3 oldParkedVehiclePos = default(Vector3);
					Services.VehicleService.ProcessParkedVehicle(parkedVehicleId, delegate (ushort parkedVehId, ref VehicleParked parkedVehicle) {
						oldParkedVehiclePos = parkedVehicle.m_position;
						parkedToCitizen = (parkedVehicle.m_position - citizenPos).magnitude;
						if (parkedToCitizen > GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome) {
							/*
							 * parked car is far away from current location
							 * -> relocate parked car and try again
							 */
							movedCar = TryMoveParkedVehicle(parkedVehicleId, ref parkedVehicle, citizenPos, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome, homeId);
						}
						return true;
					});

					if (movedCar) {
						/*
						 * successfully moved the parked car to a closer location
						 * -> retry path-finding
						 */

						extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
#if DEBUG
						if (fineDebug)
							Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Relocated parked car {parkedVehicleId} to a closer location (old pos/distance: {oldParkedVehiclePos}/{parkedToCitizen}, new pos/distance: {Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position}/{(Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - citizenPos).magnitude}) for citizen @ {citizenPos}. Retrying path-finding. CurrentPathMode={extInstance.pathMode}");
#endif

						return ExtSoftPathState.FailedSoft;
					} else {
						/*
						 * could not move car
						 * -> despawn parked car, walk to target or use public transport
						 */
#if DEBUG
						if (fineDebug)
							Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Releasing unreachable parked vehicle {parkedVehicleId} for citizen instance {extInstance.instanceId}. CurrentPathMode={extInstance.pathMode}");
#endif
						Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
					}
				}
			}

			// check if path-finding may be repeated
			ExtSoftPathState ret = ExtSoftPathState.FailedHard;
			switch (extInstance.pathMode) {
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
					// try to walk to target
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Path failed but it may be retried to walk to the target.");
#endif
					extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
					ret = ExtSoftPathState.FailedSoft;
					break;
				default:
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Path failed and walking to target is not an option. Resetting ext. instance.");
#endif
					extInstance.Reset();
					break;
			}

#if DEBUG
			if (debug)
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Setting CurrentPathMode for citizen instance {extInstance.instanceId} to {extInstance.pathMode}, ret={ret}");
#endif

			// reset current transport mode for hard failures
			if (ret == ExtSoftPathState.FailedHard) {
				extCitizen.transportMode = ExtCitizen.ExtTransportMode.None;
			}

			return ret;
		}

		/// <summary>
		/// Handles a path-finding failure for citizen instances and activated Parking AI.
		/// </summary>
		/// <param name="vehicleId">Vehicle id</param>
		/// <param name="vehicleData">Vehicle data</param>
		/// <param name="driverInstanceData">Driver citizen instance data</param>
		/// <param name="driverExtInstance">extended citizen instance information of driver</param>
		/// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
		protected ExtSoftPathState OnCarPathFindFailure(ushort vehicleId, ref Vehicle vehicleData, ref CitizenInstance driverInstanceData, ref ExtCitizenInstance driverExtInstance) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == driverExtInstance.instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverInstanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == driverInstanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == driverInstanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path-finding failed for driver citizen instance {driverExtInstance.instanceId}. CurrentPathMode={driverExtInstance.pathMode}");
#endif

			// update parking demands
			switch (driverExtInstance.pathMode) {
				case ExtPathMode.None:
				case ExtPathMode.CalculatingCarPathToAltParkPos:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
					// could not reach target building by driving: increase parking space demand
#if DEBUG
					if (fineDebug)
						Log._Debug($"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Increasing parking space demand of target building {driverInstanceData.m_targetBuilding}");
#endif
					if (driverInstanceData.m_targetBuilding != 0) {
						ExtBuildingManager.Instance.ExtBuildings[driverInstanceData.m_targetBuilding].AddParkingSpaceDemand((uint)GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement);
					}
					break;
			}

			// check if path-finding may be repeated
			ExtSoftPathState ret = ExtSoftPathState.FailedHard;
			switch (driverExtInstance.pathMode) {
				case ExtPathMode.CalculatingCarPathToAltParkPos:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
					// try to drive directly to the target if public transport is allowed
					if ((driverInstanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
#if DEBUG
						if (debug)
							Log._Debug($"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path failed but it may be retried to drive directly to the target / using public transport.");
#endif
						driverExtInstance.pathMode = ExtPathMode.RequiresMixedCarPathToTarget;
						ret = ExtSoftPathState.FailedSoft;
					}
					break;
				default:
#if DEBUG
					if (debug)
						Log._Debug($"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path failed and a direct target is not an option. Resetting driver ext. instance.");
#endif
					driverExtInstance.Reset();
					break;
			}

#if DEBUG
			if (debug)
				Log._Debug($"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Setting CurrentPathMode for driver citizen instance {driverExtInstance.instanceId} to {driverExtInstance.pathMode}, ret={ret}");
#endif

			return ret;
		}

		public bool TryMoveParkedVehicle(ushort parkedVehicleId, ref VehicleParked parkedVehicle, Vector3 refPos, float maxDistance, ushort homeId) {
			ExtParkingSpaceLocation parkingSpaceLocation;
			ushort parkingSpaceLocationId;
			Vector3 parkPos;
			Quaternion parkRot;
			float parkOffset;

			bool found = false;
#if BENCHMARK
			using (var bm = new Benchmark(null, "FindParkingSpaceInVicinity")) {
#endif
			found = AdvancedParkingManager.Instance.FindParkingSpaceInVicinity(refPos, parkedVehicle.Info, homeId, 0, maxDistance, out parkingSpaceLocation, out parkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);
#if BENCHMARK
			}
#endif
			if (found) {
				Singleton<VehicleManager>.instance.RemoveFromGrid(parkedVehicleId, ref parkedVehicle);
				parkedVehicle.m_position = parkPos;
				parkedVehicle.m_rotation = parkRot;
				Singleton<VehicleManager>.instance.AddToGrid(parkedVehicleId, ref parkedVehicle);
			}

			return found;
		}

		public bool FindParkingSpaceForCitizen(Vector3 endPos, VehicleInfo vehicleInfo, ref ExtCitizenInstance extDriverInstance, ushort homeId, bool goingHome, ushort vehicleId, bool allowTourists, out Vector3 parkPos, ref PathUnit.Position endPathPos, out bool calculateEndPos) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == extDriverInstance.instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == extDriverInstance.GetCitizenId()) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == Singleton<CitizenManager>.instance.m_instances.m_buffer[extDriverInstance.instanceId].m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == Singleton<CitizenManager>.instance.m_instances.m_buffer[extDriverInstance.instanceId].m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			calculateEndPos = true;
			parkPos = default(Vector3);

			if (!allowTourists) {
				// TODO remove this from this method
				uint citizenId = extDriverInstance.GetCitizenId();
				if (citizenId == 0 ||
					(Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Tourist) != Citizen.Flags.None)
					return false;
			}

#if DEBUG
			if (fineDebug)
				Log._Debug($"Citizen instance {extDriverInstance.instanceId} (CurrentPathMode={extDriverInstance.pathMode}) can still use their passenger car and is either not a tourist or wants to find an alternative parking spot. Finding a parking space before starting path-finding.");
#endif

			ExtParkingSpaceLocation knownParkingSpaceLocation;
			ushort knownParkingSpaceLocationId;
			Quaternion parkRot;
			float parkOffset;

			// find a free parking space
			bool success = FindParkingSpaceInVicinity(endPos, vehicleInfo, homeId, vehicleId, goingHome ? GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome : GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, out knownParkingSpaceLocation, out knownParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

			extDriverInstance.parkingSpaceLocation = knownParkingSpaceLocation;
			extDriverInstance.parkingSpaceLocationId = knownParkingSpaceLocationId;

			if (success) {
#if DEBUG
				if (fineDebug)
					Log._Debug($"Found a parking spot for citizen instance {extDriverInstance.instanceId} (CurrentPathMode={extDriverInstance.pathMode}) before starting car path: {knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
#endif

				if (knownParkingSpaceLocation == ExtParkingSpaceLocation.RoadSide) {
					// found segment with parking space
					Vector3 pedPos;
					uint laneId;
					int laneIndex;
					float laneOffset;

#if DEBUG
					if (debug)
						Log._Debug($"Found segment {knownParkingSpaceLocationId} for road-side parking position for citizen instance {extDriverInstance.instanceId}!");
#endif

					// determine nearest sidewalk position for parking position at segment
					if (Singleton<NetManager>.instance.m_segments.m_buffer[knownParkingSpaceLocationId].GetClosestLanePosition(parkPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out pedPos, out laneId, out laneIndex, out laneOffset)) {
						endPathPos.m_segment = knownParkingSpaceLocationId;
						endPathPos.m_lane = (byte)laneIndex;
						endPathPos.m_offset = (byte)(parkOffset * 255f);
						calculateEndPos = false;

						//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
						if (debug)
							Log._Debug($"Found an parking spot sidewalk position for citizen instance {extDriverInstance.instanceId} @ segment {knownParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! CurrentPathMode={extDriverInstance.pathMode}");
#endif
						return true;
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"Could not find an alternative parking spot sidewalk position for citizen instance {extDriverInstance.instanceId}! CurrentPathMode={extDriverInstance.pathMode}");
#endif
						return false;
					}
				} else if (knownParkingSpaceLocation == ExtParkingSpaceLocation.Building) {
					// found a building with parking space
					if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, endPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, NetInfo.LaneType.None, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, out endPathPos)) {
						calculateEndPos = false;
					}

					//endPos = parkPos;

					//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
					if (debug)
						Log._Debug($"Navigating citizen instance {extDriverInstance.instanceId} to parking building {knownParkingSpaceLocationId}! segment={endPathPos.m_segment}, laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. CurrentPathMode={extDriverInstance.pathMode} calculateEndPos={calculateEndPos}");
#endif
					return true;
				}
			}
			return false;
		}

		public bool TrySpawnParkedPassengerCar(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos, out ParkingUnableReason reason) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenId;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (fineDebug && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car for citizen {citizenId}, home {homeId} @ {refPos}");
#endif

			Vector3 roadParkPos;
			ParkingUnableReason roadParkReason;
			bool roadParkSuccess = TrySpawnParkedPassengerCarRoadSide(citizenId, refPos, vehicleInfo, out roadParkPos, out roadParkReason);

			Vector3 buildingParkPos;
			ParkingUnableReason buildingParkReason;
			bool buildingParkSuccess = TrySpawnParkedPassengerCarBuilding(citizenId, homeId, refPos, vehicleInfo, out buildingParkPos, out buildingParkReason);

			if ((!roadParkSuccess && !buildingParkSuccess) || (roadParkSuccess && !buildingParkSuccess)) {
				parkPos = roadParkPos;
				reason = roadParkReason;
				return roadParkSuccess;
			} else if (buildingParkSuccess && !roadParkSuccess) {
				parkPos = buildingParkPos;
				reason = buildingParkReason;
				return buildingParkSuccess;
			} else if ((roadParkPos - refPos).sqrMagnitude < (buildingParkPos - refPos).sqrMagnitude) {
				parkPos = roadParkPos;
				reason = roadParkReason;
				return roadParkSuccess;
			} else {
				parkPos = buildingParkPos;
				reason = buildingParkReason;
				return buildingParkSuccess;
			}
		}

		public bool TrySpawnParkedPassengerCarRoadSide(uint citizenId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos, out ParkingUnableReason reason) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenId;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset = 0f;

			if (FindParkingSpaceRoadSide(0, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (debug)
						Log._Debug($"[SUCCESS] Spawned parked passenger car at road side for citizen {citizenId}: {parkedVehicleId} @ {parkPos}");
#endif
					reason = ParkingUnableReason.None;
					return true;
				} else {
					reason = ParkingUnableReason.LimitHit;
				}
			} else {
				reason = ParkingUnableReason.NoSpaceFound;
			}
#if DEBUG
			if (debug)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
#endif
			return false;
		}

		public bool TrySpawnParkedPassengerCarBuilding(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos, out ParkingUnableReason reason) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenId;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (fineDebug && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car next to building for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset;

			if (FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, refPos, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (fineDebug && homeId != 0)
						Log._Debug($"[SUCCESS] Spawned parked passenger car next to building for citizen {citizenId}: {parkedVehicleId} @ {parkPos}");
#endif
					reason = ParkingUnableReason.None;
					return true;
				} else {
					reason = ParkingUnableReason.LimitHit;
				}
			} else {
				reason = ParkingUnableReason.NoSpaceFound;
			}
#if DEBUG
			if (debug && homeId != 0)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car next to building for citizen {citizenId}");
#endif
			return false;
		}

		public bool FindParkingSpaceInVicinity(Vector3 targetPos, VehicleInfo vehicleInfo, ushort homeId, ushort vehicleId, float maxDist, out ExtParkingSpaceLocation parkingSpaceLocation, out ushort parkingSpaceLocationId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
#if DEBUG
			bool vehDebug = GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleId;
			bool debug = GlobalConfig.Instance.Debug.Switches[22] && vehDebug;
#endif

			// TODO check isElectric
			Vector3 roadParkPos;
			Quaternion roadParkRot;
			float roadParkOffset;
			Vector3 buildingParkPos;
			Quaternion buildingParkRot;
			float buildingParkOffset;

			ushort parkingSpaceSegmentId = FindParkingSpaceAtRoadSide(0, targetPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, maxDist, true, out roadParkPos, out roadParkRot, out roadParkOffset);
			ushort parkingBuildingId = FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, targetPos, maxDist, maxDist, true, out buildingParkPos, out buildingParkRot, out buildingParkOffset);

			if (parkingSpaceSegmentId != 0) {
				if (parkingBuildingId != 0) {
					Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

					// choose nearest parking position, after a bit of randomization
					if ((roadParkPos - targetPos).magnitude < (buildingParkPos - targetPos).magnitude
						&& rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) != 0) {
						// road parking space is closer
#if DEBUG
						if (debug)
							Log._Debug($"Found an (alternative) road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId} after comparing distance with a bulding parking position @ {parkingBuildingId}!");
#endif
						parkPos = roadParkPos;
						parkRot = roadParkRot;
						parkOffset = roadParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
						parkingSpaceLocationId = parkingSpaceSegmentId;
						return true;
					} else {
						// building parking space is closer
#if DEBUG
						if (debug)
							Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId} after comparing distance with a road-side parking position @ {parkingSpaceSegmentId}!");
#endif
						parkPos = buildingParkPos;
						parkRot = buildingParkRot;
						parkOffset = buildingParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.Building;
						parkingSpaceLocationId = parkingBuildingId;
						return true;
					}
				} else {
					// road-side but no building parking space found
#if DEBUG
					if (debug)
						Log._Debug($"Found an alternative road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId}!");
#endif
					parkPos = roadParkPos;
					parkRot = roadParkRot;
					parkOffset = roadParkOffset;
					parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
					parkingSpaceLocationId = parkingSpaceSegmentId;
					return true;
				}
			} else if (parkingBuildingId != 0) {
				// building but no road-side parking space found
#if DEBUG
				if (debug)
					Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId}!");
#endif
				parkPos = buildingParkPos;
				parkRot = buildingParkRot;
				parkOffset = buildingParkOffset;
				parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.Building;
				parkingSpaceLocationId = parkingBuildingId;
				return true;
			} else {
				//driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFailed;
				parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.None;
				parkingSpaceLocationId = 0;
				parkPos = default(Vector3);
				parkRot = default(Quaternion);
				parkOffset = -1f;
#if DEBUG
				if (debug)
					Log._Debug($"Could not find a road-side or building parking position for vehicle {vehicleId}!");
#endif
				return false;
			}
		}

		protected ushort FindParkingSpaceAtRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[22];
#endif

			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;

			int centerI = (int)(refPos.z / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int centerJ = (int)(refPos.x / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int radius = Math.Max(1, (int)(maxDistance / ((float)BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

			NetManager netManager = Singleton<NetManager>.instance;
			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

			ushort foundSegmentId = 0;
			Vector3 myParkPos = parkPos;
			Quaternion myParkRot = parkRot;
			float myParkOffset = parkOffset;

			LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, delegate (int i, int j) {
				if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 || j >= BuildingManager.BUILDINGGRID_RESOLUTION)
					return true;

				ushort segmentId = netManager.m_segmentGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int iterations = 0;
				while (segmentId != 0) {
					uint laneId;
					int laneIndex;
					float laneOffset;
					Vector3 innerParkPos;
					Quaternion innerParkRot;
					float innerParkOffset;

					NetInfo segmentInfo = netManager.m_segments.m_buffer[segmentId].Info;
					Vector3 segCenter = netManager.m_segments.m_buffer[segmentId].m_bounds.center;

					// randomize target position to allow for opposite road-side parking
					segCenter.x += Singleton<SimulationManager>.instance.m_randomizer.Int32(GlobalConfig.Instance.ParkingAI.ParkingSpacePositionRand) - GlobalConfig.Instance.ParkingAI.ParkingSpacePositionRand / 2u;
					segCenter.z += Singleton<SimulationManager>.instance.m_randomizer.Int32(GlobalConfig.Instance.ParkingAI.ParkingSpacePositionRand) - GlobalConfig.Instance.ParkingAI.ParkingSpacePositionRand / 2u;

					if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(segCenter, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
						NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
						if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, laneInfo.m_finalDirection)) {
							if (!Options.vehicleRestrictionsEnabled || (VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(segmentId, segmentInfo, (uint)laneIndex, laneInfo, VehicleRestrictionsMode.Configured) & ExtVehicleType.PassengerCar) != ExtVehicleType.None) {

								if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, innerParkPos, width, length, out innerParkPos, out innerParkRot, out innerParkOffset)) {
	#if DEBUG
									if (debug)
										Log._Debug($"FindParkingSpaceRoadSide: Found a parking space for refPos {refPos}, segment center {segCenter} @ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");
	#endif
									foundSegmentId = segmentId;
									myParkPos = innerParkPos;
									myParkRot = innerParkRot;
									myParkOffset = innerParkOffset;
									if (!randomize || rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) != 0)
										return false;
								}
							}
						}
					} else {
						/*if (debug)
							Log._Debug($"FindParkingSpaceRoadSide: Could not find closest lane position for parking @ {segmentId}!");*/
					}

					segmentId = netManager.m_segments.m_buffer[segmentId].m_nextGridSegment;
					if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				return true;
			});

			if (foundSegmentId == 0) {
#if DEBUG
				if (debug)
					Log._Debug($"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
#endif
				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundSegmentId;
		}

		protected ushort FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[22];
#endif

			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = -1f;

			int centerI = (int)(refPos.z / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int centerJ = (int)(refPos.x / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int radius = Math.Max(1, (int)(maxBuildingDistance / ((float)BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

			BuildingManager buildingMan = Singleton<BuildingManager>.instance;
			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

			ushort foundBuildingId = 0;
			Vector3 myParkPos = parkPos;
			Quaternion myParkRot = parkRot;
			float myParkOffset = parkOffset;

			LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, delegate (int i, int j) {
				if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 || j >= BuildingManager.BUILDINGGRID_RESOLUTION)
					return true;

#if DEBUG
				if (debug) {
					//Log._Debug($"FindParkingSpaceBuilding: Checking building grid @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j} for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}");
				}
#endif

				ushort buildingId = buildingMan.m_buildingGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int numIterations = 0;
				while (buildingId != 0) {
					Vector3 innerParkPos; Quaternion innerParkRot; float innerParkOffset;
#if DEBUG
					if (debug) {
						//Log._Debug($"FindParkingSpaceBuilding: Checking building {buildingId} @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j}, for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}.");
					}
#endif

					if (FindParkingSpacePropAtBuilding(vehicleInfo, homeID, ignoreParked, buildingId, ref buildingMan.m_buildings.m_buffer[(int)buildingId], segmentId, refPos, ref maxParkingSpaceDistance, randomize, out innerParkPos, out innerParkRot, out innerParkOffset)) {
#if DEBUG
						/*/if (fineDebug && homeID != 0)
							Log._Debug($"FindParkingSpaceBuilding: Found a parking space for {refPos}, homeID {homeID} @ building {buildingId}, {myParkPos}, offset {myParkOffset}!");
						*/
#endif
						foundBuildingId = buildingId;
						myParkPos = innerParkPos;
						myParkRot = innerParkRot;
						myParkOffset = innerParkOffset;

						if (!randomize || rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) != 0)
							return false;
					}
					buildingId = buildingMan.m_buildings.m_buffer[(int)buildingId].m_nextGridBuilding;
					if (++numIterations >= 49152) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				return true;
			});

			if (foundBuildingId == 0) {
#if DEBUG
				if (debug && homeID != 0)
					Log._Debug($"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");
#endif

				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundBuildingId;
		}

		public bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort buildingID, ref Building building, ushort segmentId, Vector3 refPos, ref float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[22];
#endif

			int buildingWidth = building.Width;
			int buildingLength = building.Length;

			// NON-STOCK CODE START
			parkOffset = -1f; // only set if segmentId != 0
			parkPos = default(Vector3);
			parkRot = default(Quaternion);

			if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
#if DEBUG
				if (debug)
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not created.");
#endif
				return false;
			}

			if ((building.m_problems & Notification.Problem.TurnedOff) != Notification.Problem.None) {
#if DEBUG
				if (debug)
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not active.");
#endif
				return false;
			}

			if ((building.m_flags & Building.Flags.Collapsed) != Building.Flags.None) {
#if DEBUG
				if (debug)
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is collapsed.");
#endif
				return false;
			}

			/*else {
			// NON-STOCK CODE END
				float diagWidth = Mathf.Sqrt((float)(buildingWidth * buildingWidth + buildingLength * buildingLength)) * 8f;
				if (VectorUtils.LengthXZ(building.m_position - refPos) >= maxDistance + diagWidth) {*/
#if DEBUG
			/*if (fineDebug)
				Log._Debug($"Refusing to find parking space at building {buildingID}! {VectorUtils.LengthXZ(building.m_position - refPos)} >= {maxDistance + diagWidth} (maxDistance={maxDistance})");*/
#endif
			/*return false;
		}
	}*/ // NON-STOCK CODE

			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

			bool isElectric = vehicleInfo.m_class.m_subService != ItemClass.SubService.ResidentialLow;
			BuildingInfo buildingInfo = building.Info;
			Matrix4x4 transformMatrix = default(Matrix4x4);
			bool transformMatrixCalculated = false;
			bool result = false;
			if (buildingInfo.m_class.m_service == ItemClass.Service.Residential && buildingID != homeID && rng.Int32((uint)Options.getRecklessDriverModulo()) != 0) { // NON-STOCK CODE
#if DEBUG
				/*if (fineDebug)
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is a residential building which does not match home id {homeID}.");*/
#endif
				return false;
			}

			float propMinDistance = 9999f; // NON-STOCK CODE
			if (buildingInfo.m_props != null && (buildingInfo.m_hasParkingSpaces & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
				for (int i = 0; i < buildingInfo.m_props.Length; i++) {
					BuildingInfo.Prop prop = buildingInfo.m_props[i];
					Randomizer randomizer = new Randomizer((int)buildingID << 6 | prop.m_index);
					if (randomizer.Int32(100u) < prop.m_probability && buildingLength >= prop.m_requiredLength) {
						PropInfo propInfo = prop.m_finalProp;
						if (propInfo != null) {
							propInfo = propInfo.GetVariation(ref randomizer);
							if (propInfo.m_parkingSpaces != null && propInfo.m_parkingSpaces.Length != 0) {
								if (!transformMatrixCalculated) {
									transformMatrixCalculated = true;
									Vector3 pos = Building.CalculateMeshPosition(buildingInfo, building.m_position, building.m_angle, building.Length);
									Quaternion q = Quaternion.AngleAxis(building.m_angle * 57.29578f, Vector3.down);
									transformMatrix.SetTRS(pos, q, Vector3.one);
								}
								Vector3 position = transformMatrix.MultiplyPoint(prop.m_position);
								if (CustomPassengerCarAI.FindParkingSpaceProp(isElectric, ignoreParked, propInfo, position, building.m_angle + prop.m_radAngle, prop.m_fixedHeight, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, ref propMinDistance, ref parkPos, ref parkRot)) { // NON-STOCK CODE
									result = true;
									if (randomize && propMinDistance <= maxDistance && rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) == 0)
										break;
								}
							}
						}
					}
				}
			}

			if (result && propMinDistance <= maxDistance) {
				maxDistance = propMinDistance; // NON-STOCK CODE
#if DEBUG
				if (debug)
					Log._Debug($"Found parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				if (segmentId != 0) {
					// check if building is accessible from the given segment
#if DEBUG
					if (debug)
						Log._Debug($"Calculating unspawn position of building {buildingID} for segment {segmentId}.");
#endif

					Vector3 unspawnPos;
					Vector3 unspawnTargetPos;
					building.Info.m_buildingAI.CalculateUnspawnPosition(buildingID, ref building, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, out unspawnPos, out unspawnTargetPos);

					Vector3 lanePos;
					uint laneId;
					int laneIndex;
					float laneOffset;
					// calculate segment offset
					if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(unspawnPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out lanePos, out laneId, out laneIndex, out laneOffset)) {
#if DEBUG
						if (debug)
							Log._Debug($"Succeeded in finding unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}! lanePos={lanePos}, dist={(lanePos - unspawnPos).magnitude}, laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");
#endif

						/*if (dist > 16f) {
							if (debug)
								Log._Debug($"Distance between unspawn position and lane position is too big! {dist} unspawnPos={unspawnPos} lanePos={lanePos}");
							return false;
						}*/

						parkOffset = laneOffset;
					} else {
#if DEBUG
						if (debug)
							Log.Warning($"Could not find unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}!");
#endif
					}
				}

				return true;
			} else {
#if DEBUG
				if (result && debug)
					Log._Debug($"Could not find parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				return false;
			}
		}

		public bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo, ushort ignoreParked, ushort segmentId, Vector3 refPos, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset, out uint laneId, out int laneIndex) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[22];
#endif

			float width = vehicleInfo.m_generatedInfo.m_size.x;
			float length = vehicleInfo.m_generatedInfo.m_size.z;

			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out parkPos, out laneId, out laneIndex, out parkOffset)) {
					if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
						if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, parkPos, width, length, out parkPos, out parkRot, out parkOffset)) {
#if DEBUG
							if (debug)
								Log._Debug($"FindParkingSpaceRoadSideForVehiclePos: Found a parking space for refPos {refPos} @ {parkPos}, laneId {laneId}, laneIndex {laneIndex}!");
#endif
							return true;
						}
					}
				}
			}

			//

			parkPos = default(Vector3);
			parkRot = default(Quaternion);
			laneId = 0;
			laneIndex = -1;
			parkOffset = -1f;
			return false;
		}

		public bool FindParkingSpaceRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return FindParkingSpaceAtRoadSide(ignoreParked, refPos, width, length, maxDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return FindParkingSpaceBuilding(vehicleInfo, homeID, ignoreParked, segmentId, refPos, maxBuildingDistance, maxParkingSpaceDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public bool GetBuildingInfoViewColor(ushort buildingId, ref Building buildingData, ref ExtBuilding extBuilding, InfoManager.InfoMode infoMode, out Color? color) {
			color = null;

			if (infoMode == InfoManager.InfoMode.Traffic) {
				// parking space demand info view
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)extBuilding.parkingSpaceDemand * 0.01f));
				return true;
			} else if (infoMode == InfoManager.InfoMode.Transport && !(buildingData.Info.m_buildingAI is DepotAI)) {
				// public transport demand info view
				// TODO should not depend on UI class "TrafficManagerTool"
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor, Mathf.Clamp01((float)(TrafficManagerTool.CurrentTransportDemandViewMode == TransportDemandViewMode.Outgoing ? extBuilding.outgoingPublicTransportDemand : extBuilding.incomingPublicTransportDemand) * 0.01f));
				return true;
			}

			return false;
		}

		public string EnrichLocalizedCitizenStatus(string ret, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen) {
			if (extInstance.IsValid()) {
				switch (extInstance.pathMode) {
					case ExtPathMode.ApproachingParkedCar:
					case ExtPathMode.RequiresCarPath:
					case ExtPathMode.RequiresMixedCarPathToTarget:
						ret = Translation.GetString("Entering_vehicle") + ", " + ret;
						break;
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
						ret = Translation.GetString("Walking_to_car") + ", " + ret;
						break;
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.TaxiToTarget:
					case ExtPathMode.WalkingToTarget:
						if ((extCitizen.transportMode & ExtCitizen.ExtTransportMode.PublicTransport) != ExtCitizen.ExtTransportMode.None) {
							ret = Translation.GetString("Using_public_transport") + ", " + ret;
						} else {
							ret = Translation.GetString("Walking") + ", " + ret;
						}
						break;
					case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						ret = Translation.GetString("Thinking_of_a_good_parking_spot") + ", " + ret;
						break;
				}
			}
			return ret;
		}

		public string EnrichLocalizedCarStatus(string ret, ref ExtCitizenInstance driverExtInstance) {
			if (driverExtInstance.IsValid()) {
				switch (driverExtInstance.pathMode) {
					case ExtPathMode.DrivingToAltParkPos:
						if (driverExtInstance.failedParkingAttempts <= 1) {
							ret = Translation.GetString("Driving_to_a_parking_spot") + ", " + ret;
						} else {
							ret = Translation.GetString("Driving_to_another_parking_spot") + " (#" + driverExtInstance.failedParkingAttempts + "), " + ret;
						}
						break;
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.DrivingToKnownParkPos:
						ret = Translation.GetString("Driving_to_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.ParkingFailed:
					case ExtPathMode.CalculatingCarPathToAltParkPos:
						ret = Translation.GetString("Looking_for_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.RequiresWalkingPathToTarget:
						ret = Locale.Get("VEHICLE_STATUS_PARKING") + ", " + ret;
						break;
				}
			}
			return ret;
		}
	}
}

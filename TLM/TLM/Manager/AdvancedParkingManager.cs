using ColossalFramework;
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
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.ExtCitizenInstance;

namespace TrafficManager.Manager {
	public class AdvancedParkingManager : AbstractCustomManager {
		public enum CarUsagePolicy {
			/// <summary>
			/// Citizens may use their own car
			/// </summary>
			Allowed,
			/// <summary>
			/// Citizens are forced to use their car
			/// </summary>
			Forced,
			/// <summary>
			/// Citizens are forbidden to use their car
			/// </summary>
			Forbidden
		}

		public enum ParkedCarApproachState {
			/// <summary>
			/// Citizen is not approaching their parked car
			/// </summary>
			None,
			/// <summary>
			/// Citizen is currently approaching their parked car
			/// </summary>
			Approaching,
			/// <summary>
			/// Citizen has approaching their parked car
			/// </summary>
			Approached,
			/// <summary>
			/// Citizen failed to approach their parked car
			/// </summary>
			Failure
		}

		public static AdvancedParkingManager Instance { get; private set; } = null;

		static AdvancedParkingManager() {
			Instance = new AdvancedParkingManager();
		}

		/// <summary>
		/// Determines the color the given building should be colorized with given the current info view mode.
		/// </summary>
		/// <param name="buildingId">building id</param>
		/// <param name="buildingData">building data</param>
		/// <param name="infoMode">current info view mode</param>
		/// <param name="color">output color</param>
		/// <returns>true if a custom color should be displayed, false otherwise</returns>
		public bool GetBuildingInfoViewColor(ushort buildingId, ref Building buildingData, InfoManager.InfoMode infoMode, out Color? color) {
			color = null;

			if (infoMode == InfoManager.InfoMode.Traffic) {
				// parking space demand info view
				ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingId);
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)extBuilding.ParkingSpaceDemand * 0.01f));
				return true;
			} else if (infoMode == InfoManager.InfoMode.Transport && !(buildingData.Info.m_buildingAI is DepotAI)) {
				// public transport demand info view
				// TODO should not depend on UI class "TrafficManagerTool"
				ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingId);
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor, Mathf.Clamp01((float)(TrafficManagerTool.CurrentTransportDemandViewMode == TransportDemandViewMode.Outgoing ? extBuilding.OutgoingPublicTransportDemand : extBuilding.IncomingPublicTransportDemand) * 0.01f));
				return true;
			}

			return false;
		}

		public ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId, ref CitizenInstance citizenInstance, ref Citizen citizen, ExtPathState mainPathState) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}) called.");
#endif
			if (mainPathState == ExtPathState.Calculating) {
				// main path is still calculating, do not check return path
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): still calculating main path. returning CALCULATING.");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(citizenInstanceId);

			if (extInstance == null) {
				// no citizen
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): no citizen found!");
#endif
				return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
			}

			if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
				// main path failed or non-existing: reset return path
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): mainPathSate is None or Failed. Resetting instance and returning FAILED.");
#endif

				if (mainPathState == ExtPathState.Failed) {
					return OnCitizenPathFindFailure(citizenInstanceId, ref citizenInstance, extInstance);
				}

				extInstance.Reset();
				return ExtSoftPathState.FailedHard;
			}

			// main path state is READY

			// main path calculation succeeded: update return path state and check its state if necessary
			extInstance.UpdateReturnPathState();

			bool success = true;
			switch (extInstance.ReturnPathState) {
				case ExtPathState.None:
				default:
					// no return path calculated: ignore
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): return path state is None. Ignoring and returning main path state.");
#endif
					break;
				case ExtPathState.Calculating: // OK
											   // return path not read yet: wait for it
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): return path state is still calculating.");
#endif
					return ExtSoftPathState.Calculating;
				case ExtPathState.Failed: // OK
										  // no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Return path FAILED.");
#endif
					success = false;
					break;
				case ExtPathState.Ready:
					// handle valid return path
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): Path is READY.");
#endif
					break;
			}

			extInstance.ReleaseReturnPath();

			ExtSoftPathState ret;
			if (success) {
				// handle path find success
				return OnCitizenPathFindSuccess(citizenInstanceId, ref citizenInstance, ref citizen);
			} else {
				// handle path find failure
				return OnCitizenPathFindFailure(citizenInstanceId, ref citizenInstance, extInstance);
			}
		}

		/// <summary>
		/// Updates the vehicle's main path state by checking against the return path state
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="mainPathState">current state of the vehicle's main path</param>
		/// <returns></returns>
		public ExtPathState UpdateCarPathState(ushort vehicleId, ref Vehicle vehicleData, ExtPathState mainPathState) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}) called.");
#endif
			if (mainPathState == ExtPathState.Calculating) {
				// main path is still calculating, do not check return path
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): still calculating main path. returning CALCULATING.");
#endif
				return mainPathState;
			}

			ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(CustomPassengerCarAI.GetDriverInstance(vehicleId, ref vehicleData));

			if (driverExtInstance == null) {
				// no driver
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): no driver found!");
#endif
				return mainPathState;
			}

			if (VehicleStateManager.Instance.VehicleStates[vehicleId].vehicleType != ExtVehicleType.PassengerCar) {
				// non-passenger cars are not handled
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): not a passenger car!");
#endif
				driverExtInstance.Reset();
				return mainPathState;
			}

			if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
				// main path failed or non-existing: reset return path
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): mainPathSate is None or Failed. Resetting instance and returning FAILED.");
#endif
				driverExtInstance.Reset();
				return ExtPathState.Failed;
			}

			// main path state is READY

			// main path calculation succeeded: update return path state and check its state
			driverExtInstance.UpdateReturnPathState();

			switch (driverExtInstance.ReturnPathState) {
				case ExtPathState.None:
				default:
					// no return path calculated: ignore
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): return path state is None. Ignoring and returning main path state.");
#endif
					return mainPathState;
				case ExtPathState.Calculating:
					// return path not read yet: wait for it
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): return path state is still calculating.");
#endif
					return ExtPathState.Calculating;
				case ExtPathState.Failed:
					// no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Return path {driverExtInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
#endif
					driverExtInstance.Reset();
					return ExtPathState.Failed;
				case ExtPathState.Ready:
					// handle valid return path
					driverExtInstance.ReleaseReturnPath();
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"CAdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Path is ready for vehicle {vehicleId}, citizen instance {driverExtInstance.InstanceId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[vehicleData.m_path].m_laneTypes;
					bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;

					if (usesPublicTransport && (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos || driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToAltParkPos)) {
						driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
						driverExtInstance.ParkingSpaceLocation = ExtParkingSpaceLocation.None;
						driverExtInstance.ParkingSpaceLocationId = 0;
					}

					if (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
						driverExtInstance.PathMode = ExtPathMode.DrivingToAltParkPos;
						driverExtInstance.ParkingPathStartPosition = null;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Path to an alternative parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					} else if (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToTarget) {
						driverExtInstance.PathMode = ExtPathMode.DrivingToTarget;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Car path is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					} else if (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) {
						driverExtInstance.PathMode = ExtPathMode.DrivingToKnownParkPos;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): Car path to known parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					}
					return ExtPathState.Ready;
			}
		}

		public ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(ushort instanceId, ref CitizenInstance instanceData, Vector3 physicsLodRefPos, ref VehicleParked parkedCar) {
			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CheckCitizenReachedParkedCar({instanceId}): citizen instance {instanceId} is waiting for path-finding to complete.");
#endif
				return ParkedCarApproachState.None;
			}

			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (extInstance.PathMode != ExtPathMode.ApproachingParkedCar && extInstance.PathMode != ExtPathMode.WalkingToParkedCar) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} is not reaching a parked car ({extInstance.PathMode})");
#endif
				return ParkedCarApproachState.None;
			}

			if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
#if DEBUG
				/*if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} is not spawned!");*/
#endif
				return ParkedCarApproachState.None;
			}

			if (extInstance.PathMode == ExtPathMode.WalkingToParkedCar) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					extInstance.PathMode = ExtPathMode.ApproachingParkedCar;
					instanceData.m_targetPos = parkedCar.GetClosestDoorPosition(parkedCar.m_position, VehicleInfo.DoorType.Enter);
					instanceData.m_targetPos.w = 1f;
					instanceData.m_targetDir = VectorUtils.XZ(VectorUtils.NormalizeXZ(parkedCar.m_position - instanceData.GetLastFramePosition()));

					extInstance.LastDistanceToParkedCar = (instanceData.GetLastFramePosition() - (Vector3)instanceData.m_targetPos).sqrMagnitude;

					CitizenInstance.Frame frameData = instanceData.GetLastFrameData();
					frameData.m_velocity = Vector3.zero;
					instanceData.SetLastFrameData(frameData);

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): citizen instance {instanceId} was walking to parked car and reached final path position. Switched PathMode to {extInstance.PathMode}.");
#endif
				}
			}

			if (extInstance.PathMode == ExtPathMode.ApproachingParkedCar) {
				CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, physicsLodRefPos);

				float sqrDist = (instanceData.GetLastFramePosition() - (Vector3)instanceData.m_targetPos).sqrMagnitude;
				if (sqrDist > GlobalConfig.Instance.MaxParkedCarInstanceSwitchSqrDistance) {
					// citizen is still too far away from the parked car
					ExtPathMode oldPathMode = extInstance.PathMode;
					if (sqrDist > extInstance.LastDistanceToParkedCar + 1024f) {
						// distance has increased dramatically since the last time

#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log.Warning($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} is currently reaching their parked car but distance increased! dist={sqrDist}, LastDistanceToParkedCar={extInstance.LastDistanceToParkedCar}.");

						if (GlobalConfig.Instance.DebugSwitches[6]) {
							Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): FORCED PAUSE. Distance increased! Citizen instance {instanceId}. dist={sqrDist}");
							Singleton<SimulationManager>.instance.SimulationPaused = true;
						}
#endif

						extInstance.PathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
						return ParkedCarApproachState.Failure;
					} else if (sqrDist < extInstance.LastDistanceToParkedCar) {
						extInstance.LastDistanceToParkedCar = sqrDist;
					}
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} is currently reaching their parked car (dist={sqrDist}, LastDistanceToParkedCar={extInstance.LastDistanceToParkedCar}). CurrentDepartureMode={extInstance.PathMode}");
#endif

					return ParkedCarApproachState.Approaching;
				} else {
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkedCarApproached;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep({instanceId}): Citizen instance {instanceId} reached parking position (dist={sqrDist}). Set targetPos to parked vehicle position. Calculating remaining path now. CurrentDepartureMode={extInstance.PathMode}");
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
		}

		public bool CitizenApproachingTargetSimulationStep(ushort instanceId, ref CitizenInstance instanceData) {
			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is waiting for path-finding to complete.");
#endif
				return false;
			}

			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (extInstance.PathMode != ExtCitizenInstance.ExtPathMode.WalkingToTarget &&
				extInstance.PathMode != ExtCitizenInstance.ExtPathMode.PublicTransportToTarget &&
				extInstance.PathMode != ExtCitizenInstance.ExtPathMode.TaxiToTarget) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is not reaching target ({extInstance.PathMode})");
#endif
				return false;
			}

			if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
#if DEBUG
				/*if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): citizen instance {instanceId} is not spawned!");*/
#endif
				return false;
			}


			
			// check if path is complete
			PathUnit.Position pos;
			if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
				extInstance.Reset();
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): Citizen instance {instanceId} reached target. CurrentDepartureMode={extInstance.PathMode}");
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
		/// <param name="citizenData">Citizen data</param>
		/// <param name="handleSoftPathFindFailure">if true, path-finding may be repeated</param>
		/// <param name="handleSuccess">if true, the vanilla procedure of handling a successful path must be skipped</param>
		/// <returns>if true the calculated path may be used, false otherwise</returns>
		protected ExtSoftPathState OnCitizenPathFindSuccess(ushort instanceId, ref CitizenInstance instanceData, ref Citizen citizenData) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path-finding succeeded for citizen instance {instanceId}. Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
#endif

			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);

			if (extInstance == null) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Ext. citizen instance not found.");
#endif
				return ExtSoftPathState.FailedHard;
			}

			if (citizenData.m_vehicle == 0) {
				// citizen already has not already a vehicle assigned

				if (extInstance.PathMode == ExtPathMode.TaxiToTarget) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen uses a taxi. Decreasing public transport demand and returning READY.");
#endif

					// cim uses taxi
					if (instanceData.m_sourceBuilding != 0)
						ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, true);
					if (instanceData.m_targetBuilding != 0)
						ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, false);

					return ExtSoftPathState.Ready;
				}

				ushort parkedVehicleId = citizenData.m_parkedVehicle;
				float sqrDistToParkedVehicle = 0f;
				if (parkedVehicleId != 0) {
					// calculate distance to parked vehicle
					sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).sqrMagnitude;
				}

				byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes;
				ushort vehicleTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_vehicleTypes;
				bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;
				bool usesCar = (laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0 && (vehicleTypes & (ushort)(VehicleInfo.VehicleType.Car)) != 0;

				if (usesPublicTransport && usesCar && extInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) {
					/*
					 * when using public transport together with a car (assuming a "source -> walk -> drive -> walk -> use public transport -> walk -> target" path)
					 * discard parking space information since the cim has to park near the public transport stop
					 * (instead of parking in the vicinity of the target building).
					 * 
					 * TODO we could check if the path looks like "source -> walk -> use public transport -> walk -> drive -> [walk ->] target" (in this case parking space information would still be valid)
					*/
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen uses their car together with public transport. Discarding parking space information and setting path mode to CalculatingCarPathToTarget.");
#endif
					extInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
					extInstance.ParkingSpaceLocation = ExtParkingSpaceLocation.None;
					extInstance.ParkingSpaceLocationId = 0;
				}

				switch (extInstance.PathMode) {
					case ExtPathMode.None: // citizen starts at source building
					default:
						if (extInstance.PathMode != ExtPathMode.None) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Unexpected path mode {extInstance.PathMode}! {extInstance}");
#endif
						}

						if (usesCar) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path for citizen instance {instanceId} contains passenger car section. Ensuring that citizen is allowed to use their car.");
#endif

							// check if citizen is at an outside connection
							bool isAtOutsideConnection = false;
							ushort sourceBuildingId = instanceData.m_sourceBuilding;

							if (sourceBuildingId != 0) {
								isAtOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;// Info.m_buildingAI is OutsideConnectionAI;
								float distToOutsideConnection = (instanceData.GetLastFramePosition() - Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_position).magnitude;
								if (isAtOutsideConnection && distToOutsideConnection > GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance) {
									isAtOutsideConnection = false;
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[4]) {
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Source building {sourceBuildingId} of citizen instance {instanceId} is an outside connection but cim is too far away: {distToOutsideConnection}");
									}
#endif
								}
							} else {
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4]) {
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): No source building!");
								}
#endif
							}

#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2] && isAtOutsideConnection) {
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} is located at an incoming outside connection.");
							}
#endif

							if (!isAtOutsideConnection) {
								ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;

								if (parkedVehicleId == 0) {
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId} does not have a parked vehicle! CurrentPathMode={extInstance.PathMode}");
#endif

									// try to spawn parked vehicle in the vicinity of the starting point.
									VehicleInfo vehicleInfo = null;
									if (instanceData.Info.m_agePhase > Citizen.AgePhase.Child) {
										// get a random car info (due to the fact we are using a different randomizer, car assignment differs from the selection in ResidentAI.GetVehicleInfo/TouristAI.GetVehicleInfo method, but this should not matter since we are reusing parked vehicle infos there)
										vehicleInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref Singleton<SimulationManager>.instance.m_randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialLow, ItemClass.Level.Level1);
									}

									if (vehicleInfo != null) {
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId} is using their own passenger car. CurrentPathMode={extInstance.PathMode}");
#endif

										// determine current position vector
										Vector3 currentPos;
										ushort currentBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].GetBuildingByLocation();
										if (currentBuildingId != 0) {
											currentPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_position;
#if DEBUG
											if (GlobalConfig.Instance.DebugSwitches[2])
												Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Taking current position from source building {sourceBuildingId} for citizen {instanceData.m_citizen} (citizen instance {instanceId}): {currentPos} CurrentPathMode={extInstance.PathMode}");
#endif
										} else {
											currentPos = instanceData.GetLastFramePosition();
#if DEBUG
											if (GlobalConfig.Instance.DebugSwitches[2])
												Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Taking current position from last frame position for citizen {instanceData.m_citizen} (citizen instance {instanceId}): {currentPos}. Home {homeId} pos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position} CurrentPathMode={extInstance.PathMode}");
#endif
										}

										// spawn a passenger car near the current position
										Vector3 parkPos;
										if (AdvancedParkingManager.Instance.TrySpawnParkedPassengerCar(instanceData.m_citizen, homeId, currentPos, vehicleInfo, out parkPos)) {
											parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
#if DEBUG
											if (GlobalConfig.Instance.DebugSwitches[2] && sourceBuildingId != 0)
												Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Parked vehicle for citizen {instanceData.m_citizen} (instance {instanceId}) is {parkedVehicleId} now.");
#endif

											if (sourceBuildingId != 0) {
												ExtBuildingManager.Instance.GetExtBuilding(sourceBuildingId).ModifyParkingSpaceDemand(parkPos, GlobalConfig.Instance.MinSpawnedCarParkingSpaceDemandDelta, GlobalConfig.Instance.MaxSpawnedCarParkingSpaceDemandDelta);
											}
										} else {
#if DEBUG
											if (GlobalConfig.Instance.DebugSwitches[2]) {
												Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): >> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} (citizen instance {instanceId}). homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
											}
#endif

											if (sourceBuildingId != 0) {
												ExtBuildingManager.Instance.GetExtBuilding(sourceBuildingId).AddParkingSpaceDemand(GlobalConfig.Instance.FailedSpawnParkingSpaceDemandIncrement);
											}
										}
									} else {
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2]) {
											Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}), source building {sourceBuildingId}, home {homeId} does not own a vehicle.");
										}
#endif
									}
								}

								if (parkedVehicleId != 0) {
									if (instanceData.m_targetBuilding != 0) {
										// check distance between parked vehicle and target building. If it is too small then the cim is walking/using transport to get to their target
										float parkedDistToTarget = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_position - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).magnitude;
										if ((instanceData.m_targetBuilding != homeId && parkedDistToTarget < GlobalConfig.Instance.MaxParkedCarDistanceToBuilding) ||
											(instanceData.m_targetBuilding == homeId && parkedDistToTarget <= GlobalConfig.Instance.MaxParkedCarDistanceToHome)) {
											extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;

#if DEBUG
											if (GlobalConfig.Instance.DebugSwitches[2])
												Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Parked vehicle {parkedVehicleId} of citizen instance {instanceId} is {parkedDistToTarget} units away from target building {instanceData.m_targetBuilding}. Forcing citizen to walk to target, the car should stay there. PathMode={extInstance.PathMode}");
#endif

											return ExtSoftPathState.FailedSoft;
										}
									}

									// citizen has to reach their parked vehicle first
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceId}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif

									extInstance.PathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
									//instanceData.m_targetPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
									//extInstance.ParkedVehiclePosition = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;

									return ExtSoftPathState.FailedSoft;
								} else {
									// error! could not find/spawn parked car
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} still does not have a parked vehicle! Retrying: Cim should walk to target");
#endif

									extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
									return ExtSoftPathState.FailedSoft;
								}
							} else {
								// citizen is located at an outside connection: allow spawning of pocket cars (stock procedure).
								extInstance.PathMode = ExtPathMode.DrivingToTarget;

#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4])
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen {instanceData.m_citizen} (citizen instance {instanceId}) is located at an outside connection: {sourceBuildingId} CurrentPathMode={extInstance.PathMode}");
#endif

								return ExtSoftPathState.Ready;
							}
						} else {
							// path does not contain a car section: path can be reused for walking
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car path OR initial path was queried that does not contain a car section. Switching path mode to walking.");
#endif

							if (usesPublicTransport) {
								// decrease public tranport demand
								if (instanceData.m_sourceBuilding != 0)
									ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, true);
								if (instanceData.m_targetBuilding != 0)
									ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, false);
								extInstance.PathMode = ExtPathMode.PublicTransportToTarget;
							} else {
								extInstance.PathMode = ExtPathMode.WalkingToTarget;
							}

							return ExtSoftPathState.Ready;
						}
					case ExtPathMode.CalculatingCarPathToTarget: // citizen has not yet entered their car (but is close to do so) and tries to reach the target directly
					case ExtPathMode.CalculatingCarPathToKnownParkPos: // citizen has not yet entered their (but is close to do so) car and tries to reach a parking space in the vicinity of the target
					case ExtPathMode.CalculatingCarPathToAltParkPos: // citizen has not yet entered their car (but is close to do so) and tries to reach an alternative parking space in the vicinity of the target
						if (usesCar) {
							// parked car should be reached now
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Path for citizen instance {instanceId} contains passenger car section and citizen should stand in front of their car.");
#endif

							if (parkedVehicleId == 0) {
								// error! could not find/spawn parked car
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} still does not have a parked vehicle! Retrying: Cim should walk to target");
#endif

								extInstance.Reset();
								extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
								return ExtSoftPathState.FailedSoft;
							} else if (sqrDistToParkedVehicle > 2f * GlobalConfig.Instance.MaxParkedCarInstanceSwitchSqrDistance) {
								// error! parked car is too far away
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} cannot enter parked vehicle because it is too far away (sqrDistToParkedVehicle={sqrDistToParkedVehicle})! Retrying: Cim should walk to parked car");
#endif
								extInstance.PathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
								return ExtSoftPathState.FailedSoft;
							} else {
								// path using passenger car has been calculated
								ushort vehicleId;
								if (CustomHumanAI.EnterParkedCar(instanceId, ref instanceData, parkedVehicleId, out vehicleId)) { // TODO move here
									extInstance.PathMode = extInstance.PathMode == ExtPathMode.CalculatingCarPathToTarget ? ExtPathMode.DrivingToTarget : ExtPathMode.DrivingToKnownParkPos;

#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[4])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} has entered their car and is now travelling by car (vehicleId={vehicleId}). CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
									return ExtSoftPathState.Ignore;
								} else {
									// error! parked car could not be entered (reached vehicle limit?): try to walk to target
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceId}. Trying to walk to target. CurrentDepartureMode={extInstance.PathMode}");
#endif

									extInstance.Reset();
									extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
									return ExtSoftPathState.FailedSoft;
								}
							}
						} else {
							// citizen does not need a car for the calculated path...
							switch (extInstance.PathMode) {
								case ExtPathMode.CalculatingCarPathToTarget:
									// ... and the path can be reused for walking
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car path was queried that does not contain a car section. Switching path mode to walking.");
#endif
									extInstance.Reset();

									if (usesPublicTransport) {
										// decrease public tranport demand
										if (instanceData.m_sourceBuilding != 0)
											ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, true);
										if (instanceData.m_targetBuilding != 0)
											ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, false);
										extInstance.PathMode = ExtPathMode.PublicTransportToTarget;
									} else {
										extInstance.PathMode = ExtPathMode.WalkingToTarget;
									}

									return ExtSoftPathState.Ready;
								case ExtPathMode.CalculatingCarPathToKnownParkPos:
								case ExtPathMode.CalculatingCarPathToAltParkPos:
								default:
									// ... and a path to a parking spot was calculated: dismiss path and restart path-finding for walking
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A parking space car path was queried but it turned out that no car is needed. Retrying path-finding for walking.");
#endif
									extInstance.Reset();
									extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
									return ExtSoftPathState.FailedSoft;
							}
						}
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
						// path to parked vehicle has been calculated...
						if (parkedVehicleId == 0) {
							// ... but the parked vehicle has vanished
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} shall walk to their parked vehicle but it disappeared. Retrying path-find for walking.");
#endif
							extInstance.Reset();
							extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
							return ExtSoftPathState.FailedSoft;
						} else {
							extInstance.PathMode = ExtPathMode.WalkingToParkedCar;
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
							return ExtSoftPathState.Ready;
						}
					case ExtPathMode.CalculatingWalkingPathToTarget:
						// final walking path to target has been calculated
						extInstance.PathMode = ExtPathMode.WalkingToTarget;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen instance {instanceId} is now travelling by foot to their final target. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
						return ExtSoftPathState.Ready;
				}
			} else {
				// citizen has a vehicle assigned

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log.Warning($"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): Citizen has a vehicle assigned but this method does not handle this situation. Forcing path-find to fail.");
#endif
				extInstance.Reset();
				return ExtSoftPathState.FailedHard;
			}
		}

		/// <summary>
		/// Handles a path-finding failure for citizen instances and activated Parking AI.
		/// </summary>
		/// <param name="instanceID">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="extInstance">extended citizen instance information</param>
		/// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
		protected static ExtSoftPathState OnCitizenPathFindFailure(ushort instanceID, ref CitizenInstance instanceData, ExtCitizenInstance extInstance) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Path-finding failed for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
#endif

			// update demands
			if (instanceData.m_targetBuilding != 0) {
				switch (extInstance.PathMode) {
					/*case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						//ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).AddParkingSpaceDemand((uint)Options.debugValues[27]);
						break;*/
					case ExtPathMode.None:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.PublicTransportToTarget:
					case ExtPathMode.TaxiToTarget:
						// could not reach target building by walking/driving/public transport: increase public transport demand
						if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4])
								Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Increasing public transport demand of target building {instanceData.m_targetBuilding} and source building {instanceData.m_sourceBuilding}");
#endif

							if (instanceData.m_targetBuilding != 0) {
								ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).AddPublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandIncrement, false);
							}
							if (instanceData.m_sourceBuilding != 0) {
								ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).AddPublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandIncrement, true);
							}
						}
						break;
				}
			}

			if (extInstance.PathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
				// parked car is unreachable: despawn parked car
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Releasing parked vehicle {parkedVehicleId} for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
#endif
					Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
				}
			}

			// check if path-finding may be repeated
			ExtSoftPathState ret = ExtSoftPathState.FailedHard;
			switch (extInstance.PathMode) {
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
					// try to walk to target
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Path failed but it may be retried to walk to the target.");
#endif
					extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					ret = ExtSoftPathState.FailedSoft;
					break;
				default:
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Path failed and walking to target is not an option. Resetting ext. instance.");
#endif
					extInstance.Reset();
					break;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"AdvancedParkingManager.OnCitizenPathFindFailure({instanceID}): Setting CurrentPathMode for citizen instance {extInstance.InstanceId} to {extInstance.PathMode}, ret={ret}");
#endif

			return ret;
		}

		/// <summary>
		/// Finds a free parking space in the vicinity of the given target position <paramref name="endPos"/> for the given citizen instance <paramref name="extDriverInstance"/>.
		/// </summary>
		/// <param name="endPos">target position</param>
		/// <param name="vehicleInfo">vehicle type that is being used</param>
		/// <param name="extDriverInstance">cititzen instance that is driving the car</param>
		/// <param name="homeId">Home building of the citizen (may be 0 for tourists/homeless cims)</param>
		/// <param name="goingHome">Specifies if the citizen is going home</param>
		/// <param name="vehicleId">Vehicle that is being used (used for logging)</param>
		/// <param name="allowTourists">If true, method fails if given citizen is a tourist (TODO remove this parameter)</param>
		/// <param name="parkPos">parking position (output)</param>
		/// <param name="endPathPos">sidewalk path position near parking space (output). only valid if <paramref name="calculateEndPos"/> yields false.</param>
		/// <param name="calculateEndPos">if false, a parking space path position could be calculated (TODO negate & rename parameter)</param>
		/// <returns>true if a parking space could be found, false otherwise</returns>
		public bool FindParkingSpaceForCitizen(Vector3 endPos, VehicleInfo vehicleInfo, ExtCitizenInstance extDriverInstance, ushort homeId, bool goingHome, ushort vehicleId, bool allowTourists, out Vector3 parkPos, ref PathUnit.Position endPathPos, out bool calculateEndPos) {
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
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"Citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) can still use their passenger car and is either not a tourist or wants to find an alternative parking spot. Finding a parking space before starting path-finding.");
#endif

			ExtParkingSpaceLocation knownParkingSpaceLocation;
			ushort knownParkingSpaceLocationId;
			Quaternion parkRot;
			float parkOffset;

			// find a free parking space
			bool success = FindParkingSpaceInVicinity(endPos, vehicleInfo, homeId, vehicleId, goingHome ? GlobalConfig.Instance.MaxParkedCarDistanceToHome : GlobalConfig.Instance.MaxParkedCarDistanceToBuilding, out knownParkingSpaceLocation, out knownParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

			extDriverInstance.ParkingSpaceLocation = knownParkingSpaceLocation;
			extDriverInstance.ParkingSpaceLocationId = knownParkingSpaceLocationId;

			if (success) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Found a parking spot for citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) before starting car path: {knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
#endif

				if (knownParkingSpaceLocation == ExtParkingSpaceLocation.RoadSide) {
					// found segment with parking space
					Vector3 pedPos;
					uint laneId;
					int laneIndex;
					float laneOffset;

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Found segment {knownParkingSpaceLocationId} for road-side parking position for citizen instance {extDriverInstance.InstanceId}!");
#endif

					// determine nearest sidewalk position for parking position at segment
					if (Singleton<NetManager>.instance.m_segments.m_buffer[knownParkingSpaceLocationId].GetClosestLanePosition(parkPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out pedPos, out laneId, out laneIndex, out laneOffset)) {
						endPathPos.m_segment = knownParkingSpaceLocationId;
						endPathPos.m_lane = (byte)laneIndex;
						endPathPos.m_offset = (byte)(parkOffset * 255f);
						calculateEndPos = false;

						//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Found an parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId} @ segment {knownParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return true;
					} else {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Could not find an alternative parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return false;
					}
				} else if (knownParkingSpaceLocation == ExtParkingSpaceLocation.Building) {
					// found a building with parking space
					if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, endPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, out endPathPos)) {
						calculateEndPos = false;
					}

					//endPos = parkPos;

					//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Navigating citizen instance {extDriverInstance.InstanceId} to parking building {knownParkingSpaceLocationId}! segment={endPathPos.m_segment}, laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. CurrentPathMode={extDriverInstance.PathMode} calculateEndPos={calculateEndPos}");
#endif
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		internal bool TrySpawnParkedPassengerCar(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car for citizen {citizenId}, home {homeId} @ {refPos}");
#endif
			if (TrySpawnParkedPassengerCarRoadSide(citizenId, refPos, vehicleInfo, out parkPos))
				return true;
			return TrySpawnParkedPassengerCarBuilding(citizenId, homeId, refPos, vehicleInfo, out parkPos);
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> at a road segment in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		public bool TrySpawnParkedPassengerCarRoadSide(uint citizenId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset = 0f;

			if (FindParkingSpaceRoadSide(0, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, GlobalConfig.Instance.MaxParkedCarDistanceToBuilding, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"[SUCCESS] Spawned parked passenger car at road side for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
#endif
			return false;
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> at a building in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		public bool TrySpawnParkedPassengerCarBuilding(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car next to building for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset;

			if (FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, refPos, GlobalConfig.Instance.MaxParkedCarDistanceToBuilding, GlobalConfig.Instance.MaxParkedCarDistanceToBuilding, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
						Log._Debug($"[SUCCESS] Spawned parked passenger car next to building for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2] && homeId != 0)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car next to building for citizen {citizenId}");
#endif
			return false;
		}

		public bool FindParkingSpaceInVicinity(Vector3 targetPos, VehicleInfo vehicleInfo, ushort homeId, ushort vehicleId, float maxDist, out ExtParkingSpaceLocation parkingSpaceLocation, out ushort parkingSpaceLocationId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
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
					// choose nearest parking position
					if ((roadParkPos - targetPos).magnitude < (buildingParkPos - targetPos).magnitude) {
						// road parking space is closer
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
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
						if (GlobalConfig.Instance.DebugSwitches[2])
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
					if (GlobalConfig.Instance.DebugSwitches[2])
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
				if (GlobalConfig.Instance.DebugSwitches[2])
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
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Could not find a road-side or building parking position for vehicle {vehicleId}!");
#endif
				return false;
			}
		}

		internal ushort FindParkingSpaceAtRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
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

					Vector3 segCenter = netManager.m_segments.m_buffer[segmentId].m_bounds.center;

					// randomize target position to allow for opposite road-side parking
					segCenter.x += Singleton<SimulationManager>.instance.m_randomizer.Int32(GlobalConfig.Instance.ParkingSpacePositionRand) - GlobalConfig.Instance.ParkingSpacePositionRand / 2u;
					segCenter.z += Singleton<SimulationManager>.instance.m_randomizer.Int32(GlobalConfig.Instance.ParkingSpacePositionRand) - GlobalConfig.Instance.ParkingSpacePositionRand / 2u;

					if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(segCenter, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
						if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
							if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, innerParkPos, width, length, out innerParkPos, out innerParkRot, out innerParkOffset)) {
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4])
									Log._Debug($"FindParkingSpaceRoadSide: Found a parking space for refPos {refPos}, segment center {segCenter} @ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");
#endif
								foundSegmentId = segmentId;
								myParkPos = innerParkPos;
								myParkRot = innerParkRot;
								myParkOffset = innerParkOffset;
								if (!randomize || rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) != 0)
									return false;
							}
						}
					} else {
						/*if (GlobalConfig.Instance.DebugSwitches[2])
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
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
#endif
				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundSegmentId;
		}

		public ushort FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
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
				if (GlobalConfig.Instance.DebugSwitches[4]) {
					//Log._Debug($"FindParkingSpaceBuilding: Checking building grid @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j} for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}");
				}
#endif

				ushort buildingId = buildingMan.m_buildingGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int numIterations = 0;
				while (buildingId != 0) {
					Vector3 innerParkPos; Quaternion innerParkRot; float innerParkOffset;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4]) {
						//Log._Debug($"FindParkingSpaceBuilding: Checking building {buildingId} @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j}, for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}.");
					}
#endif

					if (FindParkingSpacePropAtBuilding(vehicleInfo, homeID, ignoreParked, buildingId, ref buildingMan.m_buildings.m_buffer[(int)buildingId], segmentId, refPos, ref maxParkingSpaceDistance, randomize, out innerParkPos, out innerParkRot, out innerParkOffset)) {
#if DEBUG
						/*/if (GlobalConfig.Instance.DebugSwitches[4] && homeID != 0)
							Log._Debug($"FindParkingSpaceBuilding: Found a parking space for {refPos}, homeID {homeID} @ building {buildingId}, {myParkPos}, offset {myParkOffset}!");
						*/
#endif
						foundBuildingId = buildingId;
						myParkPos = innerParkPos;
						myParkRot = innerParkRot;
						myParkOffset = innerParkOffset;

						if (!randomize || rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) != 0)
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
				if (GlobalConfig.Instance.DebugSwitches[2] && homeID != 0)
					Log._Debug($"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");
#endif

				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundBuildingId;
		}

		/// <summary>
		/// Finds a parking space (prop) that belongs to a given building
		/// </summary>
		/// <param name="homeID"></param>
		/// <param name="ignoreParked"></param>
		/// <param name="buildingID"></param>
		/// <param name="building"></param>
		/// <param name="segmentId"></param>
		/// <param name="refPos"></param>
		/// <param name="width"></param>
		/// <param name="length"></param>
		/// <param name="maxDistance"></param>
		/// <param name="parkPos"></param>
		/// <param name="parkRot"></param>
		/// <param name="parkOffset"></param>
		/// <returns></returns>
		public bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort buildingID, ref Building building, ushort segmentId, Vector3 refPos, ref float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			int buildingWidth = building.Width;
			int buildingLength = building.Length;

			// NON-STOCK CODE START
			parkOffset = -1f; // only set if segmentId != 0
			parkPos = default(Vector3);
			parkRot = default(Quaternion);

			if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not created.");
#endif
				return false;
			}

			if ((building.m_problems & Notification.Problem.TurnedOff) != Notification.Problem.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not active.");
#endif
				return false;
			}

			if ((building.m_flags & Building.Flags.Collapsed) != Building.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is collapsed.");
#endif
				return false;
			}

			/*else {
			// NON-STOCK CODE END
				float diagWidth = Mathf.Sqrt((float)(buildingWidth * buildingWidth + buildingLength * buildingLength)) * 8f;
				if (VectorUtils.LengthXZ(building.m_position - refPos) >= maxDistance + diagWidth) {*/
#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"Refusing to find parking space at building {buildingID}! {VectorUtils.LengthXZ(building.m_position - refPos)} >= {maxDistance + diagWidth} (maxDistance={maxDistance})");*/
#endif
			/*return false;
		}
	}*/ // NON-STOCK CODE

			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

			BuildingInfo buildingInfo = building.Info;
			Matrix4x4 transformMatrix = default(Matrix4x4);
			bool transformMatrixCalculated = false;
			bool result = false;
			if (buildingInfo.m_class.m_service == ItemClass.Service.Residential && buildingID != homeID && rng.Int32((uint)Options.getRecklessDriverModulo()) != 0) { // NON-STOCK CODE
#if DEBUG
				/*if (GlobalConfig.Instance.DebugSwitches[4])
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
								if (CustomPassengerCarAI.FindParkingSpaceProp(ignoreParked, propInfo, position, building.m_angle + prop.m_radAngle, prop.m_fixedHeight, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, ref propMinDistance, ref parkPos, ref parkRot)) { // NON-STOCK CODE
									result = true;
									if (randomize && propMinDistance <= maxDistance && rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) == 0)
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
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Found parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				if (segmentId != 0) {
					// check if building is accessible from the given segment
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
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
						if (GlobalConfig.Instance.DebugSwitches[4])
							Log._Debug($"Succeeded in finding unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}! lanePos={lanePos}, dist={(lanePos - unspawnPos).magnitude}, laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");
#endif

						/*if (dist > 16f) {
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"Distance between unspawn position and lane position is too big! {dist} unspawnPos={unspawnPos} lanePos={lanePos}");
							return false;
						}*/

						parkOffset = laneOffset;
					} else {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log.Warning($"Could not find unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}!");
#endif
					}
				}

				return true;
			} else {
#if DEBUG
				if (result && GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Could not find parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				return false;
			}
		}

		/// <summary>
		/// Finds a free parking space for a given vehicle position on a given segment
		/// </summary>
		/// <param name="ignoreParked"></param>
		/// <param name="segmentId"></param>
		/// <param name="refPos"></param>
		/// <param name="width"></param>
		/// <param name="length"></param>
		/// <param name="parkPos"></param>
		/// <param name="parkRot"></param>
		/// <param name="laneId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="parkOffset"></param>
		/// <returns></returns>
		public bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo, ushort ignoreParked, ushort segmentId, Vector3 refPos, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset, out uint laneId, out int laneIndex) {
			float width = vehicleInfo.m_generatedInfo.m_size.x;
			float length = vehicleInfo.m_generatedInfo.m_size.z;

			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out parkPos, out laneId, out laneIndex, out parkOffset)) {
					if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
						if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, parkPos, width, length, out parkPos, out parkRot, out parkOffset)) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4])
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
			return AdvancedParkingManager.Instance.FindParkingSpaceAtRoadSide(ignoreParked, refPos, width, length, maxDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, ignoreParked, segmentId, refPos, maxBuildingDistance, maxParkingSpaceDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public string EnrichLocalizedCitizenStatus(string ret, ExtCitizenInstance extInstance) {
			if (extInstance != null) {
				switch (extInstance.PathMode) {
					case ExtPathMode.ApproachingParkedCar:
					case ExtPathMode.ParkedCarApproached:
						ret = Translation.GetString("Entering_vehicle") + ", " + ret;
						break;
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
						ret = Translation.GetString("Walking_to_car") + ", " + ret;
						break;
					case ExtPathMode.PublicTransportToTarget:
					case ExtPathMode.TaxiToTarget:
						ret = Translation.GetString("Using_public_transport") + ", " + ret;
						break;
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.WalkingToTarget:
						ret = Translation.GetString("Walking") + ", " + ret;
						break;
					case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						ret = Translation.GetString("Thinking_of_a_good_parking_spot") + ", " + ret;
						break;
				}
			}
			return ret;
		}

		public string EnrichLocalizedCarStatus(string ret, ExtCitizenInstance driverExtInstance) {
			if (driverExtInstance != null) {
				switch (driverExtInstance.PathMode) {
					case ExtPathMode.DrivingToAltParkPos:
						ret = Translation.GetString("Driving_to_another_parking_spot") + " (#" + driverExtInstance.FailedParkingAttempts + "), " + ret;
						break;
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.DrivingToKnownParkPos:
						ret = Translation.GetString("Driving_to_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.ParkingFailed:
					case ExtPathMode.CalculatingCarPathToAltParkPos:
						ret = Translation.GetString("Looking_for_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.ParkingSucceeded:
						ret = Locale.Get("VEHICLE_STATUS_PARKING") + ", " + ret;
						break;
				}
			}
			return ret;
		}
	}
}

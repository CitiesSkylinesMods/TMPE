using ColossalFramework;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Manager;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;
using System;
using static TrafficManager.Traffic.ExtCitizenInstance;
using TrafficManager.Util;
using ColossalFramework.Math;
using TrafficManager.UI;

namespace TrafficManager.Custom.AI {
	class CustomHumanAI : CitizenAI {
		public void CustomSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			if ((instanceData.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None && (instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
				uint citizenId = instanceData.m_citizen;
				Singleton<CitizenManager>.instance.ReleaseCitizenInstance(instanceID);
				if (citizenId != 0u) {
					Singleton<CitizenManager>.instance.ReleaseCitizen(citizenId);
				}
				return;
			}

			// NON-STOCK CODE START
			ExtCitizenInstance extInstance = null;
			if (Options.prohibitPocketCars) {
				// query the state of the return path
				extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);
				extInstance.UpdateReturnPathState();
			}
			// NON-STOCK CODE END

			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None
				&& (!Options.prohibitPocketCars || extInstance.ReturnPathState != ExtPathState.Calculating)) { // NON-STOCK CODE: Parking AI: wait for the return path to be calculated
				PathManager pathManager = Singleton<PathManager>.instance;
				byte pathFindFlags = pathManager.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

				// NON-STOCK CODE START
				bool pathFindFailed = (pathFindFlags & PathUnit.FLAG_FAILED) != 0 || instanceData.m_path == 0;
				bool pathFindSucceeded = (pathFindFlags & PathUnit.FLAG_READY) != 0;
				bool handleSuccess = true;

				if (Options.prohibitPocketCars) {
					// Parking AI
					if (extInstance.ReturnPathState == ExtPathState.Failed) {
						// no walking from parking space to target found
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomPassengerCarAI.CustomSimulationStep: Return path {extInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
#endif
						pathFindSucceeded = false;
						pathFindFailed = true;
					}

					extInstance.ReleaseReturnPath();

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4] && (pathFindFailed || pathFindSucceeded)) {
						Log._Debug($"CustomHumanAI::CustomSimulationStep Citizen instance {instanceID}, citizen {instanceData.m_citizen} is called {Singleton<CitizenManager>.instance.GetCitizenName(instanceData.m_citizen)} and is {(Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].Age*100)/255} years old. PathMode={extInstance.PathMode} ReturnPathState={extInstance.ReturnPathState}");
					}
#endif

					if (pathFindSucceeded) {
						// check if path-finding has to be repeated in order to prevent pocket cars
						bool handleSoftPathFindFailure;
						if (!CustomHumanAI.OnPathFindSuccess(instanceID, ref instanceData, ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen], out handleSoftPathFindFailure, out handleSuccess)) {
							// we cannot simply use the calculated path
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								Log._Debug($"CustomHumanAI.CustomSimulationStep: " + (handleSoftPathFindFailure ? "Soft" : "Hard") + $" path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
							}
#endif

							if (handleSoftPathFindFailure) {
								// path mode has been updated, repeat path-finding
								instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
								instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
								this.InvalidPath(instanceID, ref instanceData);
								return;
							} else {
								// something went wrong
								pathFindSucceeded = false;
								pathFindFailed = true;
							}
						} else if (! handleSuccess) {
							return;
						}
					} else if (pathFindFailed) {
						// update info view statistics
						if (CustomHumanAI.OnPathFindFailure(instanceID, ref instanceData, extInstance)) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								Log._Debug($"CustomHumanAI.CustomSimulationStep: Handled path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
							}
#endif

							//GetBuildingTargetPosition(instanceID, ref instanceData, 0f);
							instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
							instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
							this.InvalidPath(instanceID, ref instanceData);
							return;
						}
					}
				}
				// NON-STOCK CODE END

				if (pathFindSucceeded) { // NON-STOCK CODE
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
#endif
					this.Spawn(instanceID, ref instanceData);
					instanceData.m_pathPositionIndex = 255;
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					this.PathfindSuccess(instanceID, ref instanceData);
				} else if (pathFindFailed) { // NON-STOCK CODE
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding failed for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
#endif
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
					instanceData.m_path = 0u;
					this.PathfindFailure(instanceID, ref instanceData);
					return;
				}
			}

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				// check if the citizen has reached a parked car or target
				if (CustomHumanAI.CheckReachedParkedCar(instanceID, ref instanceData)) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Citizen instance {instanceID} arrives at parked car or parked car is too far way to enter. PathMode={extInstance.PathMode}");
#endif
					if (instanceData.m_path != 0) {
						Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
						instanceData.m_path = 0;
					}
					instanceData.m_flags = instanceData.m_flags & (CitizenInstance.Flags.Created | CitizenInstance.Flags.Deleted | CitizenInstance.Flags.Underground | CitizenInstance.Flags.CustomName | CitizenInstance.Flags.Character | CitizenInstance.Flags.BorrowCar | CitizenInstance.Flags.HangAround | CitizenInstance.Flags.InsideBuilding | CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.TryingSpawnVehicle | CitizenInstance.Flags.CannotUseTransport | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.OnPath | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.AtTarget | CitizenInstance.Flags.RequireSlowStart | CitizenInstance.Flags.Transition | CitizenInstance.Flags.RidingBicycle | CitizenInstance.Flags.OnBikeLane | CitizenInstance.Flags.CannotUseTaxi | CitizenInstance.Flags.CustomColor | CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating | CitizenInstance.Flags.TargetFlags);
					if (!this.StartPathFind(instanceID, ref instanceData)) {
						instanceData.Unspawn(instanceID);
						extInstance.Reset();
					}

					return;
				}
				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.ReachingParkedCar) {
					ReachingParkedCarSimulationStep(instanceID, ref instanceData, physicsLodRefPos);
					return;
				}
			}
			// NON-STOCK CODE END

			base.SimulationStep(instanceID, ref instanceData, physicsLodRefPos);

			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			ushort vehicleId = 0;
			if (instanceData.m_citizen != 0u) {
				vehicleId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)instanceData.m_citizen)].m_vehicle;
			}
			if (vehicleId != 0) {
				VehicleInfo vehicleInfo = vehicleManager.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
					vehicleInfo.m_vehicleAI.SimulationStep(vehicleId, ref vehicleManager.m_vehicles.m_buffer[(int)vehicleId], vehicleId, ref vehicleManager.m_vehicles.m_buffer[(int)vehicleId], 0);
					vehicleId = 0;
				}
			}
			if (vehicleId == 0 && (instanceData.m_flags & (CitizenInstance.Flags.Character | CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) == CitizenInstance.Flags.None) {
				instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
				this.ArriveAtDestination(instanceID, ref instanceData, false);
				citizenManager.ReleaseCitizenInstance(instanceID);
			}
		}

		protected static void ReachingParkedCarSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			if ((instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
				CitizenInstance.Frame lastFrameData = instanceData.GetLastFrameData();
				int oldGridX = Mathf.Clamp((int)(lastFrameData.m_position.x / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION-1);
				int oldGridY = Mathf.Clamp((int)(lastFrameData.m_position.z / (float)CitizenManager.CITIZENGRID_CELL_SIZE + (float)CitizenManager.CITIZENGRID_RESOLUTION / 2f), 0, CitizenManager.CITIZENGRID_RESOLUTION - 1);
				bool lodPhysics = Vector3.SqrMagnitude(physicsLodRefPos - lastFrameData.m_position) >= 62500f;
				ReachingParkedCarSimulationStep(instanceID, ref instanceData, ref lastFrameData, lodPhysics);
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

		protected static void ReachingParkedCarSimulationStep(ushort instanceID, ref CitizenInstance instanceData, ref CitizenInstance.Frame frameData, bool lodPhysics) {
			/*uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			float sqrVelocity = frameData.m_velocity.sqrMagnitude;
			float lodSqrVelocity = Mathf.Max(sqrVelocity * 3f, 3f);
			if (lodPhysics && (currentFrameIndex >> 4 & 3) == (instanceID & 3)) {
				lodSqrVelocity = lodSqrVelocity * 4f;
			}*/

			frameData.m_position = frameData.m_position + (frameData.m_velocity * 0.5f);

			Vector3 targetDiff = (Vector3)instanceData.m_targetPos - frameData.m_position;
			Vector3 targetVelDiff = targetDiff - frameData.m_velocity;
			float targetVelDiffMag = targetVelDiff.magnitude;

			targetVelDiff = targetVelDiff * (2f / Mathf.Max(targetVelDiffMag, 2f));
			frameData.m_velocity = frameData.m_velocity + targetVelDiff;
			frameData.m_velocity = frameData.m_velocity - (Mathf.Max(0f, Vector3.Dot((frameData.m_position + frameData.m_velocity) - (Vector3)instanceData.m_targetPos, frameData.m_velocity)) / Mathf.Max(0.01f, frameData.m_velocity.sqrMagnitude) * frameData.m_velocity);
		}

		/// <summary>
		/// Handles a path-finding failure for activated Parking AI.
		/// </summary>
		/// <param name="instanceID">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="extInstance">extended citizen instance information</param>
		/// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
		protected static bool OnPathFindFailure(ushort instanceID, ref CitizenInstance instanceData, ExtCitizenInstance extInstance) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Path-finding failed for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
#endif

			// update statistics
			if (instanceData.m_targetBuilding != 0) {
				switch (extInstance.PathMode) {
					/*case ExtPathMode.CalculatingCarPathToTarget:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
						//ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).AddParkingSpaceDemand((uint)Options.debugValues[27]);
						break;*/
					case ExtPathMode.None:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.TaxiToTarget:
						if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
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
				// parked car is unreachable
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindFailure: Releasing parked vehicle {parkedVehicleId} for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
#endif
					Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
				}
			}

			// check if path-finding may be repeated
			bool ret = false;
			switch (extInstance.PathMode) {
				case ExtPathMode.CalculatingCarPathToTarget:
				case ExtPathMode.CalculatingCarPathToKnownParkPos:
				case ExtPathMode.CalculatingWalkingPathToParkedCar:
					// try to walk to target
					extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					ret = true;
					break;
				default:
					extInstance.Reset();
					break;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Setting CurrentPathMode for citizen instance {extInstance.InstanceId} to {extInstance.PathMode}, ret={ret}");
#endif

			return ret;
		}

		/// <summary>
		/// Handles a path-finding success for activated Parking AI.
		/// </summary>
		/// <param name="instanceID">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="citizenData">Citizen data</param>
		/// <param name="handleSoftPathFindFailure">if true, path-finding may be repeated</param>
		/// <param name="handleSuccess">if true, the vanilla procedure of handling a successful path must be skipped</param>
		/// <returns>if true the calculated path may be used, false otherwise</returns>
		protected static bool OnPathFindSuccess(ushort instanceID, ref CitizenInstance instanceData, ref Citizen citizenData, out bool handleSoftPathFindFailure, out bool handleSuccess) {
			handleSoftPathFindFailure = false;
			handleSuccess = true;
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
#endif

			if (citizenData.m_vehicle == 0) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);

				if (extInstance.PathMode == ExtPathMode.TaxiToTarget) {
					// cim uses taxi
					if (instanceData.m_sourceBuilding != 0)
						ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, true);
					if (instanceData.m_targetBuilding != 0)
						ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, false);

					return true;
				}

				ushort parkedVehicleId = citizenData.m_parkedVehicle;
				float sqrDistToParkedVehicle = 0f;
				if (parkedVehicleId != 0) {
					sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).sqrMagnitude;
				}

				byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes;
				byte vehicleTypes = CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_vehicleTypes;
				bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;
				bool usesCar = (laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0 && (vehicleTypes & (byte)(VehicleInfo.VehicleType.Car)) != 0;

				if (usesPublicTransport && usesCar && extInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) {
					/*
					 * when using public transport together with a car (assuming a "source -> walk -> drive -> walk -> use public transport -> walk -> target" path)
					 * discard parking space information since the cim has to park near the public transport stop
					 * (instead of parking in the vicinity of the target building).
					 * 
					 * TODO we could check if the path looks like "source -> walk -> use public transport -> walk -> drive -> [walk ->] target" (in this case parking space information would still be valid)
					*/ 
					extInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
					extInstance.ParkingSpaceLocation = ExtParkingSpaceLocation.None;
					extInstance.ParkingSpaceLocationId = 0;
				}

				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.None || (
					(extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget || extInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos) &&
					(parkedVehicleId == 0 /*|| sqrDistToParkedVehicle >= GlobalConfig.Instance.MaxParkedCarReachSqrDistance*/)
					)) {

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: PathMode={extInstance.PathMode}, parkedVehicleId={parkedVehicleId}, sqrDistToParkedVehicle={sqrDistToParkedVehicle} - for citizen instance {instanceID}.");
#endif
					if (usesCar) {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} contains passenger car section and citizen is walking at the moment. Ensuring that citizen is allowed to use their car.");
#endif

						// check if citizen is at an outside connection
						bool isAtOutsideConnection = false;
						ushort sourceBuildingId = instanceData.m_sourceBuilding;
						/*if (sourceBuildingId == 0) {
							sourceBuildingId = citizenData.GetBuildingByLocation();
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2]) {
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Source building = 0 for citizen instance {instanceID}. Building by location: {sourceBuildingId}");
							}
#endif
						} else {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4]) {
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Source building = {sourceBuildingId} (from data)");
							}
#endif
						}

						if (sourceBuildingId == 0) {
							sourceBuildingId = Singleton<BuildingManager>.instance.FindBuilding(instanceData.GetLastFramePosition(), GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, ItemClass.Service.Road, ItemClass.SubService.None, Building.Flags.IncomingOutgoing, Building.Flags.None);
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2]) {
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Source building = 0 for citizen instance {instanceID}. Building from FindBuilding: {sourceBuildingId}");
							}
#endif
						}*/

						if (sourceBuildingId != 0) {
							isAtOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;// Info.m_buildingAI is OutsideConnectionAI;
							float distToOutsideConnection = (instanceData.GetLastFramePosition() - Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_position).magnitude;
							if (isAtOutsideConnection && distToOutsideConnection > GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance) {
								isAtOutsideConnection = false;
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4]) {
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Source building {sourceBuildingId} of citizen instance {instanceID} is an outside connection but cim is too far away: {distToOutsideConnection}");
								}
#endif
							}
						} else {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4]) {
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Source building = 0 !");
							}
#endif
						}

#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2] && isAtOutsideConnection) {
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is located at an incoming outside connection.");
						}
#endif

						if (!isAtOutsideConnection) {
							ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;

							if (parkedVehicleId == 0) {
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} does not have a parked vehicle! CurrentPathMode={extInstance.PathMode}");
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
										Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} is using their own passenger car. CurrentPathMode={extInstance.PathMode}");
#endif

									// determine current position vector
									Vector3 currentPos;
									ushort currentBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].GetBuildingByLocation();
									if (currentBuildingId != 0) {
										currentPos = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_position;
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"CustomHumanAI.OnPathFindSuccess: Taking current position from source building {sourceBuildingId} for citizen {instanceData.m_citizen} (citizen instance {instanceID}): {currentPos} CurrentPathMode={extInstance.PathMode}");
#endif
									} else {
										currentPos = instanceData.GetLastFramePosition();
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"CustomHumanAI.OnPathFindSuccess: Taking current position from last frame position for citizen {instanceData.m_citizen} (citizen instance {instanceID}): {currentPos}. Home {homeId} pos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position} CurrentPathMode={extInstance.PathMode}");
#endif
									}

									// spawn a passenger car near the current position
									Vector3 parkPos;
									if (CustomCitizenAI.TrySpawnParkedPassengerCar(instanceData.m_citizen, homeId, currentPos, vehicleInfo, out parkPos)) {
										parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2] && sourceBuildingId != 0)
											Log._Debug($"Parked vehicle for citizen {instanceData.m_citizen} (instance {instanceID}) is {parkedVehicleId} now.");
#endif

										if (sourceBuildingId != 0) {
											ExtBuildingManager.Instance.GetExtBuilding(sourceBuildingId).ModifyParkingSpaceDemand(parkPos, GlobalConfig.Instance.MinSpawnedCarParkingSpaceDemandDelta, GlobalConfig.Instance.MaxSpawnedCarParkingSpaceDemandDelta);
										}
									} else {
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2]) {
											Log._Debug($">> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} (citizen instance {instanceID}). homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
										}
#endif

										if (sourceBuildingId != 0) {
											ExtBuildingManager.Instance.GetExtBuilding(sourceBuildingId).AddParkingSpaceDemand(GlobalConfig.Instance.FailedSpawnParkingSpaceDemandIncrement);
										}
									}
								} else {
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2]) {
										Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId}, home {homeId} does not own a vehicle.");
									}
#endif
								}
							}

							if (parkedVehicleId != 0) {
								if (instanceData.m_targetBuilding != 0) {
									// check distance between parked vehicle and target building. If it is too small then the cim is walking/using transport to get to their target
									float parkedDistToTarget = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_position - Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position).magnitude;
									if (parkedDistToTarget < GlobalConfig.Instance.MinParkedCarToTargetBuildingDistance) {
										extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToTarget;
										handleSoftPathFindFailure = true;

#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"CustomHumanAI.OnPathFindSuccess: Parked vehicle {parkedVehicleId} of citizen instance {instanceID} is {parkedDistToTarget} units away from target building {instanceData.m_targetBuilding}. Forcing citizen to walk. PathMode={extInstance.PathMode}");
#endif

										return false;
									}
								}

								// citizen has to reach their parked vehicle first
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceID}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif

								extInstance.PathMode = ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToParkedCar;
								//instanceData.m_targetPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
								//extInstance.ParkedVehiclePosition = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;

								handleSoftPathFindFailure = true;
								return false;
							} else {
								// error!
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} does not have a parked vehicle! Forcing path-finding to fail.");
#endif

								return false;
							}
						} else {
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;

#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4])
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen {instanceData.m_citizen} (citizen instance {instanceID}) is located at an outside connection: {sourceBuildingId} CurrentPathMode={extInstance.PathMode}");
#endif

							return true;
						}
					} else {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[4])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} does not contain passenger car section.");
#endif
						if (usesPublicTransport) {
							if (instanceData.m_sourceBuilding != 0)
								ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_sourceBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, true);
							if (instanceData.m_targetBuilding != 0)
								ExtBuildingManager.Instance.GetExtBuilding(instanceData.m_targetBuilding).RemovePublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandUsageDecrement, false);
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.PublicTransportToTarget;
						} else {
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
						}
						return true;
					}
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
					// path to parked vehicle has been calculated
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToParkedCar;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
					return true;
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget) {
					// path using passenger car has been calculated
					ushort vehicleId;
					if (CustomHumanAI.EnterParkedCar(instanceID, ref instanceData, parkedVehicleId, out vehicleId)) {
						extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;
						handleSuccess = false;

#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[4])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car (vehicleId={vehicleId}). CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
						return true;
					} else {
						// error!
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceID}. GIVING UP. CurrentDepartureMode={extInstance.PathMode}");
#endif

						return false;
					}
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos) {
					// path using passenger car with known parking position has been calculated
					ushort vehicleId;
					if (CustomHumanAI.EnterParkedCar(instanceID, ref instanceData, parkedVehicleId, out vehicleId)) {
						extInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos;
						handleSuccess = false;

#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[4])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car (vehicleId={vehicleId}) and knows where to park. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
						return true;
					} else {
						// error!
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Entering parked vehicle {parkedVehicleId} failed for citizen instance {instanceID}. GIVING UP. CurrentDepartureMode={extInstance.PathMode}");
#endif

						return false;
					}
				} else if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToTarget) {
					// final walking path to target has been calculated
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.WalkingToTarget;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by foot to their final target. CurrentDepartureMode={extInstance.PathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
#endif
					return true;
				}
			}
			return true;
		}

		/// <summary>
		/// Makes the given citizen instance enter their parked car.
		/// </summary>
		/// <param name="instanceID">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="parkedVehicleId">Parked vehicle id</param>
		/// <param name="vehicleId">Vehicle id</param>
		/// <returns>true if entering the car succeeded, false otherwise</returns>
		protected static bool EnterParkedCar(ushort instanceID, ref CitizenInstance instanceData, ushort parkedVehicleId, out ushort vehicleId) {
			VehicleManager vehManager = Singleton<VehicleManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			CitizenManager citManager = Singleton<CitizenManager>.instance;

			Vector3 parkedVehPos = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
			Quaternion parkedVehRot = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].m_rotation;
			VehicleInfo vehicleInfo = vehManager.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

			PathUnit.Position vehLanePathPos;
			if (! CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(0, out vehLanePathPos)) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Could not get first car path position of citizen instance {instanceID}!");
#endif

				vehicleId = 0;
				return false;
			}
			uint vehLaneId = PathManager.GetLaneID(vehLanePathPos);

			Vector3 vehLanePos;
			float vehLaneOff;
			netManager.m_lanes.m_buffer[vehLaneId].GetClosestPosition(parkedVehPos, out vehLanePos, out vehLaneOff);
			byte vehLaneOffset = (byte)Mathf.Clamp(Mathf.RoundToInt(vehLaneOff * 255f), 0, 255);

			// movement vector from parked vehicle position to road position
			Vector3 forwardVector = parkedVehPos + Vector3.ClampMagnitude(vehLanePos - parkedVehPos, 5f);
			if (vehManager.CreateVehicle(out vehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkedVehPos, TransferManager.TransferReason.None, false, false)) {
				// update frame data
				Vehicle.Frame frame = vehManager.m_vehicles.m_buffer[(int)vehicleId].m_frame0;
				frame.m_rotation = parkedVehRot;

				vehManager.m_vehicles.m_buffer[vehicleId].m_frame0 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame1 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame2 = frame;
				vehManager.m_vehicles.m_buffer[vehicleId].m_frame3 = frame;
				vehicleInfo.m_vehicleAI.FrameDataUpdated(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId], ref frame);

				// update vehicle target position
				vehManager.m_vehicles.m_buffer[vehicleId].m_targetPos0 = new Vector4(vehLanePos.x, vehLanePos.y, vehLanePos.z, 2f);

				// update other fields
				vehManager.m_vehicles.m_buffer[vehicleId].m_flags = (vehManager.m_vehicles.m_buffer[vehicleId].m_flags | Vehicle.Flags.Stopped);
				vehManager.m_vehicles.m_buffer[vehicleId].m_path = instanceData.m_path;
				vehManager.m_vehicles.m_buffer[vehicleId].m_pathPositionIndex = 0;
				vehManager.m_vehicles.m_buffer[vehicleId].m_lastPathOffset = vehLaneOffset;
				vehManager.m_vehicles.m_buffer[vehicleId].m_transferSize = (ushort)(instanceData.m_citizen & 65535u);

				if (! vehicleInfo.m_vehicleAI.TrySpawn(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId])) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.EnterParkedCar: Could not spawn a {vehicleInfo.m_vehicleType} for citizen instance {instanceID}!");
#endif
					return false;
				}
				
				// change instances
				InstanceID parkedVehInstance = InstanceID.Empty;
				parkedVehInstance.ParkedVehicle = parkedVehicleId;
				InstanceID vehInstance = InstanceID.Empty;
				vehInstance.Vehicle = vehicleId;
				Singleton<InstanceManager>.instance.ChangeInstance(parkedVehInstance, vehInstance);

				// set vehicle id for citizen instance
				instanceData.m_path = 0u;
				citManager.m_citizens.m_buffer[instanceData.m_citizen].SetParkedVehicle(instanceData.m_citizen, 0);
				citManager.m_citizens.m_buffer[instanceData.m_citizen].SetVehicle(instanceData.m_citizen, vehicleId, 0u);

				// update citizen instance flags
				instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
				instanceData.m_flags &= ~CitizenInstance.Flags.EnteringVehicle;
				instanceData.m_flags &= ~CitizenInstance.Flags.TryingSpawnVehicle;
				instanceData.m_flags &= ~CitizenInstance.Flags.BoredOfWaiting;
				instanceData.m_waitCounter = 0;

				// unspawn citizen instance
				instanceData.Unspawn(instanceID);

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Citizen instance {instanceID} is now entering vehicle {vehicleId}. Set vehicle target position to {vehLanePos} (segment={vehLanePathPos.m_segment}, lane={vehLanePathPos.m_lane}, offset={vehLanePathPos.m_offset})");
#endif

				return true;
			} else {
				// failed to find a road position
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomHumanAI.EnterParkedCar: Could not find a road position for citizen instance {instanceID} near parked vehicle {parkedVehicleId}!");
#endif
				return false;
			}
		}

		internal static bool CheckReachedParkedCar(ushort instanceID, ref CitizenInstance instanceData) {
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);

			if (instanceData.m_citizen == 0) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomHumanAI.NeedsCarPath: citizen instance {instanceID} is not assigned to a valid citizen!");
#endif
				extInstance.Reset();
				return false;
			}

			bool spawned = ((instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None);

			if (!spawned)
				return false;

			bool walkingToCar = extInstance.PathMode == ExtCitizenInstance.ExtPathMode.WalkingToParkedCar || extInstance.PathMode == ExtCitizenInstance.ExtPathMode.ReachingParkedCar;
			bool walkingToTarget = extInstance.PathMode == ExtCitizenInstance.ExtPathMode.WalkingToTarget ||
				extInstance.PathMode == ExtCitizenInstance.ExtPathMode.PublicTransportToTarget ||
				extInstance.PathMode == ExtCitizenInstance.ExtPathMode.TaxiToTarget;


#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[4] && (walkingToCar || walkingToTarget)) {
				bool? hasParkedVehicle = null;
				if (walkingToCar) {
					hasParkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle != 0;
				}
				Log._Debug($"CustomHumanAI.NeedsCarPath: called for citizen instance {instanceID}. walkingToCar={walkingToCar}, walkingToTarget={walkingToTarget}, spawned={spawned}, hasParkedVehicle={hasParkedVehicle}");
			}*/
#endif

			if (walkingToCar || walkingToTarget) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					if (walkingToCar) {
						ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
						if (parkedVehicleId != 0) {
							instanceData.m_targetPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
							instanceData.m_targetPos.w = 1f;

							float sqrDist = (instanceData.GetLastFramePosition() - (Vector3)instanceData.m_targetPos).sqrMagnitude;

							if (sqrDist >= GlobalConfig.Instance.MaxParkedCarInstanceSwitchSqrDistance) {
								// citizen is too far away from the parked car
								ExtPathMode oldPathMode = extInstance.PathMode;
								if (extInstance.PathMode == ExtPathMode.ReachingParkedCar) {
									if (sqrDist > 2f * extInstance.LastDistanceToParkedCar) {
										// distance has increased dramatically since the last time

#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log.Warning($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} is currently reaching their parked car {parkedVehicleId} but distance increased! dist={sqrDist}, LastDistanceToParkedCar={extInstance.LastDistanceToParkedCar}.");

										if (GlobalConfig.Instance.DebugSwitches[6]) {
											Log._Debug($"CustomHumanAI.NeedsCarPath: FORCED PAUSE. Distance increased! Citizen instance {instanceID}. dist={sqrDist}");
											Singleton<SimulationManager>.instance.SimulationPaused = true;
										}
#endif

										extInstance.PathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
										return true;
									}
								}
#if DEBUG
								else {
									if (GlobalConfig.Instance.DebugSwitches[6] && sqrDist >= 2500) {
										Log._Debug($"CustomHumanAI.NeedsCarPath: FORCED PAUSE. Distance very large! Citizen instance {instanceID}. dist={sqrDist}");
										Singleton<SimulationManager>.instance.SimulationPaused = true;
									}
								}
#endif
								extInstance.LastDistanceToParkedCar = sqrDist;

								extInstance.PathMode = ExtPathMode.ReachingParkedCar;
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4])
									Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} is currently reaching their parked car {parkedVehicleId} (dist={sqrDist}). CurrentDepartureMode={extInstance.PathMode}");
#endif

								return false;
							} else {
								extInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkedCarReached;
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position (dist={sqrDist}). Set targetPos to parked vehicle position. Calculating remaining path now. CurrentDepartureMode={extInstance.PathMode}");
#endif
								return true;
							}
						} else {
							extInstance.Reset();
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position but does not own a parked car. Illegal state! Setting CurrentDepartureMode={extInstance.PathMode}");
#endif
							return false;
						}
					} else {
						extInstance.Reset();
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached target. CurrentDepartureMode={extInstance.PathMode}");
#endif
						return false;
					}
				}
			}

			return false;
		}

		internal static string EnrichLocalizedStatus(string ret, ExtCitizenInstance extInstance) {
			if (extInstance != null) {
				switch (extInstance.PathMode) {
					case ExtPathMode.ReachingParkedCar:
					case ExtPathMode.ParkedCarReached:
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

		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(node) : null;

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var stepWaitTime = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			RoadBaseAI.TrafficLightState pedestrianLightState;
			bool startNode = instance.m_segments.m_buffer[segment].m_startNode == node;
			CustomSegmentLights lights = CustomSegmentLightsManager.Instance.GetSegmentLights(segment, startNode, false);

			if (lights == null || nodeSimulation == null || !nodeSimulation.IsSimulationActive()) {
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if ((pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) && !pedestrians && stepWaitTime >= 196u) {
					RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					return true;
				}
			} else {
				if (lights.InvalidPedestrianLight) {
					pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				} else {
					pedestrianLightState = (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
				}
			}
			// NON-STOCK CODE END //

			switch (pedestrianLightState) {
				case RoadBaseAI.TrafficLightState.RedToGreen:
					if (stepWaitTime < 60u) {
						return false;
					}
					break;
				case RoadBaseAI.TrafficLightState.Red:
				case RoadBaseAI.TrafficLightState.GreenToRed:
					return false;
			}
			return true;
		}

		private void ArriveAtDestination(ushort instanceID, ref CitizenInstance citizenData, bool success) {
			Log.Error($"HumanAI.ArriveAtDestination is not overriden!");
		}

		private void PathfindFailure(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindFailure is not overriden!");
		}

		private void PathfindSuccess(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindSuccess is not overriden!");
		}

		private void Spawn(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.Spawn is not overriden!");
		}

		private void GetBuildingTargetPosition(ushort instanceID, ref CitizenInstance citizenData, float minSqrDistance) {
			Log.Error($"HumanAI.GetBuildingTargetPosition is not overriden!");
		}
	}
}

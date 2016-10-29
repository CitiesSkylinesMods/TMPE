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

namespace TrafficManager.Custom.AI {
	class CustomHumanAI : CitizenAI {
		public void CustomSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
				PathManager instance = Singleton<PathManager>.instance;
				byte pathFindFlags = instance.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

				// NON-STOCK CODE START
				bool pathFindFailed = (pathFindFlags & 8) != 0;
				bool pathFindSucceeded = (pathFindFlags & 4) != 0;

				if (Options.prohibitPocketCars) {
					if (pathFindSucceeded) {
						bool handleSoftPathFindFailure;
						if (!CustomHumanAI.OnPathFindSuccess(instanceID, ref instanceData, out handleSoftPathFindFailure)) {
							if (Options.debugSwitches[1]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
								Log._Debug($"CustomHumanAI.CustomSimulationStep: " + (handleSoftPathFindFailure ? "Soft" : "Hard") + $" path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.CurrentPathMode} parkedVehicleId={parkedVehicleId}");
							}

							if (handleSoftPathFindFailure) {
								instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
								instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
								this.InvalidPath(instanceID, ref instanceData);
								return;
							} else {
								pathFindSucceeded = false;
								pathFindFailed = true;
							}
						}
					} else if (pathFindFailed) {
						ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
						if (CustomHumanAI.OnPathFindFailure(extInstance)) {
							if (Options.debugSwitches[1]) {
								ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
								Log._Debug($"CustomHumanAI.CustomSimulationStep: Handled path-find failure: Citizen instance {instanceID} needs a new path. CurrentPathMode={extInstance.CurrentPathMode} parkedVehicleId={parkedVehicleId}");
							}

							instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
							instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
							this.InvalidPath(instanceID, ref instanceData);
							return;
						}
					}
				}
				// NON-STOCK CODE END

				if (pathFindSucceeded) { // NON-STOCK CODE
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
					this.Spawn(instanceID, ref instanceData);
					instanceData.m_pathPositionIndex = 255;
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					this.PathfindSuccess(instanceID, ref instanceData);
				} else if (pathFindFailed) { // NON-STOCK CODE
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: (Vanilla) Path-finding failed for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
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
				if (CustomHumanAI.NeedsCarPath(instanceID, ref instanceData)) {
					this.InvalidPath(instanceID, ref instanceData);
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
			if (vehicleId == 0 && (instanceData.m_flags & (CitizenInstance.Flags.Character | CitizenInstance.Flags.WaitingPath)) == CitizenInstance.Flags.None) {
				instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
				this.ArriveAtDestination(instanceID, ref instanceData, false);
				citizenManager.ReleaseCitizenInstance(instanceID);
			}
		}

		protected static bool OnPathFindFailure(ExtCitizenInstance extInstance) {
			if (Options.debugSwitches[1])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Path-finding failed for citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.CurrentPathMode}");

			bool ret = false;

			
			switch (extInstance.CurrentPathMode) {
				case PathMode.CalculatingCarPath:
				case PathMode.CalculatingKnownCarPath:
				case PathMode.CalculatingWalkingPathToParkedCar:
					extInstance.CurrentPathMode = PathMode.CalculatingWalkingPathToTarget;
					ret = true;
					break;
				default:
					extInstance.Reset();
					break;
			}

			if (Options.debugSwitches[1])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Setting CurrentPathMode for citizen instance {extInstance.InstanceId} to {extInstance.CurrentPathMode}, ret={ret}");

			return ret;
		}

		internal static bool OnPathFindSuccess(ushort instanceID, ref CitizenInstance instanceData, out bool handleSoftPathFindFailure) {
			handleSoftPathFindFailure = false;
			if (Options.debugSwitches[2])
				Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} vehicle={Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle}");

			if (Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle == 0) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.None) {
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: DepartureMode={extInstance.CurrentPathMode} for citizen instance {instanceID}.");
					if ((CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} contains passenger car section and citizen is walking at the moment. Ensuring that citizen is allowed to use their car.");

						// check if citizen is at an outside connection
						//ushort sourceBuildingId = instanceData.m_sourceBuilding;
						/*if (sourceBuildingId != 0) {
							// check if current building is an outside connection
							bool isAtOutsideConnection = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].Info.m_buildingAI is OutsideConnectionAI;
							if (isAtOutsideConnection) {
								if (Options.debugSwitches[2])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is currently located at outside connection {sourceBuildingId}. Allowing pocket cars.");
								extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.DrivingToTarget;xxx
								return true;
							}
						}*/

						ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
						if (parkedVehicleId != 0) {
							// citizen has to reach their parked vehicle first
							if (Options.debugSwitches[2])
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceID}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");

							extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.RequiresPathToParkedCar;
							extInstance.ParkedVehicleTargetPosition = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;

							handleSoftPathFindFailure = true;
							return false;
						} else {
							if (Options.debugSwitches[1])
								Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} does not have a parked vehicle! Forcing path-finding to fail.");

							return false;
						}
					} else {
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} does not contain passenger car section.");
						extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.WalkingToTarget;
						return true;
					}
				} else if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingWalkingPathToParkedCar) {
					// path to parked vehicle has been calculated
					extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.WalkingToParkedCar;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.CurrentPathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				} else if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingCarPath) {
					// path using passenger car has been calculated
					extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.DrivingToTarget;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car. CurrentDepartureMode={extInstance.CurrentPathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				} else if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingKnownCarPath) {
					// path using passenger car with known parking position has been calculated
					extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.DrivingToKnownParkPos;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car and knows where to park. CurrentDepartureMode={extInstance.CurrentPathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				} else if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget) {
					// final walking path to target has been calculated
					extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.WalkingToTarget;
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by foot to their final target. CurrentDepartureMode={extInstance.CurrentPathMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				}
			}
			return true;
		}

		internal static bool NeedsCarPath(ushort instanceID, ref CitizenInstance instanceData) {
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

			if (instanceData.m_citizen == 0) {
				if (Options.debugSwitches[1])
					Log._Debug($"CustomHumanAI.NeedsCarPath: citizen instance {instanceID} is not assigned to a valid citizen!");
				extInstance.Reset();
				return false;
			}

			bool walkingToCar = extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.WalkingToParkedCar;
			bool walkingToTarget = extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.WalkingToTarget;
			bool spawned = ((instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None);

			if (Options.debugSwitches[4] && (walkingToCar || walkingToTarget)) {
				bool? hasParkedVehicle = null;
				if (walkingToCar) {
					hasParkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle != 0;
				}
				
				Log._Debug($"CustomHumanAI.NeedsCarPath: called for citizen instance {instanceID}. walkingToCar={walkingToCar}, walkingToTarget={walkingToTarget}, spawned={spawned}, hasParkedVehicle={hasParkedVehicle}");
			}

			if (spawned && (walkingToCar || walkingToTarget)) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					if (walkingToCar) {
						ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
						if (parkedVehicleId != 0) {
							extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.ParkedCarReached;
							if (Options.debugSwitches[2])
								Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position. Calculating remaining path now. CurrentDepartureMode={extInstance.CurrentPathMode}");
							return true;
						} else {
							extInstance.Reset();
							if (Options.debugSwitches[1])
								Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached parking position but does not own a parked car. Illegal state! Setting CurrentDepartureMode={extInstance.CurrentPathMode}");
							return false;
						}
					} else {
						extInstance.Reset();
						if (Options.debugSwitches[2])
							Log._Debug($"CustomHumanAI.NeedsCarPath: Citizen instance {instanceID} reached target. CurrentDepartureMode={extInstance.CurrentPathMode}");
						return false;
					}
				}
			}

			return false;
		}

		public bool CustomCheckTrafficLights(ushort node, ushort segment) {
			var nodeSimulation = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance().GetNodeSimulation(node) : null;

			var instance = Singleton<NetManager>.instance;
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			var num = (uint)((node << 8) / 32768);
			var stepWaitTime = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			RoadBaseAI.TrafficLightState pedestrianLightState;
			CustomSegmentLights lights = CustomTrafficLightsManager.Instance().GetSegmentLights(node, segment);

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
	}
}

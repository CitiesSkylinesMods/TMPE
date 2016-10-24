using ColossalFramework;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Manager;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;
using System;

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
						bool handlePathFindFailure;
						if (!CustomHumanAI.OnPathFindSuccess(instanceID, ref instanceData, out handlePathFindFailure)) {
							this.InvalidPath(instanceID, ref instanceData);
							return;
						} else if (handlePathFindFailure) {
							pathFindFailed = true;
							pathFindSucceeded = false;
						}
					} else if (pathFindFailed) {
						CustomHumanAI.OnPathFindFailure(instanceID, ref instanceData);
					}
				}
				// NON-STOCK CODE END

				if (pathFindSucceeded) { // NON-STOCK CODE
					if (Options.debugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path}");
					this.Spawn(instanceID, ref instanceData);
					instanceData.m_pathPositionIndex = 255;
					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
					this.PathfindSuccess(instanceID, ref instanceData);
				} else if (pathFindFailed) { // NON-STOCK CODE
					Log._Debug($"CustomHumanAI.CustomSimulationStep: Path-finding failed for citizen instance {instanceID}. Path: {instanceData.m_path}");
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
				if (CustomHumanAI.ReachedParkingPos(instanceID, ref instanceData)) {
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

		internal static void OnPathFindFailure(ushort instanceID, ref CitizenInstance instanceData) {
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
			extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.None;
			Log._Debug($"CustomHumanAI.OnPathFindFailure: Path-finding failed for citizen instance {instanceID}. Path: {instanceData.m_path}, CurrentDepartureMode={extInstance.CurrentDepartureMode}");
		}

		internal static bool OnPathFindSuccess(ushort instanceID, ref CitizenInstance instanceData, out bool handlePathFindFailure) {
			handlePathFindFailure = false;
			Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path}");

			if (Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle == 0) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.None) {
					Log._Debug($"CustomHumanAI.OnPathFindSuccess: DepartureMode={extInstance.CurrentDepartureMode} for citizen instance {instanceID}.");
					if ((CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes & (byte)(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != 0) {
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} contains passenger car section and citizen is walking at the moment. Ensuring that citizen is allowed to use their car. Flags={extInstance.Flags}");
						extInstance.Flags &= ~ExtCitizenInstance.ExtFlags.CannotUsePassengerCar;

						ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
						if (parkedVehicleId != 0) {
							if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.None) {
								// citizen has to reach their parked vehicle first
								if (Options.debugSwitches[1])
									Log._Debug($"CustomHumanAI.OnPathFindSuccess: Calculating path to reach parked vehicle {parkedVehicleId} for citizen instance {instanceID}. targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");

								extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.CalculatingPathToParkPos;
								Vector3 parkedVehiclePos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
								VehicleInfo parkedVehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

								extInstance.ParkedVehicleTargetPosition = parkedVehiclePos;

								return false;
							}
						} else {
							Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} does not have a parked vehicle! Forcing path-finding to fail.");
							handlePathFindFailure = true;

							return true;
						}
					} else {
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Path for citizen instance {instanceID} does not contain passenger car section. Flags={extInstance.Flags}");
						return true;
					}
				} else if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.CalculatingPathToParkPos) {
					// path to parked vehicle has been calculated
					extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.OnPathToParkPos;
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now on their way to its parked vehicle. CurrentDepartureMode={extInstance.CurrentDepartureMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
					return true;
				} else if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.CalculatingCarPath) {
					// path using passenger car has been calculated
					extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.OnCarPath;
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.OnPathFindSuccess: Citizen instance {instanceID} is now travelling by car. CurrentDepartureMode={extInstance.CurrentDepartureMode}, targetPos={instanceData.m_targetPos} lastFramePos={instanceData.GetLastFramePosition()}");
				}
			}
			return true;
		}

		internal static bool ReachedParkingPos(ushort instanceID, ref CitizenInstance instanceData) {
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
			if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.OnPathToParkPos) {
				// check if path is complete
				PathUnit.Position pos;
				if (instanceData.m_pathPositionIndex != 255 && (instanceData.m_path == 0 || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path].GetPosition(instanceData.m_pathPositionIndex >> 1, out pos))) {
					extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.ParkPosReached;
					if (Options.debugSwitches[1])
						Log._Debug($"CustomHumanAI.ReachedParkingPos: Citizen instance {instanceID} reached parking position. Calculating remaining path now. CurrentDepartureMode={extInstance.CurrentDepartureMode}");

					return true;
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

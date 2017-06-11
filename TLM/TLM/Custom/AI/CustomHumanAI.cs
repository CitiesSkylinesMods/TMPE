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
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	class CustomHumanAI : CitizenAI {
		public void CustomSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
			uint citizenId = instanceData.m_citizen;
			if ((instanceData.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None && (instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
				Singleton<CitizenManager>.instance.ReleaseCitizenInstance(instanceID);
				if (citizenId != 0u) {
					Singleton<CitizenManager>.instance.ReleaseCitizen(citizenId);
				}
				return;
			}

			if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
				PathManager pathManager = Singleton<PathManager>.instance;
				byte pathFindFlags = pathManager.m_pathUnits.m_buffer[instanceData.m_path].m_pathFindFlags;

				// NON-STOCK CODE START
				ExtPathState mainPathState = ExtPathState.Calculating;
				if ((pathFindFlags & PathUnit.FLAG_FAILED) != 0 || instanceData.m_path == 0) {
					mainPathState = ExtPathState.Failed;
				} else if ((pathFindFlags & PathUnit.FLAG_READY) != 0) {
					mainPathState = ExtPathState.Ready;
				}

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path: {instanceData.m_path}, mainPathState={mainPathState}");
#endif

				ExtSoftPathState finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
				if (Options.prohibitPocketCars) {
					finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(instanceID, ref instanceData, ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen], mainPathState);
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Applied Parking AI logic. Path: {instanceData.m_path}, mainPathState={mainPathState}, extCitizenInstance={ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID)}");
#endif
				}

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding succeeded for citizen instance {instanceID}. Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
#endif

				switch (finalPathState) {
					case ExtSoftPathState.Ready:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding succeeded for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
#endif
						this.Spawn(instanceID, ref instanceData);
						instanceData.m_pathPositionIndex = 255;
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
						// NON-STOCK CODE START (transferred from ResidentAI.PathfindSuccess)
						if (citizenId != 0 && (Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags & (Citizen.Flags.Tourist | Citizen.Flags.MovingIn | Citizen.Flags.DummyTraffic)) == Citizen.Flags.MovingIn) {
							StatisticBase statisticBase = Singleton<StatisticsManager>.instance.Acquire<StatisticInt32>(StatisticType.MoveRate);
							statisticBase.Add(1);
						}
						// NON-STOCK CODE END
						this.PathfindSuccess(instanceID, ref instanceData);
						break;
					case ExtSoftPathState.Ignore:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result shall be ignored for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- ignoring");
#endif
						return;
					case ExtSoftPathState.Calculating:
					default:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result undetermined for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- continue");
#endif
						break;
					case ExtSoftPathState.FailedHard:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): HARD path-finding failure for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
#endif
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
						Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
						instanceData.m_path = 0u;
						this.PathfindFailure(instanceID, ref instanceData);
						return;
					case ExtSoftPathState.FailedSoft:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): SOFT path-finding failure for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
#endif
						// path mode has been updated, repeat path-finding
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
						this.InvalidPath(instanceID, ref instanceData);
						return;
				}
				// NON-STOCK CODE END
			}

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				// check if the citizen has reached a parked car or target
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);

				if (extInstance.PathMode == ExtPathMode.WalkingToParkedCar || extInstance.PathMode == ExtPathMode.ReachingParkedCar) {
					ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
					if (parkedVehicleId == 0) {
						// citizen is reaching their parked car but does not own a parked car
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log.Warning($"CustomHumanAI.CustomSimulationStep({instanceID}): Citizen instance {instanceID} was walking to / reaching their parked car ({extInstance.PathMode}) but parked car has disappeared. RESET.");
#endif

						extInstance.Reset();

						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown);
						this.InvalidPath(instanceID, ref instanceData);
						return;
					} else if (AdvancedParkingManager.Instance.CheckCitizenReachedParkedCar(instanceID, ref instanceData, ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId])) {
						// citizen reached their parked car
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Citizen instance {instanceID} arrives at parked car. PathMode={extInstance.PathMode}");
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

					if (extInstance.PathMode == ExtPathMode.ReachingParkedCar) {
						// citizen is currently reaching their parked car
						ReachingParkedCarSimulationStep(instanceID, ref instanceData, physicsLodRefPos);
						return;
					}
				} else if ((extInstance.PathMode == ExtCitizenInstance.ExtPathMode.WalkingToTarget ||
						extInstance.PathMode == ExtCitizenInstance.ExtPathMode.PublicTransportToTarget ||
						extInstance.PathMode == ExtCitizenInstance.ExtPathMode.TaxiToTarget)
				) {
					AdvancedParkingManager.Instance.CheckCitizenReachedTarget(instanceID, ref instanceData);
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
		/// Makes the given citizen instance enter their parked car.
		/// </summary>
		/// <param name="instanceID">Citizen instance id</param>
		/// <param name="instanceData">Citizen instance data</param>
		/// <param name="parkedVehicleId">Parked vehicle id</param>
		/// <param name="vehicleId">Vehicle id</param>
		/// <returns>true if entering the car succeeded, false otherwise</returns>
		public static bool EnterParkedCar(ushort instanceID, ref CitizenInstance instanceData, ushort parkedVehicleId, out ushort vehicleId) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}, ..., {parkedVehicleId}) called.");
#endif
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
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"CustomHumanAI.EnterParkedCar: Determined vehicle position for citizen instance {instanceID}: seg. {vehLanePathPos.m_segment}, lane {vehLanePathPos.m_lane}, off {vehLanePathPos.m_offset} (lane id {vehLaneId})");
#endif

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
			var netManager = Singleton<NetManager>.instance;

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			var num = (uint)((node << 8) / 32768);
			var stepWaitTime = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //
			var nodeSimulation = Options.timedLightsEnabled ? TrafficLightSimulationManager.Instance.GetNodeSimulation(node) : null;
			RoadBaseAI.TrafficLightState pedestrianLightState;
			bool startNode = netManager.m_segments.m_buffer[segment].m_startNode == node;

			CustomSegmentLights lights = null;
			if (nodeSimulation != null && nodeSimulation.IsSimulationActive()) {
				lights = CustomSegmentLightsManager.Instance.GetSegmentLights(segment, startNode, false);
			}

			if (lights == null) {
				// NON-STOCK CODE END //
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

				RoadBaseAI.GetTrafficLightState(node, ref netManager.m_segments.m_buffer[segment], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if ((pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) && !pedestrians && stepWaitTime >= 196u) {
					RoadBaseAI.SetTrafficLightState(node, ref netManager.m_segments.m_buffer[segment], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					return true;
				}
				// NON-STOCK CODE START //
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

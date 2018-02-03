using ColossalFramework;
using TrafficManager.State;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using TrafficManager.Manager;
using UnityEngine;
using TrafficManager.Traffic;
using TrafficManager.Custom.PathFinding;
using System;
using TrafficManager.Util;
using ColossalFramework.Math;
using TrafficManager.UI;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using System.Runtime.CompilerServices;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using TrafficManager.Traffic.Data;
using CSUtil.Commons.Benchmark;

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
				if (GlobalConfig.Instance.Debug.Switches[2])
					Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path: {instanceData.m_path}, mainPathState={mainPathState}");
#endif

				ExtSoftPathState finalPathState = ExtSoftPathState.None;
#if BENCHMARK
				using (var bm = new Benchmark(null, "ConvertPathStateToSoftPathState+UpdateCitizenPathState")) {
#endif
					finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
					if (Options.prohibitPocketCars) {
						finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(instanceID, ref instanceData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[citizenId], ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen], mainPathState);
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Applied Parking AI logic. Path: {instanceData.m_path}, mainPathState={mainPathState}, finalPathState={finalPathState}, extCitizenInstance={ExtCitizenInstanceManager.Instance.ExtInstances[instanceID]}");
#endif
					}
#if BENCHMARK
				}
#endif

				switch (finalPathState) {
					case ExtSoftPathState.Ready:
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding succeeded for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
#endif
						this.Spawn(instanceID, ref instanceData);
						instanceData.m_pathPositionIndex = 255;
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
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
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result shall be ignored for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- ignoring");
#endif
						return;
					case ExtSoftPathState.Calculating:
					default:
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result undetermined for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- continue");
#endif
						break;
					case ExtSoftPathState.FailedHard:
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): HARD path-finding failure for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindFailure");
#endif
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
						Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
						instanceData.m_path = 0u;
						this.PathfindFailure(instanceID, ref instanceData);
						return;
					case ExtSoftPathState.FailedSoft:
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): SOFT path-finding failure for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.InvalidPath");
#endif
						// path mode has been updated, repeat path-finding
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
						this.InvalidPath(instanceID, ref instanceData);
						return;
				}
				// NON-STOCK CODE END
			}

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "ExtSimulationStep")) {
#endif
				if (Options.prohibitPocketCars) {
					if (ExtSimulationStep(instanceID, ref instanceData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], physicsLodRefPos)) {
						return;
					}
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			base.SimulationStep(instanceID, ref instanceData, physicsLodRefPos);

			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;
			ushort vehicleId = 0;
			if (instanceData.m_citizen != 0u) {
				vehicleId = citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle;
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
				CustomArriveAtDestination(instanceID, ref instanceData, false);
				citizenManager.ReleaseCitizenInstance(instanceID);
			}
		}

		internal bool ExtSimulationStep(ushort instanceID, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, Vector3 physicsLodRefPos) {
			// check if the citizen has reached a parked car or target
			if (extInstance.pathMode == ExtPathMode.WalkingToParkedCar || extInstance.pathMode == ExtPathMode.ApproachingParkedCar) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId == 0) {
					// citizen is reaching their parked car but does not own a parked car
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log.Warning($"CustomHumanAI.ExtSimulationStep({instanceID}): Citizen instance {instanceID} was walking to / reaching their parked car ({extInstance.pathMode}) but parked car has disappeared. RESET.");
#endif

					extInstance.Reset();

					instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
					instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
					this.InvalidPath(instanceID, ref instanceData);
					return true;
				} else {
					ParkedCarApproachState approachState = AdvancedParkingManager.Instance.CitizenApproachingParkedCarSimulationStep(instanceID, ref instanceData, ref extInstance, physicsLodRefPos, ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId]);
					switch (approachState) {
						case ParkedCarApproachState.None:
						default:
							break;
						case ParkedCarApproachState.Approaching:
							// citizen approaches their parked car
							return true;
						case ParkedCarApproachState.Approached:
							// citizen reached their parked car
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[4])
									Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Citizen instance {instanceID} arrived at parked car. PathMode={extInstance.pathMode}");
#endif
							if (instanceData.m_path != 0) {
								Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
								instanceData.m_path = 0;
							}
							instanceData.m_flags = instanceData.m_flags & (CitizenInstance.Flags.Created | CitizenInstance.Flags.Cheering | CitizenInstance.Flags.Deleted | CitizenInstance.Flags.Underground | CitizenInstance.Flags.CustomName | CitizenInstance.Flags.Character | CitizenInstance.Flags.BorrowCar | CitizenInstance.Flags.HangAround | CitizenInstance.Flags.InsideBuilding | CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.TryingSpawnVehicle | CitizenInstance.Flags.CannotUseTransport | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.OnPath | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.AtTarget | CitizenInstance.Flags.RequireSlowStart | CitizenInstance.Flags.Transition | CitizenInstance.Flags.RidingBicycle | CitizenInstance.Flags.OnBikeLane | CitizenInstance.Flags.CannotUseTaxi | CitizenInstance.Flags.CustomColor | CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating | CitizenInstance.Flags.TargetFlags);
							if (!this.StartPathFind(instanceID, ref instanceData)) {
								instanceData.Unspawn(instanceID);
								extInstance.Reset();
							}

							return true;
						case ParkedCarApproachState.Failure:
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[2])
									Log._Debug($"CustomHumanAI.ExtSimulationStep({instanceID}): Citizen instance {instanceID} failed to arrive at parked car. PathMode={extInstance.pathMode}");
#endif
							// repeat path-finding
							instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
							instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
							this.InvalidPath(instanceID, ref instanceData);
							return true;

					}
				}
			} else if ((extInstance.pathMode == ExtCitizenInstance.ExtPathMode.WalkingToTarget ||
					extInstance.pathMode == ExtCitizenInstance.ExtPathMode.PublicTransportToTarget ||
					extInstance.pathMode == ExtCitizenInstance.ExtPathMode.TaxiToTarget)
			) {
				AdvancedParkingManager.Instance.CitizenApproachingTargetSimulationStep(instanceID, ref instanceData, ref extInstance);
			}
			return false;
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
			if (GlobalConfig.Instance.Debug.Switches[2])
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
				if (GlobalConfig.Instance.Debug.Switches[2])
					Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}): Could not get first car path position of citizen instance {instanceID}!");
#endif

				vehicleId = 0;
				return false;
			}
			uint vehLaneId = PathManager.GetLaneID(vehLanePathPos);
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[4])
				Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}): Determined vehicle position for citizen instance {instanceID}: seg. {vehLanePathPos.m_segment}, lane {vehLanePathPos.m_lane}, off {vehLanePathPos.m_offset} (lane id {vehLaneId})");
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
					if (GlobalConfig.Instance.Debug.Switches[2])
						Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}): Could not spawn a {vehicleInfo.m_vehicleType} for citizen instance {instanceID}!");
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
				if (GlobalConfig.Instance.Debug.Switches[4])
					Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}): Citizen instance {instanceID} is now entering vehicle {vehicleId}. Set vehicle target position to {vehLanePos} (segment={vehLanePathPos.m_segment}, lane={vehLanePathPos.m_lane}, offset={vehLanePathPos.m_offset})");
#endif

				return true;
			} else {
				// failed to find a road position
#if DEBUG
				if (GlobalConfig.Instance.Debug.Switches[2])
					Log._Debug($"CustomHumanAI.EnterParkedCar({instanceID}): Could not find a road position for citizen instance {instanceID} near parked vehicle {parkedVehicleId}!");
#endif
				return false;
			}
		}

		public bool CustomCheckTrafficLights(ushort nodeId, ushort segmentId) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == nodeId;
#endif

			var netManager = Singleton<NetManager>.instance;

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			var num = (uint)(((int)nodeId << 8) / 32768);
			var stepWaitTime = currentFrameIndex - num & 255u;

			// NON-STOCK CODE START //

			bool customSim = false;
#if BENCHMARK
			using (var bm = new Benchmark(null, "GetNodeSimulation")) {
#endif
				customSim = Options.timedLightsEnabled && TrafficLightSimulationManager.Instance.HasActiveSimulation(nodeId);
#if BENCHMARK
			}
#endif
			RoadBaseAI.TrafficLightState pedestrianLightState;
			bool startNode = netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId;

			ICustomSegmentLights lights = null;
#if BENCHMARK
			using (var bm = new Benchmark(null, "GetSegmentLights")) {
#endif
				if (customSim) {
					lights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, startNode, false);
				}
#if BENCHMARK
			}
#endif

			if (lights == null) {
				// NON-STOCK CODE END //
				RoadBaseAI.TrafficLightState vehicleLightState;
				bool vehicles;
				bool pedestrians;

#if DEBUGTTL
				if (debug) {
					Log._Debug($"CustomHumanAI.CustomCheckTrafficLights({nodeId}, {segmentId}): No custom simulation!");
				}
#endif

				RoadBaseAI.GetTrafficLightState(nodeId, ref netManager.m_segments.m_buffer[segmentId], currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if (pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) {
					if (!pedestrians && stepWaitTime >= 196u) {
						RoadBaseAI.SetTrafficLightState(nodeId, ref netManager.m_segments.m_buffer[segmentId], currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, true);
					}
					return false;
				}
				// NON-STOCK CODE START //
			} else {


				if (lights.InvalidPedestrianLight) {
					pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
				} else {
					pedestrianLightState = (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
				}

#if DEBUGTTL
				if (debug) {
					Log._Debug($"CustomHumanAI.CustomCheckTrafficLights({nodeId}, {segmentId}): Custom simulation! pedestrianLightState={pedestrianLightState}, lights.InvalidPedestrianLight={lights.InvalidPedestrianLight}");
				}
#endif
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

		protected void CustomArriveAtDestination(ushort instanceID, ref CitizenInstance citizenData, bool success) {
			uint citizenId = citizenData.m_citizen;
			if (citizenId != 0) {
				CitizenManager citizenMan = Singleton<CitizenManager>.instance;
				citizenMan.m_citizens.m_buffer[citizenId].SetVehicle(citizenId, 0, 0u);
				if (success) {
					citizenMan.m_citizens.m_buffer[citizenId].SetLocationByBuilding(citizenId, citizenData.m_targetBuilding);
					// NON-STOCK CODE START
					Constants.ManagerFactory.ExtCitizenManager.OnArriveAtDestination(citizenId, ref citizenMan.m_citizens.m_buffer[citizenId]);
					// NON-STOCK CODE END
				}

				if (citizenData.m_targetBuilding != 0 && citizenMan.m_citizens.m_buffer[citizenId].CurrentLocation == Citizen.Location.Visit) {
					BuildingManager buildingMan = Singleton<BuildingManager>.instance;
					BuildingInfo info = buildingMan.m_buildings.m_buffer[citizenData.m_targetBuilding].Info;
					int amount = -100;
					info.m_buildingAI.ModifyMaterialBuffer(citizenData.m_targetBuilding, ref buildingMan.m_buildings.m_buffer[citizenData.m_targetBuilding], TransferManager.TransferReason.Shopping, ref amount);
					if (info.m_class.m_service == ItemClass.Service.Beautification) {
						StatisticsManager statsMan = Singleton<StatisticsManager>.instance;
						StatisticBase stats = statsMan.Acquire<StatisticInt32>(StatisticType.ParkVisitCount);
						stats.Add(1);
					}

					ushort eventIndex = buildingMan.m_buildings.m_buffer[citizenData.m_targetBuilding].m_eventIndex;
					if (eventIndex != 0) {
						EventManager instance4 = Singleton<EventManager>.instance;
						EventInfo info2 = instance4.m_events.m_buffer[eventIndex].Info;
						info2.m_eventAI.VisitorEnter(eventIndex, ref instance4.m_events.m_buffer[eventIndex], citizenData.m_targetBuilding, citizenId);
					}
				}
			}
			if ((citizenData.m_flags & CitizenInstance.Flags.HangAround) != 0 && success) {
				return;
			}
			((CitizenAI)this).SetSource(instanceID, ref citizenData, (ushort)0);
			((CitizenAI)this).SetTarget(instanceID, ref citizenData, (ushort)0);
			citizenData.Unspawn(instanceID);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PathfindFailure(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindFailure is not overriden!");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PathfindSuccess(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindSuccess is not overriden!");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Spawn(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.Spawn is not overriden!");
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void GetBuildingTargetPosition(ushort instanceID, ref CitizenInstance citizenData, float minSqrDistance) {
			Log.Error($"HumanAI.GetBuildingTargetPosition is not overriden!");
		}
	}
}

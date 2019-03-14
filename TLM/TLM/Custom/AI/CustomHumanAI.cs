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
using TrafficManager.Traffic.Enums;
using TrafficManager.RedirectionFramework.Attributes;

namespace TrafficManager.Custom.AI {
	[TargetType(typeof(HumanAI))]
	public class CustomHumanAI : CitizenAI {
		[RedirectMethod]
		public void CustomSimulationStep(ushort instanceID, ref CitizenInstance instanceData, Vector3 physicsLodRefPos) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			CitizenManager citizenManager = Singleton<CitizenManager>.instance;

			uint citizenId = instanceData.m_citizen;
			if ((instanceData.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None && (instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
				citizenManager.ReleaseCitizenInstance(instanceID);
				if (citizenId != 0u) {
					citizenManager.ReleaseCitizen(citizenId);
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
				if (debug)
					Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path: {instanceData.m_path}, mainPathState={mainPathState}");
#endif

				ExtSoftPathState finalPathState = ExtSoftPathState.None;
#if BENCHMARK
				using (var bm = new Benchmark(null, "ConvertPathStateToSoftPathState+UpdateCitizenPathState")) {
#endif
					finalPathState = ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
					if (Options.parkingAI) {
						finalPathState = AdvancedParkingManager.Instance.UpdateCitizenPathState(instanceID, ref instanceData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[citizenId], ref citizenManager.m_citizens.m_buffer[instanceData.m_citizen], mainPathState);
#if DEBUG
						if (debug)
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Applied Parking AI logic. Path: {instanceData.m_path}, mainPathState={mainPathState}, finalPathState={finalPathState}, extCitizenInstance={ExtCitizenInstanceManager.Instance.ExtInstances[instanceID]}");
#endif
					}
#if BENCHMARK
				}
#endif

				switch (finalPathState) {
					case ExtSoftPathState.Ready:
#if DEBUG
						if (debug)
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding succeeded for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- calling HumanAI.PathfindSuccess");
#endif
						if (citizenId == 0 || citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_vehicle == 0) {
							this.Spawn(instanceID, ref instanceData);
						}
						instanceData.m_pathPositionIndex = 255;
						instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
						instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
						// NON-STOCK CODE START (transferred from ResidentAI.PathfindSuccess)
						if (citizenId != 0 && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & (Citizen.Flags.Tourist | Citizen.Flags.MovingIn | Citizen.Flags.DummyTraffic)) == Citizen.Flags.MovingIn) {
							StatisticBase statisticBase = Singleton<StatisticsManager>.instance.Acquire<StatisticInt32>(StatisticType.MoveRate);
							statisticBase.Add(1);
						}
						// NON-STOCK CODE END
						this.PathfindSuccess(instanceID, ref instanceData);
						break;
					case ExtSoftPathState.Ignore:
#if DEBUG
						if (debug)
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result shall be ignored for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- ignoring");
#endif
						return;
					case ExtSoftPathState.Calculating:
					default:
#if DEBUG
						if (debug)
							Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Path-finding result undetermined for citizen instance {instanceID} (finalPathState={finalPathState}). Path: {instanceData.m_path} -- continue");
#endif
						break;
					case ExtSoftPathState.FailedHard:
#if DEBUG
						if (debug)
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
						if (debug)
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
				if (Options.parkingAI) {
					if (ExtSimulationStep(instanceID, ref instanceData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], physicsLodRefPos)) {
						return;
					}
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			base.SimulationStep(instanceID, ref instanceData, physicsLodRefPos);

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

		public bool ExtSimulationStep(ushort instanceID, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, Vector3 physicsLodRefPos) {
			IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			// check if the citizen has reached a parked car or target
			if (extInstance.pathMode == ExtPathMode.WalkingToParkedCar || extInstance.pathMode == ExtPathMode.ApproachingParkedCar) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId == 0) {
					// citizen is reaching their parked car but does not own a parked car
#if DEBUG
						if (debug)
							Log.Warning($"CustomHumanAI.ExtSimulationStep({instanceID}): Citizen instance {instanceID} was walking to / reaching their parked car ({extInstance.pathMode}) but parked car has disappeared. RESET.");
#endif

					extCitInstMan.Reset(ref extInstance);

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
								if (fineDebug)
									Log._Debug($"CustomHumanAI.CustomSimulationStep({instanceID}): Citizen instance {instanceID} arrived at parked car. PathMode={extInstance.pathMode}");
#endif
							if (instanceData.m_path != 0) {
								Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
								instanceData.m_path = 0;
							}
							instanceData.m_flags = instanceData.m_flags & (CitizenInstance.Flags.Created | CitizenInstance.Flags.Cheering | CitizenInstance.Flags.Deleted | CitizenInstance.Flags.Underground | CitizenInstance.Flags.CustomName | CitizenInstance.Flags.Character | CitizenInstance.Flags.BorrowCar | CitizenInstance.Flags.HangAround | CitizenInstance.Flags.InsideBuilding | CitizenInstance.Flags.WaitingPath | CitizenInstance.Flags.TryingSpawnVehicle | CitizenInstance.Flags.CannotUseTransport | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.OnPath | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.AtTarget | CitizenInstance.Flags.RequireSlowStart | CitizenInstance.Flags.Transition | CitizenInstance.Flags.RidingBicycle | CitizenInstance.Flags.OnBikeLane | CitizenInstance.Flags.CannotUseTaxi | CitizenInstance.Flags.CustomColor | CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating | CitizenInstance.Flags.TargetFlags);
							if (!this.StartPathFind(instanceID, ref instanceData)) {
								instanceData.Unspawn(instanceID);
								extCitInstMan.Reset(ref extInstance);
							}

							return true;
						case ParkedCarApproachState.Failure:
#if DEBUG
								if (debug)
									Log._Debug($"CustomHumanAI.ExtSimulationStep({instanceID}): Citizen instance {instanceID} failed to arrive at parked car. PathMode={extInstance.pathMode}");
#endif
							// repeat path-finding
							instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
							instanceData.m_flags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.Panicking | CitizenInstance.Flags.SittingDown | CitizenInstance.Flags.Cheering);
							this.InvalidPath(instanceID, ref instanceData);
							return true;

					}
				}
			} else if (extInstance.pathMode == ExtPathMode.WalkingToTarget ||
					extInstance.pathMode == ExtPathMode.TaxiToTarget
			) {
				AdvancedParkingManager.Instance.CitizenApproachingTargetSimulationStep(instanceID, ref instanceData, ref extInstance);
			}
			return false;
		}
		
		[RedirectMethod]
		public bool CustomCheckTrafficLights(ushort nodeId, ushort segmentId) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == nodeId;
#endif

			var netManager = Singleton<NetManager>.instance;

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
			uint simGroup = (uint)nodeId >> 7;
			var stepWaitTime = currentFrameIndex - simGroup & 255u;

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

				RoadBaseAI.GetTrafficLightState(nodeId, ref netManager.m_segments.m_buffer[segmentId], currentFrameIndex - simGroup, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians);
				if (pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed || pedestrianLightState ==  RoadBaseAI.TrafficLightState.Red) {
					if (!pedestrians && stepWaitTime >= 196u) {
						RoadBaseAI.SetTrafficLightState(nodeId, ref netManager.m_segments.m_buffer[segmentId], currentFrameIndex - simGroup, vehicleLightState, pedestrianLightState, vehicles, true);
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

		[RedirectMethod]
		protected void CustomArriveAtDestination(ushort instanceID, ref CitizenInstance citizenData, bool success) {
			uint citizenId = citizenData.m_citizen;
			if (citizenId != 0) {
				CitizenManager citizenMan = Singleton<CitizenManager>.instance;
				citizenMan.m_citizens.m_buffer[citizenId].SetVehicle(citizenId, 0, 0u);

				if ((citizenData.m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None) {
					if (success) {
						ushort targetBuildingId = citizenData.m_targetBuilding;
						if (targetBuildingId != 0) {
							ushort transportLineId = Singleton<NetManager>.instance.m_nodes.m_buffer[targetBuildingId].m_transportLine;
							if (transportLineId != 0) {
								TransportInfo info = Singleton<TransportManager>.instance.m_lines.m_buffer[transportLineId].Info;
								if (info.m_vehicleType == VehicleInfo.VehicleType.None) {
									targetBuildingId = (((instanceID & 1) != 0) ? TransportLine.GetPrevStop(targetBuildingId) : TransportLine.GetNextStop(targetBuildingId));
									if (targetBuildingId != 0) {
										citizenData.m_flags |= CitizenInstance.Flags.OnTour;
										((CitizenAI)this).SetTarget(instanceID, ref citizenData, targetBuildingId, true);
									} else {
										// Unrolled goto statement
										if ((citizenData.m_flags & CitizenInstance.Flags.HangAround) != 0 && success) {
											return;
										}
										((CitizenAI)this).SetSource(instanceID, ref citizenData, (ushort)0);
										((CitizenAI)this).SetTarget(instanceID, ref citizenData, (ushort)0);
										citizenData.Unspawn(instanceID);
									}
									return;
								}
								citizenData.m_flags |= CitizenInstance.Flags.OnTour;
								this.WaitTouristVehicle(instanceID, ref citizenData, targetBuildingId);
								return;
							}
						}
					}
				} else {
					if (success) {
						citizenMan.m_citizens.m_buffer[citizenId].SetLocationByBuilding(citizenId, citizenData.m_targetBuilding);
						// NON-STOCK CODE START
						Constants.ManagerFactory.ExtCitizenManager.OnArriveAtDestination(citizenId, ref citizenMan.m_citizens.m_buffer[citizenId], ref citizenMan.m_instances.m_buffer[instanceID]);
						// NON-STOCK CODE END
					}

					if (citizenData.m_targetBuilding != 0 && citizenMan.m_citizens.m_buffer[citizenId].CurrentLocation == Citizen.Location.Visit) {
						BuildingManager buildingMan = Singleton<BuildingManager>.instance;
						BuildingInfo info = buildingMan.m_buildings.m_buffer[citizenData.m_targetBuilding].Info;
						info.m_buildingAI.VisitorEnter(citizenData.m_targetBuilding, ref buildingMan.m_buildings.m_buffer[citizenData.m_targetBuilding], citizenId);
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

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PathfindFailure(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindFailure is not overriden!");
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void PathfindSuccess(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.PathfindSuccess is not overriden!");
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Spawn(ushort instanceID, ref CitizenInstance data) {
			Log.Error($"HumanAI.Spawn is not overriden!");
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void GetBuildingTargetPosition(ushort instanceID, ref CitizenInstance data, float minSqrDistance) {
			Log.Error($"HumanAI.GetBuildingTargetPosition is not overriden!");
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void WaitTouristVehicle(ushort instanceID, ref CitizenInstance data, ushort targetBuildingId) {
			Log.Error($"HumanAI.InvokeWaitTouristVehicle is not overriden!");
		}
	}
}

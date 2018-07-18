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
using TrafficManager.Traffic.Data;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using CSUtil.Commons.Benchmark;
using static TrafficManager.Custom.PathFinding.CustomPathManager;

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	public class CustomCitizenAI : CitizenAI {

		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo, bool enableTransport, bool ignoreCost) {
			return ExtStartPathFind(instanceID, ref citizenData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[Singleton<CitizenManager>.instance.m_instances.m_buffer[instanceID].m_citizen], startPos, endPos, vehicleInfo, enableTransport, ignoreCost);
		}

		public bool ExtStartPathFind(ushort instanceID, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo, bool enableTransport, bool ignoreCost) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log.Warning($"CustomCitizenAI.ExtStartPathFind({instanceID}): called for citizen {instanceData.m_citizen}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding}, pathMode={extInstance.pathMode}, enableTransport={enableTransport}, ignoreCost={ignoreCost}");
#endif

			// NON-STOCK CODE START
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort parkedVehicleId = citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
			ushort homeId = citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;
			CarUsagePolicy carUsageMode = CarUsagePolicy.Allowed;

#if BENCHMARK
			using (var bm = new Benchmark(null, "ParkingAI.Preparation")) {
#endif
			if (Options.prohibitPocketCars) {
				switch (extInstance.pathMode) {
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
					case ExtPathMode.ApproachingParkedCar:
						if (parkedVehicleId == 0) {
							/*
							 * Parked vehicle not present but citizen wants to reach it
							 * -> Reset path mode
							 */
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

							extInstance.Reset();
						} else {
							/*
							 * Parked vehicle is present and citizen wants to reach it
							 * -> Prohibit car usage
							 */
#if DEBUG
							if (fineDebug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'CalculatingWalkingPathToParkedCar'.");
#endif
							extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
							carUsageMode = CarUsagePolicy.Forbidden;
						}
						break;
					case ExtPathMode.RequiresWalkingPathToTarget:
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.WalkingToTarget:
						/*
						 * Citizen walks to target
						 * -> Reset path mode
						 */
#if DEBUG
						if (fineDebug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'CalculatingWalkingPathToTarget'.");
#endif
						extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
						carUsageMode = CarUsagePolicy.Forbidden;
						break;
					case ExtPathMode.RequiresCarPath:
					case ExtPathMode.DrivingToTarget:
					case ExtPathMode.DrivingToKnownParkPos:
					case ExtPathMode.DrivingToAltParkPos:
					case ExtPathMode.CalculatingCarPathToAltParkPos:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.CalculatingCarPathToTarget:
						if (parkedVehicleId == 0) {
							/*
							 * Citizen wants to drive to target but parked vehicle is not present
							 * -> Reset path mode
							 */

#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

							extInstance.Reset();
						} else {
							/*
							 * Citizen wants to drive to target and parked vehicle is present
							 * -> Force parked car usage
							 */

#if DEBUG
							if (fineDebug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'RequiresCarPath'.");
#endif

							extInstance.pathMode = ExtPathMode.RequiresCarPath;
							carUsageMode = CarUsagePolicy.ForcedParked;
							startPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position; // force to start from the parked car
						}
						break;
					default:
#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'None'.");
#endif
						extInstance.Reset();
						break;
				}

				if (extInstance.pathMode == ExtPathMode.None) {
					if ((instanceData.m_flags & CitizenInstance.Flags.OnTour) != CitizenInstance.Flags.None || ignoreCost) {
						/*
						 * Citizen is on a walking tour or is a mascot
						 * -> Prohibit car usage
						 */
#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen ignores cost ({ignoreCost}) or is on a walking tour ({(instanceData.m_flags & CitizenInstance.Flags.OnTour) != CitizenInstance.Flags.None}): Setting path mode to 'CalculatingWalkingPathToTarget'");
#endif
						carUsageMode = CarUsagePolicy.Forbidden;
						extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					} else {
						/*
						 * Citizen is not on a walking tour and is not a mascot
						 * -> Check if citizen is located at an outside connection and make them obey Parking AI restrictions
						 */

						if (instanceData.m_sourceBuilding != 0) {
							ItemClass.Service sourceBuildingService = Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].Info.m_class.m_service;

							if (Constants.ManagerFactory.ExtCitizenInstanceManager.IsAtOutsideConnection(instanceID, ref instanceData, ref citizenManager.m_citizens.m_buffer[instanceData.m_citizen])) {
								if (sourceBuildingService == ItemClass.Service.Road) {
									if (vehicleInfo != null) {
										/*
										 * Citizen is located at a road outside connection and can spawn a car
										 * -> Force car usage
										 */
#if DEBUG
										if (debug)
											Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a road outside connection: Setting path mode to 'RequiresCarPath' and carUsageMode to 'ForcedPocket'");
#endif
										extInstance.pathMode = ExtPathMode.RequiresCarPath;
										carUsageMode = CarUsagePolicy.ForcedPocket;
									} else {
										/*
										 * Citizen is located at a non-road outside connection and cannot spawn a car
										 * -> Path-finding fails
										 */
#if DEBUG
										if (debug)
											Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a road outside connection but does not have a car template: ABORTING PATH-FINDING");
#endif
										return false;
									}
								} else {
									/*
									 * Citizen is located at a non-road outside connection
									 * -> Prohibit car usage
									 */
#if DEBUG
									if (debug)
										Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a non-road outside connection: Setting path mode to 'CalculatingWalkingPathToTarget'");
#endif
									extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
									carUsageMode = CarUsagePolicy.Forbidden;
								}
							}
						}
					}
				}

				if ((carUsageMode == CarUsagePolicy.Allowed || carUsageMode == CarUsagePolicy.ForcedParked) && parkedVehicleId != 0) {
					/*
					* Reuse parked vehicle info
					*/
					vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

					/*
					 * Check if the citizen should return their car back home
					 */
					if (extInstance.pathMode == ExtPathMode.None && // initiating a new path
						homeId != 0 && // home building present
						instanceData.m_targetBuilding == homeId // current target is home
					) {
						/*
						 * citizen travels back home
						 * -> check if their car should be returned
						 */
						if ((extCitizen.lastTransportMode & ExtCitizen.ExtTransportMode.Car) != ExtCitizen.ExtTransportMode.None) {
							/*
							 * citizen travelled by car
							 * -> return car back home
							 */
							extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar;
							carUsageMode = CarUsagePolicy.Forbidden;

#if DEBUG
							if (fineDebug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen used their car before and is not at home. Forcing to walk to parked car.");
#endif
						} else {
							/*
							 * citizen travelled by other means of transport
							 * -> check distance between home and parked car. if too far away: force to take the car back home
							 */
							float distHomeToParked = (Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position).magnitude;

							if (distHomeToParked > GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome) {
								/*
								 * force to take car back home
								 */
								extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar;
								carUsageMode = CarUsagePolicy.Forbidden;

#if DEBUG
								if (fineDebug)
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen wants to go home and parked car is too far away ({distHomeToParked}). Forcing walking to parked car.");
#endif
							}
						}
					}
				}

				/*
				 * The following holds:
				 * - pathMode is now either CalculatingWalkingPathToParkedCar, CalculatingWalkingPathToTarget, RequiresCarPath or None.
				 * - if pathMode is CalculatingWalkingPathToParkedCar or RequiresCarPath: parked car is present and citizen is not on a walking tour
				 * - carUsageMode is valid
				 * - if pathMode is RequiresCarPath: carUsageMode is either ForcedParked or ForcedPocket
				 */

				/*
				 * modify path-finding constraints (vehicleInfo, endPos) if citizen is forced to walk
				 */
				if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar || extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget) {
					/*
					 * vehicle must not be used since we need a walking path to either
					 * 1. a parked car or
					 * 2. the target building
					 */
					
					if (extInstance.pathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
						/*
						 * walk to parked car
						 * -> end position is parked car
						 */
						endPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
#if DEBUG
						if (fineDebug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen shall go to parked vehicle @ {endPos}");
#endif
					}
				}
			}
#if BENCHMARK
			}
#endif
#if DEBUG
			if (fineDebug)
				Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is allowed to drive their car? {carUsageMode}");
#endif
			// NON-STOCK CODE END

			/*
			 * semi-stock code: determine path-finding parameters (laneTypes, vehicleTypes, extVehicleType, etc.)
			 */
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			bool combustionEngine = false;
			ExtVehicleType extVehicleType = ExtVehicleType.None;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						SimulationManager instance = Singleton<SimulationManager>.instance;
						if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
							laneTypes |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
							vehicleTypes |= vehicleInfo.m_vehicleType;
							extVehicleType = ExtVehicleType.Taxi; // NON-STOCK CODE
																  // NON-STOCK CODE START
							if (Options.prohibitPocketCars) {
								extInstance.pathMode = ExtPathMode.TaxiToTarget;
							}
							// NON-STOCK CODE END
						}
					}
				} else
				// NON-STOCK CODE START
				if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
					if (carUsageMode != CarUsagePolicy.Forbidden) {
						extVehicleType = ExtVehicleType.PassengerCar;
						laneTypes |= NetInfo.LaneType.Vehicle;
						vehicleTypes |= vehicleInfo.m_vehicleType;
						combustionEngine = vehicleInfo.m_class.m_subService == ItemClass.SubService.ResidentialLow;
					}
				} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
					extVehicleType = ExtVehicleType.Bicycle;
					laneTypes |= NetInfo.LaneType.Vehicle;
					vehicleTypes |= vehicleInfo.m_vehicleType;
				}
				// NON-STOCK CODE END
			}

			// NON-STOCK CODE START
			ExtPathType extPathType = ExtPathType.None;
			PathUnit.Position endPosA = default(PathUnit.Position);
			bool calculateEndPos = true;
			bool allowRandomParking = true;
#if BENCHMARK
			using (var bm = new Benchmark(null, "ParkingAI.Main")) {
#endif
			if (Options.prohibitPocketCars) {
				// Parking AI

				if (extInstance.pathMode == ExtCitizenInstance.ExtPathMode.RequiresCarPath) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Setting startPos={startPos} for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode}");
#endif

					if (
						instanceData.m_targetBuilding == 0 ||
						(Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None
					) {
						/*
						 * the citizen is starting their journey and the target is not an outside connection
						 * -> find a suitable parking space near the target
						 */

#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding parking space at target for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode} parkedVehicleId={parkedVehicleId}");
#endif

						// find a parking space in the vicinity of the target
						bool calcEndPos;
						Vector3 parkPos;
						if (
							AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(endPos, vehicleInfo, ref extInstance, homeId, instanceData.m_targetBuilding == homeId, 0, false, out parkPos, ref endPosA, out calcEndPos) &&
							extInstance.CalculateReturnPath(parkPos, endPos)
						) {
							// success
							extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos;
							calculateEndPos = calcEndPos; // if true, the end path position still needs to be calculated
							allowRandomParking = false; // find a direct path to the calculated parking position
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding known parking space for citizen instance {instanceID}, parked vehicle {parkedVehicleId} succeeded and return path {extInstance.returnPathId} ({extInstance.returnPathState}) is calculating. PathMode={extInstance.pathMode}");
#endif
							/*if (! extInstance.CalculateReturnPath(parkPos, endPos)) {
								// TODO retry?
								if (debug)
									Log._Debug($"CustomCitizenAI.CustomStartPathFind: [PFFAIL] Could not calculate return path for citizen instance {instanceID}, parked vehicle {parkedVehicleId}. Calling OnPathFindFailed.");
								CustomHumanAI.OnPathFindFailure(extInstance);
								return false;
							}*/
						}
					}

					if (extInstance.pathMode == ExtPathMode.RequiresCarPath) {
						/*
						 * no known parking space found (pathMode has not been updated in the block above)
						 * -> calculate direct path to target
						 */
#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance {instanceID} is still at CurrentPathMode={extInstance.pathMode} (no parking space found?). Setting it to CalculatingCarPath. parkedVehicleId={parkedVehicleId}");
#endif
						extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget;
					}
				}

				/*
				 * determine path type from path mode
				 */
				extPathType = extInstance.GetPathType();

				/*
				 * the following holds:
				 * - pathMode is now either CalculatingWalkingPathToParkedCar, CalculatingWalkingPathToTarget, CalculatingCarPathToTarget, CalculatingCarPathToKnownParkPos or None.
				 */
			}
#if BENCHMARK
			}
#endif

			/*
			 * enable random parking if exact parking space was not calculated yet
			 */
			if (extVehicleType == ExtVehicleType.PassengerCar || extVehicleType == ExtVehicleType.Bicycle) {
				if (allowRandomParking &&
					instanceData.m_targetBuilding != 0 &&
					(
						Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office ||
						(instanceData.m_flags & CitizenInstance.Flags.TargetIsNode) != 0
					)) {
					randomParking = true;
				}
			}
			// NON-STOCK CODE END

			/*
			 * determine the path position of the parked vehicle
			 */
			PathUnit.Position parkedVehiclePathPos = default(PathUnit.Position);
			if (parkedVehicleId != 0 && extVehicleType == ExtVehicleType.PassengerCar) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
				CustomPathManager.FindPathPositionWithSpiralLoop(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, out parkedVehiclePathPos);
			}
			bool allowUnderground = (instanceData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

#if DEBUG
			if (debug)
				Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Requesting path-finding for citizen instance {instanceID}, citizen {instanceData.m_citizen}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding} pathMode={extInstance.pathMode}");
#endif

			/*
			 * determine start & end path positions
			 */
			bool foundEndPos = !calculateEndPos || FindPathPosition(instanceID, ref instanceData, endPos, Options.prohibitPocketCars && (instanceData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : laneTypes, vehicleTypes, false, out endPosA); // NON-STOCK CODE: with Parking AI enabled, the end position must be a pedestrian position
			bool foundStartPos = false;
			PathUnit.Position startPosA;

			if (Options.prohibitPocketCars && (extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget || extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos)) {
				/*
				 * citizen will enter their car now
				 * -> find a road start position
				 */
				foundStartPos = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, laneTypes & ~NetInfo.LaneType.Pedestrian, vehicleTypes, allowUnderground, false, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, out startPosA);
			} else {
				foundStartPos = FindPathPosition(instanceID, ref instanceData, startPos, laneTypes, vehicleTypes, allowUnderground, out startPosA);
			}

			/*
			 * start path-finding
			 */
			if (foundStartPos && // TODO probably fails if vehicle is parked too far away from road
				foundEndPos // NON-STOCK CODE
				) {

				if (enableTransport) {
					/*
					 * public transport usage is allowed for this path
					 */
					if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
						/*
						* citizen may use public transport
						*/
						laneTypes |= NetInfo.LaneType.PublicTransport;

						uint citizenId = instanceData.m_citizen;
						if (citizenId != 0u && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
							laneTypes |= NetInfo.LaneType.EvacuationTransport;
						}
					} else if (Options.prohibitPocketCars) { // TODO check for incoming connection
						/*
						* citizen tried to use public transport but waiting time was too long
						* -> add public transport demand for source building
						*/
						if (instanceData.m_sourceBuilding != 0) {
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance {instanceID} cannot uses public transport from building {instanceData.m_sourceBuilding} to {instanceData.m_targetBuilding}. Incrementing public transport demand.");
#endif
							ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].AddPublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandWaitingIncrement, true);
						}
					}
				}

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint path;
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = extPathType;
				args.extVehicleType = extVehicleType;
				args.vehicleId = 0;
				args.spawned = (instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = dummyPathPos;
				args.endPosA = endPosA;
				args.endPosB = dummyPathPos;
				args.vehiclePosition = parkedVehiclePathPos;
				args.laneTypes = laneTypes;
				args.vehicleTypes = vehicleTypes;
				args.maxLength = 20000f;
				args.isHeavyVehicle = false;
				args.hasCombustionEngine = combustionEngine;
				args.ignoreBlocked = false;
				args.ignoreFlooded = false;
				args.ignoreCosts = ignoreCost;
				args.randomParking = randomParking;
				args.stablePath = false;
				args.skipQueue = false;

				if ((instanceData.m_flags & CitizenInstance.Flags.OnTour) != 0) {
					args.stablePath = true;
					args.maxLength = 160000f;
					//args.laneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				} else {
					args.stablePath = false;
					args.maxLength = 20000f;
				}

				bool res = CustomPathManager._instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args);
				// NON-STOCK CODE END

				if (res) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleTypes}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={parkedVehiclePathPos.m_segment}, vehiclePos.m_lane={parkedVehiclePathPos.m_lane}, vehiclePos.m_offset={parkedVehiclePathPos.m_offset}");
#endif

					if (instanceData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
					}
					instanceData.m_path = path;
					instanceData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}

#if DEBUG
			if (Options.prohibitPocketCars) {
				if (debug)
					Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): CustomCitizenAI.CustomStartPathFind: [PFFAIL] failed for citizen instance {instanceID} (CurrentPathMode={extInstance.pathMode}). startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, startPosA.offset={startPosA.m_offset}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, endPosA.offset={endPosA.m_offset}, foundStartPos={foundStartPos}, foundEndPos={foundEndPos}");
			}
#endif
			return false;
		}

		public bool CustomFindPathPosition(ushort instanceID, ref CitizenInstance citizenData, Vector3 pos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, bool allowUnderground, out PathUnit.Position position) {
			position = default(PathUnit.Position);
			float minDist = 1E+10f;
			PathUnit.Position posA;
			PathUnit.Position posB;
			float distA;
			float distB;
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Road, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Beautification, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None && PathManager.FindPathPosition(pos, ItemClass.Service.PublicTransport, laneTypes, vehicleTypes, allowUnderground, false, Options.prohibitPocketCars ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			return position.m_segment != 0;
		}

		// stock code
		internal static Citizen.AgeGroup GetAgeGroup(Citizen.AgePhase agePhase) {
			switch (agePhase) {
				case Citizen.AgePhase.Child:
					return Citizen.AgeGroup.Child;
				case Citizen.AgePhase.Teen0:
				case Citizen.AgePhase.Teen1:
					return Citizen.AgeGroup.Teen;
				case Citizen.AgePhase.Young0:
				case Citizen.AgePhase.Young1:
				case Citizen.AgePhase.Young2:
					return Citizen.AgeGroup.Young;
				case Citizen.AgePhase.Adult0:
				case Citizen.AgePhase.Adult1:
				case Citizen.AgePhase.Adult2:
				case Citizen.AgePhase.Adult3:
					return Citizen.AgeGroup.Adult;
				case Citizen.AgePhase.Senior0:
				case Citizen.AgePhase.Senior1:
				case Citizen.AgePhase.Senior2:
				case Citizen.AgePhase.Senior3:
					return Citizen.AgeGroup.Senior;
				default:
					return Citizen.AgeGroup.Adult;
			}
		}
	}
}

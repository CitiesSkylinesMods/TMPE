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

		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
			return ExtStartPathFind(instanceID, ref citizenData, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[Singleton<CitizenManager>.instance.m_instances.m_buffer[instanceID].m_citizen], startPos, endPos, vehicleInfo);
		}

		public bool ExtStartPathFind(ushort instanceID, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[2])
				Log.Warning($"CustomCitizenAI.ExtStartPathFind({instanceID}): called for citizen {instanceData.m_citizen}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding}, pathMode={extInstance.pathMode}");
#endif

			// NON-STOCK CODE START
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ExtVehicleType extVehicleType = ExtVehicleType.None;
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
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[2])
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

								extInstance.Reset();
							} else {
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[4])
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'CalculatingWalkingPathToParkedCar'.");
#endif
								extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
							}
							break;
						case ExtPathMode.RequiresWalkingPathToTarget:
						case ExtPathMode.CalculatingWalkingPathToTarget:
						case ExtPathMode.WalkingToTarget:
#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[4])
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'CalculatingWalkingPathToTarget'.");
#endif
							extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
							break;
						case ExtPathMode.RequiresCarPath:
						case ExtPathMode.DrivingToTarget:
						case ExtPathMode.DrivingToKnownParkPos:
						case ExtPathMode.DrivingToAltParkPos:
						case ExtPathMode.CalculatingCarPathToAltParkPos:
						case ExtPathMode.CalculatingCarPathToKnownParkPos:
						case ExtPathMode.CalculatingCarPathToTarget:
							if (parkedVehicleId == 0) {
#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[2])
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

								extInstance.Reset();
							} else {
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[4])
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'RequiresCarPath'.");
#endif
	
								extInstance.pathMode = ExtPathMode.RequiresCarPath;
							}
							break;
						default:
#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[2])
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'None'.");
#endif
							extInstance.Reset();
							break;
					}

					/*
					 * the following holds:
					 * - pathMode is now either CalculatingWalkingPathToParkedCar, CalculatingWalkingPathToTarget, RequiresCarPath or None.
					 * - if pathMode is CalculatingWalkingPathToParkedCar or RequiresCarPath: parked car is present
					 */

					/*
					 * reuse parked vehicle info
					 */
					if (parkedVehicleId != 0) {
						vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
					}

					/*
					 * check if the citizen must use their car later on
					 */
					if (parkedVehicleId != 0 &&
							extInstance.pathMode == ExtPathMode.None &&
							homeId != 0 && instanceData.m_targetBuilding == homeId) {
						/*
						 * citizen travels back home
						 * -> check if their car should be returned
						 */
						if ((extCitizen.lastTransportMode | ExtCitizen.ExtTransportMode.Car) != ExtCitizen.ExtTransportMode.None) {
							/*
							 * citizen travelled by car
							 * -> return car back home
							 */
							extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar;

#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[4])
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

#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[4])
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen wants to go home and parked car is too far away ({distHomeToParked}). Forcing walking to parked car.");
#endif
							}
						}
					}

					/*
					 * modify path-finding constraints (vehicleInfo, endPos) if citizen is forced to walk
					 */
					if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar || extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget) {
						/*
						 * vehicle must not be used since we need a walking path to either
						 * 1. a parked car or
						 * 2. the target building
						 */
						vehicleInfo = null;
						carUsageMode = CarUsagePolicy.Forbidden;

						if (extInstance.pathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
							/*
							 * walk to parked car
							 * -> end position is parked car
							 */
							endPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[4])
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen shall go to parked vehicle @ {endPos}");
#endif
						}
					} else if (extInstance.pathMode == ExtPathMode.RequiresCarPath) {
						/*
						 * citizen stands in front of their parked vehicle
						 * -> find a car-only path now
						 */
						carUsageMode = CarUsagePolicy.Forced;
						startPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position; // force to start from the parked car

#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[4])
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is forced to drive their car");
#endif
					} else {
						/*
						 * let path-finding decide whether a parked car should be used
						 */
						carUsageMode = CarUsagePolicy.Allowed;
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[4])
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is allowed to drive their car");
#endif
					}
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			/*
			 * semi-stock code: determine path-finding parameters (laneTypes, vehicleTypes, extVehicleType, etc.)
			 */
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			bool combustionEngine = false;
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
				} else {
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
						if (instanceData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
							randomParking = true;
						}
					}
					// NON-STOCK CODE END
				}
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
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Setting startPos={startPos} for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode}");
#endif

						if (instanceData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) {
							/*
							 * the citizen is starting their journey and the target is not an outside connection
							 * -> find a suitable parking space near the target
							 */

#if DEBUG
							if (GlobalConfig.Instance.Debug.Switches[2])
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding parking space at target for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode} parkedVehicleId={parkedVehicleId}");
#endif

							// find a parking space in the vicinity of the target
							bool calcEndPos;
							Vector3 parkPos;
							if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(endPos, vehicleInfo, ref extInstance, homeId, instanceData.m_targetBuilding == homeId, 0, false, out parkPos, ref endPosA, out calcEndPos) && extInstance.CalculateReturnPath(parkPos, endPos)) {
								// success
								extInstance.pathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos;
								calculateEndPos = calcEndPos; // if true, the end path position still needs to be calculated
								allowRandomParking = false; // find a direct path to the calculated parking position
#if DEBUG
								if (GlobalConfig.Instance.Debug.Switches[2])
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding known parking space for citizen instance {instanceID}, parked vehicle {parkedVehicleId} succeeded and return path {extInstance.returnPathId} ({extInstance.returnPathState}) is calculating. PathMode={extInstance.pathMode}");
#endif
								/*if (! extInstance.CalculateReturnPath(parkPos, endPos)) {
									// TODO retry?
									if (GlobalConfig.Instance.Debug.Switches[2])
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
							if (GlobalConfig.Instance.Debug.Switches[2])
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
			if (extVehicleType == ExtVehicleType.PassengerCar) {
				if (allowRandomParking && instanceData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
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
				CustomPathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, out parkedVehiclePathPos);
			}
			bool allowUnderground = (instanceData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

#if DEBUG
			if (GlobalConfig.Instance.Debug.Switches[2])
				Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Requesting path-finding for citizen instance {instanceID}, citizen {instanceData.m_citizen}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding} pathMode={extInstance.pathMode}");
#endif

			/*
			 * determine start & end path positions
			 */ 
			bool foundEndPos = !calculateEndPos || FindPathPosition(instanceID, ref instanceData, endPos, Options.prohibitPocketCars && (instanceData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : (laneTypes | NetInfo.LaneType.Pedestrian), vehicleTypes, false, out endPosA); // NON-STOCK CODE: with Parking AI enabled, the end position must be a pedestrian position
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

				if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					if (carUsageMode != CarUsagePolicy.Forced) { // NON-STOCK CODE
						/*
						 * citizen may use public transport
						 */
						laneTypes |= NetInfo.LaneType.PublicTransport;

						uint citizenId = citizenManager.m_instances.m_buffer[instanceID].m_citizen;
						if (citizenId != 0u && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
							laneTypes |= NetInfo.LaneType.EvacuationTransport;
						}
					}
				} else if (Options.prohibitPocketCars) { // TODO check for incoming connection
					/*
					 * citizen tried to use public transport but waiting time was too long
					 * -> add public transport demand for source building
					 */
					if (instanceData.m_sourceBuilding != 0) {
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance {instanceID} cannot uses public transport from building {instanceData.m_sourceBuilding} to {instanceData.m_targetBuilding}. Incrementing public transport demand.");
#endif
						ExtBuildingManager.Instance.ExtBuildings[instanceData.m_sourceBuilding].AddPublicTransportDemand((uint)GlobalConfig.Instance.ParkingAI.PublicTransportDemandWaitingIncrement, true);
					}
				}

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint path;
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = extPathType;
				args.extVehicleType = extVehicleType;
				args.vehicleId = 0;
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
				args.randomParking = randomParking;
				args.stablePath = false;
				args.skipQueue = false;

				bool res = CustomPathManager._instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args);
				// NON-STOCK CODE END

				if (res) {
#if DEBUG
					if (GlobalConfig.Instance.Debug.Switches[2])
						Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleTypes}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={parkedVehiclePathPos.m_segment}, vehiclePos.m_lane={parkedVehiclePathPos.m_lane}, vehiclePos.m_offset={parkedVehiclePathPos.m_offset}");
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
				if (GlobalConfig.Instance.Debug.Switches[2])
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

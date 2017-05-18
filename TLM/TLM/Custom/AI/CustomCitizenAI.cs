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
using static TrafficManager.Traffic.ExtCitizenInstance;
using static TrafficManager.Manager.AdvancedParkingManager;
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	public class CustomCitizenAI : CitizenAI {

		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log.Warning($"CustomCitizenAI.CustomStartPathFind: called for citizen instance {instanceID}, citizen {citizenData.m_citizen}, startPos={startPos}, endPos={endPos}, sourceBuilding={citizenData.m_sourceBuilding}, targetBuilding={citizenData.m_targetBuilding}");
#endif

			// NON-STOCK CODE START
			ExtVehicleType extVehicleType = ExtVehicleType.None;
			ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
			//bool mayUseOwnPassengerCar = true; // allowed to use a passenger car?
			bool canUseOwnPassengerCar = false; // allowed to use a passenger car AND given vehicle type is a passenger car?
			CarUsagePolicy carUsageMode = CarUsagePolicy.Allowed;
			//bool forceUseCar = false;
			ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);
			if (Options.prohibitPocketCars) {
				switch (extInstance.PathMode) {
					case ExtPathMode.RequiresWalkingPathToParkedCar:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Switching to 'CalculatingWalkingPathToParkedCar'.");
#endif
						extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
						break;
					case ExtPathMode.ParkingSucceeded:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Change to 'CalculatingWalkingPathToTarget'.");
#endif
						extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
						break;
					case ExtPathMode.WalkingToParkedCar:
					case ExtPathMode.WalkingToTarget:
					case ExtPathMode.PublicTransportToTarget:
					case ExtPathMode.TaxiToTarget:
					//default:
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Change to 'None'.");
#endif
						extInstance.Reset();
						break;
				}

				if (extInstance.PathMode == ExtPathMode.CalculatingWalkingPathToParkedCar || extInstance.PathMode == ExtPathMode.CalculatingWalkingPathToTarget) {
					// vehicle must not be used since we need a walking path
					vehicleInfo = null;
					carUsageMode = CarUsagePolicy.Forbidden;

					if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
						// check if parked car is present
						if (parkedVehicleId == 0) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"CustomCitizenAI.CustomStartPathFind: Citizen instance {instanceID} should go to parked car (CurrentPathMode={extInstance.PathMode}) but parked vehicle could not be found. Setting CurrentPathMode='CalculatingWalkingPathToTarget'.");
#endif
							extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
						} else {
							endPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4])
								Log._Debug($"Citizen instance {instanceID} shall go to parked vehicle @ {endPos}");
#endif
						}
					}
				} else if (parkedVehicleId != 0) {
					// reuse parked vehicle info
					vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
					carUsageMode = CarUsagePolicy.Allowed;

					ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_homeBuilding;
					if (homeId != 0 && citizenData.m_targetBuilding == homeId) {
						// check distance between home and parked car. if too far away: force to take the car back home
						float distHomeToParked = (Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position).magnitude;

						if (distHomeToParked >= GlobalConfig.Instance.MinParkedCarToTargetBuildingDistance && distHomeToParked > GlobalConfig.Instance.MaxParkedCarDistanceToHome) {
							// force to take car back home
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"Citizen instance {instanceID} will try to move parkedVehicleId={parkedVehicleId} towards home. distHomeToParked={distHomeToParked}");
#endif
							vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
							carUsageMode = CarUsagePolicy.Forced;
						}
					}
				} else {
					carUsageMode = CarUsagePolicy.Allowed;
				}
			}

			/*if (Options.parkingRestrictionsEnabled && carUsageMode == CarUsagePolicy.Allowed && parkedVehicleId != 0) {
				// force removal of illegaly parked vehicle
				PathUnit.Position parkedPathPos;
				if (PathManager.FindPathPosition(Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position, ItemClass.Service.Road, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, false, false, 32f, out parkedPathPos)) {
					if (! ParkingRestrictionsManager.Instance.IsParkingAllowed(parkedPathPos.m_segment, Singleton<NetManager>.instance.m_segments.m_buffer[parkedPathPos.m_segment].Info.m_lanes[parkedPathPos.m_lane].m_finalDirection)) {
						carUsageMode = CarUsagePolicy.Forced;
						vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
					}
				}
			}*/
			// NON-STOCK CODE END

			NetInfo.LaneType laneTypes = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						SimulationManager instance = Singleton<SimulationManager>.instance;
						if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
							laneTypes |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
							vehicleType |= vehicleInfo.m_vehicleType;
							extVehicleType = ExtVehicleType.Taxi; // NON-STOCK CODE
							// NON-STOCK CODE START
							if (Options.prohibitPocketCars) {
								extInstance.PathMode = ExtPathMode.TaxiToTarget;
							}
							// NON-STOCK CODE END
						}
					}
				} else {
					// NON-STOCK CODE START
					if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						if (carUsageMode == CarUsagePolicy.Allowed || carUsageMode == CarUsagePolicy.Forced) {
							extVehicleType = ExtVehicleType.PassengerCar;
							laneTypes |= NetInfo.LaneType.Vehicle;
							vehicleType |= vehicleInfo.m_vehicleType;
							canUseOwnPassengerCar = true;
						}
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
						extVehicleType = ExtVehicleType.Bicycle;
						laneTypes |= NetInfo.LaneType.Vehicle;
						vehicleType |= vehicleInfo.m_vehicleType;
						if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
							randomParking = true;
						}
					}
					// NON-STOCK CODE END
				}
			}
			NetInfo.LaneType startLaneType = laneTypes;
			PathUnit.Position vehiclePosition = default(PathUnit.Position);

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				if (carUsageMode == CarUsagePolicy.Forced && !canUseOwnPassengerCar) {
					carUsageMode = CarUsagePolicy.Forbidden;
				}
			}

			PathUnit.Position endPosA = default(PathUnit.Position);
			bool calculateEndPos = true;
			bool allowRandomParking = true;
			if (Options.prohibitPocketCars && canUseOwnPassengerCar) {
				// Parking AI

				ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_homeBuilding;
				
				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.ParkedCarReached) {
					startLaneType &= ~NetInfo.LaneType.Pedestrian; // force to use the car from the beginning
					startPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position; // force to start from the parked car

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomCitizenAI.CustomStartPathFind: Setting startLaneType={startLaneType}, startPos={startPos} for citizen instance {instanceID}. CurrentDepartureMode={extInstance.PathMode}");
#endif

					if (citizenData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) {
						// the citizen is starting their journey and the target is not an outside connection: find a suitable parking space near the target

#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"CustomCitizenAI.CustomStartPathFind: Finding parking space at target for citizen instance {instanceID}. CurrentDepartureMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
#endif

						// find a parking space in the vicinity of the target
						bool calcEndPos;
						Vector3 parkPos;
						if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(endPos, vehicleInfo, extInstance, homeId, 0, false, out parkPos, ref endPosA, out calcEndPos) && extInstance.CalculateReturnPath(parkPos, endPos)) {
							// success
							extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos;
							calculateEndPos = calcEndPos; // if true, the end path position still needs to be calculated
							allowRandomParking = false; // find a direct path to the calculated parking position
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"CustomCitizenAI.CustomStartPathFind: Finding known parking space for citizen instance {instanceID}, parked vehicle {parkedVehicleId} succeeded and return path {extInstance.ReturnPathId} ({extInstance.ReturnPathState}) is calculating. PathMode={extInstance.PathMode}");
#endif
								/*if (! extInstance.CalculateReturnPath(parkPos, endPos)) {
									// TODO retry?
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"CustomCitizenAI.CustomStartPathFind: [PFFAIL] Could not calculate return path for citizen instance {instanceID}, parked vehicle {parkedVehicleId}. Calling OnPathFindFailed.");
									CustomHumanAI.OnPathFindFailure(extInstance);
									return false;
								}*/
						}
					}
				}

				if (extInstance.PathMode == ExtPathMode.ParkedCarReached) {
					// no known parking space found. calculate direct path to target
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Citizen instance {instanceID} is still at CurrentPathMode={extInstance.PathMode}. Setting it to CalculatingCarPath. parkedVehicleId={parkedVehicleId}");
#endif
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget;
				}
			}

			if (canUseOwnPassengerCar) {
				if (allowRandomParking && citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
					randomParking = true;
				}
			}

			// determine path type
			ExtPathType extPathType = ExtPathType.None;
			if (Options.prohibitPocketCars) {
				extPathType = extInstance.GetPathType();
			}

			// NON-STOCK CODE END

			if (parkedVehicleId != 0 && canUseOwnPassengerCar) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
				CustomPathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"Requesting path-finding for citizen instance {instanceID}, citizen {citizenData.m_citizen}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, endPos={endPos}, sourceBuilding={citizenData.m_sourceBuilding}, targetBuilding={citizenData.m_targetBuilding}");
#endif

			bool foundEndPos = !calculateEndPos || FindPathPosition(instanceID, ref citizenData, endPos, Options.prohibitPocketCars && (citizenData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : (laneTypes | NetInfo.LaneType.Pedestrian), vehicleType, false, out endPosA); // NON-STOCK CODE: with Parking AI enabled, the end position must be a pedestrian position
			bool foundStartPos = false;
			PathUnit.Position startPosA;

			if (Options.prohibitPocketCars && (extInstance.PathMode == ExtPathMode.CalculatingCarPathToTarget || extInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos)) {
				foundStartPos = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, laneTypes & ~NetInfo.LaneType.Pedestrian, vehicleType, allowUnderground, false, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, out startPosA);
			} else {
				foundStartPos = FindPathPosition(instanceID, ref citizenData, startPos, startLaneType, vehicleType, allowUnderground, out startPosA);
			}

			if (foundStartPos && // TODO probably fails if vehicle is parked too far away from road
				foundEndPos // NON-STOCK CODE
				) {

				bool canUseTransport = (citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None;
				if (canUseTransport) {
					if (carUsageMode != CarUsagePolicy.Forced) { // NON-STOCK CODE
						laneTypes |= NetInfo.LaneType.PublicTransport;

						CitizenManager citizenManager = Singleton<CitizenManager>.instance;
						uint citizenId = citizenManager.m_instances.m_buffer[instanceID].m_citizen;
						if (citizenId != 0u && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
							laneTypes |= NetInfo.LaneType.EvacuationTransport;
						}
					}
				} else if (Options.prohibitPocketCars) { // TODO check for incoming connection
					// cim tried to use public transport but waiting time was too long
					if (citizenData.m_sourceBuilding != 0) {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} cannot uses public transport from building {citizenData.m_sourceBuilding} to {citizenData.m_targetBuilding}. Incrementing public transport demand.");
#endif
						ExtBuildingManager.Instance.GetExtBuilding(citizenData.m_sourceBuilding).AddPublicTransportDemand((uint)GlobalConfig.Instance.PublicTransportDemandWaitingIncrement, true);
					}
				}

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint path;
				bool res = CustomPathManager._instance.CreatePath(extVehicleType, 0, extPathType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, dummyPathPos, endPosA, dummyPathPos, vehiclePosition, laneTypes, vehicleType, 20000f, false, false, false, false, randomParking, false);

				if (res) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={vehiclePosition.m_segment}, vehiclePos.m_lane={vehiclePosition.m_lane}, vehiclePos.m_offset={vehiclePosition.m_offset}");
#endif

					if (citizenData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
					}
					citizenData.m_path = path;
					citizenData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}

#if DEBUG
			if (Options.prohibitPocketCars) {
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"CustomCitizenAI.CustomStartPathFind: [PFFAIL] failed for citizen instance {instanceID} (CurrentPathMode={extInstance.PathMode}). startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, startPosA.offset={startPosA.m_offset}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, endPosA.offset={endPosA.m_offset}, foundStartPos={foundStartPos}, foundEndPos={foundEndPos}");
			}
#endif
			return false;
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

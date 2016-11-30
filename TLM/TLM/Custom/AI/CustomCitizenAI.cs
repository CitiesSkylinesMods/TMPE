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

namespace TrafficManager.Custom.AI {
	public class CustomCitizenAI : CitizenAI {
		public static readonly int[] FREE_TRANSPORT_USAGE_PROBABILITY = { 90, 80, 50 };
		public static readonly int[] TRANSPORT_USAGE_PROBABILITY = { 70, 60, 40 };
		public static readonly int[] DAY_TAXI_USAGE_PROBABILITY = { 5, 25, 50 }; // if a taxi is available and assigned to this citizen, this probability kicks in
		public static readonly int[] NIGHT_TAXI_USAGE_PROBABILITY = { 25, 75, 90 }; // if a taxi is available and assigned to this citizen, this probability kicks in

		internal static void OnBeforeLoadData() {
		}

		public static bool FindParkingSpaceForExtInstance(Vector3 endPos, VehicleInfo vehicleInfo, ExtCitizenInstance extDriverInstance, ushort homeId, ushort vehicleId, bool allowTourists, out Vector3 parkPos, ref PathUnit.Position endPathPos, out bool calculateEndPos) {
			calculateEndPos = true;
			parkPos = default(Vector3);

			if (!allowTourists) {
				uint citizenId = extDriverInstance.GetCitizenId();
				if (citizenId == 0 ||
					(Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Tourist) != Citizen.Flags.None)
					return false;
			}

#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[2])
				Log._Debug($"Citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) can still use their passenger car and is either not a tourist or wants to find an alternative parking spot. Finding a parking space before starting path-finding.");
#endif

			ExtParkingSpaceLocation knownParkingSpaceLocation;
			ushort knownParkingSpaceLocationId;
			Quaternion parkRot;
			float parkOffset;

			bool success = CustomPassengerCarAI.FindParkingSpaceInVicinity(endPos, vehicleInfo, homeId, vehicleId, out knownParkingSpaceLocation, out knownParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

			extDriverInstance.ParkingSpaceLocation = knownParkingSpaceLocation;
			extDriverInstance.ParkingSpaceLocationId = knownParkingSpaceLocationId;

			if (success) {
#if DEBUG
				if (GlobalConfig.Instance().DebugSwitches[2])
					Log._Debug($"Found a parking spot for citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) before starting car path: {knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
#endif

				if (knownParkingSpaceLocation == ExtParkingSpaceLocation.RoadSide) {
					// found segment with parking space
					Vector3 pedPos;
					uint laneId;
					int laneIndex;
					float laneOffset;

#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
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
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Found an parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId} @ segment {knownParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return true;
					} else {
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Could not find an alternative parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return false;
					}
				} else if (knownParkingSpaceLocation == ExtParkingSpaceLocation.Building) {
					// found a building with parking space
					if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, endPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance().MaxBuildingToPedestrianLaneDistance, out endPathPos)) {
						calculateEndPos = false;
					}
					
					//endPos = parkPos;

					//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"Navigating citizen instance {extDriverInstance.InstanceId} to parking building {knownParkingSpaceLocationId}! segment={endPathPos.m_segment}, laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. CurrentPathMode={extDriverInstance.PathMode} calculateEndPos={calculateEndPos}");
#endif
					return true;
				}
			}
			return false;
		}

		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[2])
				Log.Warning($"CustomCitizenAI.CustomStartPathFind: called for citizen instance {instanceID}, citizen {citizenData.m_citizen}, startPos={startPos}, endPos={endPos}, sourceBuilding={citizenData.m_sourceBuilding}, targetBuilding={citizenData.m_targetBuilding}");
#endif

			// NON-STOCK CODE START
			ExtVehicleType extVehicleType = ExtVehicleType.None;
			ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
			bool mayUseOwnPassengerCar = true;
			bool canUseOwnPassengerCar = false;
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				switch (extInstance.PathMode) {
					case ExtPathMode.RequiresWalkingPathToParkedCar:
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Switching to 'CalculatingWalkingPathToParkedCar'.");
#endif
						extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
						break;
					case ExtPathMode.ParkingSucceeded:
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Change to 'CalculatingWalkingPathToTarget'.");
#endif
						extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
						break;
					case ExtPathMode.WalkingToParkedCar:
					case ExtPathMode.WalkingToTarget:
					case ExtPathMode.PublicTransportToTarget:
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has CurrentPathMode={extInstance.PathMode}. Change to 'None'.");
#endif
						extInstance.Reset();
						break;
				}

				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar || extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToTarget) {
					// vehicle must not be used
					vehicleInfo = null;
					mayUseOwnPassengerCar = false;
				} else if (parkedVehicleId != 0) {
					// reuse parked vehicle info
					vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}

				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingWalkingPathToParkedCar) {
					// check if parked car is present
					if (parkedVehicleId == 0) {
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[1])
							Log._Debug($"CustomCitizenAI.CustomStartPathFind: Citizen instance {instanceID} should go to parked car (CurrentPathMode={extInstance.PathMode}) but parked vehicle could not be found. Setting CurrentPathMode='CalculatingWalkingPathToTarget'.");
#endif
						extInstance.PathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					} else {
						endPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} shall go to parked vehicle @ {endPos}");
#endif
					}
				}
			}
			// NON-STOCK CODE END

			NetInfo.LaneType laneType = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						SimulationManager instance = Singleton<SimulationManager>.instance;
						if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
							laneType |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
							vehicleType |= vehicleInfo.m_vehicleType;
							extVehicleType = ExtVehicleType.Taxi; // NON-STOCK CODE
						}
					}
				} else {
					// NON-STOCK CODE START
					if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						if (mayUseOwnPassengerCar) {
							extVehicleType = ExtVehicleType.PassengerCar;
							laneType |= NetInfo.LaneType.Vehicle;
							vehicleType |= vehicleInfo.m_vehicleType;
							canUseOwnPassengerCar = true;
						}
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
						extVehicleType = ExtVehicleType.Bicycle;
						laneType |= NetInfo.LaneType.Vehicle;
						vehicleType |= vehicleInfo.m_vehicleType;
						if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
							randomParking = true;
						}
					}
					// NON-STOCK CODE END
				}
			}
			NetInfo.LaneType startLaneType = laneType;
			PathUnit.Position vehiclePosition = default(PathUnit.Position);

			// NON-STOCK CODE START
			PathUnit.Position endPosA = default(PathUnit.Position);
			bool calculateEndPos = true;
			bool allowRandomParking = true;
			bool forceUseCar = false;
			if (Options.prohibitPocketCars && canUseOwnPassengerCar) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
				ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_homeBuilding;
				
				// if the citizen is a resident and is starting its journey: find a suitable parking space near the target
				if (extInstance.PathMode == ExtCitizenInstance.ExtPathMode.ParkedCarReached) {
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"CustomCitizenAI.CustomStartPathFind: Finding parking space at target for citizen instance {instanceID}. CurrentDepartureMode={extInstance.PathMode} parkedVehicleId={parkedVehicleId}");
#endif

					startLaneType &= ~NetInfo.LaneType.Pedestrian;
					startPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;

#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"CustomCitizenAI.CustomStartPathFind: Setting startLaneType={startLaneType}, startPos={startPos} for citizen instance {instanceID}. CurrentDepartureMode={extInstance.PathMode}");
#endif

					bool calcEndPos;
					Vector3 parkPos;
					if (FindParkingSpaceForExtInstance(endPos, vehicleInfo, extInstance, homeId, 0, false, out parkPos, ref endPosA, out calcEndPos) && extInstance.CalculateReturnPath(parkPos, endPos)) {
						extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos;
						calculateEndPos = calcEndPos;
						allowRandomParking = false;
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"CustomCitizenAI.CustomStartPathFind: Finding known parking space for citizen instance {instanceID}, parked vehicle {parkedVehicleId} succeeded. PathMode={extInstance.PathMode} Calculating return path now.");
#endif
						/*if (! extInstance.CalculateReturnPath(parkPos, endPos)) {
							// TODO retry?
							if (GlobalConfig.Instance().DebugSwitches[1])
								Log._Debug($"CustomCitizenAI.CustomStartPathFind: [PFFAIL] Could not calculate return path for citizen instance {instanceID}, parked vehicle {parkedVehicleId}. Calling OnPathFindFailed.");
							CustomHumanAI.OnPathFindFailure(extInstance);
							return false;
						}*/
					}
				}

				if (extInstance.PathMode == ExtPathMode.ParkedCarReached) {
					// no known parking space found. calculate direct path to target
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"Citizen instance {instanceID} is still at CurrentPathMode={extInstance.PathMode}. Setting it to CalculatingCarPath. parkedVehicleId={parkedVehicleId}");
#endif
					extInstance.PathMode = ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget;
				} else if (homeId != 0 && citizenData.m_targetBuilding == homeId && parkedVehicleId != 0 && extInstance.PathMode == ExtPathMode.None) {
					float distHomeToParked = (Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position).magnitude;
					//float distStartToParked = (Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position - startPos).magnitude;

					if (distHomeToParked >= GlobalConfig.Instance().MinParkedCarToTargetBuildingDistance && distHomeToParked > GlobalConfig.Instance().MaxParkedCarDistanceToHome/* && distHomeToParked > distStartToParked*/) {
						// take car back home
#if DEBUG
						if (GlobalConfig.Instance().DebugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} will try to move parkedVehicleId={parkedVehicleId} towards home. distHomeToParked={distHomeToParked}");
#endif
						forceUseCar = true;
					}
				}
			}

			if (canUseOwnPassengerCar) {
				if (allowRandomParking && citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
					randomParking = true;
				}
			}

			ExtPathType extPathType = ExtPathType.None;
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
				extPathType = extInstance.GetPathType();
			}

			// NON-STOCK CODE END

			if (parkedVehicleId != 0 && canUseOwnPassengerCar) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
				CustomPathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[2])
				Log._Debug($"Requesting path-finding for citizen instance {instanceID}, citizen {citizenData.m_citizen}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, endPos={endPos}, sourceBuilding={citizenData.m_sourceBuilding}, targetBuilding={citizenData.m_targetBuilding}");
#endif

			bool foundEndPos = !calculateEndPos || FindPathPosition(instanceID, ref citizenData, endPos, laneType, vehicleType, false, out endPosA); // NON-STOCK CODE

			PathUnit.Position startPosA;
			if (FindPathPosition(instanceID, ref citizenData, startPos, startLaneType, vehicleType, allowUnderground, out startPosA) &&
				foundEndPos // NON-STOCK CODE
				) {

				bool allowEscapeTransport = true;
				if (Options.restrictEvacBussesToShelter) {
					if (citizenData.m_targetBuilding != 0) {
						BuildingInfo targetBuildingInfo = Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info;
						allowEscapeTransport = targetBuildingInfo.GetService() == ItemClass.Service.Disaster && targetBuildingInfo.GetClassLevel() == ItemClass.Level.Level4;
					} else {
						allowEscapeTransport = false;
					}
				}

				if (!forceUseCar && (citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					laneType |= NetInfo.LaneType.PublicTransport;
				}
				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint path;
				bool res = CustomPathManager._instance.CreatePath(false, extVehicleType, 0, extPathType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, dummyPathPos, endPosA, dummyPathPos, vehiclePosition, laneType, vehicleType, 20000f, false, false, false, false, randomParking, false, allowEscapeTransport);

				if (res) {
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneType}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={vehiclePosition.m_segment}, vehiclePos.m_lane={vehiclePosition.m_lane}, vehiclePos.m_offset={vehiclePosition.m_offset}, allowEscapeTransport={allowEscapeTransport}");
#endif

					if (citizenData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
					}
					citizenData.m_path = path;
					citizenData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
#if DEBUG
				if (GlobalConfig.Instance().DebugSwitches[1])
					Log._Debug($"CustomCitizenAI.CustomStartPathFind: [PFFAIL] failed for citizen instance {instanceID} (CurrentPathMode={extInstance.PathMode}). startPosA={startPosA}, endPosA={endPosA}, foundEndPos={foundEndPos}");
#endif
			}
			return false;
		}

		internal static bool TrySpawnParkedPassengerCar(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[1] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car for citizen {citizenId}, home {homeId} @ {refPos}");
#endif
			if (TrySpawnParkedPassengerCarRoadSide(citizenId, refPos, vehicleInfo, out parkPos))
				return true;
			return TrySpawnParkedPassengerCarBuilding(citizenId, homeId, refPos, vehicleInfo, out parkPos);
		}

		protected static bool TrySpawnParkedPassengerCarRoadSide(uint citizenId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[2])
				Log._Debug($"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset = 0f;

			if (CustomPassengerCarAI.FindParkingSpaceRoadSide(0, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, GlobalConfig.Instance().VicinityParkingSpaceSearchRadius, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[2])
						Log._Debug($"[SUCCESS] Spawned parked passenger car at road side for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[1])
				Log._Debug($"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
#endif
			return false;
		}

		protected static bool TrySpawnParkedPassengerCarBuilding(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[1] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car next to building for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset;

			if (CustomPassengerCarAI.FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, refPos, GlobalConfig.Instance().VicinityParkingSpaceSearchRadius, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance().DebugSwitches[1] && homeId != 0)
						Log._Debug($"[SUCCESS] Spawned parked passenger car next to building for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance().DebugSwitches[1] && homeId != 0)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car next to building for citizen {citizenId}");
#endif
			return false;
		}

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

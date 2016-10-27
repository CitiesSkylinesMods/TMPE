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

		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
			// NON-STOCK CODE START
			ExtVehicleType extVehicleType = ExtVehicleType.None;
			ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);

				if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingWalkingPathToParkedCar) {
					vehicleInfo = null;
					endPos = extInstance.ParkedVehicleTargetPosition;
					extInstance.ParkedVehicleTargetPosition = default(Vector3);
					if (Options.debugSwitches[2])
						Log._Debug($"Citizen instance {instanceID} shall go to parked vehicle @ {endPos}");
				} else if (vehicleInfo == null && parkedVehicleId != 0) {
					// reuse parked vehicle if no vehicle info was given
					vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}
			// NON-STOCK CODE END

			NetInfo.LaneType laneType = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			bool canUseOwnPassengerCar = false; // NON-STOCK CODE
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
					if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
						extVehicleType = ExtVehicleType.Bicycle;
						laneType |= NetInfo.LaneType.Vehicle;
						vehicleType |= vehicleInfo.m_vehicleType;
						if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
							randomParking = true;
						}
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						canUseOwnPassengerCar = true;

						if (Options.prohibitPocketCars) {
							ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
							if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget || extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.CalculatingWalkingPathToParkedCar) {
								// approach mode
								canUseOwnPassengerCar = false;
								vehicleInfo = null;
								extVehicleType = ExtVehicleType.None;
							}
						}
					}
					// NON-STOCK CODE END
				}
			}
			PathUnit.Position vehiclePosition = default(PathUnit.Position);

			// NON-STOCK CODE START
			PathUnit.Position endPosA = default(PathUnit.Position);
			bool calculateEndPos = true;
			ushort sourceBuildingId = citizenData.m_sourceBuilding; //Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].GetBuildingByLocation();
			bool allowRandomParking = true;
			if (Options.prohibitPocketCars && canUseOwnPassengerCar) {
				// check if current building is an outside connection
				bool isAtOutsideConnection = false;
				if (sourceBuildingId != 0) {
					isAtOutsideConnection = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].Info.m_buildingAI is OutsideConnectionAI;
				}

				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
				ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_homeBuilding;
				if (!isAtOutsideConnection) {

					if (parkedVehicleId == 0) {
						if (Options.debugSwitches[1])
							Log._Debug($">> Citizen {citizenData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} does not have a parked vehicle! startPos: {startPos} endPos: {endPos}");

						//if (! isAtOutsideConnection) {
						if (Options.debugSwitches[2])
							Log._Debug($"Citizen {citizenData.m_citizen} (citizen instance {instanceID}), source building {sourceBuildingId} is NOT coming from an outside connection");

						// try to spawn parked vehicle in the vicinity of the starting point.
						if (TrySpawnParkedPassengerCar(citizenData.m_citizen, sourceBuildingId, startPos, vehicleInfo)) {
							parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
							if (Options.debugSwitches[1] && sourceBuildingId != 0)
								Log._Debug($"Parked vehicle for citizen {citizenData.m_citizen} (instance {instanceID}) is {parkedVehicleId} now.");

							if (sourceBuildingId != 0) {
								ExtBuildingManager.Instance().GetExtBuilding(sourceBuildingId).RemoveParkingSpaceDemand();
								//Notification.Problem oldProblems = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_problems;
								//Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_problems = Notification.RemoveProblems(oldProblems, Notification.Problem.RoadNotConnected);
							}
						} else {
							canUseOwnPassengerCar = false;

							if (Options.debugSwitches[1]) {
								Log._Debug($">> Failed to spawn parked vehicle for citizen {citizenData.m_citizen} (citizen instance {instanceID}). Distance to home: {(startPos - Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position).magnitude} homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
							}

							if (sourceBuildingId != 0) {
								ExtBuildingManager.Instance().GetExtBuilding(sourceBuildingId).AddParkingSpaceDemand();

								//Notification.Problem oldProblems = Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_problems;
								//Singleton<BuildingManager>.instance.m_buildings.m_buffer[sourceBuildingId].m_problems = Notification.AddProblems(oldProblems, Notification.Problem.RoadNotConnected);
							}
						}
					//}
					}

					// check if a parked vehicle is present if it should be reached
					if (extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.ParkedCarReached) {
						if (Options.debugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} has reached its parked vehicle. Requesting path-find for remaining route. CurrentDepartureMode={extInstance.CurrentPathMode} canUseOwnPassengerCar={canUseOwnPassengerCar}");

						// parked vehicle must be present by now
						if (parkedVehicleId == 0) {
							extInstance.Reset();
							Log._Debug($"!!! There is no parked vehicle present for citizen instance {instanceID} although they should have reached it by now. CurrentDepartureMode={extInstance.CurrentPathMode}");
							return false;
						} else if (!canUseOwnPassengerCar) {
							extInstance.Reset();
							Log._Debug($"!!! Citizen instance {instanceID} is not allowed to use parked vehicle! CurrentDepartureMode={extInstance.CurrentPathMode}");
							return false;
						}
					}
				} else {
					if (Options.debugSwitches[2])
						Log._Debug($"Citizen instance {instanceID} is coming from an outside connection. CurrentDepartureMode={extInstance.CurrentPathMode} canUseOwnPassengerCar={canUseOwnPassengerCar}");
					extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingCarPath;
				}

				// if the citizen is a resident and is starting its journey: find a suitable parking space near the target
				if (canUseOwnPassengerCar &&
					(isAtOutsideConnection ||
						extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToTarget || // TODO -> CustomPassengerCarAI
						extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos || // TODO -> CustomPassengerCarAI
						extInstance.CurrentPathMode == ExtCitizenInstance.PathMode.ParkedCarReached) &&
					citizenData.m_citizen != 0 &&
					(Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_flags & Citizen.Flags.Tourist) == Citizen.Flags.None) {

					if (Options.debugSwitches[2])
						Log._Debug($"Citizen instance {instanceID} (CurrentPathMode={extInstance.CurrentPathMode}) can still use their passenger car and is not a tourist. Finding a parking space before starting path-finding.");

					ParkingSpaceLocation knownParkingSpaceLocation;
					ushort knownParkingSpaceLocationId;
					Vector3 parkPos;
					Quaternion parkRot;
					float parkOffset;

					bool success = CustomPassengerCarAI.FindParkingSpaceInVicinity(endPos, vehicleInfo, homeId, 0, out knownParkingSpaceLocation, out knownParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

					extInstance.AltParkingSpaceLocation = knownParkingSpaceLocation;
					extInstance.AltParkingSpaceLocationId = knownParkingSpaceLocationId;

					if (success) {
						if (Options.debugSwitches[2])
							Log._Debug($"Found a parking spot for citizen instance {instanceID} (CurrentPathMode={extInstance.CurrentPathMode}) before starting car path: {knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");

						if (knownParkingSpaceLocation == ParkingSpaceLocation.RoadSide) {
							// found segment with parking space
							Vector3 pedPos;
							uint laneId;
							int laneIndex;
							float laneOffset;

							if (Options.debugSwitches[2])
								Log._Debug($"Found segment {knownParkingSpaceLocationId} for road-side parking position for citizen instance {instanceID}!");

							// determine nearest sidewalk position for parking position at segment
							if (Singleton<NetManager>.instance.m_segments.m_buffer[knownParkingSpaceLocationId].GetClosestLanePosition(parkPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out pedPos, out laneId, out laneIndex, out laneOffset)) {
								endPosA.m_segment = knownParkingSpaceLocationId;
								endPosA.m_lane = (byte)laneIndex;
								endPosA.m_offset = (byte)(parkOffset * 255f);
								calculateEndPos = false;
								allowRandomParking = false;

								extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
								if (Options.debugSwitches[2])
									Log._Debug($"Found an parking spot sidewalk position for citizen instance {instanceID} @ segment {knownParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}! CurrentPathMode={extInstance.CurrentPathMode}");
							} else {
								if (Options.debugSwitches[2])
									Log._Debug($"Could not find an alternative parking spot sidewalk position for citizen instance {instanceID}! CurrentPathMode={extInstance.CurrentPathMode}");
								return false;
							}
						} else if (knownParkingSpaceLocation == ParkingSpaceLocation.Building) {
							// found a building with parking space
							endPos = parkPos;
							allowRandomParking = false;
							extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
							if (Options.debugSwitches[2])
								Log._Debug($"Navigating citizen instance {instanceID} to parking building {knownParkingSpaceLocationId}! CurrentPathMode={extInstance.CurrentPathMode}");
						} else {
							// dead code
							return false;
						}
					}

					if (extInstance.CurrentPathMode == PathMode.ParkedCarReached) {
						if (Options.debugSwitches[2])
							Log._Debug($"Citizen instance {instanceID} is still at CurrentPathMode={extInstance.CurrentPathMode}. Setting it to CalculatingCarPath.");
						extInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingCarPath;
					}
				}
			}

			if (canUseOwnPassengerCar) {
				laneType |= NetInfo.LaneType.Vehicle;
				vehicleType |= vehicleInfo.m_vehicleType;
				extVehicleType = ExtVehicleType.PassengerCar;

				if (allowRandomParking && citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
					randomParking = true;
				}
			}

			// NON-STOCK CODE END

			if (parkedVehicleId != 0 && canUseOwnPassengerCar) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
				PathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

			if (Options.debugSwitches[1])
				Log._Debug($"Requesting path-finding for citizen instance {instanceID}, extVehicleType={extVehicleType}, startPos={startPos}, endPos={endPos}");

			bool foundEnd = !calculateEndPos || FindPathPosition(instanceID, ref citizenData, endPos, laneType, vehicleType, false, out endPosA);

			PathUnit.Position startPosA;
			if (FindPathPosition(instanceID, ref citizenData, startPos, laneType, vehicleType, allowUnderground, out startPosA) && foundEnd) {

				if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					laneType |= NetInfo.LaneType.PublicTransport;
				}
				PathUnit.Position position2 = default(PathUnit.Position);
				uint path;

				bool res = false;
				/*if (extVehicleType == ExtVehicleType.None) {
					res = Singleton<PathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, position2, endPosA, position2, vehiclePosition, laneType, vehicleType, 20000f, false, false, false, false, randomParking);
				} else*/
					res = CustomPathManager._instance.CreatePath(false, extVehicleType, 0, canUseOwnPassengerCar ? parkedVehicleId : (ushort)0, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, position2, endPosA, position2, vehiclePosition, laneType, vehicleType, 20000f, false, false, false, false, randomParking);

				if (res) {
					if (Options.debugSwitches[1])
						Log._Debug($"Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneType}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={vehiclePosition.m_segment}, vehiclePos.m_lane={vehiclePosition.m_lane}, vehiclePos.m_offset={vehiclePosition.m_offset}");

					if (citizenData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
					}
					citizenData.m_path = path;
					citizenData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}

		protected static bool TrySpawnParkedPassengerCar(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo) {
			if (Options.debugSwitches[1] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car for citizen {citizenId}, home {homeId} @ {refPos}");
			if (TrySpawnParkedPassengerCarRoadSide(citizenId, refPos, vehicleInfo))
				return true;
			return TrySpawnParkedPassengerCarBuilding(citizenId, homeId, refPos, vehicleInfo);
		}

		protected static bool TrySpawnParkedPassengerCarRoadSide(uint citizenId, Vector3 refPos, VehicleInfo vehicleInfo) {
			if (Options.debugSwitches[1])
				Log._Debug($"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");
			Vector3 parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset = 0f;

			if (CustomPassengerCarAI.FindParkingSpaceRoadSide(0, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, Options.debugValues[14], out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					if (Options.debugSwitches[1])
						Log._Debug($"[SUCCESS] Spawned parked passenger car at road side for citizen {citizenId}: {parkedVehicleId}");
					return true;
				}
			}
			if (Options.debugSwitches[1])
				Log._Debug($"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
			return false;
		}

		protected static bool TrySpawnParkedPassengerCarBuilding(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo) {
			if (Options.debugSwitches[1] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car next to building for citizen {citizenId} @ {refPos}");
			Vector3 parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset;

			if (CustomPassengerCarAI.FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, refPos, Options.debugValues[14], out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					if (Options.debugSwitches[1] && homeId != 0)
						Log._Debug($"[SUCCESS] Spawned parked passenger car next to building for citizen {citizenId}: {parkedVehicleId}");
					return true;
				}
			}
			if (Options.debugSwitches[1] && homeId != 0)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car next to building for citizen {citizenId}");
			return false;
		}

		/*public void SimulationStep(ushort instanceID, ref CitizenInstance instanceData, ref CitizenInstance.Frame lastFrameData, bool lodPhysics) {
			Log.Error($"CitizenAI.SimulationStep is not overriden!");
		}

		public bool FindPathPosition(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, NetInfo.LaneType laneType, VehicleInfo.VehicleType vehicleType, bool allowUnderground, out PathUnit.Position startPosA) {
			Log.Error($"CitizenAI.FindPathPosition is not overriden!");
			startPosA = default(PathUnit.Position);
			return false;
		}

		protected void InvalidPath(ushort instanceID, ref CitizenInstance instanceData) {
			Log.Error($"CitizenAI.InvalidPath is not overriden!");
		}*/
	}
}

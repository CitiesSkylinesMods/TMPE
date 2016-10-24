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

namespace TrafficManager.Custom.AI {
	public class CustomCitizenAI : CitizenAI {
		internal enum TransportMode {
			None,
			Car,
			PublicTransport,
			Taxi
		}

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

				if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.CalculatingPathToParkPos) {
					vehicleInfo = null;
					endPos = extInstance.ParkedVehicleTargetPosition;
					extInstance.ParkedVehicleTargetPosition = default(Vector3);
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
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						extVehicleType = ExtVehicleType.PassengerCar;
						canUseOwnPassengerCar = true;

						if (Options.prohibitPocketCars) {
							ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
							if ((extInstance.Flags & ExtCitizenInstance.ExtFlags.CannotUsePassengerCar) != ExtCitizenInstance.ExtFlags.None) {
								// approach mode
								canUseOwnPassengerCar = false;
								vehicleInfo = null;
								extVehicleType = ExtVehicleType.None;
								extInstance.Flags &= ~ExtCitizenInstance.ExtFlags.CannotUsePassengerCar;
							}
						}
					}

					if (vehicleInfo != null) {
						// NON-STOCK CODE END

						laneType |= NetInfo.LaneType.Vehicle;
						vehicleType |= vehicleInfo.m_vehicleType;
						if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
							randomParking = true;
						}
					} // NON-STOCK CODE
				}
			}
			PathUnit.Position vehiclePosition = default(PathUnit.Position);

			// NON-STOCK CODE START
			ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_homeBuilding;
			ushort currentBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].GetBuildingByLocation();
			if (Options.prohibitPocketCars && canUseOwnPassengerCar && parkedVehicleId == 0) {
				if (Options.debugSwitches[1] && currentBuildingId != 0)
					Log._Debug($">> Citizen {citizenData.m_citizen} (instance {instanceID}), home {homeId}, building {currentBuildingId} does not have a parked vehicle! homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position} startPos: {startPos} endPos: {endPos}");

				// try to spawn parked vehicle in the vicinity of the starting point.
				if (TrySpawnParkedPassengerCar(citizenData.m_citizen, homeId, startPos, vehicleInfo)) {
					parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
					if (Options.debugSwitches[1] && currentBuildingId != 0)
						Log._Debug($"Parked vehicle for citizen {citizenData.m_citizen} (instance {instanceID}) is {parkedVehicleId} now.");

					if (currentBuildingId != 0) {
						Notification.Problem oldProblems = Singleton<BuildingManager>.instance.m_buildings.m_buffer[currentBuildingId].m_problems;
						Singleton<BuildingManager>.instance.m_buildings.m_buffer[currentBuildingId].m_problems = Notification.RemoveProblems(oldProblems, Notification.Problem.RoadNotConnected);
					}
				} else if (currentBuildingId != 0) {
					if (Options.debugSwitches[1]) {
						Log._Debug($">> Failed to spawn parked vehicle for citizen {citizenData.m_citizen} (instance {instanceID}). Distance to home: {(startPos - Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position).magnitude} homePos: {Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position}");
					}

					Notification.Problem oldProblems = Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_problems;
					Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_problems = Notification.AddProblems(oldProblems, Notification.Problem.RoadNotConnected);
				}
			} else if (currentBuildingId != 0) {
				Notification.Problem oldProblems = Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_problems;
				Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_problems = Notification.RemoveProblems(oldProblems, Notification.Problem.RoadNotConnected);
			}
			// NON-STOCK CODE END

			if (parkedVehicleId != 0 && canUseOwnPassengerCar) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
				PathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;
			
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance().GetExtInstance(instanceID);
				if (extInstance.CurrentDepartureMode == ExtCitizenInstance.DepartureMode.ParkPosReached) {
					if (Options.debugSwitches[1])
						Log._Debug($"Citizen instance {instanceID} has reached its parked vehicle. Requesting path-find for remaining route. CurrentDepartureMode={extInstance.CurrentDepartureMode} canUseOwnPassengerCar={canUseOwnPassengerCar}");

					// parked vehicle must be present by now
					if (parkedVehicleId == 0) {
						extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.None;
						Log._Debug($"!!! There is no parked vehicle present for citizen instance {instanceID} although they should have reached it by now. CurrentDepartureMode={extInstance.CurrentDepartureMode}");
						return false;
					} else if (! canUseOwnPassengerCar) {
						Log._Debug($"!!! Citizen instance {instanceID} is not allowed to use parked vehicle! CurrentDepartureMode={extInstance.CurrentDepartureMode} flags={extInstance.Flags}");
					}

					extInstance.CurrentDepartureMode = ExtCitizenInstance.DepartureMode.CalculatingCarPath;
				}
			}

			if (Options.debugSwitches[1])
				Log._Debug($"Requesting path-finding for citizen instance {instanceID}, extVehicleType={extVehicleType}, startPos={startPos}, endPos={endPos}");

			PathUnit.Position startPosA;
			PathUnit.Position endPosA;
			if (FindPathPosition(instanceID, ref citizenData, startPos, laneType, vehicleType, allowUnderground, out startPosA) &&
				FindPathPosition(instanceID, ref citizenData, endPos, laneType, vehicleType, false, out endPosA)) {

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

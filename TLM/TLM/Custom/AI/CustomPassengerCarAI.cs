#define PATHRECALCx

using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using ColossalFramework.Math;
using TrafficManager.Util;
using System.Reflection;
using ColossalFramework.Globalization;
using TrafficManager.UI;
using static TrafficManager.Traffic.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	public class CustomPassengerCarAI : CarAI {
		private static FieldInfo improvedParkingAiField = null;
		private static FieldInfo parkingSearchRadiusField = null;

		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			try {
				// NON-STOCK CODE START
				VehicleState state = null;
				ExtCitizenInstance driverExtInstance = null;
				if (Options.prohibitPocketCars) {
					state = VehicleStateManager.Instance()._GetVehicleState(vehicleData.GetFirstVehicle(vehicleId));
					driverExtInstance = state.GetDriverExtInstance();
					if (driverExtInstance != null) {
						driverExtInstance.UpdateReturnPathState();
					}
				}
				// NON-STOCK CODE END

				if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && Options.enableDespawning) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} else {
					// NON-STOCK CODE START
					if (Options.prohibitPocketCars) {
						if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
							if (driverExtInstance != null) {
								PathManager pathManager = Singleton<PathManager>.instance;
								byte pathFindFlags = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].m_pathFindFlags;

								bool pathFindFailed = (pathFindFlags & PathUnit.FLAG_FAILED) != 0;
								bool pathFindSucceeded = (pathFindFlags & PathUnit.FLAG_READY) != 0;

								if (driverExtInstance.ReturnPathState == ExtPathState.Calculating) {
									// wait for the return path being calculated
									return;
								} else if (driverExtInstance.ReturnPathState == ExtPathState.Failed) {
									if (Options.debugSwitches[1])
										Log._Debug($"CustomPassengerCarAI.CustomSimulationStep: Return path {driverExtInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
									pathFindSucceeded = false;
									pathFindFailed = true;
								}

								if (driverExtInstance.ReturnPathState == ExtPathState.Ready || driverExtInstance.ReturnPathState == ExtPathState.Failed)
									driverExtInstance.ReleaseReturnPath();

								if (pathFindSucceeded) {
									OnPathFindSuccess(vehicleId, driverExtInstance);
								} else if (pathFindFailed) {
									OnPathFindFailure(driverExtInstance, vehicleId);
								}
							}
						}
					}
					// NON-STOCK CODE END
					base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
				}
			} catch (Exception ex) {
				Log.Error("Error in CustomPassengerCarAI.SimulationStep: " + ex.ToString());
			}
		}

		protected static void OnPathFindFailure(ExtCitizenInstance extInstance, ushort vehicleId) {
			if (Options.debugSwitches[1])
				Log._Debug($"CustomHumanAI.OnPathFindFailure: Path-finding failed for vehicle {vehicleId}, citizen instance {extInstance.InstanceId}. CurrentPathMode={extInstance.PathMode}");
			extInstance.Reset();
		}

		internal static void OnPathFindSuccess(ushort vehicleId, ExtCitizenInstance driverExtInstance) {
			if (Options.debugSwitches[2])
				Log._Debug($"CustomPassengerCarAI.OnPathFindSuccess: Path is ready for vehicle {vehicleId}, citizen instance {driverExtInstance.InstanceId}! CurrentPathMode={driverExtInstance.PathMode}");
			if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToAltParkPos) {
				driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos;
				driverExtInstance.ParkingPathStartPosition = null;
				if (Options.debugSwitches[2])
					Log._Debug($"Path to an alternative parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
			} else if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget) {
				driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;
				if (Options.debugSwitches[2])
					Log._Debug($"Car path is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
			} else if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos) {
				driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos;
				if (Options.debugSwitches[2])
					Log._Debug($"Car path to known parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
			}
		}

		public string CustomGetLocalizedStatus(ushort vehicleID, ref Vehicle data, out InstanceID target) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort driverInstanceId = GetDriverInstance(vehicleID, ref data);
			ushort targetBuildingId = 0;
			if (driverInstanceId != 0) {
				if ((data.m_flags & Vehicle.Flags.Parking) != (Vehicle.Flags)0) {
					uint citizen = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_citizen;
					if (citizen != 0u && citizenManager.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_parkedVehicle != 0) {
						target = InstanceID.Empty;
						return Locale.Get("VEHICLE_STATUS_PARKING");
					}
				}
				targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_targetBuilding;
			}
			if (targetBuildingId == 0) {
				target = InstanceID.Empty;
				return Locale.Get("VEHICLE_STATUS_CONFUSED");
			}
			bool flag = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			if (flag) {
				target = InstanceID.Empty;
				return Locale.Get("VEHICLE_STATUS_LEAVING");
			}
			target = InstanceID.Empty;
			target.Building = targetBuildingId;

			string ret = Locale.Get("VEHICLE_STATUS_GOINGTO");
			if (Options.prohibitPocketCars) {
				VehicleState state = VehicleStateManager.Instance()._GetVehicleState(vehicleID);
				ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
				if (driverExtInstance != null) {
					switch (driverExtInstance.PathMode) {
						case ExtPathMode.DrivingToAltParkPos:
							ret = Translation.GetString("Driving_to_a_parking_spot") + ", " + ret;
							break;
						case ExtPathMode.ParkingFailed:
						case ExtPathMode.CalculatingCarPathToAltParkPos:
							ret = Translation.GetString("Searching_for_a_parking_spot") + ", " + ret;
							break;
					}
				}
			}

			return ret;
		}


		public static ushort GetDriverInstance(ushort vehicleID, ref Vehicle data) { // TODO reverse-redirect
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint num = data.m_citizenUnits;
			int num2 = 0;
			while (num != 0u) {
				uint nextUnit = instance.m_units.m_buffer[(int)((UIntPtr)num)].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizen = instance.m_units.m_buffer[(int)((UIntPtr)num)].GetCitizen(i);
					if (citizen != 0u) {
						ushort instance2 = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
						if (instance2 != 0) {
							return instance2;
						}
					}
				}
				num = nextUnit;
				if (++num2 > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return 0;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData) {
			ushort driverInstance = GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstance != 0) {
				ushort targetBuilding = Singleton<CitizenManager>.instance.m_instances.m_buffer[(int)driverInstance].m_targetBuilding;
				if (targetBuilding != 0) {

					Randomizer randomizer = new Randomizer((int)vehicleID);

					// NON-STOCK CODE START
					/*if (Options.prohibitPocketCars) {
						VehicleState state = VehicleStateManager.Instance()._GetVehicleState(vehicleData.GetFirstVehicle(vehicleID));
						ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
						if (driverExtInstance != null) {
							if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.ParkingFailed) {
								// previous parking attempt failed
								if (driverExtInstance.FailedParkingAttempts > Options.debugValues[26]) {
									// maximum number of parking attempts reached
									if (Options.debugSwitches[2])
										Log._Debug($"Reached maximum number of parking attempts for vehicle {vehicleID}!");
									driverExtInstance.FailedParkingAttempts = 0;
									return false;
								} else {
									// Try to find an alternative parking spot and calculate a path to it.
									if (Options.debugSwitches[2])
										Log._Debug($"Retrying parking for vehicle {vehicleID}. FailedParkingAttempts={driverExtInstance.FailedParkingAttempts} CurrentPathMode={driverExtInstance.CurrentPathMode}");

									uint citizenId = Singleton<CitizenManager>.instance.m_instances.m_buffer[(int)driverInstance].m_citizen;
									ushort homeBuildingId = 0;
									if (citizenId != 0) {
										homeBuildingId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_homeBuilding;
									}

									// find a suitable parking space which is more far away
									ParkingSpaceLocation altParkingSpaceLocation;
									ushort altParkingSpaceLocationId;
									Vector3 parkPos;
									Quaternion parkRot;
									float parkOffset;

									bool success = FindParkingSpaceInVicinity(vehicleData.m_targetPos3, vehicleData.Info, homeBuildingId, vehicleID, out altParkingSpaceLocation, out altParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

									driverExtInstance.AltParkingSpaceLocation = altParkingSpaceLocation;
									driverExtInstance.AltParkingSpaceLocationId = altParkingSpaceLocationId;

									if (!success) {
										driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFindFailed;
										driverExtInstance.Reset();
										if (Options.debugSwitches[2])
											Log._Debug($"Could not find any alternative parking spot for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId}! GIVING UP.");
										return false;
									}

									if (altParkingSpaceLocation == ParkingSpaceLocation.RoadSide) {
										// found segment with parking space
										Vector3 pedPos; uint laneId; int laneIndex; float laneOffset;

										if (Options.debugSwitches[2])
											Log._Debug($"Found segment {altParkingSpaceLocationId} for road-side parking position for vehicle {vehicleID}!");

										// determine nearest sidewalk position for parking position at segment
										if (Singleton<NetManager>.instance.m_segments.m_buffer[altParkingSpaceLocationId].GetClosestLanePosition(parkPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out pedPos, out laneId, out laneIndex, out laneOffset)) {
											driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingPathToAltParkPos;
											if (Options.debugSwitches[2])
												Log._Debug($"Found an alternative parking spot sidewalk position for vehicle {vehicleID} @ segment {altParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}! CurrentPathMode={driverExtInstance.CurrentPathMode}");
											return this.StartPathFind(vehicleID, ref vehicleData, vehicleData.m_targetPos3, pedPos);
										} else {
											driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFindFailed;
											if (Options.debugSwitches[2])
												Log._Debug($"Could not find an alternative parking spot sidewalk position for vehicle {vehicleID}! CurrentPathMode={driverExtInstance.CurrentPathMode}");
											driverExtInstance.Reset();
											if (Options.debugSwitches[2])
												Log._Debug($"Could not find any alternative parking spot for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId}! GIVING UP (2).");
											return false;
										}
									} else if (altParkingSpaceLocation == ParkingSpaceLocation.Building) {
										// found a building with parking space
										targetBuilding = altParkingSpaceLocationId;
										driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingPathToAltParkPos;
										if (Options.debugSwitches[2])
											Log._Debug($"Navigating vehicle {vehicleID} to parking building {targetBuilding}! CurrentPathMode={driverExtInstance.CurrentPathMode}");
									} else {
										// "dead code"
										return false;
									}
								}
							}
						}
					}*/
					// NON-STOCK CODE END

					BuildingManager instance = Singleton<BuildingManager>.instance;
					BuildingInfo info = instance.m_buildings.m_buffer[(int)targetBuilding].Info;
					Vector3 vector;
					Vector3 endPos;
					info.m_buildingAI.CalculateUnspawnPosition(targetBuilding, ref instance.m_buildings.m_buffer[(int)targetBuilding], ref randomizer, this.m_info, out vector, out endPos);
					return this.StartPathFind(vehicleID, ref vehicleData, vehicleData.m_targetPos3, endPos); // will call custom method below
				}
			}

			return false;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			//Log._Debug($"CustomPassengerCarAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif
			PathUnit.Position startPosA = default(PathUnit.Position);
			PathUnit.Position startPosB = default(PathUnit.Position);
			PathUnit.Position endPosA = default(PathUnit.Position);
			float sqrDistA = 0f;
			float sqrDistB;

			// NON-STOCK CODE START
			bool calculateEndPos = true;
			bool allowRandomParking = true;
			bool movingToAlternativeParkingPos = false;
			bool foundStartingPos = false;
			if (Options.prohibitPocketCars) {
				VehicleState state = VehicleStateManager.Instance()._GetVehicleState(vehicleData.GetFirstVehicle(vehicleID));
				ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
				if (driverExtInstance != null) {
					switch (driverExtInstance.PathMode) {
						case ExtPathMode.CalculatingCarPathToAltParkPos:
							if (Options.debugSwitches[2])
								Log._Debug($"Vehicle {vehicleID} shall move to an alternative parking position! CurrentPathMode={driverExtInstance.PathMode}");
							movingToAlternativeParkingPos = true;
							if (driverExtInstance.ParkingPathStartPosition != null) {
								startPosA = (PathUnit.Position)driverExtInstance.ParkingPathStartPosition;
								foundStartingPos = true;
								if (Options.debugSwitches[2])
									Log._Debug($"Setting starting pos for {vehicleID} to segment={startPosA.m_segment}, laneIndex={startPosA.m_lane}, offset={startPosA.m_offset}");
							}
							startBothWays = false;
							break;
						case ExtPathMode.None:
						case ExtPathMode.DrivingToTarget:
						case ExtPathMode.DrivingToKnownParkPos:
						case ExtPathMode.ParkingFailed:
							bool allowTourists = false;
							if (driverExtInstance.PathMode == ExtPathMode.ParkingFailed) {
								// previous parking attempt failed
								driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToAltParkPos;
								allowTourists = true;

								if (driverExtInstance.FailedParkingAttempts > Options.debugValues[26]) {
									// maximum number of parking attempts reached
									if (Options.debugSwitches[2])
										Log._Debug($"Reached maximum number of parking attempts for vehicle {vehicleID}! GIVING UP.");
									driverExtInstance.Reset();
									return false;
								}
							} else {
								driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToKnownParkPos;
							}

							ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[driverExtInstance.GetCitizenId()].m_homeBuilding;
							bool calcEndPos;
							bool allowRandPark;
							Vector3 parkPos;

							if (CustomCitizenAI.FindParkingSpaceForExtInstance(endPos, vehicleData.Info, driverExtInstance, homeId, allowTourists, out parkPos, ref endPosA, out calcEndPos, out allowRandPark)) {
								calculateEndPos = calcEndPos;
								allowRandomParking = allowRandPark;
								if (!driverExtInstance.CalculateReturnPath(parkPos, endPos)) {
									if (Options.debugSwitches[1])
										Log._Debug($"Could not calculate return path for citizen instance {driverExtInstance.InstanceId}, vehicle {vehicleID}. Resetting instance.");
									driverExtInstance.Reset();
									return false;
								}
							} else if (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
								// no alternative parking spot found: abort
								if (Options.debugSwitches[2])
									Log._Debug($"No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId} with CurrentPathMode={driverExtInstance.PathMode}! GIVING UP.");
								//driverExtInstance.Reset();
								return false;
							} else {
								// calculate a direct path to target
								if (Options.debugSwitches[2])
									Log._Debug($"No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId} with CurrentPathMode={driverExtInstance.PathMode}! Setting CurrentPathMode to 'CalculatingCarPath'.");
								driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
							}
							break;
					}
				}
			}
			// NON-STOCK CODE END

			VehicleInfo info = this.m_info;
			ushort driverInstance = CustomPassengerCarAI.GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstance == 0) {
				return false;
			}
			CitizenManager instance = Singleton<CitizenManager>.instance;
			CitizenInfo citizenInfo = instance.m_instances.m_buffer[(int)driverInstance].Info;
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle; // NON-STOCK CODE
																   // NON-STOCK CODE START
			if (!movingToAlternativeParkingPos) {
				laneTypes |= NetInfo.LaneType.Pedestrian;
			}
			// NON-STOCK CODE END

			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			bool randomParking = false;
			ushort targetBuilding = instance.m_instances.m_buffer[(int)driverInstance].m_targetBuilding;
			if (allowRandomParking && // NON-STOCK CODE
				!movingToAlternativeParkingPos &&
				targetBuilding != 0 &&
				Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
				randomParking = true;
			}

			if (Options.debugSwitches[1])
				Log._Debug($"Requesting path-finding for passenger care {vehicleID}, startPos={startPos}, endPos={endPos}");

			// NON-STOCK CODE START
			if (! foundStartingPos) {
				foundStartingPos = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out sqrDistA, out sqrDistB);
			}

			bool foundEndPos = !calculateEndPos || citizenInfo.m_citizenAI.FindPathPosition(driverInstance, ref instance.m_instances.m_buffer[(int)driverInstance], endPos, laneTypes | NetInfo.LaneType.Pedestrian, vehicleType, undergroundTarget, out endPosA);
			// NON-STOCK CODE END

			if (foundStartingPos &&
				foundEndPos) { // NON-STOCK CODE
				if (!startBothWays || sqrDistA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				PathUnit.Position endPosB = default(PathUnit.Position);
				SimulationManager instance2 = Singleton<SimulationManager>.instance;
				uint path;
				PathUnit.Position def = default(PathUnit.Position);
				if (CustomPathManager._instance.CreatePath(
#if PATHRECALC
					recalcRequested
#else
					false
#endif
					, ExtVehicleType.PassengerCar, vehicleID, 0, out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleType, 20000f, false, false, false, false, randomParking)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance()._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

					if (Options.debugSwitches[1])
						Log._Debug($"Path-finding starts for passenger car {vehicleID}, path={path}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");

					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}

		public static bool FindParkingSpaceInVicinity(Vector3 targetPos, VehicleInfo vehicleInfo, ushort homeId, out ExtParkingSpaceLocation parkingSpaceLocation, out ushort parkingSpaceLocationId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return FindParkingSpaceInVicinity(targetPos, vehicleInfo, homeId, out parkingSpaceLocation, out parkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);
		}

		public static bool FindParkingSpaceInVicinity(Vector3 targetPos, VehicleInfo vehicleInfo, ushort homeId, ushort vehicleId, out ExtParkingSpaceLocation parkingSpaceLocation, out ushort parkingSpaceLocationId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Vector3 roadParkPos;
			Quaternion roadParkRot;
			float roadParkOffset;
			Vector3 buildingParkPos;
			Quaternion buildingParkRot;
			float buildingParkOffset;
			bool foundParkingPosRoadSide = false;
			bool foundParkingPosBuilding = false;

			ushort parkingSpaceSegmentId = CustomPassengerCarAI.FindParkingSpaceAtRoadSide(0, targetPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, Options.debugValues[14], true, out roadParkPos, out roadParkRot, out roadParkOffset);
			ushort parkingBuildingId = CustomPassengerCarAI.FindParkingSpaceAtBuilding(vehicleInfo, homeId, 0, 0, targetPos, Options.debugValues[14], true, out buildingParkPos, out buildingParkRot, out buildingParkOffset);

			if (parkingSpaceSegmentId != 0) {
				if (parkingBuildingId != 0) {
					// choose nearest parking position
					if ((roadParkPos - targetPos).magnitude < (buildingParkPos - targetPos).magnitude) {
						// road parking space is closer
						if (Options.debugSwitches[2])
							Log._Debug($"Found an (alternative) road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId} after comparing distance with a bulding parking position @ {parkingBuildingId}!");
						foundParkingPosRoadSide = true;
						parkPos = roadParkPos;
						parkRot = roadParkRot;
						parkOffset = roadParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
						parkingSpaceLocationId = parkingSpaceSegmentId;
						return true;
					} else {
						// building parking space is closer
						if (Options.debugSwitches[2])
							Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId} after comparing distance with a road-side parking position @ {parkingSpaceSegmentId}!");
						foundParkingPosBuilding = true;
						parkPos = buildingParkPos;
						parkRot = buildingParkRot;
						parkOffset = buildingParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.Building;
						parkingSpaceLocationId = parkingBuildingId;
						return true;
					}
				} else {
					// road-side but no building parking space found
					if (Options.debugSwitches[2])
						Log._Debug($"Found an alternative road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId}!");
					foundParkingPosRoadSide = true;
					parkPos = roadParkPos;
					parkRot = roadParkRot;
					parkOffset = roadParkOffset;
					parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
					parkingSpaceLocationId = parkingSpaceSegmentId;
					return true;
				}
			} else if (parkingBuildingId != 0) {
				// building but no road-side parking space found
				if (Options.debugSwitches[2])
					Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId}!");
				foundParkingPosBuilding = true;
				parkPos = buildingParkPos;
				parkRot = buildingParkRot;
				parkOffset = buildingParkOffset;
				parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.Building;
				parkingSpaceLocationId = parkingBuildingId;
				return true;
			} else {
				//driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFailed;
				parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.None;
				parkingSpaceLocationId = 0;
				parkPos = default(Vector3);
				parkRot = default(Quaternion);
				parkOffset = -1f;
				if (Options.debugSwitches[2])
					Log._Debug($"Could not a road-side or building parking position for vehicle {vehicleId}!");
				return false;
			}
		}

		/*internal static bool FindParkingSpaceBuilding(ushort homeID, ushort ignoreParked, ushort buildingID, ref Building building, Vector3 refPos, float width, float length, ref float maxDistance, ref Vector3 parkPos, ref Quaternion parkRot) {
			Log.Error("FindParkingSpaceBuilding is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			maxDistance = 0;
			return false;
		}*/

		internal static bool FindParkingSpaceRoadSide(ushort ignoreParked, ushort requireSegment, Vector3 refPos, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpaceRoadSide is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		internal static bool FindParkingSpace(ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segment, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpace is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		internal static bool FindParkingSpaceProp(ushort ignoreParked, PropInfo info, Vector3 position, float angle, bool fixedHeight, Vector3 refPos, float width, float length, ref float maxDistance, ref Vector3 parkPos, ref Quaternion parkRot) {
			Log.Error("FindParkingSpaceProp is not overridden!");
			return false;
		}

		internal static bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return FindParkingSpaceAtBuilding(vehicleInfo, homeID, ignoreParked, segmentId, refPos, maxDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		protected static ushort FindParkingSpaceAtBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = -1f;

			int centerI = (int)((refPos.z - 72f) / 64f + 135f);
			int centerJ = (int)((refPos.x - 72f) / 64f + 135f);
			int radius = Math.Max(1, (int)(maxDistance / 32f + 1f));

			BuildingManager buildingMan = Singleton<BuildingManager>.instance;
			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

			ushort foundBuildingId = 0;
			Vector3 myParkPos = parkPos;
			Quaternion myParkRot = parkRot;
			float myParkOffset = parkOffset;

			LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, delegate (int i, int j) {
				if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 || j >= BuildingManager.BUILDINGGRID_RESOLUTION)
					return true;

				if (Options.debugSwitches[4]) {
					Log._Debug($"FindParkingSpaceBuilding: Checking building grid @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j} for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}");
				}

				ushort buildingId = buildingMan.m_buildingGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int numIterations = 0;
				while (buildingId != 0) {
					Vector3 innerParkPos; Quaternion innerParkRot; float innerParkOffset;
					if (Options.debugSwitches[4]) {
						Log._Debug($"FindParkingSpaceBuilding: Checking building {buildingId} @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j}, for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}.");
					}

					if (FindParkingSpaceBuilding(vehicleInfo, homeID, ignoreParked, buildingId, ref buildingMan.m_buildings.m_buffer[(int)buildingId], segmentId, refPos, ref maxDistance, out innerParkPos, out innerParkRot, out innerParkOffset)) {
						if (Options.debugSwitches[1] && homeID != 0)
							Log._Debug($"FindParkingSpaceBuilding: Found a parking space for {refPos}, homeID {homeID} @ building {buildingId}, {myParkPos}, offset {myParkOffset}!");
						foundBuildingId = buildingId;
						myParkPos = innerParkPos;
						myParkRot = innerParkRot;
						myParkOffset = innerParkOffset;

						if (!randomize || rng.Int32(2u) == 0)
							return false;
					}
					buildingId = buildingMan.m_buildings.m_buffer[(int)buildingId].m_nextGridBuilding;
					if (++numIterations >= 49152) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				return true;
			});

			if (foundBuildingId == 0) {
				if (Options.debugSwitches[1] && homeID != 0)
					Log._Debug($"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");

				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundBuildingId;
		}

		internal static bool FindParkingSpaceRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return FindParkingSpaceAtRoadSide(ignoreParked, refPos, width, length, maxDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		internal static ushort FindParkingSpaceAtRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;

			int centerI = (int)((refPos.z - 72f) / 64f + 135f);
			int centerJ = (int)((refPos.x - 72f) / 64f + 135f);
			int radius = Math.Max(1, (int)(maxDistance / 32f + 1f));

			NetManager netManager = Singleton<NetManager>.instance;
			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

			ushort foundSegmentId = 0;
			Vector3 myParkPos = parkPos;
			Quaternion myParkRot = parkRot;
			float myParkOffset = parkOffset;

			LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, delegate (int i, int j) {
				if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 || j >= BuildingManager.BUILDINGGRID_RESOLUTION)
					return true;

				ushort segmentId = netManager.m_segmentGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int iterations = 0;
				while (segmentId != 0) {
					uint laneId;
					int laneIndex;
					float laneOffset;
					Vector3 innerParkPos;
					Quaternion innerParkRot;
					float innerParkOffset;
					if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
						if (FindParkingSpaceRoadSide(ignoreParked, segmentId, innerParkPos, width, length, out innerParkPos, out innerParkRot, out innerParkOffset)) {
							if (Options.debugSwitches[1])
								Log._Debug($"FindParkingSpaceRoadSide: Found a parking space for refPos {refPos} @ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");
							foundSegmentId = segmentId;
							myParkPos = innerParkPos;
							myParkRot = innerParkRot;
							myParkOffset = innerParkOffset;
							if (!randomize || rng.Int32(2u) == 0)
								return false;
						}
					} else {
						/*if (Options.debugSwitches[1])
							Log._Debug($"FindParkingSpaceRoadSide: Could not find closest lane position for parking @ {segmentId}!");*/
					}

					segmentId = netManager.m_segments.m_buffer[segmentId].m_nextGridSegment;
					if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}

				return true;
			});

			if (foundSegmentId == 0) {
				if (Options.debugSwitches[1])
					Log._Debug($"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundSegmentId;
		}

		/// <summary>
		/// Finds a parking space (prop) that belongs to a given building
		/// </summary>
		/// <param name="homeID"></param>
		/// <param name="ignoreParked"></param>
		/// <param name="buildingID"></param>
		/// <param name="building"></param>
		/// <param name="segmentId"></param>
		/// <param name="refPos"></param>
		/// <param name="width"></param>
		/// <param name="length"></param>
		/// <param name="maxDistance"></param>
		/// <param name="parkPos"></param>
		/// <param name="parkRot"></param>
		/// <param name="parkOffset"></param>
		/// <returns></returns>
		public static bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort buildingID, ref Building building, ushort segmentId, Vector3 refPos, ref float maxDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			int buildingWidth = building.Width;
			int buildingLength = building.Length;

			// NON-STOCK CODE START
			parkOffset = -1f; // only set if segmentId != 0
			parkPos = default(Vector3);
			parkRot = default(Quaternion);

			if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
				if (Options.debugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not created.");
				return false;
			}

			if (segmentId != 0) {
				// check if building is accessible from the given segment
				if (Options.debugSwitches[4])
					Log._Debug($"Checking if building {buildingID} is accessible from segment {segmentId}.");

				Vector3 unspawnPos;
				Vector3 unspawnTargetPos;
				building.Info.m_buildingAI.CalculateUnspawnPosition(buildingID, ref building, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, out unspawnPos, out unspawnTargetPos);

				Vector3 lanePos; uint laneId; int laneIndex; float laneOffset;
				// calculate segment offset
				if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(unspawnPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out lanePos, out laneId, out laneIndex, out laneOffset)) {
					float dist = (lanePos - unspawnPos).magnitude;
					if (Options.debugSwitches[4])
						Log._Debug($"Succeeded in finding unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}! lanePos={lanePos}, dist={dist}, laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");

					/*if (dist > 16f) {
						if (Options.debugSwitches[2])
							Log._Debug($"Distance between unspawn position and lane position is too big! {dist} unspawnPos={unspawnPos} lanePos={lanePos}");
						return false;
					}*/

					parkOffset = laneOffset;
				} else {
					if (Options.debugSwitches[4])
						Log.Warning($"Could not find unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}!");
				}
			} else {
			// NON-STOCK CODE END
				float diagWidth = Mathf.Sqrt((float)(buildingWidth * buildingWidth + buildingLength * buildingLength)) * 8f;
				if (VectorUtils.LengthXZ(building.m_position - refPos) >= maxDistance + diagWidth) {
					if (Options.debugSwitches[4])
						Log._Debug($"Refusing to find parking space at building {buildingID}! {VectorUtils.LengthXZ(building.m_position - refPos)} >= {maxDistance + diagWidth} (maxDistance={maxDistance})");
					return false;
				}
			} // NON-STOCK CODE

			if ((building.m_flags & Building.Flags.BurnedDown) != Building.Flags.None) {
				if (Options.debugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is burned down.");
				return false;
			}

			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

			BuildingInfo buildingInfo = building.Info;
			Matrix4x4 transformMatrix = default(Matrix4x4);
			bool transformMatrixCalculated = false;
			bool result = false;
			if (buildingInfo.m_class.m_service == ItemClass.Service.Residential && buildingID != homeID && rng.Int32((uint)Options.getRecklessDriverModulo()) != 0) { // NON-STOCK CODE
				if (Options.debugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is a residential building which does not match home id {homeID}.");
				return result;
			}

			float propMaxDistance = 9999f; // NON-STOCK CODE

			if (buildingInfo.m_props != null) {
				for (int i = 0; i < buildingInfo.m_props.Length; i++) {
					BuildingInfo.Prop prop = buildingInfo.m_props[i];
					Randomizer randomizer = new Randomizer((int)buildingID << 6 | prop.m_index);
					if (randomizer.Int32(100u) < prop.m_probability && buildingLength >= prop.m_requiredLength) {
						PropInfo propInfo = prop.m_finalProp;
						if (propInfo != null) {
							propInfo = propInfo.GetVariation(ref randomizer);
							if (propInfo.m_parkingSpaces != null && propInfo.m_parkingSpaces.Length != 0) {
								if (!transformMatrixCalculated) {
									transformMatrixCalculated = true;
									Vector3 pos = Building.CalculateMeshPosition(buildingInfo, building.m_position, building.m_angle, building.Length);
									Quaternion q = Quaternion.AngleAxis(building.m_angle * 57.29578f, Vector3.down);
									transformMatrix.SetTRS(pos, q, Vector3.one);
								}
								Vector3 position = transformMatrix.MultiplyPoint(prop.m_position);
								if (FindParkingSpaceProp(ignoreParked, propInfo, position, building.m_angle + prop.m_radAngle, prop.m_fixedHeight, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, ref propMaxDistance, ref parkPos, ref parkRot)) { // NON-STOCK CODE
									result = true;
								}
							}
						}
					}
				}
			}

			maxDistance = propMaxDistance; // NON-STOCK CODE

			return result;
		}

		/// <summary>
		/// Finds a free parking space for a given vehicle position on a given segment
		/// </summary>
		/// <param name="ignoreParked"></param>
		/// <param name="segmentId"></param>
		/// <param name="refPos"></param>
		/// <param name="width"></param>
		/// <param name="length"></param>
		/// <param name="parkPos"></param>
		/// <param name="parkRot"></param>
		/// <param name="laneId"></param>
		/// <param name="laneIndex"></param>
		/// <param name="parkOffset"></param>
		/// <returns></returns>
		protected static bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo, ushort ignoreParked, ushort segmentId, Vector3 refPos, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset, out uint laneId, out int laneIndex) {
			float width = vehicleInfo.m_generatedInfo.m_size.x;
			float length = vehicleInfo.m_generatedInfo.m_size.z;

			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out parkPos, out laneId, out laneIndex, out parkOffset)) {
					if (FindParkingSpaceRoadSide(ignoreParked, segmentId, parkPos, width, length, out parkPos, out parkRot, out parkOffset)) {
						if (Options.debugSwitches[1])
							Log._Debug($"FindParkingSpaceRoadSideForVehiclePos: Found a parking space for refPos {refPos} @ {parkPos}, laneId {laneId}, laneIndex {laneIndex}!");
						return true;
					}
				}
			}

			parkPos = default(Vector3);
			parkRot = default(Quaternion);
			laneId = 0;
			laneIndex = -1;
			parkOffset = -1f;
			return false;
		}

		public bool ParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
			// NON-STOCK CODE START
			VehicleState state = null;
			ExtCitizenInstance driverExtInstance = null;
			bool prohibitPocketCars = false;
			if (Options.prohibitPocketCars) {
				state = VehicleStateManager.Instance()._GetVehicleState(vehicleData.GetFirstVehicle(vehicleID));
				driverExtInstance = state.GetDriverExtInstance();
				prohibitPocketCars = driverExtInstance != null;
			}
			// NON-STOCK CODE END

			PathManager pathManager = Singleton<PathManager>.instance;
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

			// TODO remove this:
			uint driverCitizenId = 0u;
			ushort targetBuildingId = 0; // NON-STOCK CODE
			uint curCitizenUnitId = vehicleData.m_citizenUnits;
			int numIterations = 0;
			while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
				uint nextUnit = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnitId)].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizen = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnitId)].GetCitizen(i);
					if (citizen != 0u) {
						ushort driverCitizenInstanceId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
						if (driverCitizenInstanceId != 0) {
							driverCitizenId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_citizen;
							// NON-STOCK CODE START
							if (prohibitPocketCars) {
								targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_targetBuilding;
							}
							// NON-STOCK CODE END
							break;
						}
					}
				}
				curCitizenUnitId = nextUnit;
				if (++numIterations > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}

			if (driverCitizenId != 0u) {
				uint laneID = PathManager.GetLaneID(pathPos);
				segmentOffset = (byte)Singleton<SimulationManager>.instance.m_randomizer.Int32(1, 254);
				Vector3 refPos;
				Vector3 vector;
				netManager.m_lanes.m_buffer[(int)((UIntPtr)laneID)].CalculatePositionAndDirection((float)segmentOffset * 0.003921569f, out refPos, out vector);
				NetInfo info = netManager.m_segments.m_buffer[(int)pathPos.m_segment].Info;
				bool isSegmentInverted = (netManager.m_segments.m_buffer[(int)pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
				bool isPosNegative = info.m_lanes[(int)pathPos.m_lane].m_position < 0f;
				vector.Normalize();
				Vector3 searchDir;
				if (isSegmentInverted != isPosNegative) {
					searchDir.x = -vector.z;
					searchDir.y = 0f;
					searchDir.z = vector.x;
				} else {
					searchDir.x = vector.z;
					searchDir.y = 0f;
					searchDir.z = -vector.x;
				}
				ushort homeID = 0;
				if (driverCitizenId != 0u) {
					homeID = Singleton<CitizenManager>.instance.m_citizens.m_buffer[(int)((UIntPtr)driverCitizenId)].m_homeBuilding;
				}
				Vector3 parkPos = default(Vector3);
				Quaternion parkRot = default(Quaternion);
				float parkOffset = -1f;
				
				// NON-STOCK CODE START
				bool foundParkingSpace = false;

				if (prohibitPocketCars) {
					if (Options.debugSwitches[2])
						Log._Debug($"Vehicle {vehicleID} tries to park on a parking position now! path={vehicleData.m_path} pathPositionIndex={vehicleData.m_pathPositionIndex} segmentId={pathPos.m_segment} laneIndex={pathPos.m_lane} offset={pathPos.m_offset} nextPath={nextPath} refPos={refPos} searchDir={searchDir} home={homeID}");

					if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) {
						// try to use previously found parking space
						if (Options.debugSwitches[2])
							Log._Debug($"Vehicle {vehicleID} tries to park on an (alternative) parking position now! CurrentPathMode={driverExtInstance.PathMode} altParkingSpaceLocation={driverExtInstance.ParkingSpaceLocation} altParkingSpaceLocationId={driverExtInstance.ParkingSpaceLocationId}");

						switch (driverExtInstance.ParkingSpaceLocation) {
							case ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide:
								uint parkLaneID; int parkLaneIndex;
								if (Options.debugSwitches[2])
									Log._Debug($"Vehicle {vehicleID} wants to park road-side @ segment {driverExtInstance.ParkingSpaceLocationId}");
								foundParkingSpace = FindParkingSpaceRoadSideForVehiclePos(this.m_info, 0, driverExtInstance.ParkingSpaceLocationId, refPos, out parkPos, out parkRot, out parkOffset, out parkLaneID, out parkLaneIndex);
								break;
							case ExtCitizenInstance.ExtParkingSpaceLocation.Building:
								float maxDist = 9999f;
								if (Options.debugSwitches[2])
									Log._Debug($"Vehicle {vehicleID} wants to park @ building {driverExtInstance.ParkingSpaceLocationId}");
								foundParkingSpace = FindParkingSpaceBuilding(this.m_info, homeID, 0, driverExtInstance.ParkingSpaceLocationId, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[driverExtInstance.ParkingSpaceLocationId], pathPos.m_segment, refPos, ref maxDist, out parkPos, out parkRot, out parkOffset);
								break;
							default:
								Log.Error($"No alternative parking position stored for vehicle {vehicleID}!");
								foundParkingSpace = CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset);
								break;
						}
					}
				}

				if (! foundParkingSpace) {
					foundParkingSpace = Options.prohibitPocketCars ?
						CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset) :
						foundParkingSpace = FindParkingSpace(homeID, refPos, searchDir, pathPos.m_segment, this.m_info.m_generatedInfo.m_size.x, this.m_info.m_generatedInfo.m_size.z, out parkPos, out parkRot, out parkOffset);
				}

				// NON-STOCK CODE END
				ushort parkedVehicleId;
				if (foundParkingSpace && vehicleManager.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, this.m_info, parkPos, parkRot, driverCitizenId)) {
					// we have reached a parking position
					citizenManager.m_citizens.m_buffer[(int)((UIntPtr)driverCitizenId)].SetParkedVehicle(driverCitizenId, parkedVehicleId);
					if (parkOffset >= 0f) {
						segmentOffset = (byte)(parkOffset * 255f);
					}

					// NON-STOCK CODE START
					if (prohibitPocketCars) {
						if (/*(driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && */targetBuildingId != 0) {
							// decrease parking space demand of target building
							ExtBuildingManager.Instance().GetExtBuilding(targetBuildingId).ModifyParkingSpaceDemand(parkPos);
						}

						//if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
							// we have reached an (alternative) parking position and succeeded in finding a parking space
							driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkingSucceeded;
							driverExtInstance.FailedParkingAttempts = 0;
							driverExtInstance.ParkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.None;
							driverExtInstance.ParkingSpaceLocationId = 0;
							if (Options.debugSwitches[2])
								Log._Debug($"Vehicle {vehicleID} has reached an (alternative) parking position! CurrentPathMode={driverExtInstance.PathMode} position={parkPos}");
						//}
					}
				} else if (prohibitPocketCars) {
					// could not find parking space. vehicle would despawn.

					if (/*(driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && */targetBuildingId != 0) {
						// increase parking space demand of target building
						ExtBuildingManager.Instance().GetExtBuilding(targetBuildingId).AddParkingSpaceDemand(5u);
					}

					// Find parking space in the vicinity, redo path-finding to the parking space, park the vehicle and do citizen path-finding to the current target
					++driverExtInstance.FailedParkingAttempts;
					driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkingFailed; // TODO if NOT ... ?
					driverExtInstance.ParkingPathStartPosition = pathPos;

					if (Options.debugSwitches[2])
						Log._Debug($"Parking failed for vehicle {vehicleID}! Trying to find parking space in the vicinity. FailedParkingAttempts={driverExtInstance.FailedParkingAttempts}, CurrentPathMode={driverExtInstance.PathMode} foundParkingSpace={foundParkingSpace}");

					// invalidate paths of all passengers in order to force path recalculation
					uint curUnitId = vehicleData.m_citizenUnits;
					int numIter = 0;
					while (curUnitId != 0u) {
						uint nextUnit = citizenManager.m_units.m_buffer[curUnitId].m_nextUnit;
						for (int i = 0; i < 5; i++) {
							uint curCitizenId = citizenManager.m_units.m_buffer[curUnitId].GetCitizen(i);
							if (curCitizenId != 0u) {
								ushort citizenInstanceId = citizenManager.m_citizens.m_buffer[curCitizenId].m_instance;
								if (citizenInstanceId != 0) {
									if (Options.debugSwitches[2])
										Log._Debug($"Releasing path for citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {citizenManager.m_instances.m_buffer[citizenInstanceId].m_path}).");
									if (citizenManager.m_instances.m_buffer[citizenInstanceId].m_path != 0) {
										Singleton<PathManager>.instance.ReleasePath(citizenManager.m_instances.m_buffer[citizenInstanceId].m_path);
										citizenManager.m_instances.m_buffer[citizenInstanceId].m_path = 0u;
									}
								}
							}
						}
						curUnitId = nextUnit;
						if (++numIter > CitizenManager.MAX_UNIT_COUNT) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
					return false;
					// NON-STOCK CODE END
				}
			} else {
				segmentOffset = pathPos.m_offset;
			}

			// parking has succeeded
			if (driverCitizenId != 0u) {
				uint curCitizenUnit = vehicleData.m_citizenUnits;
				int numIter = 0;
				while (curCitizenUnit != 0u) {
					uint nextUnit = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnit)].m_nextUnit;
					for (int j = 0; j < 5; j++) {
						uint citId = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnit)].GetCitizen(j);
						if (citId != 0u) {
							ushort citizenInstanceId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)citId)].m_instance;
							if (citizenInstanceId != 0) {
								// NON-STOCK CODE START
								if (prohibitPocketCars) {
									if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.ParkingSucceeded) {
										if (Options.debugSwitches[2])
											Log._Debug($"Parking succeeded: Doing nothing for citizen instance {citizenInstanceId}! path: {citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path}");
										ExtCitizenInstanceManager.Instance().GetExtInstance(citizenInstanceId).PathMode = ExtCitizenInstance.ExtPathMode.ParkingSucceeded;
										continue;
									}
								}
								// NON-STOCK CODE END

								if (pathManager.AddPathReference(nextPath)) {
									if (citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path != 0u) {
										pathManager.ReleasePath(citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path);
									}
									citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path = nextPath;
									citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_pathPositionIndex = (byte)nextPositionIndex;
									citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_lastPathOffset = segmentOffset;
									if (Options.debugSwitches[2])
										Log._Debug($"Parking succeeded (default): Setting path of citizen instance {citizenInstanceId} to {nextPath}!");
								}
							}
						}
					}
					curCitizenUnit = nextUnit;
					if (++numIter > 524288) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}

			if (prohibitPocketCars) {
				if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.ParkingSucceeded) {
					if (Options.debugSwitches[2])
						Log._Debug($"Parking succeeded (alternative parking spot): Citizen instance {driverExtInstance} has to walk for the remaining path!");
					/*driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget;
					if (Options.debugSwitches[2])
						Log._Debug($"Setting CurrentPathMode of vehicle {vehicleID} to {driverExtInstance.CurrentPathMode}");*/
				}
			}

			return true;
		}

		private static bool CustomFindParkingSpace(VehicleInfo vehicleInfo, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segment, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Type experimentsToggleType = null;

			float searchRadius = 16f;
			uint chanceOfParkingOffRoad = 3u;

			Vector3 searchMagnitude = refPos + searchDir * 16f;

			bool improvedParkingAi = improvedParkingAiField != null ? (bool)improvedParkingAiField.GetValue(null) : false;
			if (improvedParkingAi) {
				if (Options.debugSwitches[2])
					Log._Debug("RushHour's Improved Parking AI is active.");
				searchRadius = parkingSearchRadiusField != null ? (float)parkingSearchRadiusField.GetValue(null) : searchRadius;
				chanceOfParkingOffRoad = 80u;
			} else {
				if (Options.debugSwitches[2])
					Log._Debug("RushHour's Improved Parking AI is NOT active.");
			}

			if (Options.prohibitPocketCars) {
				searchRadius = Mathf.Max(32f, searchRadius);
			}

			Vector3 refPos2 = refPos + searchDir * 16f;
			if (Singleton<SimulationManager>.instance.m_randomizer.Int32(chanceOfParkingOffRoad) == 0) {
				float width = vehicleInfo.m_generatedInfo.m_size.x;
				float length = vehicleInfo.m_generatedInfo.m_size.z;

				if (FindParkingSpaceRoadSide(0, segment, refPos, width - 0.2f, length, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
				if (FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segment, refPos2, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
			} else {
				if (FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segment, refPos2, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}

				float width = vehicleInfo.m_generatedInfo.m_size.x;
				float length = vehicleInfo.m_generatedInfo.m_size.z;

				if (FindParkingSpaceRoadSide(0, segment, refPos, width - 0.2f, length, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
			}
			return false;
		}

		internal static void OnLevelLoaded() {
			try {
				foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
					Log._Debug($"Found Assembly {a.FullName} @ {a.Location}");
					foreach (Type t in a.GetTypes()) {
						Log._Debug($"\tFound type {t.Name} ({t.FullName}) in {t.Namespace}. // {t.AssemblyQualifiedName}");
					}
				}

				Type experimentsToggleType = null;
				if (LoadingExtension.IsRushHourLoaded) {
					experimentsToggleType = Assembly.GetExecutingAssembly().GetType("RushHour.Experiments.ExperimentsToggle", true);
					if (experimentsToggleType != null) {
						parkingSearchRadiusField = experimentsToggleType.GetField("ParkingSearchRadius", BindingFlags.Public | BindingFlags.Static);
						improvedParkingAiField = experimentsToggleType.GetField("ImprovedParkingAI", BindingFlags.Public | BindingFlags.Static);
						Log._Debug($"parkingSearchRadiusField = {parkingSearchRadiusField}");
						Log._Debug($"improvedParkingAiField = {improvedParkingAiField}");
					}
					Log._Debug($"experimentsToggleType = {experimentsToggleType}");
				}
			} catch (Exception ex) {
				Log.Error("CustomPassengerCarAI.OnLevelLoaded: " + ex.ToString());
			}
		}
	}
}

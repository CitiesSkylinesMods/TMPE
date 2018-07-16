using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.PrioritySegment;

namespace TrafficManager.Manager.Impl {
	public class VehicleBehaviorManager : AbstractCustomManager, IVehicleBehaviorManager {
		public const float MIN_SPEED = 8f * 0.2f; // 10 km/h
		public const float ICY_ROADS_MIN_SPEED = 8f * 0.4f; // 20 km/h
		public const float ICY_ROADS_STUDDED_MIN_SPEED = 8f * 0.8f; // 40 km/h
		public const float WET_ROADS_MAX_SPEED = 8f * 2f; // 100 km/h
		public const float WET_ROADS_FACTOR = 0.75f;
		public const float BROKEN_ROADS_MAX_SPEED = 8f * 1.6f; // 80 km/h
		public const float BROKEN_ROADS_FACTOR = 0.75f;

		public const VehicleInfo.VehicleType RECKLESS_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

		private static PathUnit.Position DUMMY_POS = default(PathUnit.Position);
		private static readonly uint[] POW2MASKS = new uint[] {
			1u, 2u, 4u, 8u,
			16u, 32u, 64u, 128u,
			256u, 512u, 1024u, 2048u,
			4096u, 8192u, 16384u, 32768u,
			65536u, 131072u, 262144u, 524288u,
			1048576u, 2097152u, 4194304u, 8388608u,
			16777216u, 33554432u, 67108864u, 134217728u,
			268435456u, 536870912u, 1073741824u, 2147483648u
		};

		public static readonly VehicleBehaviorManager Instance = new VehicleBehaviorManager();

		private VehicleBehaviorManager() {

		}

		public bool ParkPassengerCar(ushort vehicleID, ref Vehicle vehicleData, VehicleInfo vehicleInfo, uint driverCitizenId, ushort driverCitizenInstanceId, ref ExtCitizenInstance driverExtInstance, ushort targetBuildingId, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverCitizenId;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			PathManager pathManager = Singleton<PathManager>.instance;
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

			// NON-STOCK CODE START
			bool allowPocketCars = true;
			// NON-STOCK CODE END

			if (driverCitizenId != 0u) {
				if (Options.parkingAI && driverCitizenInstanceId != 0) {
					allowPocketCars = false;
				}

				uint laneID = PathManager.GetLaneID(pathPos);
				segmentOffset = (byte)Singleton<SimulationManager>.instance.m_randomizer.Int32(1, 254);
				Vector3 refPos;
				Vector3 vector;
				netManager.m_lanes.m_buffer[laneID].CalculatePositionAndDirection((float)segmentOffset * 0.003921569f, out refPos, out vector);
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
					homeID = Singleton<CitizenManager>.instance.m_citizens.m_buffer[driverCitizenId].m_homeBuilding;
				}
				Vector3 parkPos = default(Vector3);
				Quaternion parkRot = default(Quaternion);
				float parkOffset = -1f;

				// NON-STOCK CODE START
				bool foundParkingSpace = false;
				bool searchedParkingSpace = false;

				if (!allowPocketCars) {
#if DEBUG
					if (debug)
						Log._Debug($"Vehicle {vehicleID} tries to park on a parking position now (flags: {vehicleData.m_flags})! CurrentPathMode={driverExtInstance.pathMode} path={vehicleData.m_path} pathPositionIndex={vehicleData.m_pathPositionIndex} segmentId={pathPos.m_segment} laneIndex={pathPos.m_lane} offset={pathPos.m_offset} nextPath={nextPath} refPos={refPos} searchDir={searchDir} home={homeID} driverCitizenId={driverCitizenId} driverCitizenInstanceId={driverCitizenInstanceId}");
#endif

					if (driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) {
						// try to use previously found parking space
#if DEBUG
						if (debug)
							Log._Debug($"Vehicle {vehicleID} tries to park on an (alternative) parking position now! CurrentPathMode={driverExtInstance.pathMode} altParkingSpaceLocation={driverExtInstance.parkingSpaceLocation} altParkingSpaceLocationId={driverExtInstance.parkingSpaceLocationId}");
#endif

						switch (driverExtInstance.parkingSpaceLocation) {
							case ExtParkingSpaceLocation.RoadSide:
								uint parkLaneID; int parkLaneIndex;
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleID} wants to park road-side @ segment {driverExtInstance.parkingSpaceLocationId}");
#endif
								searchedParkingSpace = true;
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpaceRoadSideForVehiclePos(vehicleInfo, 0, driverExtInstance.parkingSpaceLocationId, refPos, out parkPos, out parkRot, out parkOffset, out parkLaneID, out parkLaneIndex);
								break;
							case ExtParkingSpaceLocation.Building:
								float maxDist = 9999f;
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleID} wants to park @ building {driverExtInstance.parkingSpaceLocationId}");
#endif
								searchedParkingSpace = true;
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpacePropAtBuilding(vehicleInfo, homeID, 0, driverExtInstance.parkingSpaceLocationId, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[driverExtInstance.parkingSpaceLocationId], pathPos.m_segment, refPos, ref maxDist, true, out parkPos, out parkRot, out parkOffset);
								break;
							default:
#if DEBUG
								Log._Debug($"No alternative parking position stored for vehicle {vehicleID}! PathMode={driverExtInstance.pathMode}");
#endif
								break;
						}
					}
				}

				if (!searchedParkingSpace) {
#if DEBUG
					if (debug)
						Log._Debug($"No parking space has yet been queried for vehicle {vehicleID}. Searching now.");
#endif

					searchedParkingSpace = true;
					ExtParkingSpaceLocation location;
					ushort locationId;
					foundParkingSpace = Constants.ManagerFactory.AdvancedParkingManager.FindParkingSpaceInVicinity(refPos, searchDir, vehicleInfo, homeID, vehicleID, Options.parkingAI ? 32f : 16f, out location, out locationId, out parkPos, out parkRot, out parkOffset);
				}

				// NON-STOCK CODE END
				ushort parkedVehicleId = 0;
				bool parkedCarCreated = foundParkingSpace && vehicleManager.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, driverCitizenId);
				if (foundParkingSpace && parkedCarCreated) {
					// we have reached a parking position
#if DEBUG
					float sqrDist = (refPos - parkPos).sqrMagnitude;
					if (fineDebug)
						Log._Debug($"Vehicle {vehicleID} succeeded in parking! CurrentPathMode={driverExtInstance.pathMode} sqrDist={sqrDist}");

					if (GlobalConfig.Instance.Debug.Switches[6] && sqrDist >= 16000) {
						Log._Debug($"CustomPassengerCarAI.CustomParkVehicle: FORCED PAUSE. Distance very large! Vehicle {vehicleID}. dist={sqrDist}");
						Singleton<SimulationManager>.instance.SimulationPaused = true;
					}
#endif

					citizenManager.m_citizens.m_buffer[driverCitizenId].SetParkedVehicle(driverCitizenId, parkedVehicleId);
					if (parkOffset >= 0f) {
						segmentOffset = (byte)(parkOffset * 255f);
					}

					// NON-STOCK CODE START
					if (!allowPocketCars) {
						if ((driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
							// decrease parking space demand of target building
							Constants.ManagerFactory.ExtBuildingManager.ModifyParkingSpaceDemand(ref ExtBuildingManager.Instance.ExtBuildings[targetBuildingId], parkPos, GlobalConfig.Instance.ParkingAI.MinFoundParkPosParkingSpaceDemandDelta, GlobalConfig.Instance.ParkingAI.MaxFoundParkPosParkingSpaceDemandDelta);
						}

						//if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
						// we have reached an (alternative) parking position and succeeded in finding a parking space
						driverExtInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
						driverExtInstance.failedParkingAttempts = 0;
						driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
						driverExtInstance.parkingSpaceLocationId = 0;
#if DEBUG
						if (debug)
							Log._Debug($"Vehicle {vehicleID} has reached an (alternative) parking position! CurrentPathMode={driverExtInstance.pathMode} position={parkPos}");
#endif
						//}
					}
				} else if (!allowPocketCars) {
					// could not find parking space. vehicle would despawn.
					if (targetBuildingId != 0 && (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None) {
						// target is an outside connection
						return true;
					}

					// Find parking space in the vicinity, redo path-finding to the parking space, park the vehicle and do citizen path-finding to the current target

					if (!foundParkingSpace && (driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
						// increase parking space demand of target building
						if (driverExtInstance.failedParkingAttempts > 1) {
							Constants.ManagerFactory.ExtBuildingManager.AddParkingSpaceDemand(ref ExtBuildingManager.Instance.ExtBuildings[targetBuildingId], GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement * (uint)(driverExtInstance.failedParkingAttempts - 1));
						}
					}

					if (!foundParkingSpace) {
						++driverExtInstance.failedParkingAttempts;
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"Parking failed for vehicle {vehicleID}: Parked car could not be created. ABORT.");
#endif
						driverExtInstance.failedParkingAttempts = GlobalConfig.Instance.ParkingAI.MaxParkingAttempts + 1;
					}
					driverExtInstance.pathMode = ExtPathMode.ParkingFailed;
					driverExtInstance.parkingPathStartPosition = pathPos;

#if DEBUG
					if (debug)
						Log._Debug($"Parking failed for vehicle {vehicleID}! (flags: {vehicleData.m_flags}) pathPos segment={pathPos.m_segment}, lane={pathPos.m_lane}, offset={pathPos.m_offset}. Trying to find parking space in the vicinity. FailedParkingAttempts={driverExtInstance.failedParkingAttempts}, CurrentPathMode={driverExtInstance.pathMode} foundParkingSpace={foundParkingSpace}");
#endif

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

#if DEBUG
									if (debug)
										Log._Debug($"Releasing path for citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {citizenManager.m_instances.m_buffer[citizenInstanceId].m_path}).");
#endif
									if (citizenInstanceId != driverCitizenInstanceId) {
#if DEBUG
										if (debug)
											Log._Debug($"Resetting pathmode for passenger citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode}).");
#endif

										Constants.ManagerFactory.ExtCitizenInstanceManager.ResetInstance(citizenInstanceId);
									}

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
				uint curCitizenUnitId = vehicleData.m_citizenUnits;
				int numIter = 0;
				while (curCitizenUnitId != 0u) {
					uint nextUnit = citizenManager.m_units.m_buffer[curCitizenUnitId].m_nextUnit;
					for (int j = 0; j < 5; j++) {
						uint citId = citizenManager.m_units.m_buffer[curCitizenUnitId].GetCitizen(j);
						if (citId != 0u) {
							ushort citizenInstanceId = citizenManager.m_citizens.m_buffer[citId].m_instance;
							if (citizenInstanceId != 0) {
								// NON-STOCK CODE START
								if (!allowPocketCars) {
									if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
#if DEBUG
										if (debug)
											Log._Debug($"Parking succeeded: Doing nothing for citizen instance {citizenInstanceId}! path: {citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path}");
#endif
										ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode = ExtPathMode.RequiresWalkingPathToTarget;
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
#if DEBUG
									if (debug)
										Log._Debug($"Parking succeeded (default): Setting path of citizen instance {citizenInstanceId} to {nextPath}!");
#endif
								}
							}
						}
					}
					curCitizenUnitId = nextUnit;
					if (++numIter > 524288) {
						CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
						break;
					}
				}
			}

			if (!allowPocketCars) {
				if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
#if DEBUG
					if (debug)
						Log._Debug($"Parking succeeded (alternative parking spot): Citizen instance {driverExtInstance} has to walk for the remaining path!");
#endif
					/*driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget;
					if (debug)
						Log._Debug($"Setting CurrentPathMode of vehicle {vehicleID} to {driverExtInstance.CurrentPathMode}");*/
				}
			}

			return true;
		}

		public bool StartPassengerCarPathFind(ushort vehicleID, ref Vehicle vehicleData, VehicleInfo vehicleInfo, ushort driverInstanceId, ref CitizenInstance driverInstanceData, ref ExtCitizenInstance driverExtInstance, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget, bool isHeavyVehicle, bool hasCombustionEngine, bool ignoreBlocked) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverInstanceData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log.Warning($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): called for vehicle {vehicleID}, driverInstanceId={driverInstanceId}, startPos={startPos}, endPos={endPos}, sourceBuilding={vehicleData.m_sourceBuilding}, targetBuilding={vehicleData.m_targetBuilding} pathMode={driverExtInstance.pathMode}");
#endif

			PathUnit.Position startPosA = default(PathUnit.Position);
			PathUnit.Position startPosB = default(PathUnit.Position);
			PathUnit.Position endPosA = default(PathUnit.Position);
			float sqrDistA = 0f;
			float sqrDistB;

			ushort targetBuildingId = driverInstanceData.m_targetBuilding;
			uint driverCitizenId = driverInstanceData.m_citizen;

			// NON-STOCK CODE START
			bool calculateEndPos = true;
			bool allowRandomParking = true;
			bool movingToParkingPos = false;
			bool foundStartingPos = false;
			bool skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
			ExtPathType extPathType = ExtPathType.None;
#if BENCHMARK
			using (var bm = new Benchmark(null, "ParkingAI")) {
#endif
			if (Options.parkingAI) {
				//if (driverExtInstance != null) {
#if DEBUG
					if (debug)
						Log.Warning($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): PathMode={driverExtInstance.pathMode} for vehicle {vehicleID}, driver citizen instance {driverExtInstance.instanceId}!");
#endif

				if (targetBuildingId != 0 && (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None) {
					// target is outside connection
					driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
				} else {
					if (driverExtInstance.pathMode == ExtPathMode.DrivingToTarget || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos || driverExtInstance.pathMode == ExtPathMode.ParkingFailed)
						skipQueue = true;

					bool allowTourists = false;
					bool searchAtCurrentPos = false;
					if (driverExtInstance.pathMode == ExtPathMode.ParkingFailed) {
						// previous parking attempt failed
						driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToAltParkPos;
						allowTourists = true;
						searchAtCurrentPos = true;

#if DEBUG
							if (debug)
								Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Vehicle {vehicleID} shall move to an alternative parking position! CurrentPathMode={driverExtInstance.pathMode} FailedParkingAttempts={driverExtInstance.failedParkingAttempts}");
#endif

						if (driverExtInstance.parkingPathStartPosition != null) {
							startPosA = (PathUnit.Position)driverExtInstance.parkingPathStartPosition;
							foundStartingPos = true;
#if DEBUG
								if (debug)
									Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Setting starting pos for {vehicleID} to segment={startPosA.m_segment}, laneIndex={startPosA.m_lane}, offset={startPosA.m_offset}");
#endif
						}
						startBothWays = false;

						if (driverExtInstance.failedParkingAttempts > GlobalConfig.Instance.ParkingAI.MaxParkingAttempts) {
							// maximum number of parking attempts reached
#if DEBUG
								if (debug)
									Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Reached maximum number of parking attempts for vehicle {vehicleID}! GIVING UP.");
#endif
							Constants.ManagerFactory.ExtCitizenInstanceManager.Reset(ref driverExtInstance);

							// pocket car fallback
							//vehicleData.m_flags |= Vehicle.Flags.Parking;
							return false;
						} else {
#if DEBUG
								if (fineDebug)
									Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Increased number of parking attempts for vehicle {vehicleID}: {driverExtInstance.failedParkingAttempts}/{GlobalConfig.Instance.ParkingAI.MaxParkingAttempts}");
#endif
						}
					} else {
						driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToKnownParkPos;
					}

					ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[driverCitizenId].m_homeBuilding;
					bool calcEndPos;
					Vector3 parkPos;

					if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(searchAtCurrentPos ? vehicleData.GetLastFramePosition() : endPos, vehicleData.Info, ref driverExtInstance, homeId, targetBuildingId == homeId, vehicleID, allowTourists, out parkPos, ref endPosA, out calcEndPos)) {
						calculateEndPos = calcEndPos;
						allowRandomParking = false;
						movingToParkingPos = true;

						if (!Constants.ManagerFactory.ExtCitizenInstanceManager.CalculateReturnPath(ref driverExtInstance, parkPos, endPos)) {
#if DEBUG
								if (debug)
									Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Could not calculate return path for citizen instance {driverExtInstance.instanceId}, vehicle {vehicleID}. Resetting instance.");
#endif
							Constants.ManagerFactory.ExtCitizenInstanceManager.Reset(ref driverExtInstance);
							return false;
						}
					} else if (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
						// no alternative parking spot found: abort
#if DEBUG
							if (debug)
								Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.instanceId} with CurrentPathMode={driverExtInstance.pathMode}! GIVING UP.");
#endif
						Constants.ManagerFactory.ExtCitizenInstanceManager.Reset(ref driverExtInstance);
						return false;
					} else {
						// calculate a direct path to target
#if DEBUG
							if (debug)
								Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.instanceId} with CurrentPathMode={driverExtInstance.pathMode}! Setting CurrentPathMode to 'CalculatingCarPath'.");
#endif
						driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
					}
				}

				extPathType = driverExtInstance.GetPathType();
				/*} else {
#if DEBUG
					if (debug)
						Log.Warning($"CustomPassengerCarAI.CustomStartPathFind: No driver citizen instance found for vehicle {vehicleID}!");
#endif
				}*/
			}
#if BENCHMARK
			}
#endif

			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle;
			if (!movingToParkingPos) {
				laneTypes |= NetInfo.LaneType.Pedestrian;
			}
			// NON-STOCK CODE END

			VehicleInfo.VehicleType vehicleTypes = vehicleInfo.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			bool randomParking = false;
			bool combustionEngine = vehicleInfo.m_class.m_subService == ItemClass.SubService.ResidentialLow;
			if (allowRandomParking && // NON-STOCK CODE
				!movingToParkingPos &&
				targetBuildingId != 0 &&
				(
					Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].Info.m_class.m_service > ItemClass.Service.Office ||
					(driverInstanceData.m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None
				)) {
				randomParking = true;
			}

#if DEBUG
			if (fineDebug)
				Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Requesting path-finding for passenger car {vehicleID}, startPos={startPos}, endPos={endPos}, extPathType={extPathType}");
#endif

			// NON-STOCK CODE START
			if (!foundStartingPos) {
				foundStartingPos = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleTypes, allowUnderground, false, 32f, out startPosA, out startPosB, out sqrDistA, out sqrDistB);
			}

			bool foundEndPos = !calculateEndPos || Constants.ManagerFactory.ExtCitizenInstanceManager.FindEndPathPosition(driverInstanceId, ref driverInstanceData, endPos, Options.parkingAI && (targetBuildingId == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : (laneTypes | NetInfo.LaneType.Pedestrian), vehicleTypes, undergroundTarget, out endPosA);
			// NON-STOCK CODE END

			if (foundStartingPos &&
				foundEndPos) { // NON-STOCK CODE

				if (!startBothWays || sqrDistA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				PathUnit.Position endPosB = default(PathUnit.Position);
				SimulationManager simMan = Singleton<SimulationManager>.instance;
				uint path;
				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = extPathType;
				args.extVehicleType = ExtVehicleType.PassengerCar;
				args.vehicleId = vehicleID;
				args.buildIndex = simMan.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = dummyPathPos;
				args.laneTypes = laneTypes;
				args.vehicleTypes = vehicleTypes;
				args.maxLength = 20000f;
				args.isHeavyVehicle = isHeavyVehicle;
				args.hasCombustionEngine = hasCombustionEngine;
				args.ignoreBlocked = ignoreBlocked;
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = randomParking;
				args.stablePath = false;
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

				if (CustomPathManager._instance.CustomCreatePath(out path, ref simMan.m_randomizer, args)) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Path-finding starts for passenger car {vehicleID}, path={path}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleTypes}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
#endif
					// NON-STOCK CODE END

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

		public bool IsSpaceReservationAllowed(ushort transitNodeId, PathUnit.Position sourcePos, PathUnit.Position targetPos) {
			if (!Options.timedLightsEnabled) {
				return true;
			}

			if (TrafficLightSimulationManager.Instance.HasActiveTimedSimulation(transitNodeId)) {
				RoadBaseAI.TrafficLightState vehLightState;
				RoadBaseAI.TrafficLightState pedLightState;
#if DEBUG
				Vehicle dummyVeh = default(Vehicle);
#endif
				Constants.ManagerFactory.TrafficLightSimulationManager.GetTrafficLightState(
#if DEBUG
					0, ref dummyVeh,
#endif
					transitNodeId, sourcePos.m_segment, sourcePos.m_lane, targetPos.m_segment, ref Singleton<NetManager>.instance.m_segments.m_buffer[sourcePos.m_segment], 0, out vehLightState, out pedLightState);

				if (vehLightState == RoadBaseAI.TrafficLightState.Red) {
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Checks for traffic lights and priority signs when changing segments (for rail vehicles).
		/// Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change is not allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
		/// </summary>
		/// <param name="frontVehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="sqrVelocity">last frame squared velocity</param>
		/// <param name="prevPos">previous path position</param>
		/// <param name="prevTargetNodeId">previous target node</param>
		/// <param name="prevLaneID">previous lane</param>
		/// <param name="position">current path position</param>
		/// <param name="targetNodeId">transit node</param>
		/// <param name="laneID">current lane</param>
		/// <returns>true, if the vehicle may change segments, false otherwise.</returns>
		public bool MayChangeSegment(ushort frontVehicleId, ref Vehicle vehicleData, float sqrVelocity, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID) {
			VehicleJunctionTransitState transitState = MayChangeSegment(frontVehicleId, ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId], ref vehicleData, sqrVelocity, ref prevPos, ref prevSegment, prevTargetNodeId, prevLaneID, ref position, targetNodeId, ref targetNode, laneID, ref DUMMY_POS, 0);
			Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId], transitState);
			return transitState == VehicleJunctionTransitState.Leave /* || transitState == VehicleJunctionTransitState.Blocked*/;
		}

		/// <summary>
		/// Checks for traffic lights and priority signs when changing segments (for road & rail vehicles).
		/// Sets the maximum allowed speed <paramref name="maxSpeed"/> if segment change is not allowed (otherwise <paramref name="maxSpeed"/> has to be set by the calling method).
		/// </summary>
		/// <param name="frontVehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="sqrVelocity">last frame squared velocity</param>
		/// <param name="prevPos">previous path position</param>
		/// <param name="prevTargetNodeId">previous target node</param>
		/// <param name="prevLaneID">previous lane</param>
		/// <param name="position">current path position</param>
		/// <param name="targetNodeId">transit node</param>
		/// <param name="laneID">current lane</param>
		/// <param name="nextPosition">next path position</param>
		/// <param name="nextTargetNodeId">next target node</param>
		/// <returns>true, if the vehicle may change segments, false otherwise.</returns>
		public bool MayChangeSegment(ushort frontVehicleId, ref Vehicle vehicleData, float sqrVelocity, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID, ref PathUnit.Position nextPosition, ushort nextTargetNodeId) {
			VehicleJunctionTransitState transitState = MayChangeSegment(frontVehicleId, ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId], ref vehicleData, sqrVelocity, ref prevPos, ref prevSegment, prevTargetNodeId, prevLaneID, ref position, targetNodeId, ref targetNode, laneID, ref nextPosition, nextTargetNodeId);
			Constants.ManagerFactory.ExtVehicleManager.SetJunctionTransitState(ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[frontVehicleId], transitState);
			return transitState == VehicleJunctionTransitState.Leave /* || transitState == VehicleJunctionTransitState.Blocked*/;
		}

		protected VehicleJunctionTransitState MayChangeSegment(ushort frontVehicleId, ref ExtVehicle extVehicle, ref Vehicle vehicleData, float sqrVelocity, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID, ref PathUnit.Position nextPosition, ushort nextTargetNodeId) {
			//public bool MayChangeSegment(ushort frontVehicleId, ref VehicleState vehicleState, ref Vehicle vehicleData, float sqrVelocity, bool isRecklessDriver, ref PathUnit.Position prevPos, ref NetSegment prevSegment, ushort prevTargetNodeId, uint prevLaneID, ref PathUnit.Position position, ushort targetNodeId, ref NetNode targetNode, uint laneID, ref PathUnit.Position nextPosition, ushort nextTargetNodeId, out float maxSpeed) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[13] && (GlobalConfig.Instance.Debug.NodeId <= 0 || targetNodeId == GlobalConfig.Instance.Debug.NodeId);
#endif

			if (prevTargetNodeId != targetNodeId
				|| (vehicleData.m_blockCounter == 255 && !VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) // NON-STOCK CODE
			) {
				// method should only be called if targetNodeId == prevTargetNode
				return VehicleJunctionTransitState.Leave;
			}

			if (extVehicle.junctionTransitState == VehicleJunctionTransitState.Leave) {
				// vehicle was already allowed to leave the junction
				if (sqrVelocity <= TrafficPriorityManager.MAX_SQR_STOP_VELOCITY && (extVehicle.vehicleType & ExtVehicleType.RoadVehicle) != ExtVehicleType.None) {
					// vehicle is not moving. reset allowance to leave junction
#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState from LEAVE to BLOCKED (speed to low)");
#endif
					return VehicleJunctionTransitState.Blocked;
				} else {
					return VehicleJunctionTransitState.Leave;
				}
			}

			uint currentFrameIndex = Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
			if ((extVehicle.junctionTransitState == VehicleJunctionTransitState.Stop || extVehicle.junctionTransitState == VehicleJunctionTransitState.Blocked) &&
				extVehicle.lastTransitStateUpdate >> ExtVehicleManager.JUNCTION_RECHECK_SHIFT >= currentFrameIndex >> ExtVehicleManager.JUNCTION_RECHECK_SHIFT) {
				// reuse recent result
				return extVehicle.junctionTransitState;
			}

			bool isRecklessDriver = extVehicle.recklessDriver;

			var netManager = Singleton<NetManager>.instance;
			IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;

			bool hasActiveTimedSimulation = (Options.timedLightsEnabled && TrafficLightSimulationManager.Instance.HasActiveTimedSimulation(targetNodeId));
			bool hasTrafficLightFlag = (targetNode.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
			if (hasActiveTimedSimulation && !hasTrafficLightFlag) {
				TrafficLightManager.Instance.AddTrafficLight(targetNodeId, ref targetNode);
			}
			bool hasTrafficLight = hasTrafficLightFlag || hasActiveTimedSimulation;
			bool checkTrafficLights = true;
			bool isTargetStartNode = prevSegment.m_startNode == targetNodeId;
			bool isLevelCrossing = (targetNode.m_flags & NetNode.Flags.LevelCrossing) != NetNode.Flags.None;
			if ((vehicleData.Info.m_vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) == VehicleInfo.VehicleType.None) {
				// check if to check space

#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {frontVehicleId} is not a train.");
#endif

				// stock priority signs
				if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) == (Vehicle.Flags)0 &&
					((NetLane.Flags)netManager.m_lanes.m_buffer[prevLaneID].m_flags & (NetLane.Flags.YieldStart | NetLane.Flags.YieldEnd)) != NetLane.Flags.None &&
					(targetNode.m_flags & (NetNode.Flags.Junction | NetNode.Flags.TrafficLights | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction) {
					if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram || vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train) {
						if ((vehicleData.m_flags2 & Vehicle.Flags2.Yielding) == (Vehicle.Flags2)0) {
							if (sqrVelocity < 0.01f) {
								vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
							}
							return VehicleJunctionTransitState.Stop;
						} else {
							vehicleData.m_waitCounter = (byte)Mathf.Min((int)(vehicleData.m_waitCounter + 1), 4);
							if (vehicleData.m_waitCounter < 4) {
								return VehicleJunctionTransitState.Stop;
							}
							vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
							vehicleData.m_waitCounter = 0;
						}
					} else if (sqrVelocity > 0.01f) {
						return VehicleJunctionTransitState.Stop;
					}
				}

				// entering blocked junctions
				if (MustCheckSpace(prevPos.m_segment, isTargetStartNode, ref targetNode, isRecklessDriver)) {
					// check if there is enough space
					var len = extVehicle.totalLength + 2f;
					if (!netManager.m_lanes.m_buffer[laneID].CheckSpace(len)) {
						var sufficientSpace = false;
						if (nextPosition.m_segment != 0 && netManager.m_lanes.m_buffer[laneID].m_length < 30f) {
							NetNode.Flags nextTargetNodeFlags = netManager.m_nodes.m_buffer[nextTargetNodeId].m_flags;
							if ((nextTargetNodeFlags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction ||
								netManager.m_nodes.m_buffer[nextTargetNodeId].CountSegments() == 2) {
								uint nextLaneId = PathManager.GetLaneID(nextPosition);
								if (nextLaneId != 0u) {
									sufficientSpace = netManager.m_lanes.m_buffer[nextLaneId].CheckSpace(len);
								}
							}
						}

						if (!sufficientSpace) {
#if DEBUG
							if (debug)
								Log._Debug($"Vehicle {frontVehicleId}: Setting JunctionTransitState to BLOCKED");
#endif

							return VehicleJunctionTransitState.Blocked;
						}
					}
				}

				bool isJoinedJunction = ((NetLane.Flags)netManager.m_lanes.m_buffer[prevLaneID].m_flags & NetLane.Flags.JoinedJunction) != NetLane.Flags.None;
				checkTrafficLights = !isJoinedJunction || isLevelCrossing;
			} else {
#if DEBUG
				if (debug)
					Log._Debug($"CustomVehicleAI.MayChangeSegment: Vehicle {frontVehicleId} is a train/metro/monorail.");
#endif

				if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Monorail) {
					// vanilla traffic light flags are not rendered on monorail tracks
					checkTrafficLights = hasActiveTimedSimulation;
				} else if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train) {
					// vanilla traffic light flags are not rendered on train tracks, except for level crossings
					checkTrafficLights = hasActiveTimedSimulation || isLevelCrossing;
				}
			}

			VehicleJunctionTransitState transitState = extVehicle.junctionTransitState;
			if (extVehicle.junctionTransitState == VehicleJunctionTransitState.Blocked) {
#if DEBUG
				if (debug)
					Log._Debug($"Vehicle {frontVehicleId}: Setting JunctionTransitState from BLOCKED to APPROACH");
#endif
				transitState = VehicleJunctionTransitState.Approach;
			}

			ITrafficPriorityManager prioMan = TrafficPriorityManager.Instance;
			ICustomSegmentLightsManager segLightsMan = CustomSegmentLightsManager.Instance;
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
				return VehicleJunctionTransitState.Leave;
			} else if (hasTrafficLight && checkTrafficLights) {
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Node {targetNodeId} has a traffic light.");
				}
#endif
				bool stopCar = false;
				uint simGroup = (uint)targetNodeId >> 7;

				RoadBaseAI.TrafficLightState vehicleLightState;
				RoadBaseAI.TrafficLightState pedestrianLightState;
				bool vehicles;
				bool pedestrians;
				Constants.ManagerFactory.TrafficLightSimulationManager.GetTrafficLightState(
#if DEBUG
						frontVehicleId, ref vehicleData,
#endif
						targetNodeId, prevPos.m_segment, prevPos.m_lane, position.m_segment, ref prevSegment, currentFrameIndex - simGroup, out vehicleLightState, out pedestrianLightState, out vehicles, out pedestrians); // TODO current frame index or reference frame index?

				if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car && isRecklessDriver) { // TODO no reckless driving at railroad crossings
					vehicleLightState = RoadBaseAI.TrafficLightState.Green;
				}

#if DEBUG
				if (debug)
					Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle {frontVehicleId} has TL state {vehicleLightState} at node {targetNodeId} (recklessDriver={isRecklessDriver})");
#endif

				uint random = currentFrameIndex - simGroup & 255u;
				if (!vehicles && random >= 196u) {
					vehicles = true;
					RoadBaseAI.SetTrafficLightState(targetNodeId, ref prevSegment, currentFrameIndex - simGroup, vehicleLightState, pedestrianLightState, vehicles, pedestrians);
				}

				switch (vehicleLightState) {
					case RoadBaseAI.TrafficLightState.RedToGreen:
						if (random < 60u) {
							stopCar = true;
						}
						break;
					case RoadBaseAI.TrafficLightState.Red:
						stopCar = true;
						break;
					case RoadBaseAI.TrafficLightState.GreenToRed:
						if (random >= 30u) {
							stopCar = true;
						}
						break;
				}

				// check priority rules at unprotected traffic lights
				if (!stopCar && Options.prioritySignsEnabled && Options.trafficLightPriorityRules && segLightsMan.IsSegmentLight(prevPos.m_segment, isTargetStartNode)) {
					bool hasPriority = prioMan.HasPriority(frontVehicleId, ref vehicleData, ref prevPos, targetNodeId, isTargetStartNode, ref position, ref targetNode);

					if (!hasPriority) {
						// green light but other cars are incoming and they have priority: stop
#if DEBUG
						if (debug)
							Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Green traffic light but detected traffic with higher priority: stop.");
#endif
						stopCar = true;
					}
				}

				if (stopCar) {
#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to STOP");
#endif

					if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram || vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train) {
						vehicleData.m_flags2 |= Vehicle.Flags2.Yielding;
						vehicleData.m_waitCounter = 0;
					}

					vehicleData.m_blockCounter = 0;
					return VehicleJunctionTransitState.Stop;
				} else {
#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to LEAVE ({vehicleLightState})");
#endif

					if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Tram || vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Train) {
						vehicleData.m_flags2 &= ~Vehicle.Flags2.Yielding;
						vehicleData.m_waitCounter = 0;
					}

					return VehicleJunctionTransitState.Leave;
				}
			} else if (Options.prioritySignsEnabled && vehicleData.Info.m_vehicleType != VehicleInfo.VehicleType.Monorail) {
#if DEBUG
				if (debug)
					Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {targetNodeId} which is not a traffic light.");
#endif

				var sign = prioMan.GetPrioritySign(prevPos.m_segment, isTargetStartNode);
				if (sign != PriorityType.None) {
#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle is arriving @ seg. {prevPos.m_segment} ({position.m_segment}, {nextPosition.m_segment}), node {targetNodeId} which is not a traffic light and is a priority segment.");
#endif

#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): JunctionTransitState={transitState.ToString()}");
#endif

					if (transitState == VehicleJunctionTransitState.None) {
#if DEBUG
						if (debug)
							Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to APPROACH (prio)");
#endif
						transitState = VehicleJunctionTransitState.Approach;
					}

					if (sign == PriorityType.Stop) {
						if (transitState == VehicleJunctionTransitState.Approach) {
							extVehicle.waitTime = 0;
						}

						if (sqrVelocity <= TrafficPriorityManager.MAX_SQR_STOP_VELOCITY) {
							++extVehicle.waitTime;

							if (extVehicle.waitTime < 2) {
								vehicleData.m_blockCounter = 0;
								return VehicleJunctionTransitState.Stop;
							}
						} else {
#if DEBUG
							if (debug)
								Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Vehicle has come to a full stop.");
#endif
							vehicleData.m_blockCounter = 0;
							return VehicleJunctionTransitState.Stop;
						}
					}
#if DEBUG
					if (debug)
						Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): {sign} sign. waittime={extVehicle.waitTime}");
#endif
					if (extVehicle.waitTime < GlobalConfig.Instance.PriorityRules.MaxPriorityWaitTime) {
						extVehicle.waitTime++;
#if DEBUG
						if (debug)
							Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to STOP (wait)");
#endif
						bool hasPriority = prioMan.HasPriority(frontVehicleId, ref vehicleData, ref prevPos, targetNodeId, isTargetStartNode, ref position, ref targetNode);
#if DEBUG
						if (debug)
							Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): hasPriority: {hasPriority}");
#endif

						if (!hasPriority) {
							vehicleData.m_blockCounter = 0;
							return VehicleJunctionTransitState.Stop;
						} else {
#if DEBUG
							if (debug)
								Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to LEAVE (no conflicting cars)");
#endif
							return VehicleJunctionTransitState.Leave;
						}
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"VehicleBehaviorManager.MayChangeSegment({frontVehicleId}): Setting JunctionTransitState to LEAVE (max wait timeout)");
#endif
						return VehicleJunctionTransitState.Leave;
					}
				} else {
					return VehicleJunctionTransitState.Leave;
				}
			} else {
				return VehicleJunctionTransitState.Leave;
			}
		}

		/// <summary>
		/// Checks if a vehicle must check if the subsequent segment is empty while going from segment <paramref name="segmentId"/>
		/// through node <paramref name="startNode"/>.
		/// </summary>
		/// <param name="segmentId">source segment id</param>
		/// <param name="startNode">is transit node start node of source segment?</param>
		/// <param name="node">transit node</param>
		/// <param name="isRecklessDriver">reckless driver?</param>
		/// <returns></returns>
		protected bool MustCheckSpace(ushort segmentId, bool startNode, ref NetNode node, bool isRecklessDriver) {
			if (isRecklessDriver) {
				return false;
			} else {
				bool checkSpace;
				if (Options.junctionRestrictionsEnabled) {
					checkSpace = !JunctionRestrictionsManager.Instance.IsEnteringBlockedJunctionAllowed(segmentId, startNode);
				} else {
					checkSpace = (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut | NetNode.Flags.OneWayIn)) == NetNode.Flags.Junction;
				}

				return checkSpace & node.CountSegments() != 2;
			}
		}

		public bool MayDespawn(ref Vehicle vehicleData) {
			return !Options.disableDespawning || ((vehicleData.m_flags2 & (Vehicle.Flags2.Blown | Vehicle.Flags2.Floating)) != 0) || (vehicleData.m_flags & Vehicle.Flags.Parking) != 0;
		}

		public float CalcMaxSpeed(ushort vehicleId, ref Vehicle vehicleData, VehicleInfo vehicleInfo, PathUnit.Position position, ref NetSegment segment, Vector3 pos, float maxSpeed) {
			if (Singleton<NetManager>.instance.m_treatWetAsSnow) {
				DistrictManager districtManager = Singleton<DistrictManager>.instance;
				byte district = districtManager.GetDistrict(pos);
				DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPolicies;
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.StuddedTires) != DistrictPolicies.CityPlanning.None) {
					if (Options.strongerRoadConditionEffects) {
						if (maxSpeed > ICY_ROADS_STUDDED_MIN_SPEED)
							maxSpeed = ICY_ROADS_STUDDED_MIN_SPEED + (float)(255 - segment.m_wetness) * 0.0039215686f * (maxSpeed - ICY_ROADS_STUDDED_MIN_SPEED);
					} else {
						maxSpeed *= 1f - (float)segment.m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
					}
					districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPoliciesEffect |= DistrictPolicies.CityPlanning.StuddedTires;
				} else {
					if (Options.strongerRoadConditionEffects) {
						if (maxSpeed > ICY_ROADS_MIN_SPEED)
							maxSpeed = ICY_ROADS_MIN_SPEED + (float)(255 - segment.m_wetness) * 0.0039215686f * (maxSpeed - ICY_ROADS_MIN_SPEED);
					} else {
						maxSpeed *= 1f - (float)segment.m_wetness * 0.00117647066f; // vanilla: -30% .. ±0%
					}
				}
			} else {
				if (Options.strongerRoadConditionEffects) {
					float minSpeed = Math.Min(maxSpeed * WET_ROADS_FACTOR, WET_ROADS_MAX_SPEED); // custom: -25% .. 0
					if (maxSpeed > minSpeed)
						maxSpeed = minSpeed + (float)(255 - segment.m_wetness) * 0.0039215686f * (maxSpeed - minSpeed);
				} else {
					maxSpeed *= 1f - (float)segment.m_wetness * 0.0005882353f; // vanilla: -15% .. ±0%
				}
			}

			if (Options.strongerRoadConditionEffects) {
				float minSpeed = Math.Min(maxSpeed * BROKEN_ROADS_FACTOR, BROKEN_ROADS_MAX_SPEED);
				if (maxSpeed > minSpeed) {
					maxSpeed = minSpeed + (float)segment.m_condition * 0.0039215686f * (maxSpeed - minSpeed);
				}
			} else {
				maxSpeed *= 1f + (float)segment.m_condition * 0.0005882353f; // vanilla: ±0% .. +15 %
			}

			maxSpeed = ApplyRealisticSpeeds(maxSpeed, vehicleId, ref vehicleData, vehicleInfo);
			maxSpeed = Math.Max(MIN_SPEED, maxSpeed); // at least 10 km/h

			return maxSpeed;
		}

		public float ApplyRealisticSpeeds(float speed, ushort vehicleId, ref Vehicle vehicleData, VehicleInfo vehicleInfo) {
			bool isRecklessDriver = IsRecklessDriver(vehicleId, ref vehicleData);
			if (Options.realisticSpeeds) {
				float vehicleRand = 0.01f * (float)Constants.ManagerFactory.ExtVehicleManager.GetVehicleRand(vehicleId);
				if (vehicleInfo.m_isLargeVehicle) {
					speed *= 0.75f + vehicleRand * 0.25f; // a little variance, 0.75 .. 1
				} else if (isRecklessDriver) {
					speed *= 1.3f + vehicleRand * 1.7f; // woohooo, 1.3 .. 3
				} else {
					speed *= 0.8f + vehicleRand * 0.5f; // a little variance, 0.8 .. 1.3
				}
			} else if (isRecklessDriver) {
				speed *= 1.5f;
			}
			return speed;
		}

		public bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData) {
			if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
				return true;
			}
			if (Options.evacBussesMayIgnoreRules && vehicleData.Info.GetService() == ItemClass.Service.Disaster) {
				return true;
			}
			if (Options.recklessDrivers == 3) {
				return false;
			}
			if ((vehicleData.Info.m_vehicleType & RECKLESS_VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
				return false;
			}

			return (uint)vehicleId % Options.getRecklessDriverModulo() == 0;
		}

		public int FindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref ExtVehicle vehicleState, uint currentLaneId, PathUnit.Position currentPathPos, NetInfo currentSegInfo, PathUnit.Position next1PathPos, NetInfo next1SegInfo, PathUnit.Position next2PathPos, NetInfo next2SegInfo, PathUnit.Position next3PathPos, NetInfo next3SegInfo, PathUnit.Position next4PathPos) {
			try {
				GlobalConfig conf = GlobalConfig.Instance;
#if DEBUG
				bool debug = false;
				if (conf.Debug.Switches[17]) {
					ushort nodeId = Services.NetService.GetSegmentNodeId(currentPathPos.m_segment, currentPathPos.m_offset < 128);
					debug = (conf.Debug.VehicleId == 0 || conf.Debug.VehicleId == vehicleId) && (conf.Debug.NodeId == 0 || conf.Debug.NodeId == nodeId);
				}

				if (debug) {
					Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): currentLaneId={currentLaneId}, currentPathPos=[seg={currentPathPos.m_segment}, lane={currentPathPos.m_lane}, off={currentPathPos.m_offset}] next1PathPos=[seg={next1PathPos.m_segment}, lane={next1PathPos.m_lane}, off={next1PathPos.m_offset}] next2PathPos=[seg={next2PathPos.m_segment}, lane={next2PathPos.m_lane}, off={next2PathPos.m_offset}] next3PathPos=[seg={next3PathPos.m_segment}, lane={next3PathPos.m_lane}, off={next3PathPos.m_offset}] next4PathPos=[seg={next4PathPos.m_segment}, lane={next4PathPos.m_lane}, off={next4PathPos.m_offset}]");
				}
#endif

				if (vehicleState.lastAltLaneSelSegmentId == currentPathPos.m_segment) {
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping alternative lane selection: Already calculated.");
					}
#endif
					return next1PathPos.m_lane;
				}
				vehicleState.lastAltLaneSelSegmentId = currentPathPos.m_segment;

				bool recklessDriver = vehicleState.recklessDriver;

				// cur -> next1
				float vehicleLength = 1f + vehicleState.totalLength;
				bool startNode = currentPathPos.m_offset < 128;
				uint currentFwdRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(currentLaneId, startNode);

#if DEBUG
				if (currentFwdRoutingIndex < 0 || currentFwdRoutingIndex >= RoutingManager.Instance.laneEndForwardRoutings.Length) {
					Log.Error($"Invalid array index: currentFwdRoutingIndex={currentFwdRoutingIndex}, RoutingManager.Instance.laneEndForwardRoutings.Length={RoutingManager.Instance.laneEndForwardRoutings.Length} (currentLaneId={currentLaneId}, startNode={startNode})");
				}
#endif

				if (!RoutingManager.Instance.laneEndForwardRoutings[currentFwdRoutingIndex].routed) {
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): No forward routing for next path position available.");
					}
#endif
					return next1PathPos.m_lane;
				}

				LaneTransitionData[] currentFwdTransitions = RoutingManager.Instance.laneEndForwardRoutings[currentFwdRoutingIndex].transitions;

				if (currentFwdTransitions == null) {
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): No forward transitions found for current lane {currentLaneId} at startNode {startNode}.");
					}
#endif
					return next1PathPos.m_lane;
				}

				VehicleInfo vehicleInfo = vehicleData.Info;
				float vehicleMaxSpeed = vehicleInfo.m_maxSpeed / 8f;
				float vehicleCurSpeed = vehicleData.GetLastFrameVelocity().magnitude / 8f;

				float bestStayMeanSpeed = 0f;
				float bestStaySpeedDiff = float.PositiveInfinity; // best speed difference on next continuous lane
				int bestStayTotalLaneDist = int.MaxValue;
				byte bestStayNext1LaneIndex = next1PathPos.m_lane;

				float bestOptMeanSpeed = 0f;
				float bestOptSpeedDiff = float.PositiveInfinity; // best speed difference on all next lanes
				int bestOptTotalLaneDist = int.MaxValue;
				byte bestOptNext1LaneIndex = next1PathPos.m_lane;

				bool foundSafeLaneChange = false;
				//bool foundClearBackLane = false;
				//bool foundClearFwdLane = false;

				//ushort reachableNext1LanesMask = 0;
				uint reachableNext2LanesMask = 0;
				uint reachableNext3LanesMask = 0;

				//int numReachableNext1Lanes = 0;
				int numReachableNext2Lanes = 0;
				int numReachableNext3Lanes = 0;

#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Starting lane-finding algorithm now. vehicleMaxSpeed={vehicleMaxSpeed}, vehicleCurSpeed={vehicleCurSpeed} vehicleLength={vehicleLength}");
				}
#endif

				uint mask;
				for (int i = 0; i < currentFwdTransitions.Length; ++i) {
					if (currentFwdTransitions[i].segmentId != next1PathPos.m_segment) {
						continue;
					}

					if (!(currentFwdTransitions[i].type == LaneEndTransitionType.Default ||
						currentFwdTransitions[i].type == LaneEndTransitionType.LaneConnection ||
						(recklessDriver && currentFwdTransitions[i].type == LaneEndTransitionType.Relaxed))
					) {
						continue;
					}

					if (currentFwdTransitions[i].distance > 1) {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping current transition {currentFwdTransitions[i]} (distance too large)");
						}
#endif
						continue;
					}

					if (!VehicleRestrictionsManager.Instance.MayUseLane(vehicleState.vehicleType, next1PathPos.m_segment, currentFwdTransitions[i].laneIndex, next1SegInfo)) {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping current transition {currentFwdTransitions[i]} (vehicle restrictions)");
						}
#endif
						continue;
					}

					int minTotalLaneDist = int.MaxValue;

					if (next2PathPos.m_segment != 0) {
						// next1 -> next2
						uint next1FwdRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(currentFwdTransitions[i].laneId, !currentFwdTransitions[i].startNode);
#if DEBUG
						if (next1FwdRoutingIndex < 0 || next1FwdRoutingIndex >= RoutingManager.Instance.laneEndForwardRoutings.Length) {
							Log.Error($"Invalid array index: next1FwdRoutingIndex={next1FwdRoutingIndex}, RoutingManager.Instance.laneEndForwardRoutings.Length={RoutingManager.Instance.laneEndForwardRoutings.Length} (currentFwdTransitions[i].laneId={currentFwdTransitions[i].laneId}, !currentFwdTransitions[i].startNode={!currentFwdTransitions[i].startNode})");
						}
#endif

#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Exploring transitions for next1 lane id={currentFwdTransitions[i].laneId}, seg.={currentFwdTransitions[i].segmentId}, index={currentFwdTransitions[i].laneIndex}, startNode={!currentFwdTransitions[i].startNode}: {RoutingManager.Instance.laneEndForwardRoutings[next1FwdRoutingIndex]}");
						}
#endif
						if (!RoutingManager.Instance.laneEndForwardRoutings[next1FwdRoutingIndex].routed) {
							continue;
						}
						LaneTransitionData[] next1FwdTransitions = RoutingManager.Instance.laneEndForwardRoutings[next1FwdRoutingIndex].transitions;

						if (next1FwdTransitions == null) {
							continue;
						}

						bool foundNext1Next2 = false;
						for (int j = 0; j < next1FwdTransitions.Length; ++j) {
							if (next1FwdTransitions[j].segmentId != next2PathPos.m_segment) {
								continue;
							}

							if (!(next1FwdTransitions[j].type == LaneEndTransitionType.Default ||
								next1FwdTransitions[j].type == LaneEndTransitionType.LaneConnection ||
								(recklessDriver && next1FwdTransitions[j].type == LaneEndTransitionType.Relaxed))
							) {
								continue;
							}

							if (next1FwdTransitions[j].distance > 1) {
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next1 transition {next1FwdTransitions[j]} (distance too large)");
								}
#endif
								continue;
							}

							if (!VehicleRestrictionsManager.Instance.MayUseLane(vehicleState.vehicleType, next2PathPos.m_segment, next1FwdTransitions[j].laneIndex, next2SegInfo)) {
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next1 transition {next1FwdTransitions[j]} (vehicle restrictions)");
								}
#endif
								continue;
							}

							if (next3PathPos.m_segment != 0) {
								// next2 -> next3
								uint next2FwdRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(next1FwdTransitions[j].laneId, !next1FwdTransitions[j].startNode);
#if DEBUG
								if (next2FwdRoutingIndex < 0 || next2FwdRoutingIndex >= RoutingManager.Instance.laneEndForwardRoutings.Length) {
									Log.Error($"Invalid array index: next2FwdRoutingIndex={next2FwdRoutingIndex}, RoutingManager.Instance.laneEndForwardRoutings.Length={RoutingManager.Instance.laneEndForwardRoutings.Length} (next1FwdTransitions[j].laneId={next1FwdTransitions[j].laneId}, !next1FwdTransitions[j].startNode={!next1FwdTransitions[j].startNode})");
								}
#endif
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Exploring transitions for next2 lane id={next1FwdTransitions[j].laneId}, seg.={next1FwdTransitions[j].segmentId}, index={next1FwdTransitions[j].laneIndex}, startNode={!next1FwdTransitions[j].startNode}: {RoutingManager.Instance.laneEndForwardRoutings[next2FwdRoutingIndex]}");
								}
#endif
								if (!RoutingManager.Instance.laneEndForwardRoutings[next2FwdRoutingIndex].routed) {
									continue;
								}
								LaneTransitionData[] next2FwdTransitions = RoutingManager.Instance.laneEndForwardRoutings[next2FwdRoutingIndex].transitions;

								if (next2FwdTransitions == null) {
									continue;
								}

								bool foundNext2Next3 = false;
								for (int k = 0; k < next2FwdTransitions.Length; ++k) {
									if (next2FwdTransitions[k].segmentId != next3PathPos.m_segment) {
										continue;
									}

									if (!(next2FwdTransitions[k].type == LaneEndTransitionType.Default ||
										next2FwdTransitions[k].type == LaneEndTransitionType.LaneConnection ||
										(recklessDriver && next2FwdTransitions[k].type == LaneEndTransitionType.Relaxed))
									) {
										continue;
									}

									if (next2FwdTransitions[k].distance > 1) {
#if DEBUG
										if (debug) {
											Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next2 transition {next2FwdTransitions[k]} (distance too large)");
										}
#endif
										continue;
									}

									if (!VehicleRestrictionsManager.Instance.MayUseLane(vehicleState.vehicleType, next3PathPos.m_segment, next2FwdTransitions[k].laneIndex, next3SegInfo)) {
#if DEBUG
										if (debug) {
											Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next2 transition {next2FwdTransitions[k]} (vehicle restrictions)");
										}
#endif
										continue;
									}

									if (next4PathPos.m_segment != 0) {
										// next3 -> next4
										uint next3FwdRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(next2FwdTransitions[k].laneId, !next2FwdTransitions[k].startNode);
#if DEBUG
										if (next3FwdRoutingIndex < 0 || next3FwdRoutingIndex >= RoutingManager.Instance.laneEndForwardRoutings.Length) {
											Log.Error($"Invalid array index: next3FwdRoutingIndex={next3FwdRoutingIndex}, RoutingManager.Instance.laneEndForwardRoutings.Length={RoutingManager.Instance.laneEndForwardRoutings.Length} (next2FwdTransitions[k].laneId={next2FwdTransitions[k].laneId}, !next2FwdTransitions[k].startNode={!next2FwdTransitions[k].startNode})");
										}
#endif

#if DEBUG
										if (debug) {
											Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Exploring transitions for next3 lane id={next2FwdTransitions[k].laneId}, seg.={next2FwdTransitions[k].segmentId}, index={next2FwdTransitions[k].laneIndex}, startNode={!next2FwdTransitions[k].startNode}: {RoutingManager.Instance.laneEndForwardRoutings[next3FwdRoutingIndex]}");
										}
#endif
										if (!RoutingManager.Instance.laneEndForwardRoutings[next3FwdRoutingIndex].routed) {
											continue;
										}
										LaneTransitionData[] next3FwdTransitions = RoutingManager.Instance.laneEndForwardRoutings[next3FwdRoutingIndex].transitions;

										if (next3FwdTransitions == null) {
											continue;
										}

										// check if original next4 lane is accessible via the next3 lane
										bool foundNext3Next4 = false;
										for (int l = 0; l < next3FwdTransitions.Length; ++l) {
											if (next3FwdTransitions[l].segmentId != next4PathPos.m_segment) {
												continue;
											}

											if (!(next3FwdTransitions[l].type == LaneEndTransitionType.Default ||
												next3FwdTransitions[l].type == LaneEndTransitionType.LaneConnection ||
												(recklessDriver && next3FwdTransitions[l].type == LaneEndTransitionType.Relaxed))
											) {
												continue;
											}

											if (next3FwdTransitions[l].distance > 1) {
#if DEBUG
												if (debug) {
													Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next3 transition {next3FwdTransitions[l]} (distance too large)");
												}
#endif
												continue;
											}

											if (next3FwdTransitions[l].laneIndex == next4PathPos.m_lane) {
												// we found a valid routing from [current lane] (currentPathPos) to [next1 lane] (next1Pos), [next2 lane] (next2Pos), [next3 lane] (next3Pos), and [next4 lane] (next4Pos)

												foundNext3Next4 = true;
												int totalLaneDist = next1FwdTransitions[j].distance + next2FwdTransitions[k].distance + next3FwdTransitions[l].distance;
												if (totalLaneDist < minTotalLaneDist) {
													minTotalLaneDist = totalLaneDist;
												}
#if DEBUG
												if (debug) {
													Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Found candidate transition with totalLaneDist={totalLaneDist}: {currentLaneId} -> {currentFwdTransitions[i]} -> {next1FwdTransitions[j]} -> {next2FwdTransitions[k]} -> {next3FwdTransitions[l]}");
												}
#endif
												break;
											}
										} // for l

										if (foundNext3Next4) {
											foundNext2Next3 = true;
										}
									} else {
										foundNext2Next3 = true;
									}

									if (foundNext2Next3) {
										mask = POW2MASKS[next2FwdTransitions[k].laneIndex];
										if ((reachableNext3LanesMask & mask) == 0) {
											++numReachableNext3Lanes;
											reachableNext3LanesMask |= mask;
										}
									}
								} // for k

								if (foundNext2Next3) {
									foundNext1Next2 = true;
								}
							} else {
								foundNext1Next2 = true;
							}

							if (foundNext1Next2) {
								mask = POW2MASKS[next1FwdTransitions[j].laneIndex];
								if ((reachableNext2LanesMask & mask) == 0) {
									++numReachableNext2Lanes;
									reachableNext2LanesMask |= mask;
								}
							}
						} // for j

						if (next3PathPos.m_segment != 0 && !foundNext1Next2) {
							// go to next candidate next1 lane
							continue;
						}
					}

					/*mask = POW2MASKS[currentFwdTransitions[i].laneIndex];
					if ((reachableNext1LanesMask & mask) == 0) {
						++numReachableNext1Lanes;
						reachableNext1LanesMask |= mask;
					}*/

					// This lane is a valid candidate.

					//bool next1StartNode = next1PathPos.m_offset < 128;
					//ushort next1TransitNode = 0;
					//Services.NetService.ProcessSegment(next1PathPos.m_segment, delegate (ushort next1SegId, ref NetSegment next1Seg) {
					//	next1TransitNode = next1StartNode ? next1Seg.m_startNode : next1Seg.m_endNode;
					//	return true;
					//});

					//bool next1TransitNodeIsJunction = false;
					//Services.NetService.ProcessNode(next1TransitNode, delegate (ushort nId, ref NetNode node) {
					//	next1TransitNodeIsJunction = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
					//	return true;
					//});

					/*
					 * Check if next1 lane is clear
					 */
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Checking for traffic on next1 lane id={currentFwdTransitions[i].laneId}.");
					}
#endif

					bool laneChange = currentFwdTransitions[i].distance != 0;
					/*bool next1LaneClear = true;
					if (laneChange) {
						// check for traffic on next1 lane
						float reservedSpace = 0;
						Services.NetService.ProcessLane(currentFwdTransitions[i].laneId, delegate (uint next1LaneId, ref NetLane next1Lane) {
							reservedSpace = next1Lane.GetReservedSpace();
							return true;
						});

						if (currentFwdTransitions[i].laneIndex == next1PathPos.m_lane) {
							reservedSpace -= vehicleLength;
						}

						next1LaneClear = reservedSpace <= (recklessDriver ? conf.AltLaneSelectionMaxRecklessReservedSpace : conf.AltLaneSelectionMaxReservedSpace);
					}

					if (foundClearFwdLane && !next1LaneClear) {
						continue;
					}*/

					/*
					 * Check traffic on the lanes in front of the candidate lane in order to prevent vehicles from backing up traffic
					 */
					bool prevLanesClear = true;
					if (laneChange) {
						uint next1BackRoutingIndex = RoutingManager.Instance.GetLaneEndRoutingIndex(currentFwdTransitions[i].laneId, currentFwdTransitions[i].startNode);
#if DEBUG
						if (next1BackRoutingIndex < 0 || next1BackRoutingIndex >= RoutingManager.Instance.laneEndForwardRoutings.Length) {
							Log.Error($"Invalid array index: next1BackRoutingIndex={next1BackRoutingIndex}, RoutingManager.Instance.laneEndForwardRoutings.Length={RoutingManager.Instance.laneEndForwardRoutings.Length} (currentFwdTransitions[i].laneId={currentFwdTransitions[i].laneId}, currentFwdTransitions[i].startNode={currentFwdTransitions[i].startNode})");
						}
#endif
						if (!RoutingManager.Instance.laneEndBackwardRoutings[next1BackRoutingIndex].routed) {
							continue;
						}
						LaneTransitionData[] next1BackTransitions = RoutingManager.Instance.laneEndBackwardRoutings[next1BackRoutingIndex].transitions;

						if (next1BackTransitions == null) {
							continue;
						}

						for (int j = 0; j < next1BackTransitions.Length; ++j) {
							if (next1BackTransitions[j].segmentId != currentPathPos.m_segment ||
								next1BackTransitions[j].laneIndex == currentPathPos.m_lane) {
								continue;
							}

							if (!(next1BackTransitions[j].type == LaneEndTransitionType.Default ||
								next1BackTransitions[j].type == LaneEndTransitionType.LaneConnection ||
								(recklessDriver && next1BackTransitions[j].type == LaneEndTransitionType.Relaxed))
							) {
								continue;
							}

							if (next1BackTransitions[j].distance > 1) {
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Skipping next1 backward transition {next1BackTransitions[j]} (distance too large)");
								}
#endif
								continue;
							}

#if DEBUG
							if (debug) {
								Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Checking for upcoming traffic in front of next1 lane id={currentFwdTransitions[i].laneId}. Checking back transition {next1BackTransitions[j]}");
							}
#endif

							Services.NetService.ProcessLane(next1BackTransitions[j].laneId, delegate (uint prevLaneId, ref NetLane prevLane) {
								prevLanesClear = prevLane.GetReservedSpace() <= (recklessDriver ? conf.DynamicLaneSelection.MaxRecklessReservedSpace : conf.DynamicLaneSelection.MaxReservedSpace);
								return true;
							});

							if (!prevLanesClear) {
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Back lane {next1BackTransitions[j].laneId} is not clear!");
								}
#endif
								break;
							} else {
#if DEBUG
								if (debug) {
									Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Back lane {next1BackTransitions[j].laneId} is clear!");
								}
#endif
							}
						}
					}

#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Checking for coming up traffic in front of next1 lane. prevLanesClear={prevLanesClear}");
					}
#endif

					if (/*foundClearBackLane*/foundSafeLaneChange && !prevLanesClear) {
						continue;
					}

					// calculate lane metric
#if DEBUG
					if (currentFwdTransitions[i].laneIndex < 0 || currentFwdTransitions[i].laneIndex >= next1SegInfo.m_lanes.Length) {
						Log.Error($"Invalid array index: currentFwdTransitions[i].laneIndex={currentFwdTransitions[i].laneIndex}, next1SegInfo.m_lanes.Length={next1SegInfo.m_lanes.Length}");
					}
#endif
					NetInfo.Lane next1LaneInfo = next1SegInfo.m_lanes[currentFwdTransitions[i].laneIndex];
					float next1MaxSpeed = SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(currentFwdTransitions[i].segmentId, currentFwdTransitions[i].laneIndex, currentFwdTransitions[i].laneId, next1LaneInfo);
					float targetSpeed = Math.Min(vehicleMaxSpeed, ApplyRealisticSpeeds(next1MaxSpeed, vehicleId, ref vehicleData, vehicleInfo));

					ushort meanSpeed = TrafficMeasurementManager.Instance.CalcLaneRelativeMeanSpeed(currentFwdTransitions[i].segmentId, currentFwdTransitions[i].laneIndex, currentFwdTransitions[i].laneId, next1LaneInfo);

					float relMeanSpeedInPercent = meanSpeed / (TrafficMeasurementManager.REF_REL_SPEED / TrafficMeasurementManager.REF_REL_SPEED_PERCENT_DENOMINATOR);
					float randSpeed = 0f;
					if (conf.DynamicLaneSelection.LaneSpeedRandInterval > 0) {
						randSpeed = Services.SimulationService.Randomizer.Int32((uint)conf.DynamicLaneSelection.LaneSpeedRandInterval + 1u) - conf.DynamicLaneSelection.LaneSpeedRandInterval / 2f;
						relMeanSpeedInPercent += randSpeed;
					}

					float relMeanSpeed = relMeanSpeedInPercent / (float)TrafficMeasurementManager.REF_REL_SPEED_PERCENT_DENOMINATOR;
					float next1MeanSpeed = relMeanSpeed * next1MaxSpeed;

					/*if (
#if DEBUG
					conf.Debug.Switches[19] &&
#endif
					next1LaneInfo.m_similarLaneCount > 1) {
						float relLaneInnerIndex = ((float)RoutingManager.Instance.CalcOuterSimilarLaneIndex(next1LaneInfo) / (float)next1LaneInfo.m_similarLaneCount);
						float rightObligationFactor = conf.AltLaneSelectionMostOuterLaneSpeedFactor + (conf.AltLaneSelectionMostInnerLaneSpeedFactor - conf.AltLaneSelectionMostOuterLaneSpeedFactor) * relLaneInnerIndex;
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Applying obligation factor to next1 lane {currentFwdTransitions[i].laneId}: relLaneInnerIndex={relLaneInnerIndex}, rightObligationFactor={rightObligationFactor}, next1MaxSpeed={next1MaxSpeed}, relMeanSpeedInPercent={relMeanSpeedInPercent}, randSpeed={randSpeed}, next1MeanSpeed={next1MeanSpeed} => new next1MeanSpeed={Mathf.Max(rightObligationFactor * next1MaxSpeed, next1MeanSpeed)}");
						}
#endif
						next1MeanSpeed = Mathf.Min(rightObligationFactor * next1MaxSpeed, next1MeanSpeed);
					}*/

					float speedDiff = next1MeanSpeed - targetSpeed; // > 0: lane is faster than vehicle would go. < 0: vehicle could go faster than this lane allows

#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): Calculated metric for next1 lane {currentFwdTransitions[i].laneId}: next1MaxSpeed={next1MaxSpeed} next1MeanSpeed={next1MeanSpeed} targetSpeed={targetSpeed} speedDiff={speedDiff} bestSpeedDiff={bestOptSpeedDiff} bestStaySpeedDiff={bestStaySpeedDiff}");
					}
#endif
					if (!laneChange) {
						if ((float.IsInfinity(bestStaySpeedDiff) ||
							(bestStaySpeedDiff < 0 && speedDiff > bestStaySpeedDiff) ||
							(bestStaySpeedDiff > 0 && speedDiff < bestStaySpeedDiff && speedDiff >= 0))
						) {
							bestStaySpeedDiff = speedDiff;
							bestStayNext1LaneIndex = currentFwdTransitions[i].laneIndex;
							bestStayMeanSpeed = next1MeanSpeed;
							bestStayTotalLaneDist = minTotalLaneDist;
						}
					} else {
						//bool foundFirstClearFwdLane = laneChange && !foundClearFwdLane && next1LaneClear;
						//bool foundFirstClearBackLane = laneChange && !foundClearBackLane && prevLanesClear;
						bool foundFirstSafeLaneChange = !foundSafeLaneChange && /*next1LaneClear &&*/ prevLanesClear;
						if (/*(foundFirstClearFwdLane && !foundClearBackLane) ||
							(foundFirstClearBackLane && !foundClearFwdLane) ||*/
							foundFirstSafeLaneChange ||
							float.IsInfinity(bestOptSpeedDiff) ||
							(bestOptSpeedDiff < 0 && speedDiff > bestOptSpeedDiff) ||
							(bestOptSpeedDiff > 0 && speedDiff < bestOptSpeedDiff && speedDiff >= 0)) {
							bestOptSpeedDiff = speedDiff;
							bestOptNext1LaneIndex = currentFwdTransitions[i].laneIndex;
							bestOptMeanSpeed = next1MeanSpeed;
							bestOptTotalLaneDist = minTotalLaneDist;
						}

						/*if (foundFirstClearBackLane) {
							foundClearBackLane = true;
						}

						if (foundFirstClearFwdLane) {
							foundClearFwdLane = true;
						}*/

						if (foundFirstSafeLaneChange) {
							foundSafeLaneChange = true;
						}
					}
				} // for i

#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): best lane index: {bestOptNext1LaneIndex}, best stay lane index: {bestStayNext1LaneIndex}, path lane index: {next1PathPos.m_lane})\nbest speed diff: {bestOptSpeedDiff}, best stay speed diff: {bestStaySpeedDiff}\nfoundClearBackLane=XXfoundClearBackLaneXX, foundClearFwdLane=XXfoundClearFwdLaneXX, foundSafeLaneChange={foundSafeLaneChange}\nbestMeanSpeed={bestOptMeanSpeed}, bestStayMeanSpeed={bestStayMeanSpeed}");
				}
#endif

				if (float.IsInfinity(bestStaySpeedDiff)) {
					// no continuous lane found
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> no continuous lane found -- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex}");
					}
#endif
					return bestOptNext1LaneIndex;
				}

				if (float.IsInfinity(bestOptSpeedDiff)) {
					// no lane change found
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> no lane change found -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");
					}
#endif
					return bestStayNext1LaneIndex;
				}

				// decide if vehicle should stay or change

				// vanishing lane change opportunity detection
				int vehSel = vehicleId % 6;
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): vehMod4={vehSel} numReachableNext2Lanes={numReachableNext2Lanes} numReachableNext3Lanes={numReachableNext3Lanes}");
				}
#endif
				if ((numReachableNext3Lanes == 1 && vehSel <= 2) || // 3/6 % of all vehicles will change lanes 3 segments in front
					(numReachableNext2Lanes == 1 && vehSel <= 4) // 2/6 % of all vehicles will change lanes 2 segments in front, 1/5 will change at the last opportunity
				) {
					// vehicle must reach a certain lane since lane changing opportunities will vanish

#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): vanishing lane change opportunities detected: numReachableNext2Lanes={numReachableNext2Lanes} numReachableNext3Lanes={numReachableNext3Lanes}, vehSel={vehSel}, bestOptTotalLaneDist={bestOptTotalLaneDist}, bestStayTotalLaneDist={bestStayTotalLaneDist}");
					}
#endif

					if (bestOptTotalLaneDist < bestStayTotalLaneDist) {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> vanishing lane change opportunities -- selecting bestOptTotalLaneDist={bestOptTotalLaneDist}");
						}
#endif
						return bestOptNext1LaneIndex;
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> vanishing lane change opportunities -- selecting bestStayTotalLaneDist={bestStayTotalLaneDist}");
						}
#endif
						return bestStayNext1LaneIndex;
					}
				}

				if (bestStaySpeedDiff == 0 || bestOptMeanSpeed < 0.1f) {
					/*
					 * edge cases:
					 *   (1) continuous lane is super optimal
					 *   (2) best mean speed is near zero
					 */
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> edge case: continuous lane is optimal ({bestStaySpeedDiff == 0}) / best mean speed is near zero ({bestOptMeanSpeed < 0.1f}) -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");
					}
#endif
					return bestStayNext1LaneIndex;
				}

				if (bestStayTotalLaneDist != bestOptTotalLaneDist && Math.Max(bestStayTotalLaneDist, bestOptTotalLaneDist) > conf.DynamicLaneSelection.MaxOptLaneChanges) {
					/*
					 * best route contains more lane changes than allowed: choose lane with the least number of future lane changes
					 */
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): maximum best total lane distance = {Math.Max(bestStayTotalLaneDist, bestOptTotalLaneDist)} > AltLaneSelectionMaxOptLaneChanges");
					}
#endif

					if (bestOptTotalLaneDist < bestStayTotalLaneDist) {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> selecting lane change option for minimizing number of future lane changes -- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex}");
						}
#endif
						return bestOptNext1LaneIndex;
					} else {
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> selecting stay option for minimizing number of future lane changes -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");
						}
#endif
						return bestStayNext1LaneIndex;
					}
				}

				if (bestStaySpeedDiff < 0 && bestOptSpeedDiff > bestStaySpeedDiff) {
					// found a lane change that improves vehicle speed
					//float improvement = 100f * ((bestOptSpeedDiff - bestStaySpeedDiff) / ((bestStayMeanSpeed + bestOptMeanSpeed) / 2f));
					ushort optImprovementInKmH = SpeedLimitManager.Instance.LaneToCustomSpeedLimit(bestOptSpeedDiff - bestStaySpeedDiff, false);
					float speedDiff = Mathf.Abs(bestOptMeanSpeed - vehicleCurSpeed);
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): a lane change for speed improvement is possible. optImprovementInKmH={optImprovementInKmH} km/h speedDiff={speedDiff} (bestOptMeanSpeed={bestOptMeanSpeed}, vehicleCurVelocity={vehicleCurSpeed}, foundSafeLaneChange={foundSafeLaneChange})");
					}
#endif
					if (optImprovementInKmH >= conf.DynamicLaneSelection.MinSafeSpeedImprovement &&
						(foundSafeLaneChange || (speedDiff <= conf.DynamicLaneSelection.MaxUnsafeSpeedDiff))
						) {
						// speed improvement is significant
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a faster lane to change to and speed improvement is significant -- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex} (foundSafeLaneChange={foundSafeLaneChange}, speedDiff={speedDiff})");
						}
#endif
						return bestOptNext1LaneIndex;
					}

					// insufficient improvement
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a faster lane to change to but speed improvement is NOT significant OR lane change is unsafe -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex} (foundSafeLaneChange={foundSafeLaneChange})");
					}
#endif
					return bestStayNext1LaneIndex;
				} else if (!recklessDriver && foundSafeLaneChange && bestStaySpeedDiff > 0 && bestOptSpeedDiff < bestStaySpeedDiff && bestOptSpeedDiff >= 0) {
					// found a lane change that allows faster vehicles to overtake
					float optimization = 100f * ((bestStaySpeedDiff - bestOptSpeedDiff) / ((bestStayMeanSpeed + bestOptMeanSpeed) / 2f));
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): found a lane change that optimizes overall traffic. optimization={optimization}%");
					}
#endif
					if (optimization >= conf.DynamicLaneSelection.MinSafeTrafficImprovement) {
						// traffic optimization is significant
#if DEBUG
						if (debug) {
							Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a lane that optimizes overall traffic and traffic optimization is significant -- selecting bestOptNext1LaneIndex={bestOptNext1LaneIndex}");
						}
#endif
						return bestOptNext1LaneIndex;
					}

					// insufficient optimization
#if DEBUG
					if (debug) {
						Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> found a lane that optimizes overall traffic but optimization is NOT significant -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");
					}
#endif
					return bestOptNext1LaneIndex;
				}

				// suboptimal safe lane change
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.FindBestLane({vehicleId}): ===> suboptimal safe lane change detected -- selecting bestStayNext1LaneIndex={bestStayNext1LaneIndex}");
				}
#endif
				return bestStayNext1LaneIndex;
			} catch (Exception e) {
				Log.Error($"VehicleBehaviorManager.FindBestLane({vehicleId}): Exception occurred: {e}");
			}
			return next1PathPos.m_lane;
		}

		public bool MayFindBestLane(ushort vehicleId, ref Vehicle vehicleData, ref ExtVehicle vehicleState) {
			GlobalConfig conf = GlobalConfig.Instance;
#if DEBUG
			bool debug = false; // conf.Debug.Switches[17] && (conf.Debug.VehicleId == 0 || conf.Debug.VehicleId == vehicleId);
			if (debug) {
				Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}) called.");
			}
#endif

			if (!Options.advancedAI) {
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking. Advanced Vehicle AI is disabled.");
				}
#endif
				return false;
			}

			if (vehicleState.heavyVehicle) {
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking. Vehicle is heavy.");
				}
#endif
				return false;
			}

			if ((vehicleState.vehicleType & (ExtVehicleType.RoadVehicle & ~ExtVehicleType.Bus)) == ExtVehicleType.None) {
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking. vehicleType={vehicleState.vehicleType}");
				}
#endif
				return false;
			}

			uint vehicleRand = Constants.ManagerFactory.ExtVehicleManager.GetVehicleRand(vehicleId);

			if (vehicleRand < 100 - (int)Options.altLaneSelectionRatio) {
#if DEBUG
				if (debug) {
					Log._Debug($"VehicleBehaviorManager.MayFindBestLane({vehicleId}): Skipping lane checking (randomization).");
				}
#endif
				return false;
			}

			return true;
		}
	}
}

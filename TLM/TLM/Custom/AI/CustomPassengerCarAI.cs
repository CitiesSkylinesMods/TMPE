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
using System.Xml;
using System.IO;
using CSUtil.Commons;
using TrafficManager.Manager.Impl;
using TrafficManager.Traffic.Data;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;
using CSUtil.Commons.Benchmark;
using System.Runtime.CompilerServices;
using static TrafficManager.Custom.PathFinding.CustomPathManager;
using TrafficManager.Traffic.Enums;
using TrafficManager.RedirectionFramework.Attributes;

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	[TargetType(typeof(PassengerCarAI))]
	public class CustomPassengerCarAI : CarAI {
		[RedirectMethod]
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				return;
			}

			base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
		}

		[RedirectMethod]
		public string CustomGetLocalizedStatus(ushort vehicleID, ref Vehicle data, out InstanceID target) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort driverInstanceId = GetDriverInstance(vehicleID, ref data);
			ushort targetBuildingId = 0;
			bool targetIsNode = false;
			if (driverInstanceId != 0) {
				if ((data.m_flags & Vehicle.Flags.Parking) != (Vehicle.Flags)0) {
					uint citizen = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_citizen;
					if (citizen != 0u && citizenManager.m_citizens.m_buffer[citizen].m_parkedVehicle != 0) {
						target = InstanceID.Empty;
						return Locale.Get("VEHICLE_STATUS_PARKING");
					}
				}
				targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_targetBuilding;
				targetIsNode = (citizenManager.m_instances.m_buffer[driverInstanceId].m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None;
			}
			if (targetBuildingId == 0) {
				target = InstanceID.Empty;
				return Locale.Get("VEHICLE_STATUS_CONFUSED");
			}
			string ret;
			bool leavingCity = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			if (leavingCity) {
				target = InstanceID.Empty;
				ret = Locale.Get("VEHICLE_STATUS_LEAVING");
			} else {
				target = InstanceID.Empty;
				if (targetIsNode) {
					target.NetNode = targetBuildingId;
				} else {
					target.Building = targetBuildingId;
				}

				ret = Locale.Get("VEHICLE_STATUS_GOINGTO");
			}

			// NON-STOCK CODE START
			if (Options.parkingAI) {
				ret = AdvancedParkingManager.Instance.EnrichLocalizedCarStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId]);
			}
			// NON-STOCK CODE END

			return ret;
		}

		[RedirectMethod]
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			ushort driverInstanceId = GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstanceId == 0) {
				return false;
			}

			return Constants.ManagerFactory.VehicleBehaviorManager.StartPassengerCarPathFind(vehicleID, ref vehicleData, this.m_info, driverInstanceId, ref Singleton<CitizenManager>.instance.m_instances.m_buffer[(int)driverInstanceId], ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId], startPos, endPos, startBothWays, endBothWays, undergroundTarget, IsHeavyVehicle(), CombustionEngine(), IgnoreBlocked(vehicleID, ref vehicleData));
		}

		public void CustomUpdateParkedVehicle(ushort parkedId, ref VehicleParked data) {
			float x = this.m_info.m_generatedInfo.m_size.x;
			float z = this.m_info.m_generatedInfo.m_size.z;
			uint ownerCitizenId = data.m_ownerCitizen;
			ushort homeID = 0;
			if (ownerCitizenId != 0u) {
				homeID = Singleton<CitizenManager>.instance.m_citizens.m_buffer[ownerCitizenId].m_homeBuilding;
			}

			// NON-STOCK CODE START
			if (!AdvancedParkingManager.Instance.TryMoveParkedVehicle(parkedId, ref data, data.m_position, GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding, homeID)) {
				Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedId);
			}
			// NON-STOCK CODE END
		}

		[RedirectMethod]
		public bool CustomParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;

			// TODO remove this:
			uint driverCitizenId = 0u;
			ushort driverCitizenInstanceId = 0;
			ushort targetBuildingId = 0; // NON-STOCK CODE
			uint curCitizenUnitId = vehicleData.m_citizenUnits;
			int numIterations = 0;
			while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
				uint nextUnit = citizenManager.m_units.m_buffer[curCitizenUnitId].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizenId = citizenManager.m_units.m_buffer[curCitizenUnitId].GetCitizen(i);
					if (citizenId != 0u) {
						driverCitizenInstanceId = citizenManager.m_citizens.m_buffer[citizenId].m_instance;
						if (driverCitizenInstanceId != 0) {
							driverCitizenId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_citizen;
							// NON-STOCK CODE START
							targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverCitizenInstanceId].m_targetBuilding;
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

#if BENCHMARK
			using (var bm = new Benchmark(null, "ExtParkVehicle")) {
#endif
			return ExtParkVehicle(vehicleID, ref vehicleData, driverCitizenId, ref citizenManager.m_citizens.m_buffer[driverCitizenId], driverCitizenInstanceId, ref citizenManager.m_instances.m_buffer[driverCitizenInstanceId], ref ExtCitizenInstanceManager.Instance.ExtInstances[driverCitizenInstanceId], targetBuildingId, pathPos, nextPath, nextPositionIndex, out segmentOffset);
#if BENCHMARK
			}
#endif
		}

		internal bool ExtParkVehicle(ushort vehicleID, ref Vehicle vehicleData, uint driverCitizenId, ref Citizen driverCitizen, ushort driverCitizenInstanceId, ref CitizenInstance driverInstance, ref ExtCitizenInstance driverExtInstance, ushort targetBuildingId, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
			IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
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
			bool prohibitPocketCars = false;
			// NON-STOCK CODE END

			if (driverCitizenId != 0u) {
				if (Options.parkingAI && driverCitizenInstanceId != 0) {
					prohibitPocketCars = true;
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
					homeID = driverCitizen.m_homeBuilding;
				}
				Vector3 parkPos = default(Vector3);
				Quaternion parkRot = default(Quaternion);
				float parkOffset = -1f;

				// NON-STOCK CODE START
				bool foundParkingSpace = false;

				if (prohibitPocketCars) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Vehicle {vehicleID} tries to park on a parking position now (flags: {vehicleData.m_flags})! CurrentPathMode={driverExtInstance.pathMode} path={vehicleData.m_path} pathPositionIndex={vehicleData.m_pathPositionIndex} segmentId={pathPos.m_segment} laneIndex={pathPos.m_lane} offset={pathPos.m_offset} nextPath={nextPath} refPos={refPos} searchDir={searchDir} home={homeID} driverCitizenId={driverCitizenId} driverCitizenInstanceId={driverCitizenInstanceId}");
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
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpaceRoadSideForVehiclePos(this.m_info, 0, driverExtInstance.parkingSpaceLocationId, refPos, out parkPos, out parkRot, out parkOffset, out parkLaneID, out parkLaneIndex);
								break;
							case ExtParkingSpaceLocation.Building:
								float maxDist = 9999f;
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleID} wants to park @ building {driverExtInstance.parkingSpaceLocationId}");
#endif
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpacePropAtBuilding(this.m_info, homeID, 0, driverExtInstance.parkingSpaceLocationId, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[driverExtInstance.parkingSpaceLocationId], pathPos.m_segment, refPos, ref maxDist, true, out parkPos, out parkRot, out parkOffset);
								break;
							default:
#if DEBUG
								Log.Error($"No alternative parking position stored for vehicle {vehicleID}! PathMode={driverExtInstance.pathMode}");
#endif
								foundParkingSpace = CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset);
								break;
						}
					}
				}

				if (!foundParkingSpace) {
					foundParkingSpace = /*prohibitPocketCars ?*/
						CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset) /*:
						FindParkingSpace(homeID, refPos, searchDir, pathPos.m_segment, this.m_info.m_generatedInfo.m_size.x, this.m_info.m_generatedInfo.m_size.z, out parkPos, out parkRot, out parkOffset)*/;
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Found parking space? {foundParkingSpace}. parkPos={parkPos}, parkRot={parkRot}, parkOffset={parkOffset}");
#endif
				}

				// NON-STOCK CODE END
				ushort parkedVehicleId = 0;
				bool parkedCarCreated = foundParkingSpace && vehicleManager.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, this.m_info, parkPos, parkRot, driverCitizenId);
#if DEBUG
				if (debug)
					Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parked car created? {parkedCarCreated}");
#endif

				IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
				if (foundParkingSpace && parkedCarCreated) {
					// we have reached a parking position
#if DEBUG
					float sqrDist = (refPos - parkPos).sqrMagnitude;
					if (fineDebug)
						Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Vehicle {vehicleID} succeeded in parking! CurrentPathMode={driverExtInstance.pathMode} sqrDist={sqrDist}");
#endif

					driverCitizen.SetParkedVehicle(driverCitizenId, parkedVehicleId);
					if (parkOffset >= 0f) {
						segmentOffset = (byte)(parkOffset * 255f);
					}

					// NON-STOCK CODE START
					if (prohibitPocketCars) {
						if ((driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
							// decrease parking space demand of target building
							Constants.ManagerFactory.ExtBuildingManager.ModifyParkingSpaceDemand(ref extBuildingManager.ExtBuildings[targetBuildingId], parkPos, GlobalConfig.Instance.ParkingAI.MinFoundParkPosParkingSpaceDemandDelta, GlobalConfig.Instance.ParkingAI.MaxFoundParkPosParkingSpaceDemandDelta);
						}

						//if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
						// we have reached an (alternative) parking position and succeeded in finding a parking space
						driverExtInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
						driverExtInstance.failedParkingAttempts = 0;
						driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
						driverExtInstance.parkingSpaceLocationId = 0;
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Vehicle {vehicleID} has reached an (alternative) parking position! CurrentPathMode={driverExtInstance.pathMode} position={parkPos}");
#endif
						//}
					}
				} else if (prohibitPocketCars) {
					// could not find parking space. vehicle would despawn.
					if (
						targetBuildingId != 0 &&
						(Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None &&
						(refPos - Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_position).magnitude <= GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
					) {
						// vehicle is at target and target is an outside connection: accept despawn
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Driver citizen instance {driverCitizenInstanceId} wants to park at an outside connection. Aborting.");
#endif
						return true;
					}

					// Find parking space in the vicinity, redo path-finding to the parking space, park the vehicle and do citizen path-finding to the current target

					if (!foundParkingSpace && (driverExtInstance.pathMode == ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
						// increase parking space demand of target building
						if (driverExtInstance.failedParkingAttempts > 1) {
							extBuildingManager.AddParkingSpaceDemand(ref extBuildingManager.ExtBuildings[targetBuildingId], GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement * (uint)(driverExtInstance.failedParkingAttempts - 1));
						}
					}

					if (!foundParkingSpace) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking failed for vehicle {vehicleID}: Could not find parking space. ABORT.");
#endif
						++driverExtInstance.failedParkingAttempts;
					} else {
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking failed for vehicle {vehicleID}: Parked car could not be created. ABORT.");
#endif
						driverExtInstance.failedParkingAttempts = GlobalConfig.Instance.ParkingAI.MaxParkingAttempts + 1;
					}
					driverExtInstance.pathMode = ExtPathMode.ParkingFailed;
					driverExtInstance.parkingPathStartPosition = pathPos;

#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking failed for vehicle {vehicleID}! (flags: {vehicleData.m_flags}) pathPos segment={pathPos.m_segment}, lane={pathPos.m_lane}, offset={pathPos.m_offset}. Trying to find parking space in the vicinity. FailedParkingAttempts={driverExtInstance.failedParkingAttempts}, CurrentPathMode={driverExtInstance.pathMode} foundParkingSpace={foundParkingSpace}");
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
										Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Releasing path for citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {citizenManager.m_instances.m_buffer[citizenInstanceId].m_path}).");
#endif
									if (citizenInstanceId != driverCitizenInstanceId) {
#if DEBUG
										if (debug)
											Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Resetting pathmode for passenger citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode}).");
#endif

										extCitizenInstanceManager.Reset(ref extCitizenInstanceManager.ExtInstances[citizenInstanceId]);
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
								if (prohibitPocketCars) {
									if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
#if DEBUG
										if (debug)
											Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded: Doing nothing for citizen instance {citizenInstanceId}! path: {citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path}");
#endif
										extCitizenInstanceManager.ExtInstances[citizenInstanceId].pathMode = ExtPathMode.RequiresWalkingPathToTarget;
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
										Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded (default): Setting path of citizen instance {citizenInstanceId} to {nextPath}!");
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

			if (prohibitPocketCars) {
				if (driverExtInstance.pathMode == ExtPathMode.RequiresWalkingPathToTarget) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded (alternative parking spot): Citizen instance {driverExtInstance} has to walk for the remaining path!");
#endif
					/*driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget;
					if (debug)
						Log._Debug($"Setting CurrentPathMode of vehicle {vehicleID} to {driverExtInstance.CurrentPathMode}");*/
				}
			}

			return true;
		}

		private static bool CustomFindParkingSpace(VehicleInfo vehicleInfo, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segmentId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
#if DEBUG
			bool debug = GlobalConfig.Instance.Debug.Switches[22];
#endif

			float searchRadius = Options.parkingAI ? 32f : 16f;
			uint chanceOfParkingOffRoad = 3u;

			Vector3 searchMagnitude = refPos + searchDir * 16f;

			/*if (Options.parkingAI) {
				//searchRadius = Mathf.Max(32f, searchRadius);
			}*/

			Vector3 refPos2 = refPos + searchDir * 16f;
			if (Singleton<SimulationManager>.instance.m_randomizer.Int32(chanceOfParkingOffRoad) == 0) {
				float width = vehicleInfo.m_generatedInfo.m_size.x;
				float length = vehicleInfo.m_generatedInfo.m_size.z;

				if (FindParkingSpaceRoadSide(0, segmentId, refPos, width - 0.2f, length, out parkPos, out parkRot, out parkOffset)) {
					if (Options.parkingRestrictionsEnabled) {
						Vector3 innerParkPos;
						uint laneId;
						int laneIndex;
						float laneOffset;
						if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
#if BENCHMARK
							using (var bm = new Benchmark(null, "IsParkingAllowed.1")) {
#endif
							if (ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
								return true;
							}
#if BENCHMARK
							}
#endif
						}
					} else {
						return true;
					}
				}

#if BENCHMARK
				using (var bm = new Benchmark(null, "FindParkingSpaceBuilding.1")) {
#endif
				if (AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segmentId, refPos2, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
#if BENCHMARK
				}
#endif
			} else {
#if BENCHMARK
				using (var bm = new Benchmark(null, "FindParkingSpaceBuilding.2")) {
#endif
				if (AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segmentId, refPos2, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
#if BENCHMARK
				}
#endif

				float width = vehicleInfo.m_generatedInfo.m_size.x;
				float length = vehicleInfo.m_generatedInfo.m_size.z;

				if (FindParkingSpaceRoadSide(0, segmentId, refPos, width - 0.2f, length, out parkPos, out parkRot, out parkOffset)) {
					if (Options.parkingRestrictionsEnabled) {
						Vector3 innerParkPos;
						uint laneId;
						int laneIndex;
						float laneOffset;
						if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
#if BENCHMARK
							using (var bm = new Benchmark(null, "IsParkingAllowed.2")) {
#endif
							if (ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
								return true;
							}
#if BENCHMARK
							}
#endif
						}
					} else {
						return true;
					}
				}
			}
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private ushort GetDriverInstance(ushort vehicleID, ref Vehicle data) {
			Log.Error("GetDriverInstance is not overridden!");
			return 0;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpaceRoadSide(ushort ignoreParked, ushort requireSegment, Vector3 refPos, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpaceRoadSide is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpace(bool isElectric, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segment, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpace is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool FindParkingSpaceProp(bool isElectric, ushort ignoreParked, PropInfo info, Vector3 position, float angle, bool fixedHeight, Vector3 refPos, float width, float length, ref float maxDistance, ref Vector3 parkPos, ref Quaternion parkRot) {
			Log.Error("FindParkingSpaceProp is not overridden!");
			return false;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool CheckOverlap(ushort ignoreParked, ref Bezier3 bezier, float offset, float length, out float minPos, out float maxPos) {
			Log.Error("CheckOverlap is not overridden!");
			minPos = 0;
			maxPos = 0;
			return false;
		}
	}
}

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
using System.Xml;
using System.IO;
using CSUtil.Commons;

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	public class CustomPassengerCarAI : CarAI {
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && Options.enableDespawning) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
			} else {
				base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
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
				VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
				ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
				ret = AdvancedParkingManager.Instance.EnrichLocalizedStatus(ret, driverExtInstance);
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

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log.Warning($"CustomPassengerCarAI.CustomStartPathFind: called for vehicle {vehicleID}, startPos={startPos}, endPos={endPos}, sourceBuilding={vehicleData.m_sourceBuilding}, targetBuilding={vehicleData.m_targetBuilding}");
#endif

			PathUnit.Position startPosA = default(PathUnit.Position);
			PathUnit.Position startPosB = default(PathUnit.Position);
			PathUnit.Position endPosA = default(PathUnit.Position);
			float sqrDistA = 0f;
			float sqrDistB;

			ushort driverInstanceId = CustomPassengerCarAI.GetDriverInstance(vehicleID, ref vehicleData);
			if (driverInstanceId == 0) {
				return false;
			}
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort targetBuildingId = citizenManager.m_instances.m_buffer[(int)driverInstanceId].m_targetBuilding;

			// NON-STOCK CODE START
			bool calculateEndPos = true;
			bool allowRandomParking = true;
			bool movingToParkingPos = false;
			bool foundStartingPos = false;
			bool skipQueue = false;
			ExtPathType extPathType = ExtPathType.None;
			if (Options.prohibitPocketCars) {
				VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleData.GetFirstVehicle(vehicleID));
				ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();
				if (driverExtInstance != null) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log.Warning($"CustomPassengerCarAI.CustomStartPathFind: PathMode={driverExtInstance.PathMode} for vehicle {vehicleID}, driver citizen instance {driverExtInstance.InstanceId}!");
#endif

					switch (driverExtInstance.PathMode) {
						case ExtPathMode.None:
						case ExtPathMode.ParkedCarReached:
						case ExtPathMode.DrivingToTarget:
						case ExtPathMode.DrivingToKnownParkPos:
						case ExtPathMode.ParkingFailed:
							if (targetBuildingId != 0 && (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None) {
								// target is outside connection
								driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
							} else {
								if (driverExtInstance.PathMode == ExtPathMode.DrivingToTarget || driverExtInstance.PathMode == ExtPathMode.DrivingToKnownParkPos || driverExtInstance.PathMode == ExtPathMode.ParkingFailed)
									skipQueue = true;

								bool allowTourists = false;
								if (driverExtInstance.PathMode == ExtPathMode.ParkingFailed) {
									// previous parking attempt failed
									driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToAltParkPos;
									allowTourists = true;

#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"Vehicle {vehicleID} shall move to an alternative parking position! CurrentPathMode={driverExtInstance.PathMode}");
#endif

									if (driverExtInstance.ParkingPathStartPosition != null) {
										startPosA = (PathUnit.Position)driverExtInstance.ParkingPathStartPosition;
										foundStartingPos = true;
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"Setting starting pos for {vehicleID} to segment={startPosA.m_segment}, laneIndex={startPosA.m_lane}, offset={startPosA.m_offset}");
#endif
									}
									startBothWays = false;

									if (driverExtInstance.FailedParkingAttempts > GlobalConfig.Instance.MaxParkingAttempts) {
										// maximum number of parking attempts reached
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"Reached maximum number of parking attempts for vehicle {vehicleID}! GIVING UP.");
#endif
										driverExtInstance.Reset();

										// pocket car fallback
										//vehicleData.m_flags |= Vehicle.Flags.Parking;
										return false;
									} else {
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[4])
											Log._Debug($"Increased number of parking attempts for vehicle {vehicleID}: {driverExtInstance.FailedParkingAttempts}/{GlobalConfig.Instance.MaxParkingAttempts}");
#endif
									}
								} else {
									driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToKnownParkPos;
								}

								ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[driverExtInstance.GetCitizenId()].m_homeBuilding;
								bool calcEndPos;
								Vector3 parkPos;

								if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(endPos, vehicleData.Info, driverExtInstance, homeId, vehicleID, allowTourists, out parkPos, ref endPosA, out calcEndPos)) {
									calculateEndPos = calcEndPos;
									allowRandomParking = false;
									movingToParkingPos = true;

									if (!driverExtInstance.CalculateReturnPath(parkPos, endPos)) {
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"Could not calculate return path for citizen instance {driverExtInstance.InstanceId}, vehicle {vehicleID}. Resetting instance.");
#endif
										driverExtInstance.Reset();
										return false;
									}
								} else if (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
									// no alternative parking spot found: abort
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId} with CurrentPathMode={driverExtInstance.PathMode}! GIVING UP.");
#endif
									driverExtInstance.Reset();
									return false;
								} else {
									// calculate a direct path to target
#if DEBUG
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.InstanceId} with CurrentPathMode={driverExtInstance.PathMode}! Setting CurrentPathMode to 'CalculatingCarPath'.");
#endif
									driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
								}
							}
							break;
					}

					extPathType = driverExtInstance.GetPathType();
				} else {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log.Warning($"CustomPassengerCarAI.CustomStartPathFind: No driver citizen instance found for vehicle {vehicleID}!");
#endif
				}
			}

			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle;
			if (!movingToParkingPos) {
				laneTypes |= NetInfo.LaneType.Pedestrian;
			}
			// NON-STOCK CODE END

			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			bool randomParking = false;
			if (allowRandomParking && // NON-STOCK CODE
				!movingToParkingPos &&
				targetBuildingId != 0 &&
				Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].Info.m_class.m_service > ItemClass.Service.Office) {
				randomParking = true;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"Requesting path-finding for passenger car {vehicleID}, startPos={startPos}, endPos={endPos}, extPathType={extPathType}");
#endif

			// NON-STOCK CODE START
			if (! foundStartingPos) {
				foundStartingPos = CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out sqrDistA, out sqrDistB);
			}

			bool foundEndPos = !calculateEndPos || citizenManager.m_instances.m_buffer[(int)driverInstanceId].Info.m_citizenAI.FindPathPosition(driverInstanceId, ref citizenManager.m_instances.m_buffer[(int)driverInstanceId], endPos, Options.prohibitPocketCars && (targetBuildingId == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : (laneTypes | NetInfo.LaneType.Pedestrian), vehicleType, undergroundTarget, out endPosA);
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
					ExtVehicleType.PassengerCar, vehicleID, extPathType, out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, def, laneTypes, vehicleType, 20000f, false, false, false, skipQueue, randomParking, false)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Path-finding starts for passenger car {vehicleID}, path={path}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleType}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}");
#endif

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

		public void CustomUpdateParkedVehicle(ushort parkedId, ref VehicleParked data) {
			float x = this.m_info.m_generatedInfo.m_size.x;
			float z = this.m_info.m_generatedInfo.m_size.z;
			float minSqrDist = 256f;
			bool parkingSpotFound = false;
			uint ownerCitizen = data.m_ownerCitizen;
			ushort homeID = 0;
			if (ownerCitizen != 0u) {
				homeID = Singleton<CitizenManager>.instance.m_citizens.m_buffer[(int)((UIntPtr)ownerCitizen)].m_homeBuilding;
			}

			ExtParkingSpaceLocation parkingSpaceLocation;
			ushort parkingSpaceLocationId;
			Vector3 parkPos;
			Quaternion parkRot;
			float parkOffset;

			if (AdvancedParkingManager.Instance.FindParkingSpaceInVicinity(data.m_position, data.Info, homeID, 0, out parkingSpaceLocation, out parkingSpaceLocationId, out parkPos, out parkRot, out parkOffset)) {
				Singleton<VehicleManager>.instance.RemoveFromGrid(parkedId, ref data);
				data.m_position = parkPos;
				data.m_rotation = parkRot;
				Singleton<VehicleManager>.instance.AddToGrid(parkedId, ref data);
			} else {
				Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedId);
			}
		}

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

		public bool CustomParkVehicle(ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset) {
			PathManager pathManager = Singleton<PathManager>.instance;
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			NetManager netManager = Singleton<NetManager>.instance;
			VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

			// TODO remove this:
			uint driverCitizenId = 0u;
			ushort driverCitizenInstanceId = 0;
			ushort targetBuildingId = 0; // NON-STOCK CODE
			uint curCitizenUnitId = vehicleData.m_citizenUnits;
			int numIterations = 0;
			while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
				uint nextUnit = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnitId)].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizen = citizenManager.m_units.m_buffer[(int)((UIntPtr)curCitizenUnitId)].GetCitizen(i);
					if (citizen != 0u) {
						driverCitizenInstanceId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
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

			// NON-STOCK CODE START
			VehicleState state = null;
			ExtCitizenInstance driverExtInstance = null;
			bool prohibitPocketCars = false;
			// NON-STOCK CODE END

			if (driverCitizenId != 0u) {
				if (Options.prohibitPocketCars) {
					state = VehicleStateManager.Instance._GetVehicleState(vehicleData.GetFirstVehicle(vehicleID));
					if (driverCitizenInstanceId != 0) {
						driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(driverCitizenInstanceId);
						prohibitPocketCars = true;
					}
				}

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
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Vehicle {vehicleID} tries to park on a parking position now (flags: {vehicleData.m_flags})! CurrentPathMode={driverExtInstance.PathMode} path={vehicleData.m_path} pathPositionIndex={vehicleData.m_pathPositionIndex} segmentId={pathPos.m_segment} laneIndex={pathPos.m_lane} offset={pathPos.m_offset} nextPath={nextPath} refPos={refPos} searchDir={searchDir} home={homeID}");
#endif

					if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) {
						// try to use previously found parking space
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Vehicle {vehicleID} tries to park on an (alternative) parking position now! CurrentPathMode={driverExtInstance.PathMode} altParkingSpaceLocation={driverExtInstance.ParkingSpaceLocation} altParkingSpaceLocationId={driverExtInstance.ParkingSpaceLocationId}");
#endif

						switch (driverExtInstance.ParkingSpaceLocation) {
							case ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide:
								uint parkLaneID; int parkLaneIndex;
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"Vehicle {vehicleID} wants to park road-side @ segment {driverExtInstance.ParkingSpaceLocationId}");
#endif
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpaceRoadSideForVehiclePos(this.m_info, 0, driverExtInstance.ParkingSpaceLocationId, refPos, out parkPos, out parkRot, out parkOffset, out parkLaneID, out parkLaneIndex);
								break;
							case ExtCitizenInstance.ExtParkingSpaceLocation.Building:
								float maxDist = 9999f;
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[2])
									Log._Debug($"Vehicle {vehicleID} wants to park @ building {driverExtInstance.ParkingSpaceLocationId}");
#endif
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpacePropAtBuilding(this.m_info, homeID, 0, driverExtInstance.ParkingSpaceLocationId, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[driverExtInstance.ParkingSpaceLocationId], pathPos.m_segment, refPos, ref maxDist, true, out parkPos, out parkRot, out parkOffset);
								break;
							default:
#if DEBUG
								Log.Error($"No alternative parking position stored for vehicle {vehicleID}! PathMode={driverExtInstance.PathMode}");
#endif
								foundParkingSpace = CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset);
								break;
						}
					}
				}

				if (! foundParkingSpace) {
					foundParkingSpace = /*prohibitPocketCars ?*/
						CustomFindParkingSpace(this.m_info, homeID, refPos, searchDir, pathPos.m_segment, out parkPos, out parkRot, out parkOffset) /*:
						FindParkingSpace(homeID, refPos, searchDir, pathPos.m_segment, this.m_info.m_generatedInfo.m_size.x, this.m_info.m_generatedInfo.m_size.z, out parkPos, out parkRot, out parkOffset)*/;
				}

				// NON-STOCK CODE END
				ushort parkedVehicleId;
				if (foundParkingSpace && vehicleManager.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, this.m_info, parkPos, parkRot, driverCitizenId)) {
					// we have reached a parking position
#if DEBUG
					float sqrDist = (refPos - parkPos).sqrMagnitude;
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"Vehicle {vehicleID} succeeded in parking! CurrentPathMode={driverExtInstance?.PathMode} sqrDist={sqrDist}");

					if (GlobalConfig.Instance.DebugSwitches[6] && sqrDist >= 16000) {
						Log._Debug($"CustomPassengerCarAI.CustomParkVehicle: FORCED PAUSE. Distance very large! Vehicle {vehicleID}. dist={sqrDist}");
						Singleton<SimulationManager>.instance.SimulationPaused = true;
					}
#endif

					citizenManager.m_citizens.m_buffer[(int)((UIntPtr)driverCitizenId)].SetParkedVehicle(driverCitizenId, parkedVehicleId);
					if (parkOffset >= 0f) {
						segmentOffset = (byte)(parkOffset * 255f);
					}

					// NON-STOCK CODE START
					if (prohibitPocketCars) {
						if ((driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
							// decrease parking space demand of target building
							ExtBuildingManager.Instance.GetExtBuilding(targetBuildingId).ModifyParkingSpaceDemand(parkPos, GlobalConfig.Instance.MinFoundParkPosParkingSpaceDemandDelta, GlobalConfig.Instance.MaxFoundParkPosParkingSpaceDemandDelta);
						}

						//if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
							// we have reached an (alternative) parking position and succeeded in finding a parking space
							driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkingSucceeded;
							driverExtInstance.FailedParkingAttempts = 0;
							driverExtInstance.ParkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.None;
							driverExtInstance.ParkingSpaceLocationId = 0;
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"Vehicle {vehicleID} has reached an (alternative) parking position! CurrentPathMode={driverExtInstance.PathMode} position={parkPos}");
#endif
						//}
					}
				} else if (prohibitPocketCars) {
					// could not find parking space. vehicle would despawn.
					if (targetBuildingId != 0 && (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None) {
						// target is an outside connection
						return true;
					}

					// Find parking space in the vicinity, redo path-finding to the parking space, park the vehicle and do citizen path-finding to the current target

					if ((driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
						// increase parking space demand of target building
						ExtBuildingManager.Instance.GetExtBuilding(targetBuildingId).AddParkingSpaceDemand(GlobalConfig.Instance.FailedParkingSpaceDemandIncrement * (uint)driverExtInstance.FailedParkingAttempts);
					}

					++driverExtInstance.FailedParkingAttempts;
					driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.ParkingFailed; // TODO if NOT ... ?
					driverExtInstance.ParkingPathStartPosition = pathPos;

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Parking failed for vehicle {vehicleID}! (flags: {vehicleData.m_flags}) pathPos segment={pathPos.m_segment}, lane={pathPos.m_lane}, offset={pathPos.m_offset}. Trying to find parking space in the vicinity. FailedParkingAttempts={driverExtInstance.FailedParkingAttempts}, CurrentPathMode={driverExtInstance.PathMode} foundParkingSpace={foundParkingSpace}");
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
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"Releasing path for citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {citizenManager.m_instances.m_buffer[citizenInstanceId].m_path}).");
#endif
									if (citizenInstanceId != driverCitizenInstanceId) {
										ExtCitizenInstance extPassengerInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(citizenInstanceId);
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"Resetting pathmode for passenger citizen instance {citizenInstanceId} sitting in vehicle {vehicleID} (was {extPassengerInstance.PathMode}).");
#endif

										extPassengerInstance.Reset();
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
#if DEBUG
										if (GlobalConfig.Instance.DebugSwitches[2])
											Log._Debug($"Parking succeeded: Doing nothing for citizen instance {citizenInstanceId}! path: {citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path}");
#endif
										ExtCitizenInstanceManager.Instance.GetExtInstance(citizenInstanceId).PathMode = ExtCitizenInstance.ExtPathMode.ParkingSucceeded;
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
									if (GlobalConfig.Instance.DebugSwitches[2])
										Log._Debug($"Parking succeeded (default): Setting path of citizen instance {citizenInstanceId} to {nextPath}!");
#endif
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
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Parking succeeded (alternative parking spot): Citizen instance {driverExtInstance} has to walk for the remaining path!");
#endif
					/*driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.CalculatingWalkingPathToTarget;
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Setting CurrentPathMode of vehicle {vehicleID} to {driverExtInstance.CurrentPathMode}");*/
				}
			}

			return true;
		}

		private static bool CustomFindParkingSpace(VehicleInfo vehicleInfo, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segmentId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			float searchRadius = Options.prohibitPocketCars ? 32f : 16f;
			uint chanceOfParkingOffRoad = 3u;

			Vector3 searchMagnitude = refPos + searchDir * 16f;

			if (GlobalConfig.RushHourParkingSearchRadius != null) {
				searchRadius = (int)GlobalConfig.RushHourParkingSearchRadius;
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"RushHour's Improved Parking AI is active. searchRadius={searchRadius}");
#endif
				chanceOfParkingOffRoad = 80u;
			} else {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug("RushHour's Improved Parking AI is NOT active.");
#endif
			}

			/*if (Options.prohibitPocketCars) {
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
							if (ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
								return true;
							}
						}
					} else {
						return true;
					}
				}

				if (AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segmentId, refPos2, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}
			} else {
				if (AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, 0, segmentId, refPos2, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, searchRadius, out parkPos, out parkRot, out parkOffset)) {
					return true;
				}

				float width = vehicleInfo.m_generatedInfo.m_size.x;
				float length = vehicleInfo.m_generatedInfo.m_size.z;

				if (FindParkingSpaceRoadSide(0, segmentId, refPos, width - 0.2f, length, out parkPos, out parkRot, out parkOffset)) {
					if (Options.parkingRestrictionsEnabled) {
						Vector3 innerParkPos;
						uint laneId;
						int laneIndex;
						float laneOffset;
						if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out innerParkPos, out laneId, out laneIndex, out laneOffset)) {
							if (ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
								return true;
							}
						}
					} else {
						return true;
					}
				}
			}
			return false;
		}
	}
}

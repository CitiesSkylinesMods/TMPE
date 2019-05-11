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

namespace TrafficManager.Custom.AI {
	// TODO move Parking AI features from here to a distinct manager
	public class CustomPassengerCarAI : CarAI {
		public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 && VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
				Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				return;
			}

			base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
		}

		public string CustomGetLocalizedStatus(ushort vehicleID, ref Vehicle data, out InstanceID target) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort driverInstanceId = GetDriverInstanceId(vehicleID, ref data);
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
				targetIsNode = ((citizenManager.m_instances.m_buffer[driverInstanceId].m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None);
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
#if BENCHMARK
			using (var bm = new Benchmark(null, "EnrichLocalizedCarStatus")) {
#endif
			if (Options.prohibitPocketCars) {
				ret = AdvancedParkingManager.Instance.EnrichLocalizedCarStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId]);
			}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			return ret;
		}

		public static ushort GetDriverInstanceId(ushort vehicleID, ref Vehicle data) { // TODO reverse-redirect
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint curCitUnitId = data.m_citizenUnits;
			int numIters = 0;
			while (curCitUnitId != 0u) {
				uint nextUnit = instance.m_units.m_buffer[curCitUnitId].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizenId = instance.m_units.m_buffer[curCitUnitId].GetCitizen(i);
					if (citizenId != 0u) {
						ushort citInstanceId = instance.m_citizens.m_buffer[citizenId].m_instance;
						if (citInstanceId != 0) {
							return citInstanceId;
						}
					}
				}
				curCitUnitId = nextUnit;
				if (++numIters > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return 0;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			ushort driverInstanceId = CustomPassengerCarAI.GetDriverInstanceId(vehicleID, ref vehicleData);
			if (driverInstanceId == 0) {
				return false;
			}

#if BENCHMARK
			using (var bm = new Benchmark(null, "ExtStartPathFind")) {
#endif
			return ExtStartPathFind(vehicleID, ref vehicleData, driverInstanceId, ref Singleton<CitizenManager>.instance.m_instances.m_buffer[(int)driverInstanceId], ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId], startPos, endPos, startBothWays, endBothWays, undergroundTarget);
#if BENCHMARK
			}
#endif
		}

		public bool ExtStartPathFind(ushort vehicleID, ref Vehicle vehicleData, ushort driverInstanceId, ref CitizenInstance driverInstance, ref ExtCitizenInstance driverExtInstance, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleID) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == driverExtInstance.instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverInstance.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == driverInstance.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == driverInstance.m_targetBuilding)
			;
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

			ushort targetBuildingId = driverInstance.m_targetBuilding;
			uint driverCitizenId = driverInstance.m_citizen;

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
			if (Options.prohibitPocketCars) {
				//if (driverExtInstance != null) {
#if DEBUG
				if (debug)
					Log.Warning($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): PathMode={driverExtInstance.pathMode} for vehicle {vehicleID}, driver citizen instance {driverExtInstance.instanceId}!");
#endif

				if (driverExtInstance.pathMode == ExtPathMode.RequiresMixedCarPathToTarget) {
					driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
					startBothWays = false;
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): PathMode was RequiresDirectCarPathToTarget: Parking spaces will NOT be searched beforehand. Setting pathMode={driverExtInstance.pathMode}");
#endif
				} else if (
					driverExtInstance.pathMode != ExtPathMode.ParkingFailed &&
					targetBuildingId != 0 &&
					(Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None
				) {
					// target is outside connection
					driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;

#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): PathMode was not ParkingFailed and target is outside connection: Setting pathMode={driverExtInstance.pathMode}");
#endif
				} else {
					if (driverExtInstance.pathMode == ExtPathMode.DrivingToTarget || driverExtInstance.pathMode == ExtPathMode.DrivingToKnownParkPos || driverExtInstance.pathMode == ExtPathMode.ParkingFailed) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Skipping queue. pathMode={driverExtInstance.pathMode}");
#endif
						skipQueue = true;
					}

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
							driverExtInstance.Reset();

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
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): No parking involved: Setting pathMode={driverExtInstance.pathMode}");
#endif
					}

					ushort homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[driverCitizenId].m_homeBuilding;
					bool calcEndPos;
					Vector3 parkPos;

					Vector3 returnPos = searchAtCurrentPos ? (Vector3)vehicleData.m_targetPos3 : endPos;
					if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(returnPos, vehicleData.Info, ref driverExtInstance, homeId, targetBuildingId == homeId, vehicleID, allowTourists, out parkPos, ref endPosA, out calcEndPos)) {
						calculateEndPos = calcEndPos;
						allowRandomParking = false;
						movingToParkingPos = true;

						if (!driverExtInstance.CalculateReturnPath(parkPos, returnPos)) {
#if DEBUG
							if (debug)
								Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Could not calculate return path for citizen instance {driverExtInstance.instanceId}, vehicle {vehicleID}. Resetting instance.");
#endif
							driverExtInstance.Reset();
							return false;
						}
					} else if (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos) {
						// no alternative parking spot found: abort
#if DEBUG
						if (debug)
							Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): No alternative parking spot found for vehicle {vehicleID}, citizen instance {driverExtInstance.instanceId} with CurrentPathMode={driverExtInstance.pathMode}! GIVING UP.");
#endif
						driverExtInstance.Reset();
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
				driverExtInstance.atOutsideConnection = Constants.ManagerFactory.ExtCitizenInstanceManager.IsAtOutsideConnection(driverInstanceId, ref driverInstance, ref driverExtInstance, startPos);
			}
#if BENCHMARK
			}
#endif

			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle;
			if (!movingToParkingPos) {
				laneTypes |= NetInfo.LaneType.Pedestrian;
				
				if (Options.prohibitPocketCars && (driverInstance.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					/*
					 * citizen may use public transport
					 */
					laneTypes |= NetInfo.LaneType.PublicTransport;

					uint citizenId = driverInstance.m_citizen;
					if (citizenId != 0u && (Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
						laneTypes |= NetInfo.LaneType.EvacuationTransport;
					}
				}
			}
			// NON-STOCK CODE END

			VehicleInfo.VehicleType vehicleTypes = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			bool randomParking = false;
			bool combustionEngine = this.m_info.m_class.m_subService == ItemClass.SubService.ResidentialLow;
			if (allowRandomParking && // NON-STOCK CODE
				!movingToParkingPos &&
				targetBuildingId != 0 &&
				(
					Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuildingId].Info.m_class.m_service > ItemClass.Service.Office ||
					(driverInstance.m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None
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

			bool foundEndPos = !calculateEndPos || driverInstance.Info.m_citizenAI.FindPathPosition(driverInstanceId, ref driverInstance, endPos, Options.prohibitPocketCars && (targetBuildingId == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : (laneTypes | NetInfo.LaneType.Pedestrian), vehicleTypes, undergroundTarget, out endPosA);
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
				args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
				args.buildIndex = simMan.m_currentBuildIndex;
				args.startPosA = startPosA;
				args.startPosB = startPosB;
				args.endPosA = endPosA;
				args.endPosB = endPosB;
				args.vehiclePosition = dummyPathPos;
				args.laneTypes = laneTypes;
				args.vehicleTypes = vehicleTypes;
				args.maxLength = 20000f;
				args.isHeavyVehicle = this.IsHeavyVehicle();
				args.hasCombustionEngine = this.CombustionEngine();
				args.ignoreBlocked = this.IgnoreBlocked(vehicleID, ref vehicleData);
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = randomParking;
				args.stablePath = false;
				args.skipQueue = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;

				if (CustomPathManager._instance.CreatePath(out path, ref simMan.m_randomizer, args)) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomPassengerCarAI.ExtStartPathFind({vehicleID}): Path-finding starts for passenger car {vehicleID}, path={path}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, startPosA.offset={startPosA.m_offset}, startPosB.segment={startPosB.m_segment}, startPosB.lane={startPosB.m_lane}, startPosB.offset={startPosB.m_offset}, laneType={laneTypes}, vehicleType={vehicleTypes}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, endPosA.offset={endPosA.m_offset}, endPosB.segment={endPosB.m_segment}, endPosB.lane={endPosB.m_lane}, endPosB.offset={endPosB.m_offset}");
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

			if (Options.prohibitPocketCars) {
				driverExtInstance.Reset();
			}
			return false;
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
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.VehicleId == 0 || GlobalConfig.Instance.Debug.VehicleId == vehicleID) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == driverExtInstance.instanceId) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == driverInstance.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == driverInstance.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == driverInstance.m_targetBuilding)
			;
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
				if (Options.prohibitPocketCars && driverCitizenInstanceId != 0) {
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

					if (driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) {
						// try to use previously found parking space
#if DEBUG
						if (debug)
							Log._Debug($"Vehicle {vehicleID} tries to park on an (alternative) parking position now! CurrentPathMode={driverExtInstance.pathMode} altParkingSpaceLocation={driverExtInstance.parkingSpaceLocation} altParkingSpaceLocationId={driverExtInstance.parkingSpaceLocationId}");
#endif

						switch (driverExtInstance.parkingSpaceLocation) {
							case ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide:
								uint parkLaneID; int parkLaneIndex;
#if DEBUG
								if (debug)
									Log._Debug($"Vehicle {vehicleID} wants to park road-side @ segment {driverExtInstance.parkingSpaceLocationId}");
#endif
								foundParkingSpace = AdvancedParkingManager.Instance.FindParkingSpaceRoadSideForVehiclePos(this.m_info, 0, driverExtInstance.parkingSpaceLocationId, refPos, out parkPos, out parkRot, out parkOffset, out parkLaneID, out parkLaneIndex);
								break;
							case ExtCitizenInstance.ExtParkingSpaceLocation.Building:
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
						if ((driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
							// decrease parking space demand of target building
							ExtBuildingManager.Instance.ExtBuildings[targetBuildingId].ModifyParkingSpaceDemand(parkPos, GlobalConfig.Instance.ParkingAI.MinFoundParkPosParkingSpaceDemandDelta, GlobalConfig.Instance.ParkingAI.MaxFoundParkPosParkingSpaceDemandDelta);
						}

						//if (driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToAltParkPos || driverExtInstance.CurrentPathMode == ExtCitizenInstance.PathMode.DrivingToKnownParkPos) {
						// we have reached an (alternative) parking position and succeeded in finding a parking space
						driverExtInstance.pathMode = ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToTarget;
						driverExtInstance.failedParkingAttempts = 0;
						driverExtInstance.parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.None;
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

					if (!foundParkingSpace && (driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos || driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos) && targetBuildingId != 0) {
						// increase parking space demand of target building
						if (driverExtInstance.failedParkingAttempts > 1) {
							ExtBuildingManager.Instance.ExtBuildings[targetBuildingId].AddParkingSpaceDemand(GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement * (uint)(driverExtInstance.failedParkingAttempts - 1));
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
					driverExtInstance.pathMode = ExtCitizenInstance.ExtPathMode.ParkingFailed;
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

										ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].Reset();
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
									if (driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToTarget) {
#if DEBUG
										if (debug)
											Log._Debug($"CustomPassengerCarAI.ExtParkVehicle({vehicleID}): Parking succeeded: Doing nothing for citizen instance {citizenInstanceId}! path: {citizenManager.m_instances.m_buffer[(int)citizenInstanceId].m_path}");
#endif
										ExtCitizenInstanceManager.Instance.ExtInstances[citizenInstanceId].pathMode = ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToTarget;
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
				if (driverExtInstance.pathMode == ExtCitizenInstance.ExtPathMode.RequiresWalkingPathToTarget) {
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

			float searchRadius = Options.prohibitPocketCars ? 32f : 16f;
			uint chanceOfParkingOffRoad = 3u;

			Vector3 searchMagnitude = refPos + searchDir * 16f;

			if (GlobalConfig.RushHourParkingSearchRadius != null) {
				searchRadius = (int)GlobalConfig.RushHourParkingSearchRadius;
#if DEBUG
				if (debug)
					Log._Debug($"RushHour's Improved Parking AI is active. searchRadius={searchRadius}");
#endif
				chanceOfParkingOffRoad = 80u;
			} else {
#if DEBUG
				if (debug)
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

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static bool FindParkingSpaceRoadSide(ushort ignoreParked, ushort requireSegment, Vector3 refPos, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpaceRoadSide is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static bool FindParkingSpace(bool isElectric, ushort homeID, Vector3 refPos, Vector3 searchDir, ushort segment, float width, float length, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Log.Error("FindParkingSpace is not overridden!");
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static bool FindParkingSpaceProp(bool isElectric, ushort ignoreParked, PropInfo info, Vector3 position, float angle, bool fixedHeight, Vector3 refPos, float width, float length, ref float maxDistance, ref Vector3 parkPos, ref Quaternion parkRot) {
			Log.Error("FindParkingSpaceProp is not overridden!");
			return false;
		}
	}
}

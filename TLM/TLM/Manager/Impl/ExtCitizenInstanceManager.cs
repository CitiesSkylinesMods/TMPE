using ColossalFramework;
using ColossalFramework.Globalization;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Manager.Impl {
	public class ExtCitizenInstanceManager : AbstractCustomManager, ICustomDataManager<List<Configuration.ExtCitizenInstanceData>>, IExtCitizenInstanceManager {
		public static ExtCitizenInstanceManager Instance = new ExtCitizenInstanceManager();

		/// <summary>
		/// All additional data for citizen instance. Index: citizen instance id
		/// </summary>
		public ExtCitizenInstance[] ExtInstances { get; private set; }

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended citizen instance data:");
			for (int i = 0; i < ExtInstances.Length; ++i) {
				if (!IsValid((ushort)i)) {
					continue;
				}
				Log._Debug($"Citizen instance {i}: {ExtInstances[i]}");
			}
		}

		public void OnReleaseInstance(ushort instanceId) {
			Reset(ref ExtInstances[instanceId]);
		}

		public void ResetInstance(ushort instanceId) {
			Reset(ref ExtInstances[instanceId]);
		}

		private ExtCitizenInstanceManager() {
			ExtInstances = new ExtCitizenInstance[CitizenManager.MAX_INSTANCE_COUNT];
			for (uint i = 0; i < CitizenManager.MAX_INSTANCE_COUNT; ++i) {
				ExtInstances[i] = new ExtCitizenInstance((ushort)i);
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			Reset();
		}

		internal void Reset() {
			for (int i = 0; i < ExtInstances.Length; ++i) {
				Reset(ref ExtInstances[i]);
			}
		}

		public String GetTouristLocalizedStatus(ushort instanceID, ref CitizenInstance data, out bool mayAddCustomStatus, out InstanceID target) {
			if ((data.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None) {
				target = InstanceID.Empty;
				mayAddCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint citizenId = data.m_citizen;
			ushort vehicleId = 0;
			if (citizenId != 0u) {
				vehicleId = instance.m_citizens.m_buffer[citizenId].m_vehicle;
			}

			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				mayAddCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			if ((data.m_flags & CitizenInstance.Flags.TargetIsNode) != 0) {
				if (vehicleId != 0) {
					VehicleManager vehManager = Singleton<VehicleManager>.instance;
					VehicleInfo info = vehManager.m_vehicles.m_buffer[vehicleId].Info;
					if (info.m_class.m_service == ItemClass.Service.Residential && info.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
						if (info.m_vehicleAI.GetOwnerID(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId]).Citizen == citizenId) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_DRIVINGTO");
						}
					} else if (info.m_class.m_service == ItemClass.Service.PublicTransport || info.m_class.m_service == ItemClass.Service.Disaster) {
						ushort transportLine = Singleton<NetManager>.instance.m_nodes.m_buffer[targetBuilding].m_transportLine;
						if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != 0) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
						}
						if (vehManager.m_vehicles.m_buffer[vehicleId].m_transportLine != transportLine) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
						}
					}
				}

				if ((data.m_flags & CitizenInstance.Flags.OnTour) != 0) {
					target = InstanceID.Empty;
					target.NetNode = targetBuilding;
					mayAddCustomStatus = true;
					return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_VISITING");
				}

				target = InstanceID.Empty;
				target.NetNode = targetBuilding;
				mayAddCustomStatus = true;
				return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_GOINGTO");
			}

			bool isOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool hangsAround = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;

			if (vehicleId != 0) {
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				VehicleInfo vehicleInfo = vehManager.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (vehicleInfo.m_class.m_service == ItemClass.Service.Residential && vehicleInfo.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (vehicleInfo.m_vehicleAI.GetOwnerID(vehicleId, ref vehManager.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizenId) {
						if (isOutsideConnection) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}

						target = InstanceID.Empty;
						target.Building = targetBuilding;
						mayAddCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
					}
				} else if (vehicleInfo.m_class.m_service == ItemClass.Service.PublicTransport || vehicleInfo.m_class.m_service == ItemClass.Service.Disaster) {
					if (isOutsideConnection) {
						target = InstanceID.Empty;
						mayAddCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
					}
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					mayAddCustomStatus = true;
					return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
				}
			}
			if (isOutsideConnection) {
				target = InstanceID.Empty;
				mayAddCustomStatus = true;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
			}

			if (hangsAround) {
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				mayAddCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_VISITING");
			}

			target = InstanceID.Empty;
			target.Building = targetBuilding;
			mayAddCustomStatus = true;
			return Locale.Get("CITIZEN_STATUS_GOINGTO");
		}

		public String GetResidentLocalizedStatus(ushort instanceID, ref CitizenInstance data, out bool mayAddCustomStatus, out InstanceID target) {
			if ((data.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None) {
				target = InstanceID.Empty;
				mayAddCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			CitizenManager citMan = Singleton<CitizenManager>.instance;
			uint citizenId = data.m_citizen;
			bool isStudent = false;
			ushort homeId = 0;
			ushort workId = 0;
			ushort vehicleId = 0;
			if (citizenId != 0u) {
				homeId = citMan.m_citizens.m_buffer[citizenId].m_homeBuilding;
				workId = citMan.m_citizens.m_buffer[citizenId].m_workBuilding;
				vehicleId = citMan.m_citizens.m_buffer[citizenId].m_vehicle;
				isStudent = ((citMan.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Student) != Citizen.Flags.None);
			}
			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				mayAddCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			if ((data.m_flags & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None) {
				if (vehicleId != 0) {
					VehicleManager vehManager = Singleton<VehicleManager>.instance;
					VehicleInfo vehicleInfo = vehManager.m_vehicles.m_buffer[vehicleId].Info;
					if (vehicleInfo.m_class.m_service == ItemClass.Service.Residential && vehicleInfo.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
						if (vehicleInfo.m_vehicleAI.GetOwnerID(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId]).Citizen == citizenId) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_DRIVINGTO");
						}
					} else if (vehicleInfo.m_class.m_service == ItemClass.Service.PublicTransport || vehicleInfo.m_class.m_service == ItemClass.Service.Disaster) {
						ushort transportLine = Singleton<NetManager>.instance.m_nodes.m_buffer[targetBuilding].m_transportLine;
						if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != 0) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
						}

						if (vehManager.m_vehicles.m_buffer[vehicleId].m_transportLine != transportLine) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							mayAddCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
						}
					}
				}

				if ((data.m_flags & CitizenInstance.Flags.OnTour) != 0) {
					target = InstanceID.Empty;
					target.NetNode = targetBuilding;
					mayAddCustomStatus = true;
					return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_VISITING");
				}

				target = InstanceID.Empty;
				target.NetNode = targetBuilding;
				mayAddCustomStatus = true;
				return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_GOINGTO");
			}

			bool isOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool hangsAround = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;
			if (vehicleId != 0) {
				VehicleManager vehicleMan = Singleton<VehicleManager>.instance;
				VehicleInfo vehicleInfo = vehicleMan.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (vehicleInfo.m_class.m_service == ItemClass.Service.Residential && vehicleInfo.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (vehicleInfo.m_vehicleAI.GetOwnerID(vehicleId, ref vehicleMan.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizenId) {
						if (isOutsideConnection) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}

						if (targetBuilding == homeId) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_HOME");
						} else if (targetBuilding == workId) {
							target = InstanceID.Empty;
							mayAddCustomStatus = true;
							return Locale.Get((!isStudent) ? "CITIZEN_STATUS_DRIVINGTO_WORK" : "CITIZEN_STATUS_DRIVINGTO_SCHOOL");
						} else {
							target = InstanceID.Empty;
							target.Building = targetBuilding;
							mayAddCustomStatus = true;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
						}
					}
				} else if (vehicleInfo.m_class.m_service == ItemClass.Service.PublicTransport || vehicleInfo.m_class.m_service == ItemClass.Service.Disaster) {
					if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != CitizenInstance.Flags.None) {
						target = InstanceID.Empty;
						mayAddCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
					}
					if (isOutsideConnection) {
						target = InstanceID.Empty;
						mayAddCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
					}
					if (targetBuilding == homeId) {
						target = InstanceID.Empty;
						mayAddCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_HOME");
					}
					if (targetBuilding == workId) {
						target = InstanceID.Empty;
						mayAddCustomStatus = true;
						return Locale.Get((!isStudent) ? "CITIZEN_STATUS_TRAVELLINGTO_WORK" : "CITIZEN_STATUS_TRAVELLINGTO_SCHOOL");
					}
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					mayAddCustomStatus = true;
					return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
				}
			}

			if (isOutsideConnection) {
				target = InstanceID.Empty;
				mayAddCustomStatus = true;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
			}

			if (targetBuilding == homeId) {
				if (hangsAround) {
					target = InstanceID.Empty;
					mayAddCustomStatus = false;
					return Locale.Get("CITIZEN_STATUS_AT_HOME");
				}

				target = InstanceID.Empty;
				mayAddCustomStatus = true;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_HOME");
			} else if (targetBuilding == workId) {
				if (hangsAround) {
					target = InstanceID.Empty;
					mayAddCustomStatus = false;
					return Locale.Get((!isStudent) ? "CITIZEN_STATUS_AT_WORK" : "CITIZEN_STATUS_AT_SCHOOL");
				}
				target = InstanceID.Empty;
				mayAddCustomStatus = true;
				return Locale.Get((!isStudent) ? "CITIZEN_STATUS_GOINGTO_WORK" : "CITIZEN_STATUS_GOINGTO_SCHOOL");
			} else {
				if (hangsAround) {
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					mayAddCustomStatus = false;
					return Locale.Get("CITIZEN_STATUS_VISITING");
				}
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				mayAddCustomStatus = true;
				return Locale.Get("CITIZEN_STATUS_GOINGTO");
			}
		}

		public bool StartPathFind(ushort instanceID, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, ref ExtCitizen extCitizen, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo, bool enableTransport, bool ignoreCost) {
#if DEBUG
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceID) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == instanceData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == instanceData.m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log.Warning($"CustomCitizenAI.ExtStartPathFind({instanceID}): called for citizen {instanceData.m_citizen}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding}, pathMode={extInstance.pathMode}, enableTransport={enableTransport}, ignoreCost={ignoreCost}");
#endif

			// NON-STOCK CODE START
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			ushort parkedVehicleId = citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_parkedVehicle;
			ushort homeId = citizenManager.m_citizens.m_buffer[instanceData.m_citizen].m_homeBuilding;
			CarUsagePolicy carUsageMode = CarUsagePolicy.Allowed;

#if BENCHMARK
			using (var bm = new Benchmark(null, "ParkingAI.Preparation")) {
#endif
			bool startsAtOutsideConnection = false;
			if (Options.parkingAI) {
				switch (extInstance.pathMode) {
					case ExtPathMode.RequiresWalkingPathToParkedCar:
					case ExtPathMode.CalculatingWalkingPathToParkedCar:
					case ExtPathMode.WalkingToParkedCar:
					case ExtPathMode.ApproachingParkedCar:
						if (parkedVehicleId == 0) {
							/*
							 * Parked vehicle not present but citizen wants to reach it
							 * -> Reset path mode
							 */
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

							Reset(ref extInstance);
						} else {
							/*
							 * Parked vehicle is present and citizen wants to reach it
							 * -> Prohibit car usage
							 */
#if DEBUG
							if (fineDebug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'CalculatingWalkingPathToParkedCar'.");
#endif
							extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
							carUsageMode = CarUsagePolicy.Forbidden;
						}
						break;
					case ExtPathMode.RequiresWalkingPathToTarget:
					case ExtPathMode.CalculatingWalkingPathToTarget:
					case ExtPathMode.WalkingToTarget:
						/*
						 * Citizen walks to target
						 * -> Reset path mode
						 */
#if DEBUG
						if (fineDebug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'CalculatingWalkingPathToTarget'.");
#endif
						extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
						carUsageMode = CarUsagePolicy.Forbidden;
						break;
					case ExtPathMode.RequiresCarPath:
					case ExtPathMode.RequiresMixedCarPathToTarget:
					case ExtPathMode.DrivingToTarget:
					case ExtPathMode.DrivingToKnownParkPos:
					case ExtPathMode.DrivingToAltParkPos:
					case ExtPathMode.CalculatingCarPathToAltParkPos:
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.CalculatingCarPathToTarget:
						if (parkedVehicleId == 0) {
							/*
							 * Citizen wants to drive to target but parked vehicle is not present
							 * -> Reset path mode
							 */

#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode} but no parked vehicle present. Change to 'None'.");
#endif

							Reset(ref extInstance);
						} else {
							/*
							 * Citizen wants to drive to target and parked vehicle is present
							 * -> Force parked car usage
							 */

#if DEBUG
							if (fineDebug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}.  Change to 'RequiresCarPath'.");
#endif

							extInstance.pathMode = ExtPathMode.RequiresCarPath;
							carUsageMode = CarUsagePolicy.ForcedParked;
							startPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position; // force to start from the parked car
						}
						break;
					default:
#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has CurrentPathMode={extInstance.pathMode}. Change to 'None'.");
#endif
						Reset(ref extInstance);
						break;
				}

				startsAtOutsideConnection = Constants.ManagerFactory.ExtCitizenInstanceManager.IsAtOutsideConnection(instanceID, ref instanceData, ref extInstance, startPos);
				if (extInstance.pathMode == ExtPathMode.None) {
					if ((instanceData.m_flags & CitizenInstance.Flags.OnTour) != CitizenInstance.Flags.None || ignoreCost) {
						/*
						 * Citizen is on a walking tour or is a mascot
						 * -> Prohibit car usage
						 */
#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen ignores cost ({ignoreCost}) or is on a walking tour ({(instanceData.m_flags & CitizenInstance.Flags.OnTour) != CitizenInstance.Flags.None}): Setting path mode to 'CalculatingWalkingPathToTarget'");
#endif
						carUsageMode = CarUsagePolicy.Forbidden;
						extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
					} else {
						/*
						 * Citizen is not on a walking tour and is not a mascot
						 * -> Check if citizen is located at an outside connection and make them obey Parking AI restrictions
						 */

						if (instanceData.m_sourceBuilding != 0) {
							ItemClass.Service sourceBuildingService = Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].Info.m_class.m_service;

							if (startsAtOutsideConnection) {
								if (sourceBuildingService == ItemClass.Service.Road) {
									if (vehicleInfo != null) {
										/*
										 * Citizen is located at a road outside connection and can spawn a car
										 * -> Force car usage
										 */
#if DEBUG
										if (debug)
											Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a road outside connection: Setting path mode to 'RequiresCarPath' and carUsageMode to 'ForcedPocket'");
#endif
										extInstance.pathMode = ExtPathMode.RequiresCarPath;
										carUsageMode = CarUsagePolicy.ForcedPocket;
									} else {
										/*
										 * Citizen is located at a non-road outside connection and cannot spawn a car
										 * -> Path-finding fails
										 */
#if DEBUG
										if (debug)
											Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a road outside connection but does not have a car template: ABORTING PATH-FINDING");
#endif
										Reset(ref extInstance);
										return false;
									}
								} else {
									/*
									 * Citizen is located at a non-road outside connection
									 * -> Prohibit car usage
									 */
#if DEBUG
									if (debug)
										Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is located at a non-road outside connection: Setting path mode to 'CalculatingWalkingPathToTarget'");
#endif
									extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
									carUsageMode = CarUsagePolicy.Forbidden;
								}
							}
						}
					}
				}

				if ((carUsageMode == CarUsagePolicy.Allowed || carUsageMode == CarUsagePolicy.ForcedParked) && parkedVehicleId != 0) {
					/*
					* Reuse parked vehicle info
					*/
					vehicleInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;

					/*
					 * Check if the citizen should return their car back home
					 */
					if (extInstance.pathMode == ExtPathMode.None && // initiating a new path
						homeId != 0 && // home building present
						instanceData.m_targetBuilding == homeId // current target is home
					) {
						/*
						 * citizen travels back home
						 * -> check if their car should be returned
						 */
						if ((extCitizen.lastTransportMode & ExtTransportMode.Car) != ExtTransportMode.None) {
							/*
							 * citizen travelled by car
							 * -> return car back home
							 */
							extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
							carUsageMode = CarUsagePolicy.Forbidden;

#if DEBUG
							if (fineDebug)
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
								extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToParkedCar;
								carUsageMode = CarUsagePolicy.Forbidden;

#if DEBUG
								if (fineDebug)
									Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen wants to go home and parked car is too far away ({distHomeToParked}). Forcing walking to parked car.");
#endif
							}
						}
					}
				}

				/*
				 * The following holds:
				 * - pathMode is now either CalculatingWalkingPathToParkedCar, CalculatingWalkingPathToTarget, RequiresCarPath or None.
				 * - if pathMode is CalculatingWalkingPathToParkedCar or RequiresCarPath: parked car is present and citizen is not on a walking tour
				 * - carUsageMode is valid
				 * - if pathMode is RequiresCarPath: carUsageMode is either ForcedParked or ForcedPocket
				 */

				/*
				 * modify path-finding constraints (vehicleInfo, endPos) if citizen is forced to walk
				 */
				if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar || extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget) {
					/*
					 * vehicle must not be used since we need a walking path to either
					 * 1. a parked car or
					 * 2. the target building
					 */

					if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
						/*
						 * walk to parked car
						 * -> end position is parked car
						 */
						endPos = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_position;
#if DEBUG
						if (fineDebug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen shall go to parked vehicle @ {endPos}");
#endif
					}
				}
			}
#if BENCHMARK
			}
#endif
#if DEBUG
			if (fineDebug)
				Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is allowed to drive their car? {carUsageMode}");
#endif
			// NON-STOCK CODE END

			/*
			 * semi-stock code: determine path-finding parameters (laneTypes, vehicleTypes, extVehicleType, etc.)
			 */
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			bool combustionEngine = false;
			ExtVehicleType extVehicleType = ExtVehicleType.None;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						SimulationManager instance = Singleton<SimulationManager>.instance;
						if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
							laneTypes |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
							vehicleTypes |= vehicleInfo.m_vehicleType;
							extVehicleType = ExtVehicleType.Taxi; // NON-STOCK CODE
																  // NON-STOCK CODE START
							if (Options.parkingAI) {
								extInstance.pathMode = ExtPathMode.TaxiToTarget;
							}
							// NON-STOCK CODE END
						}
					}
				} else
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
				}
				// NON-STOCK CODE END
			}

			// NON-STOCK CODE START
			ExtPathType extPathType = ExtPathType.None;
			PathUnit.Position endPosA = default(PathUnit.Position);
			bool calculateEndPos = true;
			bool allowRandomParking = true;
#if BENCHMARK
			using (var bm = new Benchmark(null, "ParkingAI.Main")) {
#endif
			if (Options.parkingAI) {
				// Parking AI

				if (extInstance.pathMode == ExtPathMode.RequiresCarPath) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Setting startPos={startPos} for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode}");
#endif

					if (
						instanceData.m_targetBuilding == 0 ||
						(Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None
					) {
						/*
						 * the citizen is starting their journey and the target is not an outside connection
						 * -> find a suitable parking space near the target
						 */

#if DEBUG
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding parking space at target for citizen instance {instanceID}. CurrentDepartureMode={extInstance.pathMode} parkedVehicleId={parkedVehicleId}");
#endif

						// find a parking space in the vicinity of the target
						bool calcEndPos;
						Vector3 parkPos;
						if (
							AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(endPos, vehicleInfo, ref extInstance, homeId, instanceData.m_targetBuilding == homeId, 0, false, out parkPos, ref endPosA, out calcEndPos) &&
							CalculateReturnPath(ref extInstance, parkPos, endPos)
						) {
							// success
							extInstance.pathMode = ExtPathMode.CalculatingCarPathToKnownParkPos;
							calculateEndPos = calcEndPos; // if true, the end path position still needs to be calculated
							allowRandomParking = false; // find a direct path to the calculated parking position
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding known parking space for citizen instance {instanceID}, parked vehicle {parkedVehicleId} succeeded and return path {extInstance.returnPathId} ({extInstance.returnPathState}) is calculating. PathMode={extInstance.pathMode}");
#endif
							/*if (! extInstance.CalculateReturnPath(parkPos, endPos)) {
								// TODO retry?
								if (debug)
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
						if (debug)
							Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance {instanceID} is still at CurrentPathMode={extInstance.pathMode} (no parking space found?). Setting it to CalculatingCarPath. parkedVehicleId={parkedVehicleId}");
#endif
						extInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
					}
				}

				/*
				 * determine path type from path mode
				 */
				extPathType = extInstance.GetPathType();
				extInstance.atOutsideConnection = startsAtOutsideConnection;
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
			if (extVehicleType == ExtVehicleType.PassengerCar || extVehicleType == ExtVehicleType.Bicycle) {
				if (allowRandomParking &&
					instanceData.m_targetBuilding != 0 &&
					(
						Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office ||
						(instanceData.m_flags & CitizenInstance.Flags.TargetIsNode) != 0
					)) {
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
				Constants.ManagerFactory.ExtPathManager.FindPathPositionWithSpiralLoop(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance, out parkedVehiclePathPos);
			}
			bool allowUnderground = (instanceData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;

#if DEBUG
			if (debug)
				Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Requesting path-finding for citizen instance {instanceID}, citizen {instanceData.m_citizen}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, targetBuilding={instanceData.m_targetBuilding} pathMode={extInstance.pathMode}");
#endif

			/*
			 * determine start & end path positions
			 */
			bool foundEndPos = !calculateEndPos || FindPathPosition(instanceID, ref instanceData, endPos, Options.parkingAI && (instanceData.m_targetBuilding == 0 || (Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_targetBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) ? NetInfo.LaneType.Pedestrian : laneTypes, vehicleTypes, false, out endPosA); // NON-STOCK CODE: with Parking AI enabled, the end position must be a pedestrian position
			bool foundStartPos = false;
			PathUnit.Position startPosA;

			if (Options.parkingAI && (extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget || extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos)) {
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

				if (enableTransport) {
					/*
					 * public transport usage is allowed for this path
					 */
					if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
						/*
						* citizen may use public transport
						*/
						laneTypes |= NetInfo.LaneType.PublicTransport;

						uint citizenId = instanceData.m_citizen;
						if (citizenId != 0u && (citizenManager.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
							laneTypes |= NetInfo.LaneType.EvacuationTransport;
						}
					} else if (Options.parkingAI) { // TODO check for incoming connection
															 /*
															 * citizen tried to use public transport but waiting time was too long
															 * -> add public transport demand for source building
															 */
						if (instanceData.m_sourceBuilding != 0) {
#if DEBUG
							if (debug)
								Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance {instanceID} cannot uses public transport from building {instanceData.m_sourceBuilding} to {instanceData.m_targetBuilding}. Incrementing public transport demand.");
#endif
							IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
							extBuildingManager.AddPublicTransportDemand(ref extBuildingManager.ExtBuildings[instanceData.m_sourceBuilding], GlobalConfig.Instance.ParkingAI.PublicTransportDemandWaitingIncrement, true);
						}
					}
				}

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint path;
				// NON-STOCK CODE START
				PathCreationArgs args;
				args.extPathType = extPathType;
				args.extVehicleType = extVehicleType;
				args.vehicleId = 0;
				args.spawned = (instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None;
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
				args.ignoreCosts = ignoreCost;
				args.randomParking = randomParking;
				args.stablePath = false;
				args.skipQueue = false;

				if ((instanceData.m_flags & CitizenInstance.Flags.OnTour) != 0) {
					args.stablePath = true;
					args.maxLength = 160000f;
					//args.laneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				} else {
					args.stablePath = false;
					args.maxLength = 20000f;
				}

				bool res = CustomPathManager._instance.CustomCreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, args);
				// NON-STOCK CODE END

				if (res) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): Path-finding starts for citizen instance {instanceID}, path={path}, extVehicleType={extVehicleType}, extPathType={extPathType}, startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, laneType={laneTypes}, vehicleType={vehicleTypes}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, vehiclePos.m_segment={parkedVehiclePathPos.m_segment}, vehiclePos.m_lane={parkedVehiclePathPos.m_lane}, vehiclePos.m_offset={parkedVehiclePathPos.m_offset}");
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
			if (Options.parkingAI) {
				if (debug)
					Log._Debug($"CustomCitizenAI.ExtStartPathFind({instanceID}): CustomCitizenAI.CustomStartPathFind: [PFFAIL] failed for citizen instance {instanceID} (CurrentPathMode={extInstance.pathMode}). startPosA.segment={startPosA.m_segment}, startPosA.lane={startPosA.m_lane}, startPosA.offset={startPosA.m_offset}, endPosA.segment={endPosA.m_segment}, endPosA.lane={endPosA.m_lane}, endPosA.offset={endPosA.m_offset}, foundStartPos={foundStartPos}, foundEndPos={foundEndPos}");
				Reset(ref extInstance);
			}
#endif
			return false;
		}

		public bool FindPathPosition(ushort instanceID, ref CitizenInstance instanceData, Vector3 pos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, bool allowUnderground, out PathUnit.Position position) {
			position = default(PathUnit.Position);
			float minDist = 1E+10f;
			PathUnit.Position posA;
			PathUnit.Position posB;
			float distA;
			float distB;
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Road, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if (PathManager.FindPathPosition(pos, ItemClass.Service.Beautification, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None && PathManager.FindPathPosition(pos, ItemClass.Service.PublicTransport, laneTypes, vehicleTypes, allowUnderground, false, Options.parkingAI ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance : 32f, out posA, out posB, out distA, out distB) && distA < minDist) {
				minDist = distA;
				position = posA;
			}
			return position.m_segment != 0;
		}

		public bool IsValid(ushort instanceId) {
			return Constants.ServiceFactory.CitizenService.IsCitizenInstanceValid(instanceId);
		}

		public uint GetCitizenId(ushort instanceId) {
			uint ret = 0;
			Constants.ServiceFactory.CitizenService.ProcessCitizenInstance(instanceId, delegate (ushort citInstId, ref CitizenInstance citizenInst) {
				ret = citizenInst.m_citizen;
				return true;
			});
			return ret;
		}

		/// <summary>
		/// Releases the return path
		/// </summary>
		public void ReleaseReturnPath(ref ExtCitizenInstance extInstance) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == GetCitizenId(extInstance.instanceId);
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			if (extInstance.returnPathId != 0) {
#if DEBUG
				if (debug)
					Log._Debug($"Releasing return path {extInstance.returnPathId} of citizen instance {extInstance.instanceId}. ReturnPathState={extInstance.returnPathState}");
#endif

				Singleton<PathManager>.instance.ReleasePath(extInstance.returnPathId);
				extInstance.returnPathId = 0;
			}
			extInstance.returnPathState = ExtPathState.None;
		}

		/// <summary>
		///
		/// </summary>
		public void UpdateReturnPathState(ref ExtCitizenInstance extInstance) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == GetCitizenId(extInstance.instanceId);
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (fineDebug)
				Log._Debug($"ExtCitizenInstance.UpdateReturnPathState() called for citizen instance {extInstance.instanceId}");
#endif
			if (extInstance.returnPathId != 0 && extInstance.returnPathState == ExtPathState.Calculating) {
				byte returnPathFlags = CustomPathManager._instance.m_pathUnits.m_buffer[extInstance.returnPathId].m_pathFindFlags;
				if ((returnPathFlags & PathUnit.FLAG_READY) != 0) {
					extInstance.returnPathState = ExtPathState.Ready;
#if DEBUG
					if (fineDebug)
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {extInstance.returnPathId} SUCCEEDED. Flags={returnPathFlags}. Setting ReturnPathState={extInstance.returnPathState}");
#endif
				} else if ((returnPathFlags & PathUnit.FLAG_FAILED) != 0) {
					extInstance.returnPathState = ExtPathState.Failed;
#if DEBUG
					if (debug)
						Log._Debug($"CustomHumanAI.CustomSimulationStep: Return path {extInstance.returnPathId} FAILED. Flags={returnPathFlags}. Setting ReturnPathState={extInstance.returnPathState}");
#endif
				}
			}
		}

		public bool CalculateReturnPath(ref ExtCitizenInstance extInstance, Vector3 parkPos, Vector3 targetPos) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == GetCitizenId(extInstance.instanceId);
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			ReleaseReturnPath(ref extInstance);

			PathUnit.Position parkPathPos;
			PathUnit.Position targetPathPos = default(PathUnit.Position);
			bool foundParkPathPos = CustomPathManager.FindCitizenPathPosition(parkPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, NetInfo.LaneType.None, VehicleInfo.VehicleType.None, false, false, out parkPathPos);
			bool foundTargetPathPos = foundParkPathPos && CustomPathManager.FindCitizenPathPosition(targetPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, NetInfo.LaneType.None, VehicleInfo.VehicleType.None, false, false, out targetPathPos);
			if (foundParkPathPos && foundTargetPathPos) {

				PathUnit.Position dummyPathPos = default(PathUnit.Position);
				uint pathId;
				PathCreationArgs args;
				args.extPathType = ExtPathType.WalkingOnly;
				args.extVehicleType = ExtVehicleType.None;
				args.vehicleId = 0;
				args.spawned = true;
				args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
				args.startPosA = parkPathPos;
				args.startPosB = dummyPathPos;
				args.endPosA = targetPathPos;
				args.endPosB = dummyPathPos;
				args.vehiclePosition = dummyPathPos;
				args.laneTypes = NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport;
				args.vehicleTypes = VehicleInfo.VehicleType.None;
				args.maxLength = 20000f;
				args.isHeavyVehicle = false;
				args.hasCombustionEngine = false;
				args.ignoreBlocked = false;
				args.ignoreFlooded = false;
				args.ignoreCosts = false;
				args.randomParking = false;
				args.stablePath = false;
				args.skipQueue = false;

				if (CustomPathManager._instance.CustomCreatePath(out pathId, ref Singleton<SimulationManager>.instance.m_randomizer, args)) {
#if DEBUG
					if (debug)
						Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Path-finding starts for return path of citizen instance {extInstance.instanceId}, path={pathId}, parkPathPos.segment={parkPathPos.m_segment}, parkPathPos.lane={parkPathPos.m_lane}, targetPathPos.segment={targetPathPos.m_segment}, targetPathPos.lane={targetPathPos.m_lane}");
#endif

					extInstance.returnPathId = pathId;
					extInstance.returnPathState = ExtPathState.Calculating;
					return true;
				}
			}

#if DEBUG
			if (debug)
				Log._Debug($"ExtCitizenInstance.CalculateReturnPath: Could not find path position(s) for either the parking position or target position of citizen instance {extInstance.instanceId}.");
#endif

			return false;
		}

		public void Reset(ref ExtCitizenInstance extInstance) {
			//Flags = ExtFlags.None;
			extInstance.pathMode = ExtPathMode.None;
			extInstance.failedParkingAttempts = 0;
			extInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
			extInstance.parkingSpaceLocationId = 0;
			extInstance.lastDistanceToParkedCar = float.MaxValue;
			extInstance.atOutsideConnection = false;
			//extInstance.ParkedVehiclePosition = default(Vector3);
			ReleaseReturnPath(ref extInstance);
		}

		public bool LoadData(List<Configuration.ExtCitizenInstanceData> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} extended citizen instances");

			foreach (Configuration.ExtCitizenInstanceData item in data) {
				try {
					uint instanceId = item.instanceId;
					ExtInstances[instanceId].pathMode = (ExtPathMode)item.pathMode;
					ExtInstances[instanceId].failedParkingAttempts = item.failedParkingAttempts;
					ExtInstances[instanceId].parkingSpaceLocationId = item.parkingSpaceLocationId;
					ExtInstances[instanceId].parkingSpaceLocation = (ExtParkingSpaceLocation)item.parkingSpaceLocation;
					if (item.parkingPathStartPositionSegment != 0) {
						PathUnit.Position pos = new PathUnit.Position();
						pos.m_segment = item.parkingPathStartPositionSegment;
						pos.m_lane = item.parkingPathStartPositionLane;
						pos.m_offset = item.parkingPathStartPositionOffset;
						ExtInstances[instanceId].parkingPathStartPosition = pos;
					} else {
						ExtInstances[instanceId].parkingPathStartPosition = null;
					}
					ExtInstances[instanceId].returnPathId = item.returnPathId;
					ExtInstances[instanceId].returnPathState = (ExtPathState)item.returnPathState;
					ExtInstances[instanceId].lastDistanceToParkedCar = item.lastDistanceToParkedCar;
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Warning("Error loading ext. citizen instance: " + e.ToString());
					success = false;
				}
			}

			return success;
		}

		public List<Configuration.ExtCitizenInstanceData> SaveData(ref bool success) {
			List<Configuration.ExtCitizenInstanceData> ret = new List<Configuration.ExtCitizenInstanceData>();
			for (uint instanceId = 0; instanceId < CitizenManager.MAX_INSTANCE_COUNT; ++instanceId) {
				try {
					if ((Singleton<CitizenManager>.instance.m_instances.m_buffer[instanceId].m_flags & CitizenInstance.Flags.Created) == CitizenInstance.Flags.None) {
						continue;
					}

					if (ExtInstances[instanceId].pathMode == ExtPathMode.None && ExtInstances[instanceId].returnPathId == 0) {
						continue;
					}

					Configuration.ExtCitizenInstanceData item = new Configuration.ExtCitizenInstanceData(instanceId);
					item.pathMode = (int)ExtInstances[instanceId].pathMode;
					item.failedParkingAttempts = ExtInstances[instanceId].failedParkingAttempts;
					item.parkingSpaceLocationId = ExtInstances[instanceId].parkingSpaceLocationId;
					item.parkingSpaceLocation = (int)ExtInstances[instanceId].parkingSpaceLocation;
					if (ExtInstances[instanceId].parkingPathStartPosition != null) {
						PathUnit.Position pos = (PathUnit.Position)ExtInstances[instanceId].parkingPathStartPosition;
						item.parkingPathStartPositionSegment = pos.m_segment;
						item.parkingPathStartPositionLane = pos.m_lane;
						item.parkingPathStartPositionOffset = pos.m_offset;
					} else {
						item.parkingPathStartPositionSegment = 0;
						item.parkingPathStartPositionLane = 0;
						item.parkingPathStartPositionOffset = 0;
					}
					item.returnPathId = ExtInstances[instanceId].returnPathId;
					item.returnPathState = (int)ExtInstances[instanceId].returnPathState;
					item.lastDistanceToParkedCar = ExtInstances[instanceId].lastDistanceToParkedCar;
					ret.Add(item);
				} catch (Exception ex) {
					Log.Error($"Exception occurred while saving ext. citizen instances @ {instanceId}: {ex.ToString()}");
					success = false;
				}
			}
			return ret;
		}

		public bool IsAtOutsideConnection(ushort instanceId, ref CitizenInstance instanceData, ref ExtCitizenInstance extInstance, Vector3 startPos) {
#if DEBUG
			bool citDebug =
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == GetCitizenId(instanceId)) &&
				(GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceId) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == Singleton<CitizenManager>.instance.m_instances.m_buffer[extInstance.instanceId].m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == Singleton<CitizenManager>.instance.m_instances.m_buffer[extInstance.instanceId].m_targetBuilding)
			;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({extInstance.instanceId}): called. Path: {instanceData.m_path} sourceBuilding={instanceData.m_sourceBuilding}");
#endif

			bool ret =
				(Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None &&
				(startPos - Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].m_position).magnitude <= GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance;

#if DEBUG
			if (debug)
				Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): ret={ret}");
#endif
			return ret;
		}
	}
}

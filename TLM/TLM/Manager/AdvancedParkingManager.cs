using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.ExtCitizenInstance;

namespace TrafficManager.Manager {
	public class AdvancedParkingManager : AbstractCustomManager {
		public enum CarUsagePolicy {
			/// <summary>
			/// Citizens may use their own car
			/// </summary>
			Allowed,
			/// <summary>
			/// Citizens are forced to use their car
			/// </summary>
			Forced,
			/// <summary>
			/// Citizens are forbidden to use their car
			/// </summary>
			Forbidden
		}

		public static AdvancedParkingManager Instance { get; private set; } = null;

		static AdvancedParkingManager() {
			Instance = new AdvancedParkingManager();
		}

		/// <summary>
		/// Determines the color the given building should be colorized with given the current info view mode.
		/// </summary>
		/// <param name="buildingId">building id</param>
		/// <param name="buildingData">building data</param>
		/// <param name="infoMode">current info view mode</param>
		/// <param name="color">output color</param>
		/// <returns>true if a custom color should be displayed, false otherwise</returns>
		public bool GetBuildingInfoViewColor(ushort buildingId, ref Building buildingData, InfoManager.InfoMode infoMode, out Color? color) {
			color = null;

			if (infoMode == InfoManager.InfoMode.Traffic) {
				// parking space demand info view
				ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingId);
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)extBuilding.ParkingSpaceDemand * 0.01f));
				return true;
			} else if (infoMode == InfoManager.InfoMode.Transport && !(buildingData.Info.m_buildingAI is DepotAI)) {
				// public transport demand info view
				// TODO should not depend on UI class "TrafficManagerTool"
				ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingId);
				color = Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor, Mathf.Clamp01((float)(TrafficManagerTool.CurrentTransportDemandViewMode == TransportDemandViewMode.Outgoing ? extBuilding.OutgoingPublicTransportDemand : extBuilding.IncomingPublicTransportDemand) * 0.01f));
				return true;
			}

			return false;
		}

		/// <summary>
		/// Updates the vehicle's main path state by checking against the return path state
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="mainPathState">current state of the vehicle's main path</param>
		/// <returns></returns>
		public ExtPathState UpdatePathState(ushort vehicleId, ref Vehicle vehicleData, ExtPathState mainPathState) {
			if (mainPathState == ExtPathState.Calculating) {
				// main path is still calculating, do not check return path
				return mainPathState;
			}

			VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleData.GetFirstVehicle(vehicleId));
			ExtCitizenInstance driverExtInstance = state.GetDriverExtInstance();

			if (driverExtInstance == null) {
				// no driver
				return mainPathState;
			}

			if (state.VehicleType != ExtVehicleType.PassengerCar) {
				// non-passenger cars are not handled
				driverExtInstance.Reset();
				return mainPathState;
			}

			if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
				// main path failed or non-existing: reset return path
				driverExtInstance.Reset();
				return ExtPathState.Failed;
			}
			
			// main path calculation succeeded: update return path state and check its state
			driverExtInstance.UpdateReturnPathState();

			switch (driverExtInstance.ReturnPathState) {
				case ExtPathState.None:
				default:
					// no return path calculated: ignore
					return mainPathState;
				case ExtPathState.Calculating:
					// return path not read yet: wait for it
					return ExtPathState.Calculating;
				case ExtPathState.Failed:
					// no walking path from parking position to target found. flag main path as 'failed'.
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomCarAI.CustomSimulationStep: Return path {driverExtInstance.ReturnPathId} FAILED. Forcing path-finding to fail.");
#endif
					driverExtInstance.Reset();
					return ExtPathState.Failed;
				case ExtPathState.Ready:
					// handle valid return path
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"CustomPassengerCarAI.OnPathFindSuccess: Path is ready for vehicle {vehicleId}, citizen instance {driverExtInstance.InstanceId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					byte laneTypes = CustomPathManager._instance.m_pathUnits.m_buffer[vehicleData.m_path].m_laneTypes;
					bool usesPublicTransport = (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;

					if (usesPublicTransport && (driverExtInstance.PathMode == ExtPathMode.CalculatingCarPathToKnownParkPos || driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToAltParkPos)) {
						driverExtInstance.PathMode = ExtPathMode.CalculatingCarPathToTarget;
						driverExtInstance.ParkingSpaceLocation = ExtParkingSpaceLocation.None;
						driverExtInstance.ParkingSpaceLocationId = 0;
					}

					if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToAltParkPos) {
						driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToAltParkPos;
						driverExtInstance.ParkingPathStartPosition = null;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Path to an alternative parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					} else if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToTarget) {
						driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToTarget;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Car path is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					} else if (driverExtInstance.PathMode == ExtCitizenInstance.ExtPathMode.CalculatingCarPathToKnownParkPos) {
						driverExtInstance.PathMode = ExtCitizenInstance.ExtPathMode.DrivingToKnownParkPos;
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Car path to known parking position is READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.PathMode}");
#endif
					}
					return ExtPathState.Ready;
			}
		}

		/// <summary>
		/// Finds a free parking space in the vicinity of the given target position <paramref name="endPos"/> for the given citizen instance <paramref name="extDriverInstance"/>.
		/// </summary>
		/// <param name="endPos">target position</param>
		/// <param name="vehicleInfo">vehicle type that is being used</param>
		/// <param name="extDriverInstance">cititzen instance that is driving the car</param>
		/// <param name="homeId">Home building of the citizen (may be 0 for tourists/homeless cims)</param>
		/// <param name="vehicleId">Vehicle that is being used (used for logging)</param>
		/// <param name="allowTourists">If true, method fails if given citizen is a tourist (TODO remove this parameter)</param>
		/// <param name="parkPos">parking position (output)</param>
		/// <param name="endPathPos">sidewalk path position near parking space (output). only valid if <paramref name="calculateEndPos"/> yields false.</param>
		/// <param name="calculateEndPos">if false, a parking space path position could be calculated (TODO negate & rename parameter)</param>
		/// <returns>true if a parking space could be found, false otherwise</returns>
		public bool FindParkingSpaceForCitizen(Vector3 endPos, VehicleInfo vehicleInfo, ExtCitizenInstance extDriverInstance, ushort homeId, ushort vehicleId, bool allowTourists, out Vector3 parkPos, ref PathUnit.Position endPathPos, out bool calculateEndPos) {
			calculateEndPos = true;
			parkPos = default(Vector3);

			if (!allowTourists) {
				// TODO remove this from this method
				uint citizenId = extDriverInstance.GetCitizenId();
				if (citizenId == 0 ||
					(Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Tourist) != Citizen.Flags.None)
					return false;
			}

#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"Citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) can still use their passenger car and is either not a tourist or wants to find an alternative parking spot. Finding a parking space before starting path-finding.");
#endif

			ExtParkingSpaceLocation knownParkingSpaceLocation;
			ushort knownParkingSpaceLocationId;
			Quaternion parkRot;
			float parkOffset;

			// find a free parking space
			bool success = FindParkingSpaceInVicinity(endPos, vehicleInfo, homeId, vehicleId, out knownParkingSpaceLocation, out knownParkingSpaceLocationId, out parkPos, out parkRot, out parkOffset);

			extDriverInstance.ParkingSpaceLocation = knownParkingSpaceLocation;
			extDriverInstance.ParkingSpaceLocationId = knownParkingSpaceLocationId;

			if (success) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Found a parking spot for citizen instance {extDriverInstance.InstanceId} (CurrentPathMode={extDriverInstance.PathMode}) before starting car path: {knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
#endif

				if (knownParkingSpaceLocation == ExtParkingSpaceLocation.RoadSide) {
					// found segment with parking space
					Vector3 pedPos;
					uint laneId;
					int laneIndex;
					float laneOffset;

#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
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
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Found an parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId} @ segment {knownParkingSpaceLocationId}, laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return true;
					} else {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Could not find an alternative parking spot sidewalk position for citizen instance {extDriverInstance.InstanceId}! CurrentPathMode={extDriverInstance.PathMode}");
#endif
						return false;
					}
				} else if (knownParkingSpaceLocation == ExtParkingSpaceLocation.Building) {
					// found a building with parking space
					if (CustomPathManager.FindPathPositionWithSpiralLoop(parkPos, endPos, ItemClass.Service.Road, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, false, false, GlobalConfig.Instance.MaxBuildingToPedestrianLaneDistance, out endPathPos)) {
						calculateEndPos = false;
					}

					//endPos = parkPos;

					//extDriverInstance.CurrentPathMode = successMode;// ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Navigating citizen instance {extDriverInstance.InstanceId} to parking building {knownParkingSpaceLocationId}! segment={endPathPos.m_segment}, laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. CurrentPathMode={extDriverInstance.PathMode} calculateEndPos={calculateEndPos}");
#endif
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		internal bool TrySpawnParkedPassengerCar(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car for citizen {citizenId}, home {homeId} @ {refPos}");
#endif
			if (TrySpawnParkedPassengerCarRoadSide(citizenId, refPos, vehicleInfo, out parkPos))
				return true;
			return TrySpawnParkedPassengerCarBuilding(citizenId, homeId, refPos, vehicleInfo, out parkPos);
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> at a road segment in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		public bool TrySpawnParkedPassengerCarRoadSide(uint citizenId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset = 0f;

			if (FindParkingSpaceRoadSide(0, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"[SUCCESS] Spawned parked passenger car at road side for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2])
				Log._Debug($"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
#endif
			return false;
		}

		/// <summary>
		/// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/> at a building in the vicinity of the given position <paramref name="refPos"/>
		/// </summary>
		/// <param name="citizenId">Citizen that requires a parked car</param>
		/// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
		/// <param name="refPos">Reference position</param>
		/// <param name="vehicleInfo">Vehicle type to spawn</param>
		/// <param name="parkPos">Parked vehicle position (output)</param>
		/// <returns>true if a passenger car could be spawned, false otherwise</returns>
		public bool TrySpawnParkedPassengerCarBuilding(uint citizenId, ushort homeId, Vector3 refPos, VehicleInfo vehicleInfo, out Vector3 parkPos) {
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
				Log._Debug($"Trying to spawn parked passenger car next to building for citizen {citizenId} @ {refPos}");
#endif
			parkPos = Vector3.zero;
			Quaternion parkRot = Quaternion.identity;
			float parkOffset;

			if (FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, refPos, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, out parkPos, out parkRot, out parkOffset)) {
				// position found, spawn a parked vehicle
				ushort parkedVehicleId;
				if (Singleton<VehicleManager>.instance.CreateParkedVehicle(out parkedVehicleId, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, parkPos, parkRot, citizenId)) {
					Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
					Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4] && homeId != 0)
						Log._Debug($"[SUCCESS] Spawned parked passenger car next to building for citizen {citizenId}: {parkedVehicleId}");
#endif
					return true;
				}
			}
#if DEBUG
			if (GlobalConfig.Instance.DebugSwitches[2] && homeId != 0)
				Log._Debug($"[FAIL] Failed to spawn parked passenger car next to building for citizen {citizenId}");
#endif
			return false;
		}

		public bool FindParkingSpaceInVicinity(Vector3 targetPos, VehicleInfo vehicleInfo, ushort homeId, ushort vehicleId, out ExtParkingSpaceLocation parkingSpaceLocation, out ushort parkingSpaceLocationId, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			Vector3 roadParkPos;
			Quaternion roadParkRot;
			float roadParkOffset;
			Vector3 buildingParkPos;
			Quaternion buildingParkRot;
			float buildingParkOffset;

			ushort parkingSpaceSegmentId = FindParkingSpaceAtRoadSide(0, targetPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, true, out roadParkPos, out roadParkRot, out roadParkOffset);
			ushort parkingBuildingId = FindParkingSpaceBuilding(vehicleInfo, homeId, 0, 0, targetPos, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, GlobalConfig.Instance.VicinityParkingSpaceSearchRadius, true, out buildingParkPos, out buildingParkRot, out buildingParkOffset);

			if (parkingSpaceSegmentId != 0) {
				if (parkingBuildingId != 0) {
					// choose nearest parking position
					if ((roadParkPos - targetPos).magnitude < (buildingParkPos - targetPos).magnitude) {
						// road parking space is closer
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Found an (alternative) road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId} after comparing distance with a bulding parking position @ {parkingBuildingId}!");
#endif
						parkPos = roadParkPos;
						parkRot = roadParkRot;
						parkOffset = roadParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
						parkingSpaceLocationId = parkingSpaceSegmentId;
						return true;
					} else {
						// building parking space is closer
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId} after comparing distance with a road-side parking position @ {parkingSpaceSegmentId}!");
#endif
						parkPos = buildingParkPos;
						parkRot = buildingParkRot;
						parkOffset = buildingParkOffset;
						parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.Building;
						parkingSpaceLocationId = parkingBuildingId;
						return true;
					}
				} else {
					// road-side but no building parking space found
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"Found an alternative road-side parking position for vehicle {vehicleId} @ segment {parkingSpaceSegmentId}!");
#endif
					parkPos = roadParkPos;
					parkRot = roadParkRot;
					parkOffset = roadParkOffset;
					parkingSpaceLocation = ExtCitizenInstance.ExtParkingSpaceLocation.RoadSide;
					parkingSpaceLocationId = parkingSpaceSegmentId;
					return true;
				}
			} else if (parkingBuildingId != 0) {
				// building but no road-side parking space found
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Found an alternative building parking position for vehicle {vehicleId} at building {parkingBuildingId}!");
#endif
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
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Could not find a road-side or building parking position for vehicle {vehicleId}!");
#endif
				return false;
			}
		}

		internal ushort FindParkingSpaceAtRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = 0f;

			int centerI = (int)(refPos.z / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int centerJ = (int)(refPos.x / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int radius = Math.Max(1, (int)(maxDistance / ((float)BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

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
						if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
							if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, innerParkPos, width, length, out innerParkPos, out innerParkRot, out innerParkOffset)) {
#if DEBUG
								if (GlobalConfig.Instance.DebugSwitches[4])
									Log._Debug($"FindParkingSpaceRoadSide: Found a parking space for refPos {refPos} @ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");
#endif
								foundSegmentId = segmentId;
								myParkPos = innerParkPos;
								myParkRot = innerParkRot;
								myParkOffset = innerParkOffset;
								if (!randomize || rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) != 0)
									return false;
							}
						}
					} else {
						/*if (GlobalConfig.Instance.DebugSwitches[2])
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
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
#endif
				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundSegmentId;
		}

		public ushort FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			parkPos = Vector3.zero;
			parkRot = Quaternion.identity;
			parkOffset = -1f;

			int centerI = (int)(refPos.z / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int centerJ = (int)(refPos.x / (float)BuildingManager.BUILDINGGRID_CELL_SIZE + (float)BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
			int radius = Math.Max(1, (int)(maxBuildingDistance / ((float)BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

			BuildingManager buildingMan = Singleton<BuildingManager>.instance;
			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

			ushort foundBuildingId = 0;
			Vector3 myParkPos = parkPos;
			Quaternion myParkRot = parkRot;
			float myParkOffset = parkOffset;

			LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, delegate (int i, int j) {
				if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 || j >= BuildingManager.BUILDINGGRID_RESOLUTION)
					return true;

#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4]) {
					//Log._Debug($"FindParkingSpaceBuilding: Checking building grid @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j} for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}");
				}
#endif

				ushort buildingId = buildingMan.m_buildingGrid[i * BuildingManager.BUILDINGGRID_RESOLUTION + j];
				int numIterations = 0;
				while (buildingId != 0) {
					Vector3 innerParkPos; Quaternion innerParkRot; float innerParkOffset;
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4]) {
						//Log._Debug($"FindParkingSpaceBuilding: Checking building {buildingId} @ i={i}, j={j}, index={i * BuildingManager.BUILDINGGRID_RESOLUTION + j}, for {refPos}, homeID {homeID}, segment {segmentId}, maxDistance {maxDistance}.");
					}
#endif

					if (FindParkingSpacePropAtBuilding(vehicleInfo, homeID, ignoreParked, buildingId, ref buildingMan.m_buildings.m_buffer[(int)buildingId], segmentId, refPos, ref maxParkingSpaceDistance, randomize, out innerParkPos, out innerParkRot, out innerParkOffset)) {
#if DEBUG
						/*/if (GlobalConfig.Instance.DebugSwitches[4] && homeID != 0)
							Log._Debug($"FindParkingSpaceBuilding: Found a parking space for {refPos}, homeID {homeID} @ building {buildingId}, {myParkPos}, offset {myParkOffset}!");
						*/
#endif
						foundBuildingId = buildingId;
						myParkPos = innerParkPos;
						myParkRot = innerParkRot;
						myParkOffset = innerParkOffset;

						if (!randomize || rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) != 0)
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
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[2] && homeID != 0)
					Log._Debug($"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");
#endif

				return 0;
			}

			parkPos = myParkPos;
			parkRot = myParkRot;
			parkOffset = myParkOffset;

			return foundBuildingId;
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
		public bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort buildingID, ref Building building, ushort segmentId, Vector3 refPos, ref float maxDistance, bool randomize, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			int buildingWidth = building.Width;
			int buildingLength = building.Length;

			// NON-STOCK CODE START
			parkOffset = -1f; // only set if segmentId != 0
			parkPos = default(Vector3);
			parkRot = default(Quaternion);

			if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not created.");
#endif
				return false;
			}

			if ((building.m_problems & Notification.Problem.TurnedOff) != Notification.Problem.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is not active.");
#endif
				return false;
			}

			if ((building.m_flags & Building.Flags.Collapsed) != Building.Flags.None) {
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is collapsed.");
#endif
				return false;
			}

			/*else {
			// NON-STOCK CODE END
				float diagWidth = Mathf.Sqrt((float)(buildingWidth * buildingWidth + buildingLength * buildingLength)) * 8f;
				if (VectorUtils.LengthXZ(building.m_position - refPos) >= maxDistance + diagWidth) {*/
#if DEBUG
			/*if (GlobalConfig.Instance.DebugSwitches[4])
				Log._Debug($"Refusing to find parking space at building {buildingID}! {VectorUtils.LengthXZ(building.m_position - refPos)} >= {maxDistance + diagWidth} (maxDistance={maxDistance})");*/
#endif
			/*return false;
		}
	}*/ // NON-STOCK CODE

			Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

			BuildingInfo buildingInfo = building.Info;
			Matrix4x4 transformMatrix = default(Matrix4x4);
			bool transformMatrixCalculated = false;
			bool result = false;
			if (buildingInfo.m_class.m_service == ItemClass.Service.Residential && buildingID != homeID && rng.Int32((uint)Options.getRecklessDriverModulo()) != 0) { // NON-STOCK CODE
#if DEBUG
				/*if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Refusing to find parking space at building {buildingID}! Building is a residential building which does not match home id {homeID}.");*/
#endif
				return false;
			}

			float propMinDistance = 9999f; // NON-STOCK CODE

			if (buildingInfo.m_props != null && (buildingInfo.m_hasParkingSpaces & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
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
								if (CustomPassengerCarAI.FindParkingSpaceProp(ignoreParked, propInfo, position, building.m_angle + prop.m_radAngle, prop.m_fixedHeight, refPos, vehicleInfo.m_generatedInfo.m_size.x, vehicleInfo.m_generatedInfo.m_size.z, ref propMinDistance, ref parkPos, ref parkRot)) { // NON-STOCK CODE
									result = true;
									if (randomize && propMinDistance <= maxDistance && rng.Int32(GlobalConfig.Instance.VicinityParkingSpaceSelectionRand) == 0)
										break;
								}
							}
						}
					}
				}
			}

			if (result && propMinDistance <= maxDistance) {
				maxDistance = propMinDistance; // NON-STOCK CODE
#if DEBUG
				if (GlobalConfig.Instance.DebugSwitches[4])
					Log._Debug($"Found parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				if (segmentId != 0) {
					// check if building is accessible from the given segment
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[4])
						Log._Debug($"Calculating unspawn position of building {buildingID} for segment {segmentId}.");
#endif

					Vector3 unspawnPos;
					Vector3 unspawnTargetPos;
					building.Info.m_buildingAI.CalculateUnspawnPosition(buildingID, ref building, ref Singleton<SimulationManager>.instance.m_randomizer, vehicleInfo, out unspawnPos, out unspawnTargetPos);

					Vector3 lanePos;
					uint laneId;
					int laneIndex;
					float laneOffset;
					// calculate segment offset
					if (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].GetClosestLanePosition(unspawnPos, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, out lanePos, out laneId, out laneIndex, out laneOffset)) {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[4])
							Log._Debug($"Succeeded in finding unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}! lanePos={lanePos}, dist={(lanePos - unspawnPos).magnitude}, laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");
#endif

						/*if (dist > 16f) {
							if (GlobalConfig.Instance.DebugSwitches[2])
								Log._Debug($"Distance between unspawn position and lane position is too big! {dist} unspawnPos={unspawnPos} lanePos={lanePos}");
							return false;
						}*/

						parkOffset = laneOffset;
					} else {
#if DEBUG
						if (GlobalConfig.Instance.DebugSwitches[2])
							Log.Warning($"Could not find unspawn position lane offset for building {buildingID}, segment {segmentId}, unspawnPos={unspawnPos}!");
#endif
					}
				}

				return true;
			} else {
#if DEBUG
				if (result && GlobalConfig.Instance.DebugSwitches[2])
					Log._Debug($"Could not find parking space prop in range ({maxDistance}) at building {buildingID}.");
#endif
				return false;
			}
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
		public bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo, ushort ignoreParked, ushort segmentId, Vector3 refPos, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset, out uint laneId, out int laneIndex) {
			float width = vehicleInfo.m_generatedInfo.m_size.x;
			float length = vehicleInfo.m_generatedInfo.m_size.z;

			NetManager netManager = Singleton<NetManager>.instance;
			if ((netManager.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None) {
				if (netManager.m_segments.m_buffer[segmentId].GetClosestLanePosition(refPos, NetInfo.LaneType.Parking, VehicleInfo.VehicleType.Car, out parkPos, out laneId, out laneIndex, out parkOffset)) {
					if (!Options.parkingRestrictionsEnabled || ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex].m_finalDirection)) {
						if (CustomPassengerCarAI.FindParkingSpaceRoadSide(ignoreParked, segmentId, parkPos, width, length, out parkPos, out parkRot, out parkOffset)) {
#if DEBUG
							if (GlobalConfig.Instance.DebugSwitches[4])
								Log._Debug($"FindParkingSpaceRoadSideForVehiclePos: Found a parking space for refPos {refPos} @ {parkPos}, laneId {laneId}, laneIndex {laneIndex}!");
#endif
							return true;
						}
					}
				}
			}

			//

			parkPos = default(Vector3);
			parkRot = default(Quaternion);
			laneId = 0;
			laneIndex = -1;
			parkOffset = -1f;
			return false;
		}

		public bool FindParkingSpaceRoadSide(ushort ignoreParked, Vector3 refPos, float width, float length, float maxDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return AdvancedParkingManager.Instance.FindParkingSpaceAtRoadSide(ignoreParked, refPos, width, length, maxDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo, ushort homeID, ushort ignoreParked, ushort segmentId, Vector3 refPos, float maxBuildingDistance, float maxParkingSpaceDistance, out Vector3 parkPos, out Quaternion parkRot, out float parkOffset) {
			return AdvancedParkingManager.Instance.FindParkingSpaceBuilding(vehicleInfo, homeID, ignoreParked, segmentId, refPos, maxBuildingDistance, maxParkingSpaceDistance, false, out parkPos, out parkRot, out parkOffset) != 0;
		}

		public string EnrichLocalizedStatus(string ret, ExtCitizenInstance driverExtInstance) {
			if (driverExtInstance != null) {
				switch (driverExtInstance.PathMode) {
					case ExtPathMode.DrivingToAltParkPos:
						ret = Translation.GetString("Driving_to_another_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.CalculatingCarPathToKnownParkPos:
					case ExtPathMode.DrivingToKnownParkPos:
						ret = Translation.GetString("Driving_to_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.ParkingFailed:
					case ExtPathMode.CalculatingCarPathToAltParkPos:
						ret = Translation.GetString("Looking_for_a_parking_spot") + ", " + ret;
						break;
					case ExtPathMode.ParkingSucceeded:
						ret = Locale.Get("VEHICLE_STATUS_PARKING") + ", " + ret;
						break;
				}
			}
			return ret;
		}
	}
}

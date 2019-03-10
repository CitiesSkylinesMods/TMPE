using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Util;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Manager.Impl {
	public class ExtCitizenInstanceManager : AbstractCustomManager, ICustomDataManager<List<Configuration.ExtCitizenInstanceData>>, IExtCitizenInstanceManager {
		public static ExtCitizenInstanceManager Instance = new ExtCitizenInstanceManager();

		/// <summary>
		/// All additional data for citizen instance. Index: citizen instance id
		/// </summary>
		public ExtCitizenInstance[] ExtInstances = null;

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended citizen instance data:");
			for (int i = 0; i < ExtInstances.Length; ++i) {
				if (!ExtInstances[i].IsValid()) {
					continue;
				}
				Log._Debug($"Citizen instance {i}: {ExtInstances[i]}");
			}
		}

		internal void OnReleaseInstance(ushort instanceId) {
			ExtInstances[instanceId].Reset();
		}

		public void ResetInstance(ushort instanceId) {
			ExtInstances[instanceId].Reset();
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
				ExtInstances[i].Reset();
			}
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

		public bool IsAtOutsideConnection(ushort instanceId, ref CitizenInstance instanceData, ref Citizen citizenData) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == instanceData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;

			if (debug)
				Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): called for citizen instance {instanceId}. Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
#endif

			Citizen.Location location = citizenData.CurrentLocation;
			switch (location) {
				case Citizen.Location.Home:
				case Citizen.Location.Visit:
				case Citizen.Location.Work:
#if DEBUG
					if (fineDebug) {
						Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): Citizen is currently at location {location}. This is not an outside connection.");
					}
#endif
					return false;
			}

			bool spawned = (instanceData.m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None;
			if (!spawned && (citizenData.m_vehicle == 0 || (Singleton<VehicleManager>.instance.m_vehicles.m_buffer[citizenData.m_vehicle].m_flags & Vehicle.Flags.Spawned) == 0)) {
#if DEBUG
				if (fineDebug) {
					Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): Citizen instance is not spawned ({instanceData.m_flags}) and does not have a spawned car. Not at an outside connection.");
				}
#endif
				return false;
			}

			if (instanceData.m_sourceBuilding == 0) {
#if DEBUG
				if (fineDebug) {
					Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): Citizen instance does not have a source building. Not at an outside connection.");
				}
#endif
				return false;
			}
			
			if ((Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) {
#if DEBUG
				if (fineDebug) {
					Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): Source building {instanceData.m_sourceBuilding} is not an outside connection.");
				}
#endif
				return false;
			}

			Vector3 pos;
			if (spawned) {
				pos = instanceData.GetLastFramePosition();
			} else {
				pos = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[citizenData.m_vehicle].GetLastFramePosition();
			}
			
			bool ret = (pos - Singleton<BuildingManager>.instance.m_buildings.m_buffer[instanceData.m_sourceBuilding].m_position).magnitude <= GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance;

#if DEBUG
			if (fineDebug) {
				Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): pos={pos}, spawned={spawned}, ret={ret}");
			}
#endif

			return ret;
		}
	}
}

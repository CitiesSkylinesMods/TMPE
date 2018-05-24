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
	}
}

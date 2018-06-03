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

namespace TrafficManager.Manager.Impl {
	public class ExtBuildingManager : AbstractCustomManager, IExtBuildingManager {
		public static ExtBuildingManager Instance { get; private set; } = null;

		static ExtBuildingManager() {
			Instance = new ExtBuildingManager();
		}
		
		/// <summary>
		/// All additional data for buildings
		/// </summary>
		public ExtBuilding[] ExtBuildings { get; private set; } = null;

		private ExtBuildingManager() {
			ExtBuildings = new ExtBuilding[BuildingManager.MAX_BUILDING_COUNT];
			for (uint i = 0; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
				ExtBuildings[i] = new ExtBuilding((ushort)i);
			}
		}

		public void OnBeforeSimulationStep(ushort buildingId, ref Building data) {
			// slowly decrease parking space demand / public transport demand if Parking AI is active
			if (! Options.parkingAI) {
				return;
			}

			uint frameIndex = Constants.ServiceFactory.SimulationService.CurrentFrameIndex >> 8;
			if ((frameIndex & 1u) == 0u) {
				RemoveDemand(ref ExtBuildings[buildingId]);
			}
		}

		protected void RemoveDemand(ref ExtBuilding extBuilding) {
			extBuilding.RemoveParkingSpaceDemand(GlobalConfig.Instance.ParkingAI.ParkingSpaceDemandDecrement);
			extBuilding.RemovePublicTransportDemand(GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement, true);
			extBuilding.RemovePublicTransportDemand(GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement, false);
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended building data:");
			for (int i = 0; i < ExtBuildings.Length; ++i) {
				if (! ExtBuildings[i].IsValid()) {
					continue;
				}
				Log._Debug($"Building {i}: {ExtBuildings[i]}");
			}
		}
		
		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < ExtBuildings.Length; ++i)
				ExtBuildings[i].Reset();
		}
	}
}

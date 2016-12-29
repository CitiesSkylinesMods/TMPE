#define USEPATHWAITCOUNTERx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager {
	public class ExtBuildingManager : AbstractCustomManager {
		public static ExtBuildingManager Instance { get; private set; } = null;

		static ExtBuildingManager() {
			Instance = new ExtBuildingManager();
		}
		
		/// <summary>
		/// All additional data for buildings
		/// </summary>
		private ExtBuilding[] ExtBuildings = null;

		private ExtBuildingManager() {
			ExtBuildings = new ExtBuilding[BuildingManager.MAX_BUILDING_COUNT];
			for (uint i = 0; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
				ExtBuildings[i] = new ExtBuilding((ushort)i);
			}
		}

		/// <summary>
		/// Retrieves the additional building data for the given building id.
		/// </summary>
		/// <param name="instanceId"></param>
		/// <returns>the additional citizen instance data</returns>
		public ExtBuilding GetExtBuilding(ushort buildingId) {
			return ExtBuildings[buildingId];
		}
		
		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < ExtBuildings.Length; ++i)
				ExtBuildings[i].Reset();
		}
	}
}

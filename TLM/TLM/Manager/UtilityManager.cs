using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public class UtilityManager : AbstractCustomManager {
		public static UtilityManager Instance { get; private set; } = null;

		static UtilityManager() {
			Instance = new UtilityManager();
		}

		/// <summary>
		/// Determines if stuck entities should be cleared
		/// </summary>
		private static bool ResetStuckEntitiesRequested = false;

		public void RequestResetStuckEntities() {
			ResetStuckEntitiesRequested = true;
		}

		internal void SimulationStep() {
			try {
				if (ResetStuckEntitiesRequested) {
					ResetStuckEntities();
					ResetStuckEntitiesRequested = false;
				}
			} finally { }
		}
		
		private void ResetStuckEntities() {
			Log._Debug($"UtilityManager.RemoveStuckEntities() called.");

			Log._Debug($"UtilityManager.RemoveStuckEntities(): Pausing simulation.");
			Singleton<SimulationManager>.instance.ForcedSimulationPaused = true;

			Log._Debug($"UtilityManager.RemoveStuckEntities(): Waiting for all paths.");
			Singleton<PathManager>.instance.WaitForAllPaths();

			Log._Debug($"UtilityManager.RemoveStuckEntities(): Resetting citizen instances that are waiting for a path.");
			for (uint citizenInstanceId = 1; citizenInstanceId < CitizenManager.MAX_INSTANCE_COUNT; ++citizenInstanceId) {
				//Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing instance {citizenInstanceId}.");
				CitizenInstance citizenData = Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId];
				if ((citizenData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
					CitizenAI ai = Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId].Info.m_citizenAI;

					if (citizenData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
						Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId].m_path = 0u;
					}
					Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId].m_flags &= ~(CitizenInstance.Flags.WaitingTransport | CitizenInstance.Flags.EnteringVehicle | CitizenInstance.Flags.BoredOfWaiting | CitizenInstance.Flags.WaitingTaxi | CitizenInstance.Flags.WaitingPath);
				}
			}

			Log._Debug($"UtilityManager.RemoveStuckEntities(): Resetting vehicles that are waiting for a path.");
			for (uint vehicleId = 1; vehicleId < VehicleManager.MAX_VEHICLE_COUNT; ++vehicleId) {
				//Log._Debug($"UtilityManager.RemoveStuckEntities(): Processing vehicle {vehicleId}.");
				Vehicle vehicleData = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId];
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0) {
					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_path = 0u;
					}
					Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags &= ~Vehicle.Flags.WaitingPath;
				}
			}

			Log._Debug($"UtilityManager.RemoveStuckEntities(): Unpausing simulation.");
			Singleton<SimulationManager>.instance.ForcedSimulationPaused = false;
		}
	}
}

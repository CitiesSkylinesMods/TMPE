using System;
using ColossalFramework;
using GenericGameBridge.Service;
using UnityEngine;

namespace CitiesGameBridge.Service {
	public class SimulationService : ISimulationService {
		public static readonly ISimulationService Instance = new SimulationService();
		
		private SimulationService() {

		}

		public bool LeftHandDrive {
			get {
				return Singleton<SimulationManager>.instance.m_metaData.m_invertTraffic == SimulationMetaData.MetaBool.True;
			}
		}

		public uint CurrentBuildIndex {
			get {
				return Singleton<SimulationManager>.instance.m_currentBuildIndex;
			}

			set {
				Singleton<SimulationManager>.instance.m_currentBuildIndex = value;
			}
		}

		public uint CurrentFrameIndex {
			get {
				return Singleton<SimulationManager>.instance.m_currentFrameIndex;
			}
		}

		public Vector3 CameraPosition {
			get {
				return Singleton<SimulationManager>.instance.m_simulationView.m_position;
			}
		}

		public void AddAction(Action action) {
			Singleton<SimulationManager>.instance.AddAction(action);
		}

		public void PauseSimulation(bool forced) {
			if (forced) {
				Singleton<SimulationManager>.instance.ForcedSimulationPaused = true;
			} else {
				Singleton<SimulationManager>.instance.SimulationPaused = true;
			}
		}

		public void ResumeSimulation(bool forced) {
			if (forced) {
				Singleton<SimulationManager>.instance.ForcedSimulationPaused = false;
			} else {
				Singleton<SimulationManager>.instance.SimulationPaused = false;
			}
		}
	}
}

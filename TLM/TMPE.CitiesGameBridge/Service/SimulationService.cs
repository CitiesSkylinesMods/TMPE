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
	}
}

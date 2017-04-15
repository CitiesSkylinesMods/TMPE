using ColossalFramework;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		public uint CurrentFrameIndex {
			get {
				return Singleton<SimulationManager>.instance.m_currentFrameIndex;
			}
		}
	}
}

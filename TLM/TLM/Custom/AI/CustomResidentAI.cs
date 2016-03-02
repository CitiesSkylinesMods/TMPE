using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Custom.AI {
	class CustomResidentAI : ResidentAI {
		public bool CustomDoRandomMove() {
			uint vehicleCount = (uint)Singleton<VehicleManager>.instance.m_vehicleCount;
			uint instanceCount = (uint)Singleton<CitizenManager>.instance.m_instanceCount;
			if (vehicleCount * 65536u > instanceCount * 16384u) { // why not "vehicleCount * 4u > instanceCount"?
				return Singleton<SimulationManager>.instance.m_randomizer.UInt32(16384u) > vehicleCount;
			}
			return Singleton<SimulationManager>.instance.m_randomizer.UInt32(65536u) > instanceCount;
		}
	}
}

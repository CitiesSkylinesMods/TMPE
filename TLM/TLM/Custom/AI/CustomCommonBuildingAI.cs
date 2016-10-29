using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager.Custom.AI {
	public class CustomCommonBuildingAI : BuildingAI {
		public void CustomSimulationStep(ushort buildingID, ref Building data) {
			// NON-STOCK CODE START
			uint frameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8;
			if ((frameIndex & 1u) == 0u)
				ExtBuildingManager.Instance().GetExtBuilding(buildingID).RemoveParkingSpaceDemand(1);
			// NON-STOCK CODE END

			base.SimulationStep(buildingID, ref data);
			if ((data.m_flags & Building.Flags.Demolishing) != Building.Flags.None) {
				Singleton<BuildingManager>.instance.ReleaseBuilding(buildingID);
			}
		}

	}
}

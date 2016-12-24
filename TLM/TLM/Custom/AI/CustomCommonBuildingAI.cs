using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.Custom.AI {
	public class CustomCommonBuildingAI : BuildingAI {
		public void CustomSimulationStep(ushort buildingID, ref Building data) {
			// NON-STOCK CODE START
			// slowly decrease parking space demand / public transport demand
			uint frameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8;
			if ((frameIndex & 1u) == 0u) {
				ExtBuildingManager.Instance.GetExtBuilding(buildingID).RemoveParkingSpaceDemand(GlobalConfig.Instance.ParkingSpaceDemandDecrement);
				ExtBuildingManager.Instance.GetExtBuilding(buildingID).RemovePublicTransportDemand(GlobalConfig.Instance.PublicTransportDemandDecrement, true);
                ExtBuildingManager.Instance.GetExtBuilding(buildingID).RemovePublicTransportDemand(GlobalConfig.Instance.PublicTransportDemandDecrement, false);
            }
			// NON-STOCK CODE END

			base.SimulationStep(buildingID, ref data);
			if ((data.m_flags & Building.Flags.Demolishing) != Building.Flags.None) {
				uint rand = (uint)(((int)buildingID << 8) / 49152);
				uint frameIndexRand = Singleton<SimulationManager>.instance.m_currentFrameIndex - rand;
				if ((data.m_flags & Building.Flags.Collapsed) == Building.Flags.None || data.GetFrameData(frameIndexRand - 256u).m_constructState == 0) {
					Singleton<BuildingManager>.instance.ReleaseBuilding(buildingID);
				}
			}
		}

	}
}

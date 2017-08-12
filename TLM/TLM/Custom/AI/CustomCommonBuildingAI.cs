using ColossalFramework;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Custom.AI {
	public class CustomCommonBuildingAI : BuildingAI {
		public void CustomSimulationStep(ushort buildingID, ref Building data) {
			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "ExtSimulationStep")) {
#endif
				// slowly decrease parking space demand / public transport demand
				uint frameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8;
				if ((frameIndex & 1u) == 0u) {
					ExtSimulationStep(buildingID, ref data, ref ExtBuildingManager.Instance.ExtBuildings[buildingID]);
				}
#if BENCHMARK
			}
#endif
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

		internal void ExtSimulationStep(ushort buildingID, ref Building data, ref ExtBuilding extBuilding) {
			extBuilding.RemoveParkingSpaceDemand(GlobalConfig.Instance.ParkingAI.ParkingSpaceDemandDecrement);
			extBuilding.RemovePublicTransportDemand(GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement, true);
			extBuilding.RemovePublicTransportDemand(GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement, false);
		}
	}
}

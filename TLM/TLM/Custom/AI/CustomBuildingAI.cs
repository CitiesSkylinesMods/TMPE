using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	public class CustomBuildingAI : BuildingAI {
		public Color CustomGetColor(ushort buildingID, ref Building data, InfoManager.InfoMode infoMode) {
			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				if (infoMode == InfoManager.InfoMode.Traffic) {
					// parking space demand info view
					ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingID);
					return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_negativeColor, Mathf.Clamp01((float)extBuilding.ParkingSpaceDemand * 0.01f));
				} else if (infoMode == InfoManager.InfoMode.Transport && !(data.Info.m_buildingAI is DepotAI)) {
					// public transport demand info view
					ExtBuilding extBuilding = ExtBuildingManager.Instance.GetExtBuilding(buildingID);
					return Color.Lerp(Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor, Mathf.Clamp01((float)(TrafficManagerTool.CurrentTransportDemandViewMode == TransportDemandViewMode.Outgoing ? extBuilding.OutgoingPublicTransportDemand : extBuilding.IncomingPublicTransportDemand) * 0.01f));
				}
			}
			// NON-STOCK CODE END

			if (infoMode != InfoManager.InfoMode.None) {
				return Singleton<InfoManager>.instance.m_properties.m_neutralColor;
			}
			if (!this.m_info.m_useColorVariations) {
				return this.m_info.m_color0;
			}
			Randomizer randomizer = new Randomizer((int)buildingID);
			switch (randomizer.Int32(4u)) {
				case 0:
					return this.m_info.m_color0;
				case 1:
					return this.m_info.m_color1;
				case 2:
					return this.m_info.m_color2;
				case 3:
					return this.m_info.m_color3;
				default:
					return this.m_info.m_color0;
			}
		}
	}
}

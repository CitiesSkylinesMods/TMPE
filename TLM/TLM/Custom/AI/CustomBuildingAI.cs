namespace TrafficManager.Custom.AI {
    using ColossalFramework.Math;
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State;
    using UnityEngine;

    [TargetType(typeof(BuildingAI))]
    public class CustomBuildingAI : BuildingAI {
        [RedirectMethod]
        [UsedImplicitly]
        public Color CustomGetColor(ushort buildingId,
                                    ref Building data,
                                    InfoManager.InfoMode infoMode) {
            if (infoMode != InfoManager.InfoMode.None) {
                // NON-STOCK CODE START

                // When the Parking AI is enabled and the traffic info view is active,
                // colorizes buildings depending on the number of succeeded/failed parking
                // maneuvers, or if the public transport info view is active, colorizes
                // buildings depending on the current unfulfilled incoming/outgoing demand
                // for public transport.
                if (Options.parkingAI) {
                    if (AdvancedParkingManager.Instance.GetBuildingInfoViewColor(
                        buildingId: buildingId,
                        buildingData: ref data,
                        extBuilding: ref ExtBuildingManager.Instance.ExtBuildings[buildingId],
                        infoMode: infoMode,
                        color: out Color? color)) {
                        return (Color)color;
                    }
                }

                // NON-STOCK CODE END
                return Singleton<InfoManager>.instance.m_properties.m_neutralColor;
            }

            if (!m_info.m_useColorVariations) {
                return m_info.m_color0;
            }

            Randomizer randomizer = new Randomizer(buildingId);
            switch (randomizer.Int32(4u)) {
                case 0:
                    return m_info.m_color0;
                case 1:
                    return m_info.m_color1;
                case 2:
                    return m_info.m_color2;
                case 3:
                    return m_info.m_color3;
                default:
                    return m_info.m_color0;
            }
        }
    }
}

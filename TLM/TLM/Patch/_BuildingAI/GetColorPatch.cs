namespace TrafficManager.Patch._BuildingAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using UnityEngine;

    [UsedImplicitly]
    [HarmonyPatch(typeof(BuildingAI), nameof(BuildingAI.GetColor))]
    public static class GetColorPatch {

        [UsedImplicitly]
        [HarmonyPrefix]
        public static bool Prefix(ref Color __result,
                                  ushort buildingID,
                                  ref Building data,
                                  InfoManager.InfoMode infoMode) {
            // When the Parking AI is enabled and the traffic info view is active,
            // colorizes buildings depending on the number of succeeded/failed parking
            // maneuvers, or if the public transport info view is active, colorizes
            // buildings depending on the current unfulfilled incoming/outgoing demand
            // for public transport.
            if (SavedGameOptions.Instance.parkingAI && infoMode != InfoManager.InfoMode.None) {
                if (AdvancedParkingManager.Instance.GetBuildingInfoViewColor(
                    buildingID,
                    ref data,
                    ref ExtBuildingManager.Instance.ExtBuildings[buildingID],
                    infoMode,
                    out Color? color)) {
                    __result = color ?? InfoManager.instance.m_properties.m_neutralColor;
                    return false;
                }
            }

            return true;
        }
    }
}
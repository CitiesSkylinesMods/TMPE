namespace TrafficManager.Patch._RoadWorldInfoPanel
{
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;

    [HarmonyPatch(typeof(RoadWorldInfoPanel), "OnAdjustRoadButton")]
    [UsedImplicitly]
    public static class OnAdjustRoadButtonPatch
    {
        /// <summary>
        /// Prefixed to prevent call to Hide.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="subMode"></param>
        /// <returns></returns>
        [UsedImplicitly]
        public static bool Prefix()
        {
            Singleton<InfoManager>.instance.SetCurrentMode(InfoManager.InfoMode.TrafficRoutes, InfoManager.SubInfoMode.WindPower);
            return false;
        }
    }
}
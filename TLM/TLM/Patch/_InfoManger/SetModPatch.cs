namespace TrafficManager.Patch._InfoManager
{
    using ColossalFramework;
    using Harmony;
    using JetBrains.Annotations;
    using TrafficManager.Util;
    using TrafficManager.UI;
    using UnityEngine;
    using static InfoManager;

    [HarmonyPatch(typeof(InfoManager), "SetMode")]
    [UsedImplicitly]
    public static class SetModePatch
    {
        [UsedImplicitly]
        public static void Prefix(InfoMode mode, SubInfoMode subMode)
        {
            if (RoadSelectionUtil.IsNetAdjustMode(mode,(int)subMode))
            {
                // UI to be handled by Default tool
                ModUI.instance_.Close();

                DefaultTool.OpenWorldInfoPanel(
                    Singleton<InstanceManager>.instance.GetSelectedInstance(),
                    Input.mousePosition);
            }
            else //if(mode!=InfoMode.None) {
            {
                Singleton<RoadWorldInfoPanel>.instance.Hide();
            }

        }
    }
}
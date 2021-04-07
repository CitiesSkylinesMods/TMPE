namespace TrafficManager.Patch._InfoManager
{
    using ColossalFramework;
    using HarmonyLib;
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
            if (!RoadSelectionPanels.Root)
                return;

            if (RoadSelectionPanels.Root.RoadWorldInfoPanelExt != null) {
                RoadSelectionPanels.Root.RoadWorldInfoPanelExt.isVisible =
                    mode == InfoMode.None ||
                    RoadSelectionUtil.IsNetAdjustMode(mode, (int)subMode);
            }
            if (RoadSelectionUtil.IsNetAdjustMode(mode, (int)subMode))
            {
                // UI to be handled by Default tool
                ModUI.Instance.CloseMainMenu();

                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(() => {
                    DefaultTool.OpenWorldInfoPanel(
                    Singleton<InstanceManager>.instance.GetSelectedInstance(),
                    Input.mousePosition);
                });
            }
            else 
            {
                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(RoadSelectionPanels.RoadWorldInfoPanel.Hide);
            }
        }
    }
}
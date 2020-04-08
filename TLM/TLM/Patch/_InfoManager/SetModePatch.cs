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
            if (RoadSelectionPanels.Root?.RoadWorldInfoPanelExt != null) {
                RoadSelectionPanels.Root.RoadWorldInfoPanelExt.isVisible =
                    mode == InfoMode.None ||
                    RoadSelectionUtil.IsNetAdjustMode(mode, (int)subMode);
            }
            if (RoadSelectionUtil.IsNetAdjustMode(mode,(int)subMode))
            {
                // UI to be handled by Default tool
                ModUI.instance_.Close();

                SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(delegate () {
                    DefaultTool.OpenWorldInfoPanel(
                    Singleton<InstanceManager>.instance.GetSelectedInstance(),
                    Input.mousePosition);
                });
            }
            else 
            {
                // TODO fix [issue #882]: the line bellow functionaly works but puts a ton of null reference exceptions in the output.
                //SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(Singleton<RoadWorldInfoPanel>.instance.Hide);
            }

        }
    }
}
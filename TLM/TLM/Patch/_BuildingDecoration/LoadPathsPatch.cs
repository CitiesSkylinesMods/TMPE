namespace TrafficManager.Patches._BuildingDecoration {
    using System.Collections.Generic;
    using System.Linq;
    using HarmonyLib;
    using CSUtil.Commons;
    using TrafficManager.State.Asset;
    using TrafficManager.Util;

    //public static void LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
    [HarmonyPatch(typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths))]
    public class LoadPathsPatch {
    
        public static void Prefix(BuildingInfo info) {
            if (!Shortcuts.InSimulationThread())
                return;
            Log._Debug($"LoadPathsPatch.Prefix({info?.ToString() ?? "null"})");
            if (info == null)
                return;
            PlaceIntersectionUtil.StartMapping(info);
        }

        public static void Postfix(BuildingInfo info) {
            if (!PlaceIntersectionUtil.Mapping)
                return; // only visualising.
            PlaceIntersectionUtil.ApplyTrafficRules(info);
        }
    }
}

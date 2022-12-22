namespace TrafficManager.Patch._RoadBaseAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(RoadBaseAI), "UpdateNode")]
    [UsedImplicitly]
    public class UpdateNodePatch {
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(ushort nodeID, ref NetNode data) {

            if (SavedGameOptions.Instance.automaticallyAddTrafficLightsIfApplicable)
            {
                return;
            }

            // test for not Flags.Junction is unnecessary because tested in IsTrafficLightToggleable
            // but it's a simple and fast exit, while IsTrafficLightToggleable is quite complex
            if(!data.m_flags.IsFlagSet(NetNode.Flags.Junction) || !TrafficLightManager.Instance.CanToggleTrafficLight(nodeID, false, ref data, out _))
            {
                return;
            }

            // UpdateNode is called for new nodes and changed nodes (-> existing AND new junctions)
            // For new junctions this method is called BEFORE traffic lights are set (done in UpdateNodeFlags)
            // Marking junction with CustomTrafficLights prevents the setting of traffic lights
            // For existing junctions this has the additional benefit of not removing the lights when the junction changed

            data.m_flags |= NetNode.Flags.CustomTrafficLights;
        }
    }
}
namespace TrafficManager.Patch._RoadBaseAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;

    [HarmonyPatch(typeof(RoadBaseAI), nameof(RoadBaseAI.TrafficLightSimulationStep))]
    public class TrafficLightSimulationStepPatch {
        /// <summary>
        /// Decides whether the stock simulation step for traffic lights should run.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(ushort nodeID, ref NetNode data) {
            bool stockTrafficLights = !SavedGameOptions.Instance.timedLightsEnabled
                     || !TrafficLightSimulationManager
                         .Instance
                         .TrafficLightSimulations[nodeID]
                         .IsSimulationRunning();
            if (!stockTrafficLights && (data.flags & NetNode.FlagsLong.PedestrianBollards) != 0)
            {
                PedestrianZoneRoadAI.BollardsSimulationStep(nodeID, ref data, null);
            }
            return stockTrafficLights;
        }
    }
}
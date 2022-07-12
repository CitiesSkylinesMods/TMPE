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
        public static bool Prefix(ushort nodeID) {
            return !Options.timedLightsEnabled
                   || !TrafficLightSimulationManager
                                .Instance
                                .TrafficLightSimulations[nodeID]
                                .IsSimulationRunning();
        }
    }
}
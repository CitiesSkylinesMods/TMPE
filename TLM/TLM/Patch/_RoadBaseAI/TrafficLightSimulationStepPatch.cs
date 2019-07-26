namespace TrafficManager.Patch._RoadBaseAI {
    using JetBrains.Annotations;
    using State;

    // [Harmony] Manually patched because struct references are used
    public class TrafficLightSimulationStepPatch {
        /// <summary>
        /// Decides whether the stock simulation step for traffic lights should run.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(RoadBaseAI __instance, ushort nodeID, ref NetNode data) {
            return !Options.timedLightsEnabled
                   || !Constants.ManagerFactory
                                .TrafficLightSimulationManager
                                .TrafficLightSimulations[nodeID]
                                .IsSimulationRunning();
        }
    }
}
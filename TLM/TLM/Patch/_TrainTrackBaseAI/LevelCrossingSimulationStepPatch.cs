namespace TrafficManager.Patch._TrainTrackBase {
    using JetBrains.Annotations;
    using State;

    // [Harmony] Manually patched because struct references are used
    public class LevelCrossingSimulationStepPatch {
        /// <summary>
        /// Decides whether the stock simulation step for traffic lights should run.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(TrainTrackBaseAI __instance, ushort nodeID, ref NetNode data) {
            return !Options.timedLightsEnabled
                   || !Constants.ManagerFactory
                                .TrafficLightSimulationManager
                                .TrafficLightSimulations[nodeID]
                                .IsSimulationRunning();
        }
    }
}
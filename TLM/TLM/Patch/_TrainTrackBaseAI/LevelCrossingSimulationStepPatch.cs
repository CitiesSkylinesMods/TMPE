namespace TrafficManager.Patch._TrainTrackBase {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.State;

    [HarmonyPatch(typeof(RoadBaseAI), nameof(RoadBaseAI.TrafficLightSimulationStep))]
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
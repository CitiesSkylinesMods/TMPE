namespace TrafficManager.Patch._RoadBaseAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;

    [HarmonyPatch(typeof(TrainTrackBaseAI), nameof(TrainTrackBaseAI.LevelCrossingSimulationStep))]
    public class TrafficLightSimulationStepPatch {
        /// <summary>
        /// Decides whether the stock simulation step for traffic lights should run.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(RoadBaseAI __instance, ushort nodeID, ref NetNode data) {
            return !Options.timedLightsEnabled
                   || !TrafficLightSimulationManager
                                .Instance
                                .TrafficLightSimulations[nodeID]
                                .IsSimulationRunning();
        }
    }
}
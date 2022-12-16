namespace TrafficManager.Patch._TrainTrackBaseAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;

    [HarmonyPatch(typeof(TrainTrackBaseAI), nameof(TrainTrackBaseAI.LevelCrossingSimulationStep))]
    public class LevelCrossingSimulationStepPatch {
        /// <summary>
        /// Decides whether the stock simulation step for traffic lights should run.
        /// </summary>
        [UsedImplicitly]
        public static bool Prefix(ushort nodeID) {
            return !SavedGameOptions.Instance.timedLightsEnabled
                   || !TrafficLightSimulationManager
                                .Instance
                                .TrafficLightSimulations[nodeID]
                                .IsSimulationRunning();
        }
    }
}
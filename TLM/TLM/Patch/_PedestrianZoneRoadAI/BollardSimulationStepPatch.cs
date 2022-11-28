namespace TrafficManager.Patch._PedestrianZoneRoadAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(PedestrianZoneRoadAI), nameof(PedestrianZoneRoadAI.BollardsSimulationStep))]
    [UsedImplicitly]
    public class BollardSimulationStepPatch {
        /// <summary>
        /// Prevents vanilla code from running bollard simulation for custom traffic lights.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(ushort nodeID,
                                  ref NetNode data) {
            if (!SavedGameOptions.Instance.timedLightsEnabled
                || !TrafficLightSimulationManager
                    .Instance
                    .TrafficLightSimulations[nodeID]
                    .IsSimulationRunning()) {
                return true;
            }

            uint currentFrameIndex = SimulationManager.instance.m_currentFrameIndex;
            for (int i = 0; i < 8; i++) {
                ushort segmentId = data.GetSegment(i);
                if (segmentId == 0) {
                    continue;
                }
                ref NetSegment segment = ref segmentId.ToSegment();
                bool isPedZoneRoad = segment.Info.IsPedestrianZoneRoad();
                bool isRegularRoadEnd = (data.flags & NetNode.FlagsLong.RegularRoadEnd) != 0;
                if (isPedZoneRoad != isRegularRoadEnd) {
                    RoadBaseAI.GetBollardState(nodeID, ref segment, currentFrameIndex - 256, out RoadBaseAI.TrafficLightState enterState, out RoadBaseAI.TrafficLightState exitState);
                    RoadBaseAI.GetBollardState(nodeID, ref segment, currentFrameIndex - 256, out RoadBaseAI.TrafficLightState enterState2, out RoadBaseAI.TrafficLightState exitState2, out bool enter, out bool exit);
                    enterState2 = ((!enter) ? RoadBaseAI.TrafficLightState.Red : RoadBaseAI.TrafficLightState.Green);

                    switch (enterState) {
                        case RoadBaseAI.TrafficLightState.Green:
                            if (enterState2 == RoadBaseAI.TrafficLightState.Red) {
                                    enterState2 = RoadBaseAI.TrafficLightState.GreenToRed;
                            }
                            break;

                        case RoadBaseAI.TrafficLightState.Red:
                            if (enterState2 == RoadBaseAI.TrafficLightState.Green) {
                                enterState2 = RoadBaseAI.TrafficLightState.RedToGreen;
                            }
                            break;
                    }
                    TrafficLightSimulationManager.SetBollardVisualState(nodeID, ref segment, currentFrameIndex, enterState2, exitState2, enter: false, exit: false);
                } else {
                    TrafficLightSimulationManager.SetBollardVisualState(nodeID, ref segment, currentFrameIndex, RoadBaseAI.TrafficLightState.Green, RoadBaseAI.TrafficLightState.Green, enter: false, exit: false);
                }
            }

            return false;
        }
    }
}
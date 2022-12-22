namespace TrafficManager.Patch._RoadBaseAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using UnityEngine;

    [HarmonyPatch(typeof(RoadBaseAI), "GetTrafficLightNodeState")]
    [UsedImplicitly]
    public class GetTrafficLightNodeStatePatch {
        /// <summary>
        /// Removes buggy yellow traffic light phases.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(ushort nodeID,
                                  ref NetNode nodeData,
                                  ushort segmentID,
                                  ref NetSegment segmentData,
                                  ref NetNode.Flags flags,
                                  ref Color color) {
            if (!SavedGameOptions.Instance.timedLightsEnabled
                || !TrafficLightSimulationManager
                             .Instance
                             .TrafficLightSimulations[nodeID]
                             .IsSimulationRunning())
            {
                return true;
            }

            uint frame = Singleton<SimulationManager>.instance.m_referenceFrameIndex - 15u;
            uint simGroup = (uint)nodeID >> 7;

            RoadBaseAI.TrafficLightState vehicleLightState;
            RoadBaseAI.TrafficLightState pedLightState;
            RoadBaseAI.GetTrafficLightState(
                nodeID,
                ref segmentData,
                frame - simGroup,
                out vehicleLightState,
                out pedLightState);

            color.a = 0.5f;
            switch (vehicleLightState) {
                case RoadBaseAI.TrafficLightState.Green:
                    color.g = 1f;
                    break;
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    color.r = 1f;
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    color.g = 0f;
                    break;
                case RoadBaseAI.TrafficLightState.GreenToRed:
                    color.r = 1f;
                    break;
            }

            switch (pedLightState) {
                case RoadBaseAI.TrafficLightState.Green:
                    color.b = 1f;
                    break;
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    color.b = 0f;
                    break;
                case RoadBaseAI.TrafficLightState.Red:
                    color.b = 0f;
                    break;
                case RoadBaseAI.TrafficLightState.GreenToRed:
                    color.b = 0f;
                    break;
            }

            return false;
        }
    }
}
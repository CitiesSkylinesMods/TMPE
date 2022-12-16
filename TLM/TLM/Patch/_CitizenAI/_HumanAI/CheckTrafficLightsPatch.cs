namespace TrafficManager.Patch._CitizenAI._HumanAI {
    using System.Reflection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.Util.Extensions;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CheckTrafficLightsPatch {
        private delegate bool TargetDelegate(ushort node, ushort segment);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(HumanAI), "CheckTrafficLights");

        [UsedImplicitly]
        public static bool Prefix(ref bool __result,
                                  ushort node,
                                  ushort segment) {
#if DEBUG
            bool logTimedLights = DebugSwitch.TimedTrafficLights.Get()
                                 && DebugSettings.NodeId == node;
#endif
            uint currentFrameIndex = SimulationManager.instance.m_currentFrameIndex;
            uint simGroup = (uint)node >> 7;
            uint stepWaitTime = currentFrameIndex - simGroup & 255u;
            ref NetSegment netSegment = ref segment.ToSegment();

            // NON-STOCK CODE START
            bool customSim = SavedGameOptions.Instance.timedLightsEnabled &&
                             TrafficLightSimulationManager.Instance.HasActiveSimulation(node);

            RoadBaseAI.TrafficLightState pedestrianLightState;
            bool startNode = netSegment.m_startNode == node;

            CustomSegmentLights lights = null;
            if (customSim) {
                lights = CustomSegmentLightsManager.Instance.GetSegmentLights(
                    segment,
                    startNode,
                    false);
            }

            if (lights == null) {
                // NON-STOCK CODE END
#if DEBUG
                Log._DebugIf(
                    logTimedLights,
                    () => $"CustomHumanAI.CustomCheckTrafficLights({node}, " +
                    $"{segment}): No custom simulation!");
#endif

                RoadBaseAI.GetTrafficLightState(
                    node,
                    ref netSegment,
                    currentFrameIndex - simGroup,
                    out RoadBaseAI.TrafficLightState vehicleLightState,
                    out pedestrianLightState,
                    out bool vehicles,
                    out bool pedestrians);

                if (pedestrianLightState == RoadBaseAI.TrafficLightState.GreenToRed
                    || pedestrianLightState == RoadBaseAI.TrafficLightState.Red) {
                    if (!pedestrians && stepWaitTime >= 196u) {
                        RoadBaseAI.SetTrafficLightState(
                            node,
                            ref netSegment,
                            currentFrameIndex - simGroup,
                            vehicleLightState,
                            pedestrianLightState,
                            vehicles,
                            true);
                    }

                    __result = false;
                    return false;
                }

                // NON-STOCK CODE START
            } else {
                if (lights.InvalidPedestrianLight) {
                    pedestrianLightState = RoadBaseAI.TrafficLightState.Green;
                } else {
                    pedestrianLightState =
                        (RoadBaseAI.TrafficLightState)lights.PedestrianLightState;
                }

#if DEBUG
                Log._DebugIf(
                    logTimedLights,
                    () => $"CustomHumanAI.CustomCheckTrafficLights({node}, {segment}): " +
                    $"Custom simulation! pedestrianLightState={pedestrianLightState}, " +
                    $"lights.InvalidPedestrianLight={lights.InvalidPedestrianLight}");
#endif
            }

            // NON-STOCK CODE END
            switch (pedestrianLightState) {
                case RoadBaseAI.TrafficLightState.RedToGreen:
                    if (stepWaitTime < 60u) {
                        __result = false;
                        return false;
                    }

                    break;
                case RoadBaseAI.TrafficLightState.Red:
                case RoadBaseAI.TrafficLightState.GreenToRed:
                    __result = false;
                    return false;
            }

            __result = true;
            return false;
        }
    }
}
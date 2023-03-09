namespace TrafficManager.Patch._RoadBaseAI {
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using System.Reflection;
    using TrafficManager.State;

    [HarmonyPatch]
    [UsedImplicitly]
    public class SegmentSimulationStepPatch {
        [UsedImplicitly]
        public static MethodBase TargetMethod()
        {
            return HarmonyLib.AccessTools.DeclaredMethod(
                typeof(RoadBaseAI),
                "SimulationStep",
                new[] { typeof(ushort), typeof(NetSegment).MakeByRefType() }) ??
                throw new System.Exception("SegmentSimulationStepPatch failed to find TargetMethod");
        }

        private static ushort lastSimulatedSegmentId = 0;
        private static byte trafficMeasurementMod = 0;

        /// <summary>
        /// Updates lane arrows and performs traffic measurement on segment.
        /// </summary>
        [UsedImplicitly]
        public static void Prefix(RoadBaseAI __instance, ushort segmentID, ref NetSegment data) {
            if (segmentID < lastSimulatedSegmentId) {
                // segment simulation restart
                ++trafficMeasurementMod;
                if (trafficMeasurementMod >= 4)
                    trafficMeasurementMod = 0;
            }

            lastSimulatedSegmentId = segmentID;

            bool doTrafficMeasurement = true;
            if (SavedGameOptions.Instance.simulationAccuracy == SimulationAccuracy.High ||
                SavedGameOptions.Instance.simulationAccuracy == SimulationAccuracy.Medium) {
                doTrafficMeasurement = (segmentID & 1) == trafficMeasurementMod;
            } else if (SavedGameOptions.Instance.simulationAccuracy <= SimulationAccuracy.Low) {
                doTrafficMeasurement = (segmentID & 3) == trafficMeasurementMod;
            }

            if (doTrafficMeasurement) {
                Constants.ManagerFactory.TrafficMeasurementManager.OnBeforeSimulationStep(
                    segmentID,
                    ref data);
            }
        }
    }
}
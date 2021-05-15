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
            // TODO check if this is required *START*
            uint curLaneId = data.m_lanes;
            int numLanes = data.Info.m_lanes.Length;
            uint laneIndex = 0;

            while (laneIndex < numLanes && curLaneId != 0u) {
                Flags.ApplyLaneArrowFlags(curLaneId);

                laneIndex++;
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
            }

            // ↑↑↑↑
            // TODO check if this is required *END*
            if (segmentID < lastSimulatedSegmentId) {
                // segment simulation restart
                ++trafficMeasurementMod;
                if (trafficMeasurementMod >= 4)
                    trafficMeasurementMod = 0;
            }

            lastSimulatedSegmentId = segmentID;

            bool doTrafficMeasurement = true;
            if (Options.simulationAccuracy == SimulationAccuracy.High ||
                Options.simulationAccuracy == SimulationAccuracy.Medium) {
                doTrafficMeasurement = (segmentID & 1) == trafficMeasurementMod;
            } else if (Options.simulationAccuracy <= SimulationAccuracy.Low) {
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
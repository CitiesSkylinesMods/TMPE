namespace TrafficManager.Patch._VehicleAI._TrainAI{
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class ForceTrafficLightsPatch {
        private delegate void TargetDelegate(ushort vehicleID, ref Vehicle vehicleData, bool reserveSpace);
        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(TrainAI), "ForceTrafficLights");

        [UsedImplicitly]
        public static bool Prefix(TrainAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  bool reserveSpace) {
            uint pathUnitId = vehicleData.m_path;
            if (pathUnitId == 0u) {
                return false;
            }

            NetManager netMan = NetManager.instance;
            PathManager pathMan = PathManager.instance;
            byte pathPosIndex = vehicleData.m_pathPositionIndex;

            if (pathPosIndex == 255) {
                pathPosIndex = 0;
            }

            pathPosIndex = (byte)(pathPosIndex >> 1);
            bool stopLoop = false; // NON-STOCK CODE
            for (int i = 0; i < 6; i++) {
                if (!pathMan.m_pathUnits.m_buffer[pathUnitId].GetPosition(pathPosIndex, out PathUnit.Position position)) {
                    return false;
                }

                // NON-STOCK CODE START
                ushort transitNodeId = position.m_offset < 128
                                        ? netMan.m_segments.m_buffer[position.m_segment].m_startNode
                                        : netMan.m_segments.m_buffer[position.m_segment].m_endNode;

                if (Options.timedLightsEnabled) {
                    // when a TTL is active only reserve space if it shows green
                    if (pathMan.m_pathUnits.m_buffer[pathUnitId].GetNextPosition(pathPosIndex, out PathUnit.Position nextPos)) {
                        if (!VehicleBehaviorManager.Instance.IsSpaceReservationAllowed(
                                transitNodeId,
                                position,
                                nextPos)) {
                            stopLoop = true;
                        }
                    }
                }

                // NON-STOCK CODE END
                if (reserveSpace && i >= 1 && i <= 2) {
                    uint laneId = PathManager.GetLaneID(position);
                    if (laneId != 0u) {
                        reserveSpace = netMan.m_lanes.m_buffer[laneId]
                                             .ReserveSpace(__instance.m_info.m_generatedInfo.m_size.z, vehicleID);
                    }
                }

                ForceTrafficLights(transitNodeId, position); // NON-STOCK CODE

                // NON-STOCK CODE START
                if (stopLoop) {
                    return false;
                }

                // NON-STOCK CODE END
                if ((pathPosIndex += 1) <
                    pathMan.m_pathUnits.m_buffer[pathUnitId].m_positionCount) {
                    continue;
                }

                pathUnitId = pathMan.m_pathUnits.m_buffer[pathUnitId].m_nextPathUnit;
                pathPosIndex = 0;
                if (pathUnitId == 0u) {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// slightly modified version of TrainAI.ForceTrafficLights(PathUnit.Position)
        /// </summary>
        /// <param name="transitNodeId"></param>
        /// <param name="position"></param>
        private static void ForceTrafficLights(ushort transitNodeId, PathUnit.Position position) {
            NetManager netMan = NetManager.instance;
            if ((netMan.m_nodes.m_buffer[transitNodeId].m_flags & NetNode.Flags.TrafficLights) == NetNode.Flags.None) {
                return;
            }

            uint frame = SimulationManager.instance.m_currentFrameIndex;
            uint simGroup = (uint)transitNodeId >> 7;
            uint rand = frame - simGroup & 255u;
            RoadBaseAI.GetTrafficLightState(
                transitNodeId,
                ref netMan.m_segments.m_buffer[position.m_segment],
                frame - simGroup,
                out RoadBaseAI.TrafficLightState vehicleLightState,
                out RoadBaseAI.TrafficLightState pedestrianLightState,
                out bool vehicles,
                out bool pedestrians);

            if (vehicles || rand < 196u) {
                return;
            }

            vehicles = true;
            RoadBaseAI.SetTrafficLightState(
                transitNodeId,
                ref netMan.m_segments.m_buffer[position.m_segment],
                frame - simGroup,
                vehicleLightState,
                pedestrianLightState,
                true,
                pedestrians);
        }
    }
}

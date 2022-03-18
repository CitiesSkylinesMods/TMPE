namespace TrafficManager.Patch._VehicleAI._AircraftAI {
    using System.Reflection;
    using _VehicleAI.Connection;
    using ColossalFramework.Math;
    using Connection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CalculateSegmentPosition2Patch {
        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position nextPosition,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        PathUnit.Position prevPos,
                                                        uint prevLaneID,
                                                        byte prevOffset,
                                                        int index,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);


        [UsedImplicitly]
        public static MethodBase TargetMethod() =>
            TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(
                typeof(AircraftAI),
                "CalculateSegmentPosition");

        private static CalculateTargetSpeedDelegate CalculateTargetSpeed;
        private static CheckOverlapDelegate CheckOverlap;

        [UsedImplicitly]
        public static void Prepare() {
            CalculateTargetSpeed = GameConnectionManager.Instance.VehicleAIConnection.CalculateTargetSpeed;
            CheckOverlap = GameConnectionManager.Instance.AircraftAIConnection.CheckOverlap;
        }

        [UsedImplicitly]
        public static bool Prefix(AircraftAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position nextPosition,
                                  PathUnit.Position position,
                                  uint laneID,
                                  byte offset,
                                  PathUnit.Position prevPos,
                                  uint prevLaneID,
                                  byte prevOffset,
                                  int index,
                                  out Vector3 pos,
                                  out Vector3 dir,
                                  out float maxSpeed) {
#if DEBUG
            bool logLogic = DebugSwitch.PriorityRules.Get() &&
                            // use only selected vehicle ()
                            InstanceManager.instance.GetSelectedInstance().Vehicle == vehicleID;
#else
            const bool logLogic = false;
#endif
            maxSpeed = 0;
            ref NetLane lane = ref laneID.ToLane();
            lane.CalculatePositionAndDirection(laneOffset: Constants.ByteToFloat(offset),
                                               position: out pos,
                                               direction: out dir);
            bool isAirportSegment = false;
            NetInfo info = position.m_segment.ToSegment().Info;
            if (info.m_lanes != null && info.m_lanes.Length > position.m_lane) {
                // NON-STOCK CODE
                float speedLimit = Options.customSpeedLimitsEnabled
                                       ? SpeedLimitManager.Instance.GetGameSpeedLimit(
                                           segmentId: position.m_segment,
                                           laneIndex: position.m_lane,
                                           laneId: laneID,
                                           laneInfo: info.m_lanes[position.m_lane])
                                       : info.m_lanes[position.m_lane].m_speedLimit;

                // NON-STOCK CODE END
                maxSpeed = CalculateTargetSpeed(__instance,
                                                vehicleID,
                                                ref vehicleData,
                                                speedLimit,
                                                lane.m_curve);
                if (speedLimit > 5f) {
                    // aircraft path segment (placed on the ground)
                    Randomizer randomizer = new Randomizer(vehicleID);
                    pos.x += randomizer.Int32(-500, 500);
                    pos.y += randomizer.Int32(1300, 1700);
                    pos.z += randomizer.Int32(-500, 500);
                } else if (speedLimit < 2f) {
                    isAirportSegment = true;
                }
            } else {
                maxSpeed = CalculateTargetSpeed(__instance, vehicleID, ref vehicleData, 1f, 0f);
            }

            ushort nextSourceNodeId;
            ushort nextTargetNodeId;
            ref NetSegment currentPositionSegment = ref position.m_segment.ToSegment();

            if (offset < position.m_offset) {
                nextSourceNodeId = currentPositionSegment.m_startNode;
                nextTargetNodeId = currentPositionSegment.m_endNode;
            } else {
                nextSourceNodeId = currentPositionSegment.m_endNode;
                nextTargetNodeId = currentPositionSegment.m_startNode;
            }

            ref NetSegment previousPositionSegment = ref prevPos.m_segment.ToSegment();
            ushort curTargetNodeId = prevOffset == 0
                                  ? previousPositionSegment.m_startNode
                                  : previousPositionSegment.m_endNode;

            if (logLogic) {
                Log._Debug($"AircraftAI.CustomCalculateSegmentPosition2({vehicleID}) called.\n" +
                           $"\tcurPosition.m_segment={prevPos.m_segment}, curPosition.m_offset={prevPos.m_offset}\n" +
                           $"\tposition.m_segment={position.m_segment}, position.m_offset={position.m_offset}\n" +
                           $"\tnextNextPosition.m_segment={nextPosition.m_segment}, nextNextPosition.m_offset={nextPosition.m_offset}\n" +
                           $"\tcurLaneId={prevLaneID}, prevOffset={prevOffset}\n" +
                           $"\tnextLaneId={laneID}, nextOffset={offset}\n" +
                           $"\tnextSourceNodeId={nextSourceNodeId}, nextTargetNodeId={nextTargetNodeId}\n" +
                           $"\tcurTargetNodeId={curTargetNodeId}, \n" +
                           $"\tcurrPosStartNodeId={currentPositionSegment.m_startNode}, currPosEndNodeId={currentPositionSegment.m_endNode}, \n" +
                           $"\tprevPosStartNodeId={previousPositionSegment.m_startNode}, prevPosEndNodeId={previousPositionSegment.m_endNode}, \n" +
                           $"\tindex={index}");
            }

            if (!isAirportSegment) {
                return false;
            }

            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            Vector3 lastFrameVehiclePos = lastFrameData.m_position;
            float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;
            float braking = __instance.m_info.m_braking;

            // aircraft position on the Bezier curve of the lane
            Vector3 refVehiclePosOnBezier = prevLaneID.ToLane().CalculatePosition(Constants.ByteToFloat(prevOffset));
            // this seems to be like the required braking force in order to stop the vehicle within its half length.
            float crazyValue = (0.5f * sqrVelocity / braking) +
                               (__instance.m_info.m_generatedInfo.m_size.z * 0.5f);
            float d = Vector3.Distance(lastFrameVehiclePos, refVehiclePosOnBezier);
            bool withinBrakingDistance = d >= crazyValue - 1f;

            if (nextSourceNodeId == curTargetNodeId &&
                withinBrakingDistance) {
                Log._DebugIf(
                    logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): withinBrakingDistance!");

                // NON-STOCK CODE START (stock code replaced)
                if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                        vehicleID,
                        ref vehicleData,
                        sqrVelocity,
                        ref prevPos,
                        ref previousPositionSegment,
                        curTargetNodeId,
                        prevLaneID,
                        ref position,
                        nextSourceNodeId,
                        ref nextSourceNodeId.ToNode(),
                        laneID,
                        ref nextPosition,
                        nextTargetNodeId,
                        out maxSpeed)) {
                    Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): Cannot change segment!");
                    // NON-STOCK CODE
                    return false;
                }
                NetInfo currentPositionSegmentInfo = currentPositionSegment.Info;
                // NON-STOCK CODE START (stock code replaced)
                VehicleAICommons.CustomCalculateTargetSpeed(
                    instance: __instance,
                    vehicleId: vehicleID,
                    vehicleData: ref vehicleData,
                    position: position,
                    laneId: laneID,
                    info: currentPositionSegmentInfo,
                    maxSpeed: out maxSpeed);

                maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(
                    vehicleId: vehicleID,
                    extVehicle: ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleID],
                    vehicleInfo: __instance.m_info,
                    position: position,
                    segment: ref currentPositionSegment,
                    pos: pos,
                    maxSpeed: maxSpeed,
                    emergency: false);

                if (logLogic) {
                    Log._Debug($"AircraftAI.CalculateSegmentPosition2({vehicleID}): Can change segment! MaxSpeed: {maxSpeed}");
                }

                ExtVehicleManager.Instance.UpdateVehiclePosition(vehicleID,
                                                                 ref vehicleData);

                if (!lane.CheckSpace(vehicleData.Info.m_generatedInfo.m_size.z * 3f/*STOCK 1000f*/, vehicleID)) {
                    maxSpeed = 0f;
                    Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): No space on lane: {laneID}!");
                    return false;
                }

                ref NetSegment segment = ref position.m_segment.ToSegment();
                Vector3 startNodePos = segment.m_startNode.ToNode().m_position;
                Vector3 endNodePos = segment.m_endNode.ToNode().m_position;
                if (CheckOverlap(new Segment3(startNodePos, endNodePos), vehicleID)) {
                    maxSpeed = 0f;
                    if (logLogic) {
                        Log._Debug($"AircraftAI.CalculateSegmentPosition2({vehicleID}): CheckOverlap failed! " +
                                   $"startNodePos: {startNodePos}, endNodePos: {endNodePos}, startNode: {segment.m_startNode}, endNode: {segment.m_endNode}");
                    }
                    return false;
                }

                return false;
            }

            Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): Speed not calculated yet. Calculating...");
            NetInfo currentPositionSegmentInfo2 = currentPositionSegment.Info;
            // NON-STOCK CODE START (stock code replaced)
            VehicleAICommons.CustomCalculateTargetSpeed(
                __instance,
                vehicleID,
                ref vehicleData,
                position,
                laneID,
                currentPositionSegmentInfo2,
                out maxSpeed);

            maxSpeed = Constants.ManagerFactory.VehicleBehaviorManager.CalcMaxSpeed(
                    vehicleID,
                    ref Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleID],
                    __instance.m_info,
                    position,
                    ref currentPositionSegment,
                    pos,
                    maxSpeed,
                    false);

            // todo-airplanes: improve lane reservation
            // (currently it checks only current and next lane (might be too short to provide nice gap between planes))

            if (nextPosition.m_segment == 0) {
                Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextPos segment 0!");
                return false;
            }

            ref NetSegment nextSegment = ref nextPosition.m_segment.ToSegment();
            NetInfo nextPosInfo = nextSegment.Info;
            if (nextPosInfo.m_lanes == null || nextPosInfo.m_lanes.Length <= nextPosition.m_lane) {
                Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextPos no lanes or too big index!");
                return false;
            }

            // NON-STOCK CODE START
            // float speedLimit2 = info.m_lanes[nextPosition.m_lane].m_speedLimit;?
            // REPLACED WITH
            float speedLimit2 = Options.customSpeedLimitsEnabled
                                   ? SpeedLimitManager.Instance.GetGameSpeedLimit(
                                       position.m_segment,
                                       position.m_lane,
                                       laneID,
                                       info.m_lanes[position.m_lane])
                                   : info.m_lanes[position.m_lane].m_speedLimit;
            // NON-STOCK CODE END
            if (speedLimit2 > 5f) {
                Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextPos lane speed limit > 5");
                return false;
            }

            uint nextLaneID = PathManager.GetLaneID(nextPosition);
            if (nextLaneID == 0) {
                Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextPos lane to laneID == 0");
                return false;
            }


            Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextLaneID {nextLaneID}");
            if (!nextLaneID.ToLane().CheckSpace(vehicleData.Info.m_generatedInfo.m_size.z * 3f/*STOCK 1000f*/)) {
                maxSpeed = 0f;
                Log._DebugIf(logLogic, () => $"AircraftAI.CalculateSegmentPosition2({vehicleID}): NextLaneID no space!");
                return false;
            }

            return false;
        }
    }
}
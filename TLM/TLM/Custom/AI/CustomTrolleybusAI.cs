namespace TrafficManager.Custom.AI {
    using ColossalFramework;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(TrolleybusAI))]
    public class CustomTrolleybusAI : CarAI {
        [RedirectMethod]
        public void CustomCalculateSegmentPosition(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   PathUnit.Position nextPosition,
                                                   PathUnit.Position prevPosition,
                                                   uint prevLaneId,
                                                   byte prevOffset,
                                                   PathUnit.Position refPosition,
                                                   uint refLaneId,
                                                   byte refOffset,
                                                   int index,
                                                   out Vector3 pos,
                                                   out Vector3 dir,
                                                   out float maxSpeed) {
            NetManager netManager = Singleton<NetManager>.instance;
            ushort prevSourceNodeId;
            ushort prevTargetNodeId;
            NetSegment[] segBuffer = netManager.m_segments.m_buffer;

            if (prevOffset < prevPosition.m_offset) {
                prevSourceNodeId = segBuffer[prevPosition.m_segment].m_startNode;
                prevTargetNodeId = segBuffer[prevPosition.m_segment].m_endNode;
            } else {
                prevSourceNodeId = segBuffer[prevPosition.m_segment].m_endNode;
                prevTargetNodeId = segBuffer[prevPosition.m_segment].m_startNode;
            }

            ushort refTargetNodeId = refOffset == 0
                                         ? segBuffer[refPosition.m_segment].m_startNode
                                         : segBuffer[refPosition.m_segment].m_endNode;
            Vehicle.Frame lastFrameData = vehicleData.GetLastFrameData();
            float sqrVelocity = lastFrameData.m_velocity.sqrMagnitude;

            netManager.m_lanes.m_buffer[prevLaneId].CalculatePositionAndDirection(
                Constants.ByteToFloat(prevOffset),
                out pos,
                out dir);
            Vector3 b = netManager.m_lanes.m_buffer[refLaneId].CalculatePosition(
                Constants.ByteToFloat(refOffset));
            Vector3 a = lastFrameData.m_position;
            Vector3 a2 = lastFrameData.m_position;
            Vector3 b2 = lastFrameData.m_rotation * new Vector3(
                             0f,
                             0f,
                             m_info.m_generatedInfo.m_wheelBase * 0.5f);
            a += b2;
            a2 -= b2;
            float crazyValue = 0.5f * sqrVelocity / m_info.m_braking;
            float a3 = Vector3.Distance(a, b);
            float b3 = Vector3.Distance(a2, b);

            if (Mathf.Min(a3, b3) >= crazyValue - 1f) {
                // dead stock code
                /*Segment3 segment;
                segment.a = pos;
                if (prevOffset < prevPosition.m_offset) {
                    segment.b = pos + dir.normalized * this.m_info.m_generatedInfo.m_size.z;
                } else {
                    segment.b = pos - dir.normalized * this.m_info.m_generatedInfo.m_size.z;
                }*/
                if (prevSourceNodeId == refTargetNodeId) {
                    if (!VehicleBehaviorManager.Instance.MayChangeSegment(
                            vehicleId,
                            ref vehicleData,
                            sqrVelocity,
                            ref refPosition,
                            ref segBuffer[refPosition.m_segment],
                            refTargetNodeId,
                            refLaneId,
                            ref prevPosition,
                            prevSourceNodeId,
                            ref netManager.m_nodes.m_buffer[
                                prevSourceNodeId],
                            prevLaneId,
                            ref nextPosition,
                            prevTargetNodeId,
                            out maxSpeed)) {
                        maxSpeed = 0;
                        return;
                    }

                    ExtVehicleManager.Instance.UpdateVehiclePosition(
                            vehicleId,
                            ref vehicleData);
                }
            }

            NetInfo info = segBuffer[prevPosition.m_segment].Info;
            if (info.m_lanes != null && info.m_lanes.Length > prevPosition.m_lane) {
                float speedLimit = Options.customSpeedLimitsEnabled
                                       ? SpeedLimitManager.Instance.GetLockFreeGameSpeedLimit(
                                           prevPosition.m_segment,
                                           prevPosition.m_lane,
                                           prevLaneId,
                                           info.m_lanes[prevPosition.m_lane])
                                       : info.m_lanes[prevPosition.m_lane].m_speedLimit;

                // NON-STOCK CODE
                maxSpeed = CalculateTargetSpeed(
                    vehicleId,
                    ref vehicleData,
                    speedLimit,
                    netManager.m_lanes.m_buffer[prevLaneId].m_curve);
            } else {
                maxSpeed = CalculateTargetSpeed(vehicleId, ref vehicleData, 1f, 0f);
            }
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays) {
            VehicleInfo info = m_info;
            bool allowUnderground =
                (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != 0;

            if (!PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    allowUnderground,
                    false,
                    32f,
                    out PathUnit.Position startPosA,
                    out PathUnit.Position startPosB,
                    out float startDistSqrA,
                    out float startDistSqrB)
                || !PathManager.FindPathPosition(
                    endPos,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle,
                    info.m_vehicleType,
                    false,
                    false,
                    32f,
                    out PathUnit.Position endPosA,
                    out PathUnit.Position endPosB,
                    out float endDistSqrA,
                    out float endDistSqrB)) {
                return false;
            }

            if (!startBothWays || startDistSqrB > startDistSqrA * 1.2f) {
                startPosB = default(PathUnit.Position);
            }

            if (!endBothWays || endDistSqrB > endDistSqrA * 1.2f) {
                endPosB = default(PathUnit.Position);
            }

            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = ExtVehicleType.Trolleybus;
            args.vehicleId = vehicleId;
            args.spawned = (vehicleData.m_flags & Vehicle.Flags.Spawned) != 0;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.laneTypes = NetInfo.LaneType.Vehicle;
            args.vehicleTypes = info.m_vehicleType;
            args.maxLength = 20000f;
            args.isHeavyVehicle = IsHeavyVehicle();
            args.hasCombustionEngine = CombustionEngine();
            args.ignoreBlocked = IgnoreBlocked(vehicleId, ref vehicleData);
            args.ignoreFlooded = false;
            args.randomParking = false;
            args.ignoreCosts = false;
            args.stablePath = true;
            args.skipQueue = true;

            if (CustomPathManager._instance.CustomCreatePath(
                out uint path,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                args)) {
                // NON-STOCK CODE END
                if (vehicleData.m_path != 0u) {
                    Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                }

                vehicleData.m_path = path;
                vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                return true;
            }

            return false;
        }
    }
}
namespace TrafficManager.Custom.AI {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Runtime.CompilerServices;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State.ConfigData;
    using UnityEngine;

    // TODO inherit from NetAI (in order to keep the correct references to `base`)
    [TargetType(typeof(TransportLineAI))]
    public class CustomTransportLineAI : TransportLineAI {
        [RedirectMethod]
        [UsedImplicitly]
        public static bool CustomStartPathFind(ushort segmentId,
                                               ref NetSegment data,
                                               ItemClass.Service netService,
                                               ItemClass.Service netService2,
                                               VehicleInfo.VehicleType vehicleType,
                                               bool skipQueue) {
            if (data.m_path != 0u) {
                Singleton<PathManager>.instance.ReleasePath(data.m_path);
                data.m_path = 0u;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            if ((netManager.m_nodes.m_buffer[data.m_startNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None) {
                for (int i = 0; i < 8; i++) {
                    ushort segment = netManager.m_nodes.m_buffer[data.m_startNode].GetSegment(i);
                    if (segment != 0 && segment != segmentId && netManager.m_segments.m_buffer[segment].m_path != 0u) {
                        return true;
                    }
                }
            }

            if ((netManager.m_nodes.m_buffer[data.m_endNode].m_flags
                 & NetNode.Flags.Ambiguous) != NetNode.Flags.None) {
                for (int j = 0; j < 8; j++) {
                    ushort segment2 = netManager.m_nodes.m_buffer[data.m_endNode].GetSegment(j);
                    if (segment2 != 0 && segment2 != segmentId
                                      && netManager.m_segments.m_buffer[segment2].m_path != 0u) {
                        return true;
                    }
                }
            }

            Vector3 position = netManager.m_nodes.m_buffer[data.m_startNode].m_position;
            Vector3 position2 = netManager.m_nodes.m_buffer[data.m_endNode].m_position;
#if DEBUG
            bool logPathfind = DebugSwitch.TransportLinePathfind.Get();
            ushort logStartNode = data.m_startNode;
            ushort logEndNode = data.m_endNode;
            Log._DebugIf(
                logPathfind,
                () => $"TransportLineAI.CustomStartPathFind({segmentId}, ..., {netService}, " +
                      $"{netService2}, {vehicleType}, {skipQueue}): " +
                      $"startNode={logStartNode} @ {position}, " +
                      $"endNode={logEndNode} @ {position2} -- " +
                      $"line: {netManager.m_nodes.m_buffer[logStartNode].m_transportLine}" +
                      $"/{netManager.m_nodes.m_buffer[logEndNode].m_transportLine}");
#else
            var logPathfind = false;
#endif

            if (!PathManager.FindPathPosition(
                    position,
                    netService,
                    netService2,
                    NetInfo.LaneType.Pedestrian,
                    VehicleInfo.VehicleType.None,
                    vehicleType,
                    true,
                    false,
                    32f,
                    out PathUnit.Position startPosA,
                    out PathUnit.Position startPosB,
                    out _,
                    out _)) {
                CheckSegmentProblems(segmentId, ref data);
                return true;
            }

            if (!PathManager.FindPathPosition(
                    position2,
                    netService,
                    netService2,
                    NetInfo.LaneType.Pedestrian,
                    VehicleInfo.VehicleType.None,
                    vehicleType,
                    true,
                    false,
                    32f,
                    out PathUnit.Position endPosA,
                    out PathUnit.Position endPosB,
                    out _,
                    out _)) {
                CheckSegmentProblems(segmentId, ref data);
                return true;
            }

            if ((netManager.m_nodes.m_buffer[data.m_startNode].m_flags & NetNode.Flags.Fixed) !=
                NetNode.Flags.None) {
                startPosB = default;
            }

            if ((netManager.m_nodes.m_buffer[data.m_endNode].m_flags & NetNode.Flags.Fixed) !=
                NetNode.Flags.None) {
                endPosB = default;
            }

            if (vehicleType != VehicleInfo.VehicleType.None) {
                startPosA.m_offset = 128;
                startPosB.m_offset = 128;
                endPosA.m_offset = 128;
                endPosB.m_offset = 128;
            } else {
                startPosA.m_offset = (byte)Mathf.Clamp(startPosA.m_offset, 1, 254);
                startPosB.m_offset = (byte)Mathf.Clamp(startPosB.m_offset, 1, 254);
                endPosA.m_offset = (byte)Mathf.Clamp(endPosA.m_offset, 1, 254);
                endPosB.m_offset = (byte)Mathf.Clamp(endPosB.m_offset, 1, 254);
            }

            bool stopLane = GetStopLane(ref startPosA, vehicleType);
            bool stopLane2 = GetStopLane(ref startPosB, vehicleType);
            bool stopLane3 = GetStopLane(ref endPosA, vehicleType);
            bool stopLane4 = GetStopLane(ref endPosB, vehicleType);

            if ((!stopLane && !stopLane2) || (!stopLane3 && !stopLane4)) {
                CheckSegmentProblems(segmentId, ref data);
                return true;
            }

            ExtVehicleType extVehicleType = ExtVehicleType.None;
            if ((vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Bus;
            }

            if ((vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro |
                                VehicleInfo.VehicleType.Monorail)) !=
                VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerTrain;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Tram;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerShip;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerPlane;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Ferry;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Blimp) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Blimp;
            }

            if ((vehicleType & VehicleInfo.VehicleType.CableCar) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.CableCar;
            }

            if ((vehicleType & VehicleInfo.VehicleType.Trolleybus) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Trolleybus;
            }

            // Log._Debug($"Transport line. extVehicleType={extVehicleType}");
            // NON-STOCK CODE START
            PathCreationArgs args;
            args.extPathType = ExtPathType.None;
            args.extVehicleType = extVehicleType;
            args.vehicleId = 0;
            args.spawned = true;
            args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
            args.startPosA = startPosA;
            args.startPosB = startPosB;
            args.endPosA = endPosA;
            args.endPosB = endPosB;
            args.vehiclePosition = default;
            args.vehicleTypes = vehicleType;
            args.isHeavyVehicle = false;
            args.hasCombustionEngine = false;
            args.ignoreBlocked = true;
            args.ignoreFlooded = false;
            args.ignoreCosts = false;
            args.randomParking = false;
            args.stablePath = true;
            args.skipQueue = skipQueue;

            if (vehicleType == VehicleInfo.VehicleType.None) {
                args.laneTypes = NetInfo.LaneType.Pedestrian;
                args.maxLength = 160000f;
            } else {
                args.laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;
                args.maxLength = 20000f;
            }

            if (CustomPathManager._instance.CustomCreatePath(
                out uint path,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                args)) {
                // NON-STOCK CODE END
                if (startPosA.m_segment != 0 && startPosB.m_segment != 0) {
                    netManager.m_nodes.m_buffer[data.m_startNode].m_flags |= NetNode.Flags.Ambiguous;
                } else {
                    netManager.m_nodes.m_buffer[data.m_startNode].m_flags &= ~NetNode.Flags.Ambiguous;
                }

                if (endPosA.m_segment != 0 && endPosB.m_segment != 0) {
                    netManager.m_nodes.m_buffer[data.m_endNode].m_flags |= NetNode.Flags.Ambiguous;
                } else {
                    netManager.m_nodes.m_buffer[data.m_endNode].m_flags &= ~NetNode.Flags.Ambiguous;
                }

                data.m_path = path;
                data.m_flags |= NetSegment.Flags.WaitingPath;
                Log._DebugIf(
                    logPathfind,
                    () => $"TransportLineAI.CustomStartPathFind({segmentId}, ..., {netService}, " +
                    $"{netService2}, {vehicleType}, {skipQueue}): Started calculating " +
                    $"path {path} for extVehicleType={extVehicleType}, " +
                    $"startPosA=[seg={startPosA.m_segment}, lane={startPosA.m_lane}, " +
                    $"off={startPosA.m_offset}], startPosB=[seg={startPosB.m_segment}, " +
                    $"lane={startPosB.m_lane}, off={startPosB.m_offset}], " +
                    $"endPosA=[seg={endPosA.m_segment}, lane={endPosA.m_lane}, " +
                    $"off={endPosA.m_offset}], endPosB=[seg={endPosB.m_segment}, " +
                    $"lane={endPosB.m_lane}, off={endPosB.m_offset}]");
                return false;
            }

            CheckSegmentProblems(segmentId, ref data);
            return true;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static bool GetStopLane(ref PathUnit.Position pos, VehicleInfo.VehicleType vehicleType) {
            Log._DebugOnlyError("CustomTransportLineAI.GetStopLane called.");
            return false;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static void CheckSegmentProblems(ushort segmentId, ref NetSegment data) {
            Log._DebugOnlyError($"CustomTransportLineAI.CheckSegmentProblems called.");
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private static void CheckNodeProblems(ushort nodeId, ref NetNode data) {
            Log._DebugOnlyError($"CustomTransportLineAI.CheckNodeProblems called.");
        }
    }
}
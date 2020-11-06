namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using UnityEngine;

    public class ExtSegmentEndManager
        : AbstractCustomManager,
          IExtSegmentEndManager
    {
        static ExtSegmentEndManager() {
            Instance = new ExtSegmentEndManager();
        }

        private ExtSegmentEndManager() {
            ExtSegmentEnds = new ExtSegmentEnd[NetManager.MAX_SEGMENT_COUNT * 2];
            for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                ExtSegmentEnds[GetIndex((ushort)i, true)] = new ExtSegmentEnd((ushort)i, true);
                ExtSegmentEnds[GetIndex((ushort)i, false)] = new ExtSegmentEnd((ushort)i, false);
            }
        }

        public static ExtSegmentEndManager Instance { get; }

        /// <summary>
        /// All additional data for segment ends
        /// </summary>
        public ExtSegmentEnd[] ExtSegmentEnds { get; }

#if DEBUG
        public string GenerateVehicleChainDebugInfo(ushort segmentId, bool startNode) {
            int index = GetIndex(segmentId, startNode);
            ushort vehicleId = ExtSegmentEnds[index].firstVehicleId;
            string ret = string.Empty;
            int numIter = 0;

            while (vehicleId != 0) {
                ref ExtVehicle extVehicle = ref Constants.ManagerFactory.ExtVehicleManager
                                                         .ExtVehicles[vehicleId];
                ret += string.Format(
                    " -> {0} (seg: {1}@{2} , adj: {3}..{4})",
                    vehicleId,
                    extVehicle.currentSegmentId,
                    extVehicle.currentStartNode,
                    extVehicle.previousVehicleIdOnSegment,
                    extVehicle.nextVehicleIdOnSegment);

                vehicleId = extVehicle.nextVehicleIdOnSegment;

                if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }

            return ret;
        }
#endif

        public void Reset(ushort segmentId) {
            Reset(ref ExtSegmentEnds[GetIndex(segmentId, true)]);
            Reset(ref ExtSegmentEnds[GetIndex(segmentId, false)]);
        }

        private void Reset(ref ExtSegmentEnd extSegmentEnd) {
            IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            int numIter = 0;

            while (extSegmentEnd.firstVehicleId != 0) {
                extVehicleMan.Unlink(ref extVehicleMan.ExtVehicles[extSegmentEnd.firstVehicleId]);

                if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

            extSegmentEnd.nodeId = 0;
            extSegmentEnd.outgoing = false;
            extSegmentEnd.incoming = false;
            extSegmentEnd.firstVehicleId = 0;
        }

        public int GetIndex(ushort segmentId, bool startNode) {
            return (segmentId * 2) + (startNode ? 0 : 1);
        }

        public int GetIndex(ushort segmentId, ushort nodeId) {
            bool found = false;
            bool startNode = false;

            ref NetSegment segment = ref segmentId.ToSegment();
            if (segment.m_startNode == nodeId) {
                found = true;
                startNode = true;
            } else if (segment.m_endNode == nodeId) {
                found = true;
            }

            if (!found) {
                Log.Warning(
                    $"ExtSegmentEndManager.GetIndex({segmentId}, {nodeId}): Node is not " +
                    "connected to segment.");
                return -1;
            }

            return GetIndex(segmentId, startNode);
        }

        public uint GetRegisteredVehicleCount(ref ExtSegmentEnd end) {
            IExtVehicleManager vehStateManager = Constants.ManagerFactory.ExtVehicleManager;
            ushort vehicleId = end.firstVehicleId;
            uint ret = 0;
            int numIter = 0;

            while (vehicleId != 0) {
                ++ret;
                vehicleId = vehStateManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment;

                if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

            return ret;
        }

        public ArrowDirection GetDirection(ref ExtSegmentEnd sourceEnd, ushort targetSegmentId) {
            IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;
            if (!extSegMan.IsValid(sourceEnd.segmentId) || !extSegMan.IsValid(targetSegmentId)) {
                return ArrowDirection.None;
            }

            bool? targetStartNode = Services.NetService.IsStartNode(targetSegmentId, sourceEnd.nodeId);

            if (targetStartNode == null) {
                return ArrowDirection.None;
            }

            ref NetSegment sourceEndSegment = ref sourceEnd.segmentId.ToSegment();
            Vector3 sourceDir = sourceEnd.startNode
                ? sourceEndSegment.m_startDirection
                : sourceEndSegment.m_endDirection;

            ref NetSegment targetSegment = ref targetSegmentId.ToSegment();
            Vector3 targetDir = (bool)targetStartNode
                ? targetSegment.m_startDirection
                : targetSegment.m_endDirection;

            return CalculateArrowDirection(sourceDir, targetDir);
        }

        public ArrowDirection GetDirection(ushort segmentId0, ushort segmentId1, ushort nodeId = 0) {
            if (nodeId == 0) {
                ref NetSegment seg = ref Singleton<NetManager>.instance.m_segments.m_buffer[0];
                nodeId = seg.GetSharedNode(segmentId1);
                if(nodeId == 0) {
                    return ArrowDirection.None;
                }
            }
            GenericGameBridge.Service.INetService netService = Constants.ServiceFactory.NetService;
            ref ExtSegmentEnd segmenEnd0 = ref ExtSegmentEnds[GetIndex(segmentId0, nodeId)];
            ArrowDirection dir = GetDirection(ref segmenEnd0, segmentId1);
            return dir;
        }

        private ArrowDirection CalculateArrowDirection(Vector3 sourceDir, Vector3 targetDir) {
            sourceDir.y = 0;
            sourceDir.Normalize();

            targetDir.y = 0;
            targetDir.Normalize();
            float c = Vector3.Cross(sourceDir, targetDir).y;

            if (c >= 0.5f) {
                // [+30°, +150°]
                return ArrowDirection.Left;
            }

            if (c <= -0.5f) {
                // [-30°, -150°]
                return ArrowDirection.Right;
            }

            // Handle cases (-30°, +30°) / (-150°, -180°] / (+150°, +180°]
            float d = Vector3.Dot(sourceDir, targetDir);
            if (d > 0) {
                // (-30°, +30°)
                if (c > 0) {
                    // (0°, 30°]
                    return ArrowDirection.Left;
                }

                if (c < 0) {
                    // (0°, -30°]
                    return ArrowDirection.Right;
                }

                // [0°]
                return ArrowDirection.Turn;
            }

            // (-150°, -180°] / (+150°, +180°]
            return ArrowDirection.Forward;
        }

        private static Vector3 GetSegmentDir(ref NetSegment segment, bool startNode) {
            return startNode ? segment.m_startDirection : segment.m_endDirection;
        }

        public void Recalculate(ushort segmentId) {
            Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, true)]);
            Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, false)]);
        }

        public void Recalculate(ushort segmentId, bool startNode) {
            Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, startNode)]);
        }

        private void Recalculate(ref ExtSegmentEnd segEnd) {
            ushort segmentId = segEnd.segmentId;
            bool startNode = segEnd.startNode;

#if DEBUG
            bool logGeometry = DebugSwitch.GeometryDebug.Get();
#else
            const bool logGeometry = false;
#endif

            if (logGeometry) {
                Log._Debug($"ExtSegmentEndManager.Recalculate({segmentId}, {startNode}) called.");
            }

            ushort nodeIdBeforeRecalc = segEnd.nodeId;
            Reset(ref segEnd);

            if (!Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
                if (nodeIdBeforeRecalc != 0) {
                    Constants.ManagerFactory.ExtNodeManager.RemoveSegment(
                        nodeIdBeforeRecalc,
                        segmentId);
                }

                return;
            }

            ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, startNode);
            segEnd.nodeId = nodeId;
            CalculateIncomingOutgoing(segmentId, nodeId, out segEnd.incoming, out segEnd.outgoing);

            if (nodeIdBeforeRecalc != 0 && nodeIdBeforeRecalc != nodeId) {
                Constants.ManagerFactory.ExtNodeManager.RemoveSegment(
                    nodeIdBeforeRecalc,
                    segmentId);
            }

            Constants.ManagerFactory.ExtNodeManager.AddSegment(nodeId, segmentId);

            if (logGeometry) {
                Log.Info($"ExtSegmentEndManager.Recalculate({segmentId}, {startNode}): " +
                         $"Recalculated ext. segment end: {segEnd}");
            }
        }

        /// <summary>
        /// This recalcualtion must requires to be called after CalcualteSegment(). therefore it is not being called together
        /// with other calculations.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="startNode"></param>
        public void CalculateCorners(ushort segmentId, bool startNode) {
            if (!Shortcuts.netService.IsSegmentValid(segmentId))
                return;
            if (!segmentId.ToSegment().Info) {
                Log.Warning($"segment {segmentId} has null info");
                return;
            }

            try {
                ref ExtSegmentEnd segEnd = ref ExtSegmentEnds[GetIndex(segmentId, startNode)];
                segmentId.ToSegment().CalculateCorner(
                    segmentID: segmentId,
                    heightOffset: true,
                    start: startNode,
                    leftSide: false,
                    cornerPos: out segEnd.RightCorner,
                    cornerDirection: out segEnd.RightCornerDir,
                    smooth: out _);
                segmentId.ToSegment().CalculateCorner(
                    segmentID: segmentId,
                    heightOffset: true,
                    start: startNode,
                    leftSide: true,
                    cornerPos: out segEnd.LeftCorner,
                    cornerDirection: out segEnd.LeftCornerDir,
                    smooth: out _);
            } catch (Exception e) {
                Log.Error($"failed calculating corner for segment:{segmentId}, info={segmentId.ToSegment().Info}\n"
                    + e.Message);
            }
        }

        private void CalculateIncomingOutgoing(ushort segmentId,
                                               ushort nodeId,
                                               out bool incoming,
                                               out bool outgoing) {
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = instance.m_segments.m_buffer[segmentId].Info;

            var dir = NetInfo.Direction.Forward;

            if (instance.m_segments.m_buffer[segmentId].m_startNode == nodeId) {
                dir = NetInfo.Direction.Backward;
            }

            NetInfo.Direction dir2 =
                ((instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) ==
                 NetSegment.Flags.None)
                    ? dir
                    : NetInfo.InvertDirection(dir);

            var hasForward = false;
            var hasBackward = false;
            var isOutgoingOneWay = true;
            uint laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
            var laneIndex = 0;

            while (laneIndex < info.m_lanes.Length && laneId != 0u) {
                bool validLane =
                    (info.m_lanes[laneIndex].m_laneType &
                     (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) !=
                    NetInfo.LaneType.None &&
                    (info.m_lanes[laneIndex].m_vehicleType &
                     (ExtVehicleManager.VEHICLE_TYPES)) != VehicleInfo.VehicleType.None;
                // TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check
                if (validLane) {
                    if ((info.m_lanes[laneIndex].m_finalDirection & dir2) !=
                        NetInfo.Direction.None) {
                        isOutgoingOneWay = false;
                    }

                    if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) !=
                        NetInfo.Direction.None) {
                        hasForward = true;
                    }

                    if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) !=
                        NetInfo.Direction.None) {
                        hasBackward = true;
                    }
                }

                laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
                laneIndex++;
            }

            bool isOneway = !(hasForward && hasBackward);
            if (!isOneway) {
                isOutgoingOneWay = false;
            }

            incoming = (hasForward || hasBackward) && !isOutgoingOneWay;
            outgoing = (hasForward || hasBackward) && (isOutgoingOneWay || !isOneway);
        }

        public bool CalculateOnlyHighways(ushort segmentId, bool startNode) {
            ushort nodeId = Services.NetService.GetSegmentNodeId(segmentId, startNode);
#if DEBUG
            bool logGeometry = DebugSwitch.GeometryDebug.Get();
#else
            const bool logGeometry = false;
#endif
            if (logGeometry) {
                Log._Debug($"Checking if segment {segmentId} is connected to highways only " +
                           $"at node {nodeId}.");
            }

            bool hasOtherSegments = false;
            ref NetNode node = ref nodeId.ToNode();

            for (var s = 0; s < 8; s++) {
                ushort otherSegmentId = node.GetSegment(s);

                if (otherSegmentId == 0 || otherSegmentId == segmentId) {
                    continue;
                }

                hasOtherSegments = true;

                // determine geometry
                CalculateIncomingOutgoing(
                    otherSegmentId,
                    nodeId,
                    out bool otherIsIncoming,
                    out bool otherIsOutgoing);

                bool otherIsOneWay = otherIsIncoming ^ otherIsOutgoing;
                bool otherIsHighway =
                    Constants.ManagerFactory.ExtSegmentManager.CalculateIsHighway(otherSegmentId);

                if (logGeometry) {
                    Log._Debug(
                        $"Segment {segmentId} is connected to segment {otherSegmentId} at node " +
                        $"{nodeId}. otherIsOneWay={otherIsOneWay} otherIsIncoming={otherIsIncoming} " +
                        $"otherIsOutgoing={otherIsOutgoing} otherIsHighway={otherIsHighway}");
                }

                if (!otherIsHighway || !otherIsOneWay) {
                    return false;
                }
            }

            return hasOtherSegments;
        }

        public void CalculateOutgoingLeftStraightRightSegments(
            ref ExtSegmentEnd segEnd,
            ref NetNode node,
            out bool left,
            out bool straight,
            out bool right)
        {
            left = false;
            straight = false;
            right = false;

            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = node.GetSegment(i);
                if (otherSegmentId == 0 || otherSegmentId == segEnd.segmentId) {
                    continue;
                }

                var otherStartNode = Constants.ServiceFactory.NetService
                                                    .IsStartNode(otherSegmentId, segEnd.nodeId);
                if (otherStartNode == null) {
                    Log.Warning($"Incorrect ExtSegmentEnd.nodeId - data integrity problem! Segment {otherSegmentId} is not connected to Node {segEnd.nodeId}");
                    continue;
                }

                ExtSegmentEnd otherSegEnd =
                    ExtSegmentEnds[GetIndex(otherSegmentId, (bool)otherStartNode)];

                if (!otherSegEnd.outgoing) {
                    continue;
                }

                switch (GetDirection(ref segEnd, otherSegmentId)) {
                    case ArrowDirection.Left:
                        left = true;
                        break;
                    case ArrowDirection.Forward:
                        straight = true;
                        break;
                    case ArrowDirection.Right:
                        right = true;
                        break;
                }
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended segment end data:");

            for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                if (!Constants.ManagerFactory.ExtSegmentManager.IsValid((ushort)i)) {
                    continue;
                }

                Log._Debug($"Segment {i} @ start node: {ExtSegmentEnds[GetIndex((ushort)i, true)]}");
                Log._Debug($"Segment {i} @ end node: {ExtSegmentEnds[GetIndex((ushort)i, false)]}");
            }
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            Log._Debug($"ExtSegmentEndManager.OnLevelLoading: Calculating {ExtSegmentEnds.Length} " +
           "extended segment ends...");

            for (int i = 0; i < ExtSegmentEnds.Length; ++i) {
                // TODO [issue #872]: move CalculateCorners to Recalculate().
                CalculateCorners(ExtSegmentEnds[i].segmentId, ExtSegmentEnds[i].startNode);
            }
            Log._Debug($"ExtSegmentEndManager.OnLevelLoading: Calculation finished.");
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < ExtSegmentEnds.Length; ++i) {
                Reset(ref ExtSegmentEnds[i]);
            }
        }
    }
}
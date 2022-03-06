namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Lifecycle;

    public class LaneConnectionManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneConnection>>,
          ILaneConnectionManager
    {
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car
                                                             | VehicleInfo.VehicleType.Train
                                                             | VehicleInfo.VehicleType.Tram
                                                             | VehicleInfo.VehicleType.Metro
                                                             | VehicleInfo.VehicleType.Monorail
                                                             | VehicleInfo.VehicleType.Trolleybus;
        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        /// <summary>
        /// The startNode values in the order in which they occurred in the old <see cref="Flags"/> array.
        /// Retaining this order in all logic ported from that class helps to avoid breaking things.
        /// </summary>
        private static readonly bool[] compatibleNodeOrder = new[] { true, false };

        static LaneConnectionManager() {
            Instance = new LaneConnectionManager();
        }

        private LaneConnectionManager() {
            LaneConnections = new LaneConnection[NetManager.MAX_LANE_COUNT * 2];
        }

        private int GetIndex(uint laneId, bool startNode) {
            return (int)(laneId * 2) + (startNode ? 0 : 1);
        }

        private int GetIndex(uint laneId, ushort nodeId) {
            bool found = false;
            bool startNode = false;

            ref NetSegment segment = ref laneId.ToLane().m_segment.ToSegment();
            if (segment.m_startNode == nodeId) {
                found = true;
                startNode = true;
            } else if (segment.m_endNode == nodeId) {
                found = true;
            }

            if (!found) {
                Log.Warning(
                    $"LaneConnectionManager.GetIndex({laneId}, {nodeId}): Node is not " +
                    "connected to segment.");
                return -1;
            }

            return GetIndex(laneId, startNode);
        }

        public static LaneConnectionManager Instance { get; }

        private LaneConnection[] LaneConnections { get; }

        public LaneConnection this[uint laneId, bool startNode] {
            get => LaneConnections[GetIndex(laneId, startNode)];
            private set => LaneConnections[GetIndex(laneId, startNode)] = value;
        }

        private LaneConnection GetOrCreate(uint laneId, bool startNode) {
            return this[laneId, startNode] ??= new LaneConnection(laneId, startNode);
        }
        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Lane Connection data:");

            for (uint i = 0; i < NetManager.MAX_LANE_COUNT; ++i) {
                ref NetLane netLane = ref i.ToLane();
                var segmentId = netLane.m_segment;

                if (!netLane.IsValid()) {
                    continue;
                }

                var startNodeConnection = this[i, true];
                var endNodeConnection = this[i, false];

                if (startNodeConnection?.IsDefault() == false)
                    Log._Debug($"LaneConnection {i} on segment {segmentId} @ start node: {startNodeConnection}");

                if (endNodeConnection?.IsDefault() == false)
                    Log._Debug($"LaneConnection {i} on segment {segmentId} @ end node: {endNodeConnection}");
            }
        }

        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (!Options.laneConnectorEnabled) {
                return true;
            }

            if (targetLaneId == 0) {
                return false;
            }

            uint[] connectedLanes = this[sourceLaneId, sourceStartNode]?.connectedLaneIds;

            return connectedLanes?.Any(laneId => laneId == targetLaneId) == true;
        }

        /// <summary>
        /// determines whether or not the input lane is heading toward a start node.
        /// </summary>
        /// <returns>true if heading toward and start node.</returns>
        private bool IsHeadingTowardsStartNode(uint sourceLaneId) {
            ushort segmentId = sourceLaneId.ToLane().m_segment;
            ref NetSegment segment = ref segmentId.ToSegment();
            uint laneId = segment.m_lanes;
            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != 0;

            foreach (var laneInfo in segment.Info.m_lanes) {
                if (laneId == sourceLaneId) {
                    return (laneInfo.m_finalDirection == NetInfo.Direction.Forward) ^ !inverted;
                }
                laneId = laneId.ToLane().m_nextLane;
            }
            throw new Exception($"Unreachable code. sourceLaneId:{sourceLaneId}, segmentId:{segmentId} ");
        }

        public bool HasConnections(uint sourceLaneId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }
            return HasConnections(sourceLaneId, IsHeadingTowardsStartNode(sourceLaneId));
        }

        public bool HasConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            return this[sourceLaneId, startNode]?.connectedLaneIds != null;
        }

        /// <summary>
        /// Determines if there exist custom lane connections at the specified segment end
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="nodeId"></param>
        public bool HasSegmentConnections(ushort segmentId, ushort nodeId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            ref NetSegment netSegment = ref segmentId.ToSegment();
            foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                if (HasConnections(
                    laneIdAndIndex.laneId,
                    netSegment.m_startNode == nodeId)) {
                    return true;
                }
            }

            return false;
       }

        public bool HasNodeConnections(ushort nodeId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    if (HasSegmentConnections(segmentId, nodeId)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasUturnConnections(ushort segmentId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            NetManager netManager = Singleton<NetManager>.instance;

            uint sourceLaneId = segmentId.ToSegment().m_lanes;
            while (sourceLaneId != 0) {
                uint[] targetLaneIds = GetLaneConnections(sourceLaneId, startNode);

                if (targetLaneIds != null) {
                    foreach (uint targetLaneId in targetLaneIds) {
                        if (targetLaneId.ToLane().m_segment == segmentId) {
                            return true;
                        }
                    }
                }

                sourceLaneId = sourceLaneId.ToLane().m_nextLane;
            }

            return false;
        }

        [UsedImplicitly]
        internal int CountConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return 0;
            }

            return this[sourceLaneId, startNode]?.connectedLaneIds?.Length ?? 0;
        }

        /// <summary>
        /// Gets all lane connections for the given lane
        /// </summary>
        /// <param name="laneId"></param>
        /// <returns></returns>
        internal uint[] GetLaneConnections(uint laneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return null;
            }

            return this[laneId, startNode]?.connectedLaneIds;
        }

        /// <summary>
        /// Removes a lane connection between two lanes
        /// </summary>
        /// <param name="laneId1"></param>
        /// <param name="laneId2"></param>
        /// <param name="startNode1"></param>
        /// <returns></returns>
        internal bool RemoveLaneConnection(uint laneId1, uint laneId2, bool startNode1) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnection({laneId1}, {laneId2}, " +
                           $"{startNode1}) called.");
            }

            bool ret = RemoveConnectedLane(laneId1, laneId2, startNode1);

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnection({laneId1}, {laneId2}, " +
                           $"{startNode1}): ret={ret}");
            }

            if (!ret) {
                return ret;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            ushort segmentId1 = laneId1.ToLane().m_segment;
            ushort segmentId2 = laneId2.ToLane().m_segment;

            GetCommonNodeId(
                laneId1,
                laneId2,
                startNode1,
                out ushort commonNodeId,
                out bool startNode2);

            RecalculateLaneArrows(laneId1, commonNodeId, startNode1);
            RecalculateLaneArrows(laneId2, commonNodeId, startNode2);

            ref NetNode commonNode = ref commonNodeId.ToNode();
            RoutingManager.Instance.RequestNodeRecalculation(ref commonNode);

            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                extSegmentManager.PublishSegmentChanges(segmentId1);
                extSegmentManager.PublishSegmentChanges(segmentId2);
            }

            // at this point ret is always true
            return true;
        }

        /// <summary>
        /// Removes all lane connections at the specified node
        /// </summary>
        /// <param name="nodeId">Affected node</param>
        internal void RemoveLaneConnectionsFromNode(ushort nodeId) {
#if DEBUG
            if (DebugSwitch.LaneConnections.Get()) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnectionsFromNode({nodeId}) called.");
            }
#endif

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    RemoveLaneConnectionsFromSegment(segmentId, segmentId.ToSegment().m_startNode == nodeId);
                }
            }
        }

        /// <summary>
        /// Removes all lane connections at the specified segment end
        /// </summary>
        /// <param name="segmentId">Affected segment</param>
        /// <param name="startNode">Affected node of that segment</param>
        internal void RemoveLaneConnectionsFromSegment(ushort segmentId,
                                                       bool startNode,
                                                       bool recalcAndPublish = true) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif
            if (logLaneConnections) {
                Log._Debug(
                    $"LaneConnectionManager.RemoveLaneConnectionsFromSegment({segmentId}, " +
                    $"{startNode}) called.");
            }

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            foreach (LaneIdAndIndex laneIdAndIndex in extSegmentManager.GetSegmentLaneIdsAndLaneIndexes(segmentId)) {
                if (logLaneConnections) {
                    Log._Debug(
                        "LaneConnectionManager.RemoveLaneConnectionsFromSegment: Removing " +
                        $"lane connections from segment {segmentId}, lane {laneIdAndIndex.laneId}.");
                }

                RemoveLaneConnections(laneIdAndIndex.laneId, startNode, false);
            }

            if (recalcAndPublish) {
                RoutingManager.Instance.RequestRecalculation(segmentId);

                if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                    ExtSegmentManager.Instance.PublishSegmentChanges(segmentId);
                }
            }
        }

        /// <summary>
        /// Removes all lane connections from the specified lane
        /// </summary>
        /// <param name="laneId">Affected lane</param>
        /// <param name="startNode">Affected node</param>
        internal void RemoveLaneConnections(uint laneId,
                                            bool startNode,
                                            bool recalcAndPublish = true) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif
            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnections({laneId}, " +
                           $"{startNode}) called.");
            }

            if (this[laneId, startNode]?.connectedLaneIds == null) {
                return;
            }

            // NetManager netManager = Singleton<NetManager>.instance;

            /*for (int i = 0; i < Flags.laneConnections[laneId][nodeArrayIndex].Length; ++i) {
                    uint otherLaneId = Flags.laneConnections[laneId][nodeArrayIndex][i];
                    if (Flags.laneConnections[otherLaneId] != null) {
                            if ((Flags.laneConnections[otherLaneId][0] != null && Flags.laneConnections[otherLaneId][0].Length == 1 && Flags.laneConnections[otherLaneId][0][0] == laneId && Flags.laneConnections[otherLaneId][1] == null) ||
                                    Flags.laneConnections[otherLaneId][1] != null && Flags.laneConnections[otherLaneId][1].Length == 1 && Flags.laneConnections[otherLaneId][1][0] == laneId && Flags.laneConnections[otherLaneId][0] == null) {

                                    ushort otherSegmentId = otherLaneId.ToLane().m_segment;
                                    UnsubscribeFromSegmentGeometry(otherSegmentId);
                            }
                    }
            }*/

            RemoveConnectedLanes(laneId, startNode);

            if (recalcAndPublish) {
                ushort segment = laneId.ToLane().m_segment;
                RoutingManager.Instance.RequestRecalculation(segment);

                if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                    ExtSegmentManager.Instance.PublishSegmentChanges(segment);
                }
            }
        }

        /// <summary>
        /// Adds a lane connection between two lanes
        /// </summary>
        /// <param name="sourceLaneId">From lane id</param>
        /// <param name="targetLaneId">To lane id</param>
        /// <param name="sourceStartNode">The affected node</param>
        /// <returns></returns>
        internal bool AddLaneConnection(uint sourceLaneId,
                                        uint targetLaneId,
                                        bool sourceStartNode) {
            if (sourceLaneId == targetLaneId) {
                return false;
            }

            bool ret = AddConnectedLane(sourceLaneId, targetLaneId, sourceStartNode);

#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.AddLaneConnection({sourceLaneId}, " +
                           $"{targetLaneId}, {sourceStartNode}): ret={ret}");
            }

            if (!ret) {
                return false;
            }

            GetCommonNodeId(
                sourceLaneId,
                targetLaneId,
                sourceStartNode,
                out ushort commonNodeId,
                out bool targetStartNode);

            RecalculateLaneArrows(sourceLaneId, commonNodeId, sourceStartNode);
            RecalculateLaneArrows(targetLaneId, commonNodeId, targetStartNode);

            NetManager netManager = Singleton<NetManager>.instance;

            ushort sourceSegmentId = sourceLaneId.ToLane().m_segment;
            ushort targetSegmentId = targetLaneId.ToLane().m_segment;

            if (sourceSegmentId == targetSegmentId) {
                JunctionRestrictionsManager.Instance.SetUturnAllowed(
                    sourceSegmentId,
                    sourceStartNode,
                    true);
            }

            RoutingManager.Instance.RequestRecalculation(sourceSegmentId, false);
            RoutingManager.Instance.RequestRecalculation(targetSegmentId, false);

            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                extSegmentManager.PublishSegmentChanges(sourceSegmentId);
                extSegmentManager.PublishSegmentChanges(targetSegmentId);
            }

            // return ret, ret is true at this point
            return true;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif
            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.HandleInvalidSegment({seg.segmentId}): " +
                           "Segment has become invalid. Removing lane connections.");
            }

            RemoveLaneConnectionsFromSegment(seg.segmentId, false, false);
            RemoveLaneConnectionsFromSegment(seg.segmentId, true);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        /// <summary>
        /// Given two lane ids and node of the first lane, determines the node id to which both lanes are connected to
        /// </summary>
        /// <param name="laneId1">First lane</param>
        /// <param name="laneId2">Second lane</param>
        internal void GetCommonNodeId(uint laneId1,
                                      uint laneId2,
                                      bool startNode1,
                                      out ushort commonNodeId,
                                      out bool startNode2) {
            ref NetSegment netSegment1 = ref laneId1.ToLane().m_segment.ToSegment();
            ref NetSegment netSegment2 = ref laneId2.ToLane().m_segment.ToSegment();

            ushort nodeId2Start = netSegment2.m_startNode;
            ushort nodeId2End = netSegment2.m_endNode;

            ushort nodeId1 = startNode1
                ? netSegment1.m_startNode
                : netSegment1.m_endNode;

            startNode2 = nodeId1 == nodeId2Start;
            if (!startNode2 && nodeId1 != nodeId2End) {
                commonNodeId = 0;
            } else {
                commonNodeId = nodeId1;
            }
        }

        internal bool GetLaneEndPoint(ushort segmentId,
                                      bool startNode,
                                      byte laneIndex,
                                      uint? laneId,
                                      NetInfo.Lane laneInfo,
                                      out bool outgoing,
                                      out bool incoming,
                                      out Vector3? pos) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            pos = null;
            outgoing = false;
            incoming = false;

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return false;
            }

            if (laneId == null) {
                laneId = FindLaneId(segmentId, laneIndex);
                if (laneId == null) {
                    return false;
                }
            }

            ref NetLane netLane = ref ((uint)laneId).ToLane();

            if ((netLane.m_flags &
                 ((ushort)NetLane.Flags.Created | (ushort)NetLane.Flags.Deleted)) !=
                (ushort)NetLane.Flags.Created) {
                return false;
            }

            if (laneInfo == null) {
                if (laneIndex < netSegment.Info.m_lanes.Length) {
                    laneInfo = netSegment.Info.m_lanes[laneIndex];
                } else {
                    return false;
                }
            }

            NetInfo.Direction laneDir = ((netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
                    ? laneInfo.m_finalDirection
                    : NetInfo.InvertDirection(laneInfo.m_finalDirection);

            if (startNode) {
                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = netLane.m_bezier.a;
            } else {
                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = netLane.m_bezier.d;
            }

            return true;
        }

        private uint? FindLaneId(ushort segmentId, byte laneIndex) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            NetInfo.Lane[] lanes = netSegment.Info.m_lanes;
            uint laneId = netSegment.m_lanes;

            for (byte i = 0; i < lanes.Length && laneId != 0; i++) {
                if (i == laneIndex) {
                    return laneId;
                }

                laneId = laneId.ToLane().m_nextLane;
            }

            return null;
        }

        /// <summary>
        /// Recalculates lane arrows based on present lane connections.
        /// </summary>
        /// <param name="laneId">Affected lane</param>
        /// <param name="nodeId">Affected node</param>
        private void RecalculateLaneArrows(uint laneId, ushort nodeId, bool startNode) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif
            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}) called");
            }

            if (!Options.laneConnectorEnabled) {
                return;
            }

            if (!Flags.CanHaveLaneArrows(laneId, startNode)) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               $"lane {laneId}, startNode? {startNode} must not have lane arrows");
                }

                return;
            }

            if (!HasConnections(laneId, startNode)) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               $"lane {laneId} does not have outgoing connections");
                }

                return;
            }

            if (nodeId == 0) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "invalid node");
                }

                return;
            }

            var arrows = LaneArrows.None;
            ushort segmentId = laneId.ToLane().m_segment;

            if (segmentId == 0) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "invalid segment");
                }

                return;
            }

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                           $"startNode? {startNode}");
            }

            ref NetNode netNode = ref nodeId.ToNode();

            if (!netNode.IsValid()) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "Node is invalid");
                }

                return;
            }

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
            
            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = netNode.GetSegment(i);
                if (otherSegmentId != 0) {
                    //TODO move the following into a function
                    ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);

                    if (logLaneConnections) {
                        Log._Debug(
                            $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}. dir={dir}");
                    }

                    // check if arrow has already been set for this direction
                    switch (dir) {
                        case ArrowDirection.Turn: {
                                if (LHT) {
                                    if ((arrows & LaneArrows.Right) != LaneArrows.None) {
                                        continue;
                                    }
                                } else {
                                    if ((arrows & LaneArrows.Left) != LaneArrows.None) {
                                        continue;
                                    }
                                }

                                break;
                            }

                        case ArrowDirection.Forward: {
                                if ((arrows & LaneArrows.Forward) != LaneArrows.None) {
                                    continue;
                                }

                                break;
                            }

                        case ArrowDirection.Left: {
                                if ((arrows & LaneArrows.Left) != LaneArrows.None) {
                                    continue;
                                }

                                break;
                            }

                        case ArrowDirection.Right: {
                                if ((arrows & LaneArrows.Right) != LaneArrows.None) {
                                    continue;
                                }

                                break;
                            }

                        default: {
                                continue;
                            }
                    }

                    if (logLaneConnections) {
                        Log._Debug(
                            $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}: need to determine arrows");
                    }

                    bool addArrow = false;
                    uint curLaneId = otherSegmentId.ToSegment().m_lanes;

                    while (curLaneId != 0) {
                        if (logLaneConnections) {
                            Log._Debug(
                                $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                                $"processing connected segment {otherSegmentId}: checking lane {curLaneId}");
                        }

                        if (AreLanesConnected(laneId, curLaneId, startNode)) {
                            if (logLaneConnections) {
                                Log._Debug(
                                    $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                                    $"processing connected segment {otherSegmentId}: checking lane " +
                                    $"{curLaneId}: lanes are connected");
                            }

                            addArrow = true;
                            break;
                        }

                        curLaneId = curLaneId.ToLane().m_nextLane;
                    }

                    if (logLaneConnections) {
                        Log._Debug(
                            $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}: finished processing " +
                            $"lanes. addArrow={addArrow} arrows (before)={arrows}");
                    }

                    if (!addArrow) {
                        continue;
                    }

                    switch (dir) {
                        case ArrowDirection.Turn: {
                                if (LHT) {
                                    arrows |= LaneArrows.Right;
                                } else {
                                    arrows |= LaneArrows.Left;
                                }

                                break;
                            }

                        case ArrowDirection.Forward: {
                                arrows |= LaneArrows.Forward;
                                break;
                            }

                        case ArrowDirection.Left: {
                                arrows |= LaneArrows.Left;
                                break;
                            }

                        case ArrowDirection.Right: {
                                arrows |= LaneArrows.Right;
                                break;
                            }

                        default: {
                                continue;
                            }
                    }

                    if (logLaneConnections) {
                        Log._Debug(
                            $"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}: arrows={arrows}");
                    }
                }
            }

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                           $"setting lane arrows to {arrows}");
            }

            LaneArrowManager.Instance.SetLaneArrows(laneId, arrows, true);
        }

        public bool LoadData(List<Configuration.LaneConnection> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} lane connections");

            foreach (Configuration.LaneConnection conn in data) {
                try {
                    ref NetLane lowerLane = ref conn.lowerLaneId.ToLane();
                    if (!lowerLane.IsValidWithSegment()) {
                        continue;
                    }

                    ref NetLane higherLane = ref conn.higherLaneId.ToLane();
                    if (!higherLane.IsValidWithSegment()) {
                        continue;
                    }

                    if (conn.lowerLaneId == conn.higherLaneId) {
                        continue;
                    }

#if DEBUGLOAD
                    Log._Debug($"Loading lane connection: lane {conn.lowerLaneId} -> {conn.higherLaneId}");
#endif
                    AddLaneConnection(conn.lowerLaneId, conn.higherLaneId, conn.lowerStartNode);
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Error($"Error loading data from lane connection: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.LaneConnection> SaveData(ref bool success) {
            var ret = new List<Configuration.LaneConnection>();

            for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
                try {
                    if (Flags.CheckLane(i)) {
                        foreach (bool startNode in compatibleNodeOrder) {

                            uint[] connectedLaneIds = this[i, startNode]?.connectedLaneIds;

                            if (connectedLaneIds == null) {
                                continue;
                            }

                            // The code below is equivalent to LINQ
                            //-------------------------------------------------------------
                            // ret.AddRange(
                            //     from otherHigherLaneId in connectedLaneIds
                            //     where otherHigherLaneId > i
                            //     where otherHigherLaneId.ToLane().IsValid()
                            //     select new Configuration.LaneConnection(i, otherHigherLaneId, startNode));
                            //-------------------------------------------------------------
                            foreach (uint otherHigherLaneId in connectedLaneIds) {
                                if (otherHigherLaneId <= i) {
                                    continue;
                                }

                                ref NetLane otherHigherLane = ref otherHigherLaneId.ToLane();
                                if (!otherHigherLane.IsValidWithSegment()) {
                                    continue;
                                }

    #if DEBUGSAVE
                                Log._Debug($"Saving lane connection: lane {i} -> {otherHigherLaneId}");
    #endif
                                ret.Add(
                                    new Configuration.LaneConnection(
                                        i,
                                        otherHigherLaneId,
                                        startNode));
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane data @ {i}: {e.ToString()}");
                    success = false;
                }
            }

            return ret;
        }

        /// <summary>
        /// Removes lane connections that point from lane <paramref name="sourceLaneId"/> to lane
        /// <paramref name="targetLaneId"/> at node <paramref name="startNode"/>.
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <param name="targetLaneId"></param>
        /// <param name="startNode"></param>
        /// <returns></returns>
        private bool RemoveSingleConnectedLane(uint sourceLaneId,
                                                uint targetLaneId,
                                                bool startNode) {
#if DEBUGFLAGS
            Log._Debug(
                $"LaneConnectionManager.RemoveSingleConnectedLane({sourceLaneId}, {targetLaneId}, {startNode}) called.");
#endif
            var laneConnection = this[sourceLaneId, startNode];
            uint[] srcLaneIds = laneConnection?.connectedLaneIds;
            if (srcLaneIds == null)
                return false;

            bool ret = false;
            int remainingConnections = 0;
            for (int i = 0; i < srcLaneIds.Length; ++i) {
                if (srcLaneIds[i] != targetLaneId) {
                    ++remainingConnections;
                } else {
                    ret = true;
                    srcLaneIds[i] = 0;
                }
            }

            if (remainingConnections <= 0) {
                laneConnection.connectedLaneIds = null;
                return ret;
            }

            if (remainingConnections != srcLaneIds.Length) {
                laneConnection.connectedLaneIds = new uint[remainingConnections];
                int k = 0;
                for (int i = 0; i < srcLaneIds.Length; ++i) {
                    if (srcLaneIds[i] == 0)
                        continue;
                    laneConnection.connectedLaneIds[k++] = srcLaneIds[i];
                }
            }

            return ret;
        }

        /// <summary>
        /// Removes any lane connections that exist between two given lanes
        /// </summary>
        /// <param name="lane1Id"></param>
        /// <param name="lane2Id"></param>
        /// <param name="startNode1"></param>
        /// <returns></returns>
        private bool RemoveConnectedLane(uint lane1Id, uint lane2Id, bool startNode1) {
#if DEBUG
            bool debug = DebugSwitch.LaneConnections.Get();
            if (debug) {
                Log._Debug($"LaneConnectionManager.RemoveConnectedLane({lane1Id}, {lane2Id}, {startNode1}) called.");
            }
#endif
            bool lane1Valid = Flags.CheckLane(lane1Id);
            bool lane2Valid = Flags.CheckLane(lane2Id);

            bool ret = false;

            if (!lane1Valid) {
                // remove all incoming/outgoing lane connections
                RemoveConnectedLanes(lane1Id);
                ret = true;
            }

            if (!lane2Valid) {
                // remove all incoming/outgoing lane connections
                RemoveConnectedLanes(lane2Id);
                ret = true;
            }

            if (lane1Valid || lane2Valid) {
                GetCommonNodeId(
                    lane1Id,
                    lane2Id,
                    startNode1,
                    out ushort commonNodeId,
                    out bool startNode2); // TODO refactor
                if (commonNodeId == 0) {
                    Log.Warning($"LaneConnectionManager.RemoveConnectedLane({lane1Id}, {lane2Id}, {startNode1}): " +
                                $"Could not identify common node between lanes {lane1Id} and {lane2Id}");
                }

                if (RemoveSingleConnectedLane(lane1Id, lane2Id, startNode1)) {
                    ret = true;
                }

                if (RemoveSingleConnectedLane(lane2Id, lane1Id, startNode2)) {
                    ret = true;
                }
            }

#if DEBUG
            if (debug) {
                Log._Debug($"LaneConnectionManager.RemoveConnectedLane({lane1Id}, {lane2Id}, {startNode1}). ret={ret}");
            }
#endif
            return ret;
        }

        /// <summary>
        /// Removes all incoming/outgoing lane connections of the given lane
        /// </summary>
        /// <param name="laneId"></param>
        /// <param name="startNode"></param>
        private void RemoveConnectedLanes(uint laneId, bool? startNode = null) {
#if DEBUG
            bool debug = DebugSwitch.LaneConnections.Get();
            if (debug) {
                Log._Debug($"LaneConnectionManager.RemoveConnectedLanes({laneId}, {startNode}) called.");
            }
#endif
            bool laneValid = Flags.CheckLane(laneId);
            bool clearBothSides = startNode == null || !laneValid;
#if DEBUG
            if (debug) {
                Log._Debug($"LaneConnectionManager.RemoveConnectedLanes({laneId}, {startNode}): laneValid={laneValid}, " +
                           $"clearBothSides={clearBothSides}");
            }
#endif
            foreach (bool startNode1 in compatibleNodeOrder) {
                if (startNode.HasValue && startNode1 != startNode.Value) {
                    continue;
                }

                var laneConnection = this[laneId, startNode1];

                if (laneConnection?.connectedLaneIds == null) {
                    continue;
                }

                for (int i = 0; i < laneConnection.connectedLaneIds.Length; ++i) {
                    uint otherLaneId = laneConnection.connectedLaneIds[i];
                    GetCommonNodeId(
                        laneId,
                        otherLaneId,
                        startNode1,
                        out ushort commonNodeId,
                        out bool startNode2); // TODO refactor

                    if (commonNodeId == 0) {
                        Log.Warning($"LaneConnectionManager.RemoveConnectedLanes({laneId}, {startNode}): Could " +
                                    $"not identify common node between lanes {laneId} and {otherLaneId}");
                    }

                    RemoveSingleConnectedLane(otherLaneId, laneId, startNode2);
                }

                laneConnection.connectedLaneIds = null;
            }
        }

        /// <summary>
        /// adds lane connections between two given lanes
        /// </summary>
        /// <param name="lane1Id"></param>
        /// <param name="lane2Id"></param>
        /// <param name="startNode1"></param>
        /// <returns></returns>
        private bool AddConnectedLane(uint lane1Id, uint lane2Id, bool startNode1) {
            bool lane1Valid = Flags.CheckLane(lane1Id);
            bool lane2Valid = Flags.CheckLane(lane2Id);

            if (!lane1Valid) {
                // remove all incoming/outgoing lane connections
                RemoveConnectedLanes(lane1Id);
            }

            if (!lane2Valid) {
                // remove all incoming/outgoing lane connections
                RemoveConnectedLanes(lane2Id);
            }

            if (!lane1Valid || !lane2Valid) {
                return false;
            }

            GetCommonNodeId(
                lane1Id,
                lane2Id,
                startNode1,
                out ushort commonNodeId,
                out bool startNode2); // TODO refactor

            if (commonNodeId != 0) {
                CreateConnectedLane(lane1Id, lane2Id, startNode1);
                CreateConnectedLane(lane2Id, lane1Id, startNode2);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a lane connection from lane <paramref name="sourceLaneId"/> to lane <paramref name="targetLaneId"/> at node <paramref name="startNode"/>
        /// Assumes that both lanes are valid.
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <param name="targetLaneId"></param>
        /// <param name="startNode"></param>
        private void CreateConnectedLane(uint sourceLaneId,
                                            uint targetLaneId,
                                            bool startNode) {

            var laneConnection = GetOrCreate(sourceLaneId, startNode);

            if (laneConnection.connectedLaneIds == null) {
                laneConnection.connectedLaneIds = new uint[] { targetLaneId };
                return;
            }

            uint[] oldConnections = laneConnection.connectedLaneIds;
            laneConnection.connectedLaneIds = new uint[oldConnections.Length + 1];
            Array.Copy(
                oldConnections,
                laneConnection.connectedLaneIds,
                oldConnections.Length);
            laneConnection.connectedLaneIds[oldConnections.Length] = targetLaneId;
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < LaneConnections.Length; ++i)
                LaneConnections[i] = null;
        }
    }
}

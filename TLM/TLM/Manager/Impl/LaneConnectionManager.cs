namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.Manager.Impl.LaneConnectionManagerData;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
    using static TrafficManager.Util.Shortcuts;

    public class LaneConnectionManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneConnection>>,
          ILaneConnectionManager {
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car
                                                             | VehicleInfo.VehicleType.Train
                                                             | VehicleInfo.VehicleType.Tram
                                                             | VehicleInfo.VehicleType.Metro
                                                             | VehicleInfo.VehicleType.Monorail
                                                             | VehicleInfo.VehicleType.Trolleybus;
        private ConnectionData connections_;
        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        static LaneConnectionManager() {
            Instance = new LaneConnectionManager();
        }

        public static LaneConnectionManager Instance { get; }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            connections_ = new ConnectionData();
        }
        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            connections_ = null;
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            connections_.PrintDebugInfo();
        }

        /// <summary>
        /// check if lane is valid. if invalid, lane connections are removed and returns false.
        /// </summary>
        private bool ValidateLane(uint laneId) {
            bool valid = laneId.ToLane().IsValidWithSegment();
            if (!valid)
                connections_.RemoveConnections(laneId);
            return valid;
        }

        /// <summary>
        /// Checks if traffic may flow from source lane to target lane according to setup lane connections
        /// </summary>
        /// <param name="sourceStartNode">check at start node of source lane?</param>
        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (!Options.laneConnectorEnabled) {
                return true;
            }

            bool valid = ValidateLane(sourceLaneId) & ValidateLane(targetLaneId);
            if (!valid) {
                return false;
            }

            return connections_.IsConnectedTo(sourceLaneId, targetLaneId, sourceStartNode);
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

        public bool HasOutgoingConnections(uint sourceLaneId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            return HasOutgoingConnections(sourceLaneId, IsHeadingTowardsStartNode(sourceLaneId));
        }

        /// <summary>
        /// Determines if the given lane has incoming/outgoing connections
        /// </summary>
        public bool HasConnections(uint laneId, bool startNode) =>
            connections_.GetConnections(laneId, startNode) != null;

        /// <summary>
        /// Determines if the given lane has outgoing connections
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <returns></returns>
        public bool HasOutgoingConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            var targets = connections_.GetConnections(sourceLaneId, startNode);
            int n = targets?.Length ?? 0;
            for (int i = 0; i < n; ++i) {
                if (targets[i].Enabled)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Determines if there exist custom lane connections at the specified node
        /// </summary>
        /// <param name="nodeId"></param>
        public bool HasNodeConnections(ushort nodeId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    bool startNode = netSegment.IsStartnode(nodeId);
                    foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                        if (connections_.GetConnections(laneIdAndIndex.laneId, startNode) != null)
                            return true;
                    }
                }
            }

            return false;
        }

        // Note: Not performance critical
        public bool HasUturnConnections(ushort segmentId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

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

        /// <summary>
        /// Gets all lane connections for the given lane
        /// Note: Not performance critical
        /// </summary>
        internal uint[] GetLaneConnections(uint laneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return null;
            }

            if (!ValidateLane(laneId)) {
                return null;
            }

            return connections_.GetConnections(laneId, startNode)
                ?.Where(item => item.Enabled)
                ?.Select(item => item.LaneId)
                .ToArray();
        }

        /// <summary>
        /// Removes the lane connection from source lane to target lane.
        /// </summary>
        internal bool RemoveLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnection({sourceLaneId}, {targetLaneId}, " +
                           $"{sourceStartNode}) called.");
            }

            bool valid = ValidateLane(sourceLaneId) & ValidateLane(targetLaneId);
            if (!valid) {
                return false;
            }

            ushort sourceSegmentId = sourceLaneId.ToLane().m_segment;
            ushort targetSegmentId = targetLaneId.ToLane().m_segment;
            ushort nodeId = sourceSegmentId.ToSegment().GetNodeId(sourceStartNode);
            var result = connections_.Disconnect(sourceLaneId, targetLaneId, nodeId);

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnection({sourceLaneId}, {targetLaneId}, " +
                           $"{sourceStartNode}): ret={result}");
            }

            if (!result) {
                return false;
            }

            RecalculateLaneArrows(sourceLaneId, nodeId, sourceStartNode);

            ref NetNode node = ref nodeId.ToNode();
            RoutingManager.Instance.RequestNodeRecalculation(ref node);

            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                extSegmentManager.PublishSegmentChanges(sourceSegmentId);
                extSegmentManager.PublishSegmentChanges(targetSegmentId);
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
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    bool startNode = netSegment.IsStartnode(nodeId);
                    foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                        connections_.GetConnections(laneIdAndIndex.laneId, startNode) = null;
                    }
                }
            }

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
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

            connections_.GetConnections(laneId, startNode) = null;

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
        internal bool AddLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (sourceLaneId == targetLaneId) {
                return false;
            }

            bool valid = ValidateLane(sourceLaneId) & ValidateLane(targetLaneId);
            if (!valid) {
                return false;
            }

            ushort sourceSegmentId = sourceLaneId.ToLane().m_segment;
            ushort targetSegmentId = targetLaneId.ToLane().m_segment;
            ushort nodeId = sourceSegmentId.ToSegment().GetNodeId(sourceStartNode);
            connections_.ConnectTo(sourceLaneId, targetLaneId, nodeId);
            Assert(AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode), $"AreLanesConnected({sourceLaneId}, {targetLaneId}, {sourceStartNode})");

#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.AddLaneConnection({sourceLaneId}, " +
                           $"{targetLaneId}, {sourceStartNode})");
            }



            RecalculateLaneArrows(sourceLaneId, nodeId, sourceStartNode);

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
            HandleInvalidSegmentImpl(seg.segmentId);
        }

        private void HandleInvalidSegmentImpl(ushort segmentId) {
#if DEBUG
            bool logLaneConnections = DebugSwitch.LaneConnections.Get();
#else
            const bool logLaneConnections = false;
#endif
            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.HandleInvalidSegment({segmentId}): " +
                           "Segment has become invalid. Removing lane connections.");
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();
            foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                connections_.RemoveConnections(laneIdAndIndex.laneId);
            }

            RoutingManager.Instance.RequestRecalculation(segmentId);
            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager.Instance.PublishSegmentChanges(segmentId);
            }
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

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

            if (!HasOutgoingConnections(laneId, startNode)) {
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
                    ref NetLane lowerLane = ref conn.sourceLaneId.ToLane();
                    if (!lowerLane.IsValidWithSegment()) {
                        continue;
                    }

                    ref NetLane higherLane = ref conn.targetLaneId.ToLane();
                    if (!higherLane.IsValidWithSegment()) {
                        continue;
                    }

                    if (conn.sourceLaneId == conn.targetLaneId) {
                        continue;
                    }

#if DEBUGLOAD
                    Log._Debug($"Loading lane connection: lane {conn.sourceLaneId} -> {conn.targetLaneId}");
#endif
                    AddLaneConnection(conn.sourceLaneId, conn.targetLaneId, conn.sourceStartNode);
                    if (conn.Legacy) {
                        ushort segmentId = conn.sourceLaneId.ToLane().m_segment;
                        ushort nodeId = segmentId.ToSegment().GetNodeId(conn.sourceStartNode);
                        bool targetStartNode = conn.targetLaneId.ToLane().IsStartNode(nodeId);
                        AddLaneConnection(conn.targetLaneId, conn.sourceLaneId, targetStartNode);
                    }
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Error($"Error loading data from lane connection: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.LaneConnection> SaveData(ref bool success) {
            var ret = new List<Configuration.LaneConnection>();

            for (uint sourceLaneId = 1; sourceLaneId < NetManager.instance.m_lanes.m_size; sourceLaneId++) {
                try {
                    foreach (bool startNode in new[] { false, true }) {
                        var targets = connections_.GetConnections(sourceLaneId, startNode);
                        if (targets == null) {
                            continue;
                        }

                        foreach (var target in targets) {
                            if (!ValidateLane(target.LaneId)) {
                                continue;
                            }
#if DEBUGSAVE 
                            Log._Debug($"Saving lane connection: lane {sourceLaneId} -> {target}");
#endif
                            ret.Add(
                                new Configuration.LaneConnection(
                                    sourceLaneId,
                                    target.LaneId,
                                    startNode));
                        }
                    }
                } catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane data @ {sourceLaneId}: {e.ToString()}");
                    success = false;
                }
            }

            return ret;
        }
    }
}
namespace TrafficManager.Manager.Impl.LaneConnection {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;
    using TrafficManager.Util.Extensions;
    using static TrafficManager.Util.Shortcuts;
    using TrafficManager.Util;
    using TrafficManager.Patch;
#if DEBUG
    using TrafficManager.State.ConfigData;
#endif

    public class LaneConnectionSubManager :
        AbstractCustomManager,
          ICustomDataManager<List<Configuration.LaneConnection>>, ILaneConnectionManager {
        private ConnectionDataBase connectionDataBase_;

#pragma warning disable RAS0002 // Readonly field for a non-readonly struct
        public readonly LaneEndTransitionGroup Group;
        public readonly NetInfo.LaneType laneTypes_;
        public readonly VehicleInfo.VehicleType vehicleTypes_;
#pragma warning restore RAS0002 // Readonly field for a non-readonly struct

#if DEBUG
        private bool verbose_ => DebugSwitch.LaneConnections.Get();
#else
        private const bool verbose_ = false;
#endif

        internal LaneConnectionSubManager(LaneEndTransitionGroup group) {
            Group = group;
            laneTypes_ = default;
            vehicleTypes_ = default;
            if (Group == LaneEndTransitionGroup.Road) {
                laneTypes_ |= TrackUtils.ROAD_LANE_TYPES;
                vehicleTypes_ |= TrackUtils.ROAD_VEHICLE_TYPES;
            }
            if (Group == LaneEndTransitionGroup.Track) {
                laneTypes_ |= TrackUtils.TRACK_LANE_TYPES;
                vehicleTypes_ |= TrackUtils.TRACK_VEHICLE_TYPES;
            }
            NetManagerEvents.Instance.ReleasingSegment += ReleasingSegment;
        }

        public NetInfo.LaneType LaneTypes => laneTypes_;

        public VehicleInfo.VehicleType VehicleTypes => vehicleTypes_;

        /// <summary>
        /// tests if the input group is supported by this sub-manager.
        /// </summary>
        public bool Supports(LaneEndTransitionGroup group) => (group & Group) != 0;

        public bool Supports(NetInfo.Lane laneInfo) => laneInfo.Matches(laneTypes_, vehicleTypes_);

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            connectionDataBase_ = new ConnectionDataBase();
        }
        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            connectionDataBase_ = null;
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.Info($"Group={Group}");
            connectionDataBase_.PrintDebugInfo();
        }

        /// <summary>
        /// check if lane is valid. if invalid, lane connections are removed and returns false.
        /// </summary>
        private bool ValidateLane(uint laneId) {
            bool valid = laneId.ToLane().IsValidWithSegment();
            if (!valid)
                connectionDataBase_.RemoveConnections(laneId);
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

            return connectionDataBase_.IsConnectedTo(sourceLaneId, targetLaneId, sourceStartNode);
        }

        /// <summary>
        /// determines whether or not the input lane is heading toward a start node.
        /// Precondition: should only be used for uni-directional lanes with lane arrows.
        /// Note: not performance critical
        /// </summary>
        /// <returns>true if heading toward and start node.</returns>
        private bool IsHeadingTowardsStartNode(uint sourceLaneId) {
            ushort segmentId = sourceLaneId.ToLane().m_segment;
            ref NetSegment segment = ref segmentId.ToSegment();
            uint laneId = segment.m_lanes;
            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != 0;

            foreach (var laneInfo in segment.Info.m_lanes) {
                if (laneId == sourceLaneId) {
                    Assert(laneInfo.m_finalDirection is NetInfo.Direction.Forward or NetInfo.Direction.Backward);
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
        /// Performance note: This act as HasOutgoingConnections for uni-directional lanes but faster
        /// </summary>
        public bool HasConnections(uint laneId, bool startNode) =>
            Options.laneConnectorEnabled && connectionDataBase_.ContainsKey(new LaneEnd(laneId, startNode));

        /// <summary>
        /// Determines if the given lane has outgoing connections
        /// Performance note: This act as HasOutgoingConnections for uni-directional lanes but faster
        /// </summary>
        public bool HasOutgoingConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            LaneEnd key = new(sourceLaneId, startNode);
            if (connectionDataBase_.TryGetValue(key, out var targets)) {
                int n = targets.Length;
                for (int i = 0; i < n; ++i) {
                    if (targets[i].Enabled)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines if there exist custom lane connections at the specified node
        /// </summary>
        public bool HasNodeConnections(ushort nodeId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    bool startNode = netSegment.IsStartNode(nodeId);
                    foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                        LaneEnd key = new(laneIdAndIndex.laneId, startNode);
                        if (connectionDataBase_.ContainsKey(key)) {
                            return true;
                        }
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

            LaneEnd key = new(laneId, startNode);
            if (connectionDataBase_.TryGetValue(key, out var targets)) {
                return targets
                .Where(item => item.Enabled)
                .Select(item => item.LaneId)
                .ToArray();
            }
            return null;
        }

        /// <summary>
        /// Removes the lane connection from source lane to target lane.
        /// </summary>
        internal bool RemoveLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RemoveLaneConnection({sourceLaneId}, {targetLaneId}, " +
                           $"{sourceStartNode}) called.");
            }

            bool valid = ValidateLane(sourceLaneId) & ValidateLane(targetLaneId);
            if (!valid) {
                return false;
            }

            ushort sourceSegmentId = sourceLaneId.ToLane().m_segment;
            ushort targetSegmentId = targetLaneId.ToLane().m_segment;
            ushort nodeId = sourceSegmentId.ToSegment().GetNodeId(sourceStartNode);
            var result = connectionDataBase_.Disconnect(sourceLaneId, targetLaneId, nodeId);

            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RemoveLaneConnection({sourceLaneId}, {targetLaneId}, " +
                           $"{sourceStartNode}): ret={result}");
            }

            if (!result) {
                return false;
            }

            if (Supports(LaneEndTransitionGroup.Road)) {
                RecalculateLaneArrows(sourceLaneId, nodeId, sourceStartNode);
            }

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
            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RemoveLaneConnectionsFromNode({nodeId}) called.");
            }

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    bool startNode = netSegment.IsStartNode(nodeId);
                    foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                        LaneEnd key = new(laneIdAndIndex.laneId, startNode);
                        connectionDataBase_.Remove(key);
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
            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RemoveLaneConnections({laneId}, " +
                           $"{startNode}) called.");
            }

            LaneEnd key = new(laneId, startNode);
            connectionDataBase_.Remove(key);

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
        /// <returns>true if any connection was added</returns>
        internal bool AddLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (sourceLaneId == targetLaneId) {
                return false;
            }

            bool valid = ValidateLane(sourceLaneId) & ValidateLane(targetLaneId);
            if (!valid) {
                return false;
            }

            var sourceLaneInfo = ExtLaneManager.Instance.GetLaneInfo(sourceLaneId);
            var targetLaneInfo = ExtLaneManager.Instance.GetLaneInfo(targetLaneId);
            ref NetLane sourceNetLane = ref sourceLaneId.ToLane();
            ref NetLane targetNetLane = ref targetLaneId.ToLane();
            bool canConnect = Supports(sourceLaneInfo) && Supports(targetLaneInfo);
            if (!canConnect) {
                return false;
            }

            ushort sourceSegmentId = sourceLaneId.ToLane().m_segment;
            ushort targetSegmentId = targetLaneId.ToLane().m_segment;
            ushort nodeId = sourceSegmentId.ToSegment().GetNodeId(sourceStartNode);
            ref NetNode netNode = ref nodeId.ToNode();

            // check if source lane goes toward the node
            // and target lane goes away from the node.
            static bool IsDirectionValid(ref NetLane lane, NetInfo.Lane laneInfo, ushort nodeId, bool source) {
                bool invert = lane.m_segment.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                bool startNode = lane.IsStartNode(nodeId);
                var dir = laneInfo.m_finalDirection;
                if (source ^ startNode ^ invert) {
                    return dir.IsFlagSet(NetInfo.Direction.Forward);
                } else {
                    return dir.IsFlagSet(NetInfo.Direction.Backward);
                }
            }
            canConnect =
                IsDirectionValid(ref sourceNetLane, sourceLaneInfo, nodeId, true) &&
                IsDirectionValid(ref targetNetLane, targetLaneInfo, nodeId, false);
            if (!canConnect) {
                return false;
            }

            if (Group == LaneEndTransitionGroup.Track) {
                bool targetStartnode = targetSegmentId.ToSegment().IsStartNode(nodeId);
                canConnect = LaneConnectionManager.CheckSegmentsTurningAngle(
                    sourceSegmentId, sourceStartNode, targetSegmentId, targetStartnode);
                if (!canConnect) {
                    return false;
                }
            }

            connectionDataBase_.ConnectTo(sourceLaneId, targetLaneId, nodeId);
            Assert(AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode), $"AreLanesConnected({sourceLaneId}, {targetLaneId}, {sourceStartNode})");

            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).AddLaneConnection({sourceLaneId}, " +
                           $"{targetLaneId}, {sourceStartNode})");
            }

            if (Supports(LaneEndTransitionGroup.Road)) {
                RecalculateLaneArrows(sourceLaneId, nodeId, sourceStartNode);
            }

            if (sourceSegmentId == targetSegmentId) {
                JunctionRestrictionsManager.Instance.SetUturnAllowed(
                    sourceSegmentId,
                    sourceStartNode,
                    true);
            }

            RoutingManager.Instance.RequestNodeRecalculation(ref netNode);

            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
                extSegmentManager.PublishSegmentChanges(sourceSegmentId);
                extSegmentManager.PublishSegmentChanges(targetSegmentId);
            }

            // return ret, ret is true at this point
            return true;
        }

        private void ReleasingSegment(ushort segmentId, ref NetSegment segment) {
            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).ReleasingSegment({segmentId}, isValid={segment.IsValid()}): " +
                           "Segment is about to become invalid. Removing lane connections.");
            }

            foreach (LaneIdAndIndex laneIdAndIndex in segment.GetSegmentLaneIdsAndLaneIndexes()) {
                connectionDataBase_.RemoveConnections(laneIdAndIndex.laneId);
            }

            RoutingManager.Instance.RequestRecalculation(segmentId);
            if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                ExtSegmentManager.Instance.PublishSegmentChanges(segmentId);
            }
        }

        /// <summary>
        /// Recalculates lane arrows based on present lane connections.
        /// </summary>
        /// <param name="laneId">Affected lane</param>
        /// <param name="nodeId">Affected node</param>
        private void RecalculateLaneArrows(uint laneId, ushort nodeId, bool startNode) {
            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}) called");
            }

            if (!Options.laneConnectorEnabled) {
                return;
            }

            if (!Flags.CanHaveLaneArrows(laneId, startNode)) {
                if (verbose_) {
                    Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                               $"lane {laneId}, startNode? {startNode} must not have lane arrows");
                }

                return;
            }

            if (!HasOutgoingConnections(laneId, startNode)) {
                if (verbose_) {
                    Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                               $"lane {laneId} does not have outgoing connections");
                }

                return;
            }

            if (nodeId == 0) {
                if (verbose_) {
                    Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "invalid node");
                }

                return;
            }

            var arrows = LaneArrows.None;
            ushort segmentId = laneId.ToLane().m_segment;

            if (segmentId == 0) {
                if (verbose_) {
                    Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "invalid segment");
                }

                return;
            }

            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                           $"startNode? {startNode}");
            }

            ref NetNode netNode = ref nodeId.ToNode();

            if (!netNode.IsValid()) {
                if (verbose_) {
                    Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
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

                    if (verbose_) {
                        Log._Debug(
                            $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
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

                    if (verbose_) {
                        Log._Debug(
                            $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}: need to determine arrows");
                    }

                    bool addArrow = false;
                    uint curLaneId = otherSegmentId.ToSegment().m_lanes;

                    while (curLaneId != 0) {
                        if (verbose_) {
                            Log._Debug(
                                $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                                $"processing connected segment {otherSegmentId}: checking lane {curLaneId}");
                        }

                        if (AreLanesConnected(laneId, curLaneId, startNode)) {
                            if (verbose_) {
                                Log._Debug(
                                    $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                                    $"processing connected segment {otherSegmentId}: checking lane " +
                                    $"{curLaneId}: lanes are connected");
                            }

                            addArrow = true;
                            break;
                        }

                        curLaneId = curLaneId.ToLane().m_nextLane;
                    }

                    if (verbose_) {
                        Log._Debug(
                            $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
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

                    if (verbose_) {
                        Log._Debug(
                            $"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                            $"processing connected segment {otherSegmentId}: arrows={arrows}");
                    }
                }
            }

            if (verbose_) {
                Log._Debug($"LaneConnectionSubManager({Group}).RecalculateLaneArrows({laneId}, {nodeId}): " +
                           $"setting lane arrows to {arrows}");
            }

            LaneArrowManager.Instance.SetLaneArrows(laneId, arrows, true);
        }

        internal void ResetLaneConnections() {
            Log.Info($"Resetting lane connections of group: {Group}");
            connectionDataBase_.ResetConnectionsDatabase();
        }

        public bool LoadData(List<Configuration.LaneConnection> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} lane connections");

            foreach (Configuration.LaneConnection conn in data) {
                try {
                    if(!Supports(conn.group)) {
                        continue;
                    }

                    ref NetLane sourceLane = ref conn.sourceLaneId.ToLane();
                    if (!sourceLane.IsValidWithSegment()) {
                        continue;
                    }

                    ref NetLane targetLane = ref conn.targetLaneId.ToLane();
                    if (!targetLane.IsValidWithSegment()) {
                        continue;
                    }

                    if (conn.sourceLaneId == conn.targetLaneId) {
                        continue;
                    }

                    ushort nodeId = sourceLane.GetNodeId(conn.sourceStartNode);
#if DEBUGLOAD
                    Log._Debug($"Loading lane connection: lane {conn.sourceLaneId} -> {conn.targetLaneId} @ node: {nodeId}");
#endif
                    AddLaneConnection(conn.sourceLaneId, conn.targetLaneId, conn.sourceStartNode);
                    if (conn.LegacyBidirectional) {
                        bool targetStartNode = targetLane.IsStartNode(nodeId);
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

            foreach (var pair in connectionDataBase_) {
                LaneEnd source = pair.Key;
                try {
                    var targets = pair.Value;
                    foreach (var target in pair.Value) {
                        if (!target.Enabled) {
                            continue;
                        }

                        // skip invalid connections. Modifying database while iterating will throw InvalidOperationException!!!
                        if (!target.LaneId.ToLane().IsValidWithSegment()) {
                            continue;
                        }
#if DEBUGSAVE
                        ushort nodeId = source.LaneId.ToLane().GetNodeId(source.StartNode);
                        Log._Debug($"Saving lane connection: lane {source.LaneId} -> {target.LaneId} @ node: {nodeId}");
#endif
                        ret.Add(
                            new Configuration.LaneConnection(
                                source.LaneId,
                                target.LaneId,
                                source.StartNode,
                                Group));
                    }
                } catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane data @ {source.LaneId}: {e.ToString()}");
                    success = false;
                }
            }

            return ret;
        }
    }
}
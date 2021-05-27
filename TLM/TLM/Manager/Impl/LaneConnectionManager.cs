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

        static LaneConnectionManager() {
            Instance = new LaneConnectionManager();
        }

        public static LaneConnectionManager Instance { get; }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for LaneConnectionManager");
        }

        /// <summary>
        /// Checks if traffic may flow from source lane to target lane according to setup lane connections
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <param name="targetLaneId"></param>
        /// <param name="sourceStartNode">(optional) check at start node of source lane?</param>
        /// <returns></returns>
        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            if (!Options.laneConnectorEnabled) {
                return true;
            }

            if (targetLaneId == 0 || Flags.laneConnections[sourceLaneId] == null) {
                return false;
            }

            int nodeArrayIndex = sourceStartNode ? 0 : 1;

            uint[] connectedLanes = Flags.laneConnections[sourceLaneId][nodeArrayIndex];
            return connectedLanes != null
                   && connectedLanes.Any(laneId => laneId == targetLaneId);
        }

        /// <summary>
        /// determines whether or not the input lane is heading toward a start node.
        /// </summary>
        /// <returns>true if heading toward and start node.</returns>
        private bool IsHeadingTowardsStartNode(uint sourceLaneId) {
            NetLane[] laneBuffer = NetManager.instance.m_lanes.m_buffer;
            ushort segmentId = laneBuffer[sourceLaneId].m_segment;
            NetSegment segment = GetSeg(segmentId);
            uint laneId = segment.m_lanes;
            bool inverted = (segment.m_flags & NetSegment.Flags.Invert) != 0;

            foreach (var laneInfo in segment.Info.m_lanes) {
                if (laneId == sourceLaneId) {
                    return (laneInfo.m_finalDirection == NetInfo.Direction.Forward) ^ !inverted;
                }
                laneId = laneBuffer[laneId].m_nextLane;
            }
            throw new Exception($"Unreachable code. sourceLaneId:{sourceLaneId}, segmentId:{segmentId} ");
        }

        public bool HasConnections(uint sourceLaneId) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }
            return HasConnections(sourceLaneId, IsHeadingTowardsStartNode(sourceLaneId));
        }

        /// <summary>
        /// Determines if the given lane has outgoing connections
        /// </summary>
        /// <param name="sourceLaneId"></param>
        /// <returns></returns>
        public bool HasConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return false;
            }

            int nodeArrayIndex = startNode ? 0 : 1;

            return Flags.laneConnections[sourceLaneId] != null &&
                   Flags.laneConnections[sourceLaneId][nodeArrayIndex] != null;
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
            bool ret = false;
            Services.NetService.IterateSegmentLanes(
              segmentId,
              (uint laneId,
               ref NetLane lane,
               NetInfo.Lane laneInfo,
               ushort segId,
               ref NetSegment seg,
               byte laneIndex) => {
                   if (HasConnections(
                        laneId,
                        seg.m_startNode == nodeId)) {
                       ret = true;
                       return false;
                   }
                   return true;
               });
            return ret;
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

            uint sourceLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
            while (sourceLaneId != 0) {
                uint[] targetLaneIds = GetLaneConnections(sourceLaneId, startNode);

                if (targetLaneIds != null) {
                    foreach (uint targetLaneId in targetLaneIds) {
                        if (netManager.m_lanes.m_buffer[targetLaneId].m_segment == segmentId) {
                            return true;
                        }
                    }
                }

                sourceLaneId = netManager.m_lanes.m_buffer[sourceLaneId].m_nextLane;
            }

            return false;
        }

        [UsedImplicitly]
        internal int CountConnections(uint sourceLaneId, bool startNode) {
            if (!Options.laneConnectorEnabled) {
                return 0;
            }

            if (Flags.laneConnections[sourceLaneId] == null) {
                return 0;
            }

            int nodeArrayIndex = startNode ? 0 : 1;
            if (Flags.laneConnections[sourceLaneId][nodeArrayIndex] == null) {
                return 0;
            }

            return Flags.laneConnections[sourceLaneId][nodeArrayIndex].Length;
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

            if (Flags.laneConnections[laneId] == null) {
                return null;
            }

            int nodeArrayIndex = startNode ? 0 : 1;
            return Flags.laneConnections[laneId][nodeArrayIndex];
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

            bool ret = Flags.RemoveLaneConnection(laneId1, laneId2, startNode1);

            if (logLaneConnections) {
                Log._Debug($"LaneConnectionManager.RemoveLaneConnection({laneId1}, {laneId2}, " +
                           $"{startNode1}): ret={ret}");
            }

            if (!ret) {
                return ret;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            ushort segmentId1 = netManager.m_lanes.m_buffer[laneId1].m_segment;
            ushort segmentId2 = netManager.m_lanes.m_buffer[laneId2].m_segment;

            GetCommonNodeId(
                laneId1,
                laneId2,
                startNode1,
                out ushort commonNodeId,
                out bool startNode2);

            RecalculateLaneArrows(laneId1, commonNodeId, startNode1);
            RecalculateLaneArrows(laneId2, commonNodeId, startNode2);

            RoutingManager.Instance.RequestRecalculation(segmentId1, false);
            RoutingManager.Instance.RequestRecalculation(segmentId2, false);

            if (OptionsManager.Instance.MayPublishSegmentChanges()) {
                Services.NetService.PublishSegmentChanges(segmentId1);
                Services.NetService.PublishSegmentChanges(segmentId2);
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

            Services.NetService.IterateSegmentLanes(
                segmentId,
                (uint laneId,
                 ref NetLane lane,
                 NetInfo.Lane laneInfo,
                 ushort segId,
                 ref NetSegment segment,
                 byte laneIndex) => {
                    if (logLaneConnections) {
                        Log._Debug(
                            "LaneConnectionManager.RemoveLaneConnectionsFromSegment: Removing " +
                            $"lane connections from segment {segmentId}, lane {laneId}.");
                    }

                    RemoveLaneConnections(laneId, startNode, false);
                    return true;
                });

            if (recalcAndPublish) {
                RoutingManager.Instance.RequestRecalculation(segmentId);

                if (OptionsManager.Instance.MayPublishSegmentChanges()) {
                    Services.NetService.PublishSegmentChanges(segmentId);
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

            if (Flags.laneConnections[laneId] == null) {
                return;
            }

            int nodeArrayIndex = startNode ? 0 : 1;

            if (Flags.laneConnections[laneId][nodeArrayIndex] == null) {
                return;
            }

            // NetManager netManager = Singleton<NetManager>.instance;

            /*for (int i = 0; i < Flags.laneConnections[laneId][nodeArrayIndex].Length; ++i) {
                    uint otherLaneId = Flags.laneConnections[laneId][nodeArrayIndex][i];
                    if (Flags.laneConnections[otherLaneId] != null) {
                            if ((Flags.laneConnections[otherLaneId][0] != null && Flags.laneConnections[otherLaneId][0].Length == 1 && Flags.laneConnections[otherLaneId][0][0] == laneId && Flags.laneConnections[otherLaneId][1] == null) ||
                                    Flags.laneConnections[otherLaneId][1] != null && Flags.laneConnections[otherLaneId][1].Length == 1 && Flags.laneConnections[otherLaneId][1][0] == laneId && Flags.laneConnections[otherLaneId][0] == null) {

                                    ushort otherSegmentId = netManager.m_lanes.m_buffer[otherLaneId].m_segment;
                                    UnsubscribeFromSegmentGeometry(otherSegmentId);
                            }
                    }
            }*/

            Flags.RemoveLaneConnections(laneId, startNode);
            
            if (recalcAndPublish) {
                ushort segment = laneId.ToLane().m_segment;
                RoutingManager.Instance.RequestRecalculation(segment);

                if (OptionsManager.Instance.MayPublishSegmentChanges()) {
                    Services.NetService.PublishSegmentChanges(segment);
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

            bool ret = Flags.AddLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);

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

            ushort sourceSegmentId = netManager.m_lanes.m_buffer[sourceLaneId].m_segment;
            ushort targetSegmentId = netManager.m_lanes.m_buffer[targetLaneId].m_segment;

            if (sourceSegmentId == targetSegmentId) {
                JunctionRestrictionsManager.Instance.SetUturnAllowed(
                    sourceSegmentId,
                    sourceStartNode,
                    true);
            }

            RoutingManager.Instance.RequestRecalculation(sourceSegmentId, false);
            RoutingManager.Instance.RequestRecalculation(targetSegmentId, false);

            if (OptionsManager.Instance.MayPublishSegmentChanges()) {
                Services.NetService.PublishSegmentChanges(sourceSegmentId);
                Services.NetService.PublishSegmentChanges(targetSegmentId);
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
            NetManager netManager = Singleton<NetManager>.instance;
            ushort segmentId1 = netManager.m_lanes.m_buffer[laneId1].m_segment;
            ushort segmentId2 = netManager.m_lanes.m_buffer[laneId2].m_segment;

            ushort nodeId2Start = netManager.m_segments.m_buffer[segmentId2].m_startNode;
            ushort nodeId2End = netManager.m_segments.m_buffer[segmentId2].m_endNode;

            ushort nodeId1 = startNode1
                                 ? netManager.m_segments.m_buffer[segmentId1].m_startNode
                                 : netManager.m_segments.m_buffer[segmentId1].m_endNode;

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
            NetManager netManager = Singleton<NetManager>.instance;

            pos = null;
            outgoing = false;
            incoming = false;

            if ((netManager.m_segments.m_buffer[segmentId].m_flags &
                 (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return false;
            }

            if (laneId == null) {
                laneId = FindLaneId(segmentId, laneIndex);
                if (laneId == null) {
                    return false;
                }
            }

            if ((netManager.m_lanes.m_buffer[(uint)laneId].m_flags &
                 ((ushort)NetLane.Flags.Created | (ushort)NetLane.Flags.Deleted)) !=
                (ushort)NetLane.Flags.Created) {
                return false;
            }

            if (laneInfo == null) {
                if (laneIndex < netManager.m_segments.m_buffer[segmentId].Info.m_lanes.Length) {
                    laneInfo = netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];
                } else {
                    return false;
                }
            }

            NetInfo.Direction laneDir =
                ((NetManager.instance.m_segments.m_buffer[segmentId].m_flags &
                  NetSegment.Flags.Invert) == NetSegment.Flags.None)
                    ? laneInfo.m_finalDirection
                    : NetInfo.InvertDirection(laneInfo.m_finalDirection);

            if (startNode) {
                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = NetManager.instance.m_lanes.m_buffer[(uint)laneId].m_bezier.a;
            } else {
                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = NetManager.instance.m_lanes.m_buffer[(uint)laneId].m_bezier.d;
            }

            return true;
        }

        private uint? FindLaneId(ushort segmentId, byte laneIndex) {
            NetInfo.Lane[] lanes = NetManager.instance.m_segments.m_buffer[segmentId].Info.m_lanes;
            uint laneId = NetManager.instance.m_segments.m_buffer[segmentId].m_lanes;

            for (byte i = 0; i < lanes.Length && laneId != 0; i++) {
                if (i == laneIndex) {
                    return laneId;
                }

                laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
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
            NetManager netManager = Singleton<NetManager>.instance;
            ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;

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

            if (!Services.NetService.IsNodeValid(nodeId)) {
                if (logLaneConnections) {
                    Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): " +
                               "Node is invalid");
                }

                return;
            }

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = node.GetSegment(i);
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
                                if (Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft) {
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
                    uint curLaneId = netManager.m_segments.m_buffer[otherSegmentId].m_lanes;

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

                        curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
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
                                if (Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft) {
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
                    if (!Services.NetService.IsLaneAndItsSegmentValid(conn.lowerLaneId)) {
                        continue;
                    }

                    if (!Services.NetService.IsLaneAndItsSegmentValid(conn.higherLaneId)) {
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
                    if (Flags.laneConnections[i] == null) {
                        continue;
                    }

                    for (int nodeArrayIndex = 0; nodeArrayIndex <= 1; ++nodeArrayIndex) {
                        uint[] connectedLaneIds = Flags.laneConnections[i][nodeArrayIndex];
                        bool startNode = nodeArrayIndex == 0;

                        if (connectedLaneIds == null) {
                            continue;
                        }

                        // The code below is equivalent to LINQ
                        //-------------------------------------------------------------
                        // ret.AddRange(
                        //     from otherHigherLaneId in connectedLaneIds
                        //     where otherHigherLaneId > i
                        //     where Services.NetService.IsLaneValid(otherHigherLaneId)
                        //     select new Configuration.LaneConnection(i, otherHigherLaneId, startNode));
                        //-------------------------------------------------------------
                        foreach (uint otherHigherLaneId in connectedLaneIds) {
                            if (otherHigherLaneId <= i) {
                                continue;
                            }

                            if (!Services.NetService.IsLaneAndItsSegmentValid(otherHigherLaneId)) {
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
                catch (Exception e) {
                    Log.Error($"Exception occurred while saving lane data @ {i}: {e.ToString()}");
                    success = false;
                }
            }

            return ret;
        }
    }
}
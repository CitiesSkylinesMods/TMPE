namespace TrafficManager.Manager.Impl {
    using TrafficManager.Manager.Impl.LaneConnection;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Extensions;
    using System.Text;
    using System.Runtime.CompilerServices;

    public class RoutingManager
        : AbstractGeometryObservingManager,
          IRoutingManager
    {
        public static readonly RoutingManager Instance = new ();

        private RoutingManager() { }

        public const NetInfo.LaneType ROUTED_LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle | NetInfo.LaneType.CargoVehicle;

        public const VehicleInfo.VehicleType ROUTED_VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro |
            VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus;

        public static bool IsSupported(NetInfo.Lane laneInfo) =>
            laneInfo.Matches(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES);

        private const byte MAX_NUM_TRANSITIONS = 64;

        private readonly ulong[] updatedSegmentBuckets = new ulong[576];

        private readonly object updateLock = new ();

        private bool segmentsUpdated;

        /// <summary>
        /// Structs for path-finding that contain required segment-related routing data
        /// </summary>
        public SegmentRoutingData[] SegmentRoutings { get; } =
            new SegmentRoutingData[NetManager.MAX_SEGMENT_COUNT];

        /// <summary>
        /// Structs for path-finding that contain required lane-end-related backward routing data.
        /// Index:
        ///    [0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
        ///    [NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
        /// </summary>
        public LaneEndRoutingData[] LaneEndBackwardRoutings { get; } =
            new LaneEndRoutingData[(uint)NetManager.MAX_LANE_COUNT * 2u];

        /// <summary>
        /// Structs for path-finding that contain required lane-end-related forward routing data.
        /// Index:
        ///    [0 .. NetManager.MAX_LANE_COUNT-1]: lane ends at start node
        ///    [NetManager.MAX_LANE_COUNT .. 2*NetManger.MAX_LANE_COUNT-1]: lane ends at end node
        /// </summary>
        public LaneEndRoutingData[] LaneEndForwardRoutings { get; } =
            new LaneEndRoutingData[(uint)NetManager.MAX_LANE_COUNT * 2u];

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            string buf = $"Segment routings:\n";

            for (var i = 0; i < SegmentRoutings.Length; ++i) {
                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                buf += $"Segment {i}: {SegmentRoutings[i]}\n";
            }

            buf += $"\nLane end backward routings:\n";

            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                ref NetLane netLane = ref laneId.ToLane();
                if (!netLane.IsValidWithSegment()) {
                    continue;
                }

                buf += $"Lane {laneId} @ start: {LaneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
                buf += $"Lane {laneId} @ end: {LaneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
            }

            buf += $"\nLane end forward routings:\n";

            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                ref NetLane netLane = ref laneId.ToLane();
                if (!netLane.IsValidWithSegment()) {
                    continue;
                }

                buf += $"Lane {laneId} @ start: {LaneEndForwardRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
                buf += $"Lane {laneId} @ end: {LaneEndForwardRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
            }

            Log._Debug(buf);
        }

        public void SimulationStep() {
            if (!segmentsUpdated || Singleton<NetManager>.instance.m_segmentsUpdated
                                 || Singleton<NetManager>.instance.m_nodesUpdated) {
                // TODO maybe refactor NetManager use (however this could influence performance)
                return;
            }

            lock(updateLock) {
                segmentsUpdated = false;

                int len = updatedSegmentBuckets.Length;
                for (int i = 0; i < len; i++) {
                    ulong segMask = updatedSegmentBuckets[i];

                    if (segMask != 0uL) {
                        for (var m = 0; m < 64; m++) {

                            if ((segMask & 1uL << m) != 0uL) {
                                var segmentId = (ushort)(i << 6 | m);
                                RecalculateSegment(segmentId);
                            }
                        }

                        updatedSegmentBuckets[i] = 0;
                    }
                }
            }
        }

        public void RequestFullRecalculation() {
            lock(updateLock) {

                for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                    updatedSegmentBuckets[segmentId >> 6] |= 1uL << (int)(segmentId & 63);
                }

                Flags.ClearHighwayLaneArrows();
                segmentsUpdated = true;

                if (Singleton<SimulationManager>.instance.SimulationPaused ||
                    Singleton<SimulationManager>.instance.ForcedSimulationPaused) {
                    SimulationStep();
                }
            }
        }

        public void RequestRecalculation(ushort segmentId, bool propagate = true) {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                         && (DebugSettings.SegmentId <= 0
                             || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.RequestRecalculation({segmentId}, {propagate}) called.");
            }

            lock(updateLock) {

                updatedSegmentBuckets[segmentId >> 6] |= 1uL << (segmentId & 63);
                ResetIncomingHighwayLaneArrows(segmentId);
                segmentsUpdated = true;
            }

            if (propagate) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                ref NetNode startNode = ref netSegment.m_startNode.ToNode();
                RequestNodeRecalculation(ref startNode);

                ref NetNode endNode = ref netSegment.m_endNode.ToNode();
                RequestNodeRecalculation(ref endNode);
            }
        }

        public void RequestNodeRecalculation(ref NetNode node) {
            for (int i = 0; i < Constants.MAX_SEGMENTS_OF_NODE; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    RequestRecalculation(segmentId, false);
                }
            }
        }

        protected void RecalculateAll() {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get();
            Log._Debug($"RoutingManager.RecalculateAll: called");
#endif
            Flags.ClearHighwayLaneArrows();
            for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                try {
                    RecalculateSegment((ushort)segmentId);
                }
                catch (Exception e) {
                    Log.Error($"An error occurred while calculating routes for segment {segmentId}: {e}");
                }
            }
        }

        protected void RecalculateSegment(ushort segmentId) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (netSegment.Info == null) {
                return;
            }

#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get() &&
                         (DebugSettings.SegmentId <= 0 || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.RecalculateSegment({segmentId}) called.");
            }

            if (!netSegment.IsValid()) {
                if (logRouting) {
                    Log._Debug($"RoutingManager.RecalculateSegment({segmentId}): " +
                               "Segment is invalid. Skipping recalculation");
                }
                return;
            }

            RecalculateSegmentRoutingData(segmentId);

            foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                RecalculateLaneEndRoutingData(segmentId, laneIdAndIndex.laneIndex, laneIdAndIndex.laneId, true);
                RecalculateLaneEndRoutingData(segmentId, laneIdAndIndex.laneIndex, laneIdAndIndex.laneId, false);
            }

            Notifier.Instance.OnSegmentNodesModified(segmentId, this);
        }

        protected void ResetIncomingHighwayLaneArrows(ushort centerSegmentId) {
            ref NetSegment centerSegment = ref centerSegmentId.ToSegment();

            if (centerSegment.m_startNode != 0) {
                ResetIncomingHighwayLaneArrowsOfNode(centerSegmentId, centerSegment.m_startNode);
            }

            if (centerSegment.m_endNode != 0) {
                ResetIncomingHighwayLaneArrowsOfNode(centerSegmentId, centerSegment.m_endNode);
            }

#if DEBUG
            if (DebugSwitch.RoutingBasicLog.Get()
                && (DebugSettings.SegmentId <= 0
                    || DebugSettings.SegmentId == centerSegmentId)) {
                Log._Debug($"RoutingManager.ResetRoutingData: Identify nodes connected to {centerSegmentId}: nodeIds={centerSegment.m_startNode}, {centerSegment.m_endNode}");
            }
#endif
        }

        /// <summary>
        /// Reset highway lane arrows on all incoming lanes into a segment.
        /// </summary>
        /// <param name="centerSegmentId">The segment in the center.</param>
        /// <param name="centerSegmentNodeId">The node of the segment in the center.</param>
        private void ResetIncomingHighwayLaneArrowsOfNode(ushort centerSegmentId, ushort centerSegmentNodeId) {
            ref NetNode node = ref centerSegmentNodeId.ToNode();

            for (int i = 0; i < Constants.MAX_SEGMENTS_OF_NODE; ++i) {
                ushort neighbourSegmentId = node.GetSegment(i);
                if (neighbourSegmentId == 0 || neighbourSegmentId == centerSegmentId) {
                    continue;
                }

                ref NetSegment neighbourSegment = ref neighbourSegmentId.ToSegment();
                foreach (LaneIdAndIndex laneIdAndIndex in neighbourSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                    if (!IsIncomingLane(
                        neighbourSegmentId,
                        neighbourSegment.m_startNode == centerSegmentNodeId,
                        laneIdAndIndex.laneIndex)) {
                        continue;
                    }

                    Flags.RemoveHighwayLaneArrowFlags(laneIdAndIndex.laneId);
                }
            }
        }

        protected void ResetRoutingData(ushort segmentId) {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == segmentId);
            bool extendedLogRouting = DebugSwitch.Routing.Get()
                                      && (DebugSettings.SegmentId <= 0
                                          || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
            const bool extendedLogRouting = false;
#endif

            if (logRouting) {
                Log._Debug($"RoutingManager.ResetRoutingData: called for segment {segmentId}");
            }

            SegmentRoutings[segmentId].Reset();
            ResetIncomingHighwayLaneArrows(segmentId);

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            ref NetSegment netSegment = ref segmentId.ToSegment();
            foreach (LaneIdAndIndex laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                if (extendedLogRouting) {
                    Log._Debug($"RoutingManager.ResetRoutingData: Resetting lane {laneIdAndIndex.laneId}, " +
                               $"idx {laneIdAndIndex.laneIndex} @ seg. {segmentId}");
                }

                ResetLaneRoutings(laneIdAndIndex.laneId, true);
                ResetLaneRoutings(laneIdAndIndex.laneId, false);
            }
        }

        protected void RecalculateSegmentRoutingData(ushort segmentId) {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == segmentId);
            bool extendedLogRouting = DebugSwitch.Routing.Get()
                                      && (DebugSettings.SegmentId <= 0
                                          || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
            const bool extendedLogRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.RecalculateSegmentRoutingData: called for seg. {segmentId}");
            }

            SegmentRoutings[segmentId].Reset();

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment seg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId];
            ExtSegmentEnd startSegEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, true)];
            ExtSegmentEnd endSegEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, false)];

            SegmentRoutings[segmentId].highway = seg.highway;
            SegmentRoutings[segmentId].startNodeOutgoingOneWay = seg.oneWay && startSegEnd.outgoing;
            SegmentRoutings[segmentId].endNodeOutgoingOneWay = seg.oneWay && endSegEnd.outgoing;

            if (logRouting) {
                Log._Debug("RoutingManager.RecalculateSegmentRoutingData: Calculated routing " +
                           $"data for segment {segmentId}: {SegmentRoutings[segmentId]}");
            }
        }

        /// <summary>
        /// Calculates and populates forward/backward lane routings to the given lane end.
        /// </summary>
        /// <param name="prevSegmentId">target segment</param>
        /// <param name="prevLaneIndex">target lane index</param>
        /// <param name="prevLaneId">target lane id</param>
        /// <param name="isNodeStartNodeOfPrevSegment">start node for the target segment end</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:Parameters should be on same line or separate lines", Justification = "beauty")]
        private void RecalculateLaneEndRoutingData(
            ushort prevSegmentId,
            int prevLaneIndex,
            uint prevLaneId,
            bool isNodeStartNodeOfPrevSegment) {
            /* first we calculate backward routings then calculates forward routings based on that.
             * prev = target of lane transition
             * next = source of lane transition
             */
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == prevSegmentId);
            bool extendedLogRouting = DebugSwitch.Routing.Get()
                                      && (DebugSettings.SegmentId <= 0
                                          || DebugSettings.SegmentId == prevSegmentId);
            void _ExtendedLogImpl(params object[] lines) => DetailLogger.LogDebug(
                $"RoutingManager.RecalculateLaneEndRoutingData({prevSegmentId}, {prevLaneIndex}, {prevLaneId}, {isNodeStartNodeOfPrevSegment}):", lines);
            DetailLogger.LogDelegate extendedLog = extendedLogRouting ? _ExtendedLogImpl : null;

#else
            const bool logRouting = false;
            const bool extendedLogRouting = false;
            const DetailLogger.LogDelegate extendedLog = null;
#endif

            if (logRouting) {
                Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({prevSegmentId}, " +
                           $"{prevLaneIndex}, {prevLaneId}, {isNodeStartNodeOfPrevSegment}) called");
            }

            ResetLaneRoutings(prevLaneId, isNodeStartNodeOfPrevSegment);

            if (!IsOutgoingLane(prevSegmentId, isNodeStartNodeOfPrevSegment, prevLaneIndex)) {

                extendedLog?.Invoke("Lane is not an outgoing lane");
                return;
            }

            ref NetSegment prevSegment = ref prevSegmentId.ToSegment();
            NetInfo prevSegmentInfo = prevSegment.Info;
            bool prevSegIsInverted = (prevSegment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment prevExtSegment = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[prevSegmentId];
            ExtSegmentEnd prevEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(prevSegmentId, isNodeStartNodeOfPrevSegment)];

            ushort nodeId = prevEnd.nodeId; // common node

            NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[prevLaneIndex];
            if (!IsSupported(prevLaneInfo)) {
                return;
            }

            LaneEndRoutingData backwardRouting = new () {
                routed = true,
            };

            int prevSimilarLaneCount = prevLaneInfo.m_similarLaneCount;
            int prevInnerSimilarLaneIndex = CalcInnerSimilarLaneIndex(prevSegmentId, prevLaneIndex);
            int prevOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(prevSegmentId, prevLaneIndex);
            bool prevHasBusLane = prevExtSegment.buslane;

            bool nodeIsJunction = false;
            bool nodeIsTransition = false;
            bool nodeIsEndOrOneWayOut = false;
            bool nodeHasTrafficLights = false;
            bool nodeHasPrioritySigns =
                Constants.ManagerFactory.TrafficPriorityManager.HasNodePrioritySign(nodeId);
            bool nodeIsRealJunction = false;
            ushort buildingId = 0;

            ref NetNode netNode = ref nodeId.ToNode();
            nodeIsJunction = (netNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
            nodeIsTransition = (netNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
            nodeHasTrafficLights = (netNode.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
            nodeIsEndOrOneWayOut = (netNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
            nodeIsRealJunction = netNode.CountSegments() >= 3;
            buildingId = NetNode.FindOwnerBuilding(nodeId, 32f);

            bool isTollBooth = buildingId != 0
                && buildingId.ToBuilding().Info.m_buildingAI is TollBoothAI;

            bool nodeIsSimpleJunction = false;
            bool nodeIsSplitJunction = false;

            if (SavedGameOptions.Instance.highwayRules && !nodeHasTrafficLights && !nodeHasPrioritySigns) {
                // determine if junction is a simple junction (highway rules only apply to simple junctions)
                int numOutgoing = 0;
                int numIncoming = 0;

                for (int segIndex = 0; segIndex < 8; ++segIndex) {
                    ushort segId = netNode.GetSegment(segIndex);
                    if (segId == 0) {
                        continue;
                    }

                    bool? start = segId.ToSegment().GetRelationToNode(nodeId);
                    if (!start.HasValue) {
                        Log.Error($"Segment with id: {segId} is not connected to the node {nodeId}");
                        Debug.LogError($"TM:PE RecalculateLaneRoutings - Segment with id {segId} is not connected to the node {nodeId}");
                        continue;
                    }
                    ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segId, start.Value)];

                    if (segEnd.incoming) {
                        ++numIncoming;
                    }

                    if (segEnd.outgoing) {
                        ++numOutgoing;
                    }
                }

                nodeIsSimpleJunction = numOutgoing == 1 || numIncoming == 1;
                nodeIsSplitJunction = numOutgoing > 1;
            }

            bool applyHighwayMergingRules = false;
            bool nodeHasConnections = LaneConnectionManager.Instance.HasNodeConnections(nodeId);

            bool nextAreOnlyOneWayHighways =
                ExtSegmentEndManager.Instance.CalculateOnlyHighways(
                    prevEnd.segmentId,
                    prevEnd.startNode);

            bool onOnewayHighway = nextAreOnlyOneWayHighways && prevEnd.outgoing && prevExtSegment.oneWay && prevExtSegment.highway;

            if (extendedLogRouting) {
                Log._Debug($"SavedGameOptions.Instance.highwayMergingRules={SavedGameOptions.Instance.highwayMergingRules}, nodeHasTrafficLights={nodeHasTrafficLights} nodeHasPrioritySigns={nodeHasPrioritySigns}, nodeHasConnections={nodeHasConnections}, onOnewayHighway={onOnewayHighway}");
            }
            if (SavedGameOptions.Instance.highwayMergingRules && onOnewayHighway && !nodeHasTrafficLights && !nodeHasPrioritySigns && !nodeHasConnections) {
                // determine if junction is a simple junction (highway rules only apply to simple junctions)
                int numIncomingSegents = 0;
                int numOutgoingSegments = 0;
                bool laneSwitching = false;

                for (int segIndex = 0; segIndex < 8; ++segIndex) {
                    ushort segmentId = netNode.GetSegment(segIndex);
                    if (segmentId == 0) {
                        continue;
                    }

                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    bool startNode = netSegment.IsStartNode(nodeId);

                    ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];
                    if (segEnd.incoming) {
                        ++numIncomingSegents;
                        laneSwitching |= JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode);
                    }

                    if (segEnd.outgoing) {
                        ++numOutgoingSegments;
                    }
                }

                if (numOutgoingSegments == 1 && numIncomingSegents == 2 && !laneSwitching) {
                    // merging
                    int numIncomingLanes = 0; // toward the node
                    int numOutgoingLanes = 0; // from the node
                    nodeId.ToNode().CountLanes(
                        nodeID: nodeId,
                        ignoreSegment: 0,
                        laneTypes: LaneArrowManager.LANE_TYPES,
                        vehicleTypes: LaneArrowManager.VEHICLE_TYPES,
                        vehicleCategories: VehicleInfo.VehicleCategory.All,
                        onePerSegment: false,
                        forward: ref numIncomingLanes,
                        backward: ref numOutgoingLanes);
                    if (numIncomingLanes == numOutgoingLanes) {
                        // lane arithmetic checks out.
                        applyHighwayMergingRules = true;
                    }
                    if (extendedLogRouting) {
                        Log._Debug($"applyHighwayMergingRules data: numIncomingLanes={numIncomingLanes} numOutgoingLanes={numOutgoingLanes} ");
                    }
                }

                if (extendedLogRouting) {
                    Log._Debug(
                        $"applyHighwayMergingRules data: numIncomingSegents={numIncomingSegents} numOutgoingSegments={numOutgoingSegments} " +
                        $"laneSwitching={laneSwitching} applyHighwayMergingRules={applyHighwayMergingRules}");
                }
            }

            // determine if highway rules should be applied
            bool onHighway = SavedGameOptions.Instance.highwayRules && onOnewayHighway;
            bool applyHighwayRules = onHighway && nodeIsSimpleJunction;
            bool applyHighwayRulesAtJunction = applyHighwayRules && nodeIsRealJunction;
            bool iterateViaGeometry = (applyHighwayRulesAtJunction || applyHighwayMergingRules) && prevLaneInfo.MatchesRoutedRoad();

            // start with u-turns at highway junctions
            ushort nextSegmentId = iterateViaGeometry ? prevSegmentId : (ushort)0;

            extendedLog?.Invoke(new { prevSegmentId, _ = "Starting exploration with ",
                nextSegmentId, nodeId, __="--", onHighway, applyHighwayRules, applyHighwayRulesAtJunction, SavedGameOptions.Instance.highwayRules,
                nodeIsSimpleJunction, nextAreOnlyOneWayHighways, prevEndOutgoingOneWay = prevEnd.outgoing && prevExtSegment.oneWay,
                prevExtSegment.highway, iterateViaGeometry});
            extendedLog?.Invoke(new { Options = new { SavedGameOptions.Instance.highwayMergingRules }, applyHighwayMergingRules });
            extendedLog?.Invoke(new { prevSegIsInverted, Shortcuts.LHT });
            extendedLog?.Invoke(new { prevSimilarLaneCount, prevInnerSimilarLaneIndex, prevOuterSimilarLaneIndex, prevHasBusLane });
            extendedLog?.Invoke(new { nodeIsJunction, nodeIsEndOrOneWayOut, nodeHasTrafficLights, nodeIsSimpleJunction, nodeIsSplitJunction, nodeIsRealJunction });
            extendedLog?.Invoke(new { nodeId, buildingId, isTollBooth });

            // running number of next incoming lanes (number is updated at each segment iteration)
            int totalIncomingLanes = 0;

            // running number of next outgoing lanes (number is updated at each segment iteration)
            int totalOutgoingLanes = 0;

            for (int segmentIndex = 0; segmentIndex < 8; ++segmentIndex) {
                if (!iterateViaGeometry) {
                    nextSegmentId = netNode.GetSegment(segmentIndex);

                    if (nextSegmentId == 0) {
                        continue;
                    }
                }

                int outgoingCarLanes = 0;
                int incomingCarLanes = 0;

                ref NetSegment nextSegment = ref nextSegmentId.ToSegment();
                bool isNodeStartNodeOfNextSegment = nextSegment.m_startNode == nodeId;

                NetInfo nextSegmentInfo = nextSegment.Info;
                bool nextSegIsInverted =
                    (nextSegment.m_flags & NetSegment.Flags.Invert) !=
                    NetSegment.Flags.None;
                uint nextFirstLaneId = nextSegment.m_lanes;

                bool nextIsHighway =
                    Constants.ManagerFactory.ExtSegmentManager.CalculateIsHighway(nextSegmentId);
                bool nextHasBusLane =
                    Constants.ManagerFactory.ExtSegmentManager.CalculateHasBusLane(nextSegmentId);

                extendedLog?.Invoke(new { _ = "Exploring", nextSegmentId });
                extendedLog?.Invoke(new {
                    isNodeStartNodeOfNextSegment, nextSegIsInverted, nextFirstLaneId,
                    nextIsHighway, nextHasBusLane, totalOutgoingLanes, totalIncomingLanes
                });

                // determine next segment direction by evaluating the geometry information
                ArrowDirection nextIncomingDir = segEndMan.GetDirection(ref prevEnd, nextSegmentId);
                bool isNextSegmentValid = nextIncomingDir != ArrowDirection.None;

                if (isNextSegmentValid) {
                    extendedLog?.Invoke(new { prevSegmentId, _ = "Exploring", nextSegmentId, __ = "--", nextFirstLaneId, ___ = "--", nextIncomingDir, isNextSegmentValid });

                    bool nextSegmentIsReversed = isNodeStartNodeOfNextSegment ^ nextSegIsInverted;

                    // expected direction of the next lane
                    NetInfo.Direction nextExpectedDirection = nextSegmentIsReversed ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;

                    LaneTransitionData[] nextRelaxedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    byte numNextRelaxedTransitionDatas = 0;
                    LaneTransitionData[] nextCompatibleTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    int[] nextCompatibleOuterSimilarIndices = new int[MAX_NUM_TRANSITIONS];
                    byte numNextCompatibleTransitionDatas = 0;
                    LaneTransitionData[] nextLaneConnectionTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    byte numNextLaneConnectionTransitionDatas = 0;
                    LaneTransitionData[] nextForcedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    byte numNextForcedTransitionDatas = 0;
                    int[] nextCompatibleTransitionDataIndices = new int[MAX_NUM_TRANSITIONS];
                    byte numNextCompatibleTransitionDataIndices = 0;
                    int[] compatibleLaneIndexToLaneConnectionIndex = new int[MAX_NUM_TRANSITIONS];

                    uint nextLaneId = nextFirstLaneId;
                    byte nextLaneIndex = 0;

                    // ushort compatibleLaneIndicesMask = 0;
                    while (nextLaneIndex < nextSegmentInfo.m_lanes.Length && nextLaneId != 0u) {
                        // determine valid lanes based on lane arrows
                        NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

                        extendedLog?.Invoke(new { prevSegmentId, _ = "Exploring", nextSegmentId, nextLaneId, nextLaneIndex });

                        // next is compatible lane
                        if (nextLaneId != prevLaneId &&
                            IsSupported(nextLaneInfo) &&
                            (prevLaneInfo.m_vehicleType & nextLaneInfo.m_vehicleType) != 0) {
                            extendedLog?.Invoke(new { _ = "vehicle type check passed for", nextLaneId, nextLaneIndex });

                            // next is incoming lane
                            if ((nextLaneInfo.m_finalDirection & nextExpectedDirection) != NetInfo.Direction.None) {
                                extendedLog?.Invoke(new { _ = "lane direction check passed for", nextLaneId, nextLaneIndex });
                                extendedLog?.Invoke(new {
                                    nextLaneId, nextLaneIndex, isNextSegmentValid, nextLaneInfo = new { nextLaneInfo.m_finalDirection },
                                    nextExpectedDirection, incomingCarLanes, outgoingCarLanes });

                                int nextSimilarLaneCount = nextLaneInfo.m_similarLaneCount;
                                int nextOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(nextSegmentId, nextLaneIndex);
                                bool reverseSimilarLaneIndex = ShouldReverseSimilarLaneIndex(
                                    segmentInvert1: prevSegIsInverted, laneInfo1: prevLaneInfo, startNode1: isNodeStartNodeOfPrevSegment,
                                    segmentInvert2: nextSegIsInverted, laneInfo2: nextLaneInfo, startNode2: isNodeStartNodeOfNextSegment);
                                int nextMatchingOuterSimilarLaneIndex =
                                    reverseSimilarLaneIndex ?
                                    nextSimilarLaneCount - 1 - nextOuterSimilarLaneIndex :
                                    nextOuterSimilarLaneIndex;
                                bool outerSimilarLaneIndexMatches = prevOuterSimilarLaneIndex == nextMatchingOuterSimilarLaneIndex;

                                bool isCompatibleLane = false;
                                var transitionType = LaneEndTransitionType.Invalid;

                                int currentLaneConnectionTransIndex = -1;

                                if (nextLaneInfo.MatchesTrack() && prevLaneInfo.MatchesTrack()) {
                                    // routing tracked vehicles (trains, trams, metros, monorails)
                                    // lane may be mixed car+tram
                                    bool nextHasConnections =
                                        LaneConnectionManager.Instance.Track.HasConnections(
                                            nextLaneId,
                                            isNodeStartNodeOfNextSegment);
                                    if (nextHasConnections) {
                                        bool connected = LaneConnectionManager.Instance.Track.AreLanesConnected(
                                                nextLaneId,
                                                prevLaneId,
                                                isNodeStartNodeOfNextSegment);
                                        if (connected) {
                                            if (numNextLaneConnectionTransitionDatas < MAX_NUM_TRANSITIONS) {
                                                nextForcedTransitionDatas[numNextForcedTransitionDatas++].Set(
                                                    nextLaneId,
                                                    nextLaneIndex,
                                                    LaneEndTransitionType.LaneConnection,
                                                    nextSegmentId,
                                                    isNodeStartNodeOfNextSegment,
                                                    distance: 0,
                                                    group: LaneEndTransitionGroup.Track);
                                            } else {
                                                Log.Warning(
                                                    $"nextTransitionDatas overflow @ source lane {prevLaneId}, " +
                                                    $"idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                            }

                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "has outgoing connections and is connected with previous lane. adding as lane connection lane." });

                                        } else {
                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "has outgoing connections but is NOT connected with previous lane" });
                                        }
                                    } else if(nextSegmentId != prevSegmentId) {
                                        bool goodTurnAngle = TrackUtils.CheckSegmentsTurnAngle(
                                            sourceSegment: ref nextSegment,
                                            targetSegment: ref prevSegment,
                                            nodeId: nodeId);
                                        bool nextIsTrackOnly = nextLaneInfo.IsTrackOnly();
                                        bool similarLaneCountMatches = prevSimilarLaneCount == nextSimilarLaneCount;
                                        bool stayInlane = nextIsTrackOnly & similarLaneCountMatches & goodTurnAngle;

                                        bool connected;
                                        if (stayInlane) {
                                            extendedLog?.Invoke("similar track networks: Preventing lane changes.");
                                            connected = outerSimilarLaneIndexMatches;
                                        } else {
                                            connected = goodTurnAngle;
                                        }

                                        extendedLog?.Invoke("prefer stay in lane information:",
                                            new { prevSegmentId, prevLaneId, prevLaneIndex, prevOuterSimilarLaneIndex, prevSimilarLaneCount },
                                            new { nextSegmentId, nextLaneId, nextLaneIndex, nextOuterSimilarLaneIndex, nextSimilarLaneCount },
                                            new { reverseSimilarLaneIndex, nextMatchingOuterSimilarLaneIndex },
                                            new { goodTurnAngle, nextIsTrackOnly },
                                            new { similarLaneCountMatches, outerSimilarLaneIndexMatches, stayInlane });

                                        if (connected) {
                                            int distance = nodeIsRealJunction ?
                                                0 :
                                                Math.Abs(prevOuterSimilarLaneIndex - nextMatchingOuterSimilarLaneIndex);

                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "is used by tracked vehicles. adding as default." });

                                            if (numNextForcedTransitionDatas < MAX_NUM_TRANSITIONS) {
                                                nextForcedTransitionDatas[numNextForcedTransitionDatas++].Set(
                                                    nextLaneId,
                                                    nextLaneIndex,
                                                    LaneEndTransitionType.Default,
                                                    nextSegmentId,
                                                    isNodeStartNodeOfNextSegment,
                                                    (byte)distance,
                                                    LaneEndTransitionGroup.Track);
                                            } else {
                                                Log.Warning("nextForcedTransitionDatas overflow @ source lane " +
                                                            $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                            }
                                        }
                                    }
                                }

                                if (nextLaneInfo.MatchesRoutedRoad() && prevLaneInfo.MatchesRoutedRoad()) {
                                    // routing road vehicles (car, SOS, bus, trolleybus, ...)
                                    // lane may be mixed car+tram
                                    ++incomingCarLanes;

                                    bool connected = true;
                                    bool nextHasConnections =
                                        LaneConnectionManager.Instance.Road.HasConnections(
                                            nextLaneId,
                                            isNodeStartNodeOfNextSegment);
                                    if (nextHasConnections) {
                                        connected = LaneConnectionManager.Instance.Road.AreLanesConnected(
                                                nextLaneId,
                                                prevLaneId,
                                                isNodeStartNodeOfNextSegment);

                                        if (connected) {
                                            if (numNextLaneConnectionTransitionDatas < MAX_NUM_TRANSITIONS) {
                                                currentLaneConnectionTransIndex =
                                                    numNextLaneConnectionTransitionDatas;

                                                nextLaneConnectionTransitionDatas[numNextLaneConnectionTransitionDatas++].Set(
                                                    nextLaneId,
                                                    nextLaneIndex,
                                                    LaneEndTransitionType.LaneConnection,
                                                    nextSegmentId,
                                                    isNodeStartNodeOfNextSegment,
                                                    distance: 0,
                                                    group: LaneEndTransitionGroup.Road);
                                            } else {
                                                Log.Warning(
                                                    $"nextTransitionDatas overflow @ source lane {prevLaneId}, " +
                                                    $"idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                            }

                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "has outgoing connections and is connected with previous lane. adding as lane connection lane." });
                                        } else {
                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "has outgoing connections but is NOT connected with previous lane" });
                                        }
                                    }

                                    if (isTollBooth) {
                                        extendedLog?.Invoke(new { nodeId, buildingId, isTollBooth,
                                            _ = "is a toll booth. Preventing lane changes." });

                                        if (outerSimilarLaneIndexMatches) {
                                            extendedLog?.Invoke(new { nextLaneId, nextLaneIndex,
                                                _ = "is associated with a toll booth. adding as Default.", buildingId });

                                            isCompatibleLane = true;
                                            transitionType = LaneEndTransitionType.Default;
                                        }
                                    } else if (!nodeIsJunction) {
                                        extendedLog?.Invoke(new { nextLaneId, nextLaneIndex, _ = "is not a junction. adding as Default." });

                                        isCompatibleLane = true;
                                        transitionType = LaneEndTransitionType.Default;
                                    } else {
                                        // check for lane arrows
                                        LaneArrows nextLaneArrows =
                                            LaneArrowManager.Instance.GetFinalLaneArrows(nextLaneId);
                                        bool hasLeftArrow = (nextLaneArrows & LaneArrows.Left) != LaneArrows.None;
                                        bool hasRightArrow = (nextLaneArrows & LaneArrows.Right) != LaneArrows.None;
                                        bool hasForwardArrow =
                                            (nextLaneArrows & LaneArrows.Forward) != LaneArrows.None ||
                                            (nextLaneArrows & LaneArrows.LeftForwardRight) ==
                                            LaneArrows.None;

                                        extendedLog?.Invoke(new { _ = "start lane arrow check for ",
                                            nextLaneId, nextLaneIndex, hasLeftArrow, hasForwardArrow, hasRightArrow });

                                        bool hasUTurnRule = JunctionRestrictionsManager.Instance.IsUturnAllowed(
                                            nextSegmentId,
                                            isNodeStartNodeOfNextSegment);
                                        bool hasFarTurnArrow = (Shortcuts.LHT && hasRightArrow) || (Shortcuts.RHT && hasLeftArrow);
                                        bool canTurn = !nodeIsRealJunction || nodeIsEndOrOneWayOut || hasFarTurnArrow || hasUTurnRule;

                                        if (applyHighwayRules || // highway rules enabled
                                            (nextIncomingDir == ArrowDirection.Right && hasLeftArrow) || // valid incoming right
                                            (nextIncomingDir == ArrowDirection.Left && hasRightArrow) || // valid incoming left
                                            (nextIncomingDir == ArrowDirection.Forward && hasForwardArrow) || // valid incoming straight
                                            (nextIncomingDir == ArrowDirection.Turn && canTurn) /*valid turning lane*/) {

                                            extendedLog?.Invoke(new { _ = "lane arrow check passed for", nextLaneId, nextLaneIndex });

                                            isCompatibleLane = true;
                                            transitionType = LaneEndTransitionType.Default;
                                        } else if (connected) {

                                            extendedLog?.Invoke(new { _ = "lane arrow check FAILED. Adding as relaxed lane: ", nextLaneId, nextLaneIndex });

                                            transitionType = LaneEndTransitionType.Relaxed;

                                            if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
                                                nextRelaxedTransitionDatas[
                                                    numNextRelaxedTransitionDatas++].Set(
                                                    nextLaneId,
                                                    nextLaneIndex,
                                                    transitionType,
                                                    nextSegmentId,
                                                    isNodeStartNodeOfNextSegment,
                                                    distance: GlobalConfig.Instance.PathFinding.IncompatibleLaneDistance,
                                                    group: LaneEndTransitionGroup.Road);
                                            } else {
                                                Log.Warning(
                                                    $"nextTransitionDatas overflow @ source lane {prevLaneId}, " +
                                                    $"idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                            }
                                        }
                                    }

                                    if (isCompatibleLane) {
                                        extendedLog?.Invoke(new { _ = "adding as compatible lane now: ", nextLaneId, nextLaneIndex });

                                        if (numNextCompatibleTransitionDatas < MAX_NUM_TRANSITIONS) {
                                            nextCompatibleOuterSimilarIndices[numNextCompatibleTransitionDatas] =
                                                nextMatchingOuterSimilarLaneIndex;

                                            compatibleLaneIndexToLaneConnectionIndex[numNextCompatibleTransitionDatas] =
                                                currentLaneConnectionTransIndex;

                                            //compatibleLaneIndicesMask |= POW2MASKS[numNextCompatibleTransitionDatas];
                                            nextCompatibleTransitionDatas[numNextCompatibleTransitionDatas++].Set(
                                                nextLaneId,
                                                nextLaneIndex,
                                                transitionType,
                                                nextSegmentId,
                                                isNodeStartNodeOfNextSegment,
                                                distance: 0,
                                                group: LaneEndTransitionGroup.Road);
                                        } else {
                                            Log.Warning(
                                                "nextCompatibleTransitionDatas overflow @ source lane " +
                                                $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                        }
                                    } else {
                                        extendedLog?.Invoke(new { nextLaneId, nextLaneIndex, _ = "is NOT compatible." });
                                    }
                                }
                            } else {
                                extendedLog?.Invoke(new { _ = "lane direction check NOT passed for", nextLaneId, nextLaneIndex, isNextSegmentValid,
                                    nextLaneInfo = new { nextLaneInfo.m_finalDirection }, nextExpectedDirection });

                                bool outgoing = (nextLaneInfo.m_finalDirection & NetInfo.InvertDirection(nextExpectedDirection)) != NetInfo.Direction.None;
                                if (outgoing && nextLaneInfo.MatchesRoutedRoad()) {
                                    ++outgoingCarLanes;

                                    extendedLog?.Invoke(new { _ = "increasing number of outgoing lanes at ",
                                        nextLaneId, nextLaneIndex, isNextSegmentValid, nextLaneInfo = new { nextLaneInfo.m_finalDirection },
                                        nextExpectedDirection, incomingCarLanes, outgoingCarLanes });
                                }
                            }
                        } else {
                            extendedLog?.Invoke(new { _ = "vehicle type check NOT passed for", nextLaneId, nextLaneIndex,
                                prevLaneInfo = new { prevLaneInfo.m_vehicleType, prevLaneInfo.m_laneType },
                                nextLaneInfo = new { nextLaneInfo.m_vehicleType, nextLaneInfo.m_laneType }
                            });
                        }

                        nextLaneId = nextLaneId.ToLane().m_nextLane;
                        ++nextLaneIndex;
                    }

                    extendedLog?.Invoke(new { isNextSegmentValid, nextCompatibleTransitionDatas = nextCompatibleTransitionDataIndices?.ArrayToString() });

                    bool laneChangesAllowed
                        = SavedGameOptions.Instance.junctionRestrictionsEnabled
                          && JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(
                                 nextSegmentId, isNodeStartNodeOfNextSegment);
                    int nextCompatibleLaneCount = numNextCompatibleTransitionDatas;

                    if (nextCompatibleLaneCount > 0) {
                        // we found compatible lanes
                        int[] tmp = new int[nextCompatibleLaneCount];
                        Array.Copy(nextCompatibleOuterSimilarIndices,
                                   tmp,
                                   nextCompatibleLaneCount);
                        nextCompatibleOuterSimilarIndices = tmp;

                        // TODO: Check performance on this LINQ
                        int[] compatibleLaneIndicesSortedByOuterSimilarIndex =
                            nextCompatibleOuterSimilarIndices
                                .Select((x, i) => new KeyValuePair<int, int>(x, i))
                                .OrderBy(p => p.Key)
                                .Select(p => p.Value)
                                .ToArray();

                        // enable highway rules only at junctions or at simple lane merging/splitting points
                        int laneDiff = nextCompatibleLaneCount - prevSimilarLaneCount;
                        bool applyHighwayRulesAtSegment =
                            applyHighwayRules
                            && (applyHighwayRulesAtJunction || Math.Abs(laneDiff) == 1);

                        extendedLog?.Invoke(new { _ = "found compatible lanes!",
                            compatibleLaneIndicesSortedByOuterSimilarIndex = compatibleLaneIndicesSortedByOuterSimilarIndex?.ArrayToString(),
                            laneDiff, applyHighwayRulesAtSegment });

                        if (applyHighwayRulesAtJunction || applyHighwayMergingRules) {
                            // we reached a highway junction where more than two segments are connected to each other
                            extendedLog?.Invoke(new { _ = "applying highway rules at junction" });

                            // number of lanes that were processed in earlier segment iterations
                            // (either all incoming or all outgoing)
                            int numLanesSeen = Math.Max(totalIncomingLanes, totalOutgoingLanes);

                            int minNextInnerSimilarIndex = -1;
                            int maxNextInnerSimilarIndex = -1;

                            // this lane will be referred as the "stay" lane with zero distance
                            int refNextInnerSimilarIndex = -1;

#if DEBUGHWJUNCTIONROUTING
                            extendedLog?.Invoke(new { _ = "applying highway rules at junction" });
                            extendedLog?.Invoke(new { totalIncomingLanes, totalOutgoingLanes, numLanesSeen, laneChangesAllowed });
                            extendedLog?.Invoke(new { prevInnerSimilarLaneIndex, prevSimilarLaneCount, nextCompatibleLaneCount });
#endif

                            if (nodeIsSplitJunction) {
                                // lane splitting at junction
                                minNextInnerSimilarIndex = prevInnerSimilarLaneIndex + numLanesSeen;

                                if (minNextInnerSimilarIndex >= nextCompatibleLaneCount) {
                                    // there have already been explored more outgoing lanes than
                                    // incoming lanes on the previous segment. Also allow vehicles
                                    // to go to the current segment.
                                    minNextInnerSimilarIndex =
                                        maxNextInnerSimilarIndex =
                                            refNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
                                } else {
                                    maxNextInnerSimilarIndex =
                                        refNextInnerSimilarIndex =
                                            minNextInnerSimilarIndex;

                                    if (laneChangesAllowed) {
                                        // allow lane changes at highway junctions
                                        if (minNextInnerSimilarIndex > 0
                                            && prevInnerSimilarLaneIndex > 0) {
                                            --minNextInnerSimilarIndex;
                                        }
                                    }
                                }

#if DEBUGHWJUNCTIONROUTING
                                extendedLog?.Invoke(new { _ = "highway rules at junction: lane splitting junction.",
                                    minNextInnerSimilarIndex, maxNextInnerSimilarIndex });
#endif
                            } else {
                                // lane merging at junction
                                minNextInnerSimilarIndex = prevInnerSimilarLaneIndex - numLanesSeen;

                                if (minNextInnerSimilarIndex < 0) {
                                    if (prevInnerSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                        // there have already been explored more incoming lanes than
                                        // outgoing lanes on the previous segment. Allow the current
                                        // segment to also join the big merging party. What a fun!
                                        minNextInnerSimilarIndex = 0;
                                        maxNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
                                    } else {
                                        // lanes do not connect (min/max = -1)
                                    }
                                } else {
                                    // allow lane changes at highway junctions
                                    refNextInnerSimilarIndex = minNextInnerSimilarIndex;

                                    if (laneChangesAllowed) {
                                        maxNextInnerSimilarIndex = Math.Min(
                                            nextCompatibleLaneCount - 1,
                                            minNextInnerSimilarIndex + 1);

                                        if (minNextInnerSimilarIndex > 0) {
                                            --minNextInnerSimilarIndex;
                                        }
                                    } else {
                                        maxNextInnerSimilarIndex = minNextInnerSimilarIndex;
                                    }

                                    if (totalIncomingLanes > 0
                                        && prevInnerSimilarLaneIndex == prevSimilarLaneCount - 1
                                        && maxNextInnerSimilarIndex < nextCompatibleLaneCount - 1) {
                                        // we reached the outermost lane on the previous segment but
                                        // there are still lanes to go on the next segment: allow merging
                                        maxNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
                                    }
                                }

#if DEBUGHWJUNCTIONROUTING
                                extendedLog?.Invoke(new { _ = "highway rules at junction: lane merging/unknown junction.",
                                    minNextInnerSimilarIndex, maxNextInnerSimilarIndex });
#endif
                            }

                            if (minNextInnerSimilarIndex >= 0) {
#if DEBUGHWJUNCTIONROUTING
                                extendedLog?.Invoke(new { _ = "minNextInnerSimilarIndex >= 0.",
                                    nextCompatibleTransitionDatas = nextCompatibleTransitionDatas.ArrayToString()});
#endif

                                // explore lanes
                                for (int nextInnerSimilarIndex = minNextInnerSimilarIndex;
                                     nextInnerSimilarIndex <= maxNextInnerSimilarIndex;
                                     ++nextInnerSimilarIndex) {
                                    int nextTransitionIndex = FindLaneByInnerIndex(
                                        nextCompatibleTransitionDatas,
                                        numNextCompatibleTransitionDatas,
                                        nextSegmentId,
                                        nextInnerSimilarIndex);

#if DEBUGHWJUNCTIONROUTING
                                    extendedLog?.Invoke(new { _ = "highway junction iteration:", nextInnerSimilarIndex, nextTransitionIndex });
#endif

                                    if (nextTransitionIndex < 0) {
                                        continue;
                                    }

                                    // calculate lane distance
                                    byte compatibleLaneDist = 0;
                                    if (refNextInnerSimilarIndex >= 0) {
                                        compatibleLaneDist = (byte)Math.Abs(
                                            refNextInnerSimilarIndex - nextInnerSimilarIndex);
                                    }

                                    // skip lanes having lane connections
                                    // in highway-rules HasConnections() gives the same result as HasOutgoingConnections but faster.
                                    if (LaneConnectionManager.Instance.Road.HasConnections(
                                        nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                        isNodeStartNodeOfNextSegment)) {
                                        int laneConnectionTransIndex =
                                            compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];

                                        if (laneConnectionTransIndex >= 0) {
                                            nextLaneConnectionTransitionDatas[
                                                laneConnectionTransIndex].distance = compatibleLaneDist;
                                        }

                                        extendedLog?.Invoke(new { _ = $"Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId}) " +
                                            "has outgoing lane connections. " +
                                            $"Skip for now but set compatibleLaneDist={compatibleLaneDist} if " +
                                            $"laneConnectionTransIndex={laneConnectionTransIndex} >= 0." });

                                        // disregard lane since it has outgoing connections
                                        continue;
                                    }

                                    nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;
#if DEBUGHWJUNCTIONROUTING
                                    extendedLog?.Invoke(new { _ = "highway junction iteration:", compatibleLaneDist });
#endif
                                    if (SavedGameOptions.Instance.highwayRules) {
                                        UpdateHighwayLaneArrows(
                                            nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                            isNodeStartNodeOfNextSegment,
                                            nextIncomingDir);
                                    }

                                    if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
                                        nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] =
                                            nextTransitionIndex;
                                    } else {
                                        Log.Warning(
                                            "nextCompatibleTransitionDataIndices overflow @ source lane " +
                                            $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                    }
                                }

#if DEBUGHWJUNCTIONROUTING
                                extendedLog?.Invoke(new { _ = "highway junction iterations finished:",
                                    nextCompatibleTransitionDataIndices = nextCompatibleTransitionDataIndices.ArrayToString() });
#endif
                            }
                        } else {
                            // This is
                            // 1. a highway lane splitting/merging point,
                            // 2. a city or highway lane continuation point (simple transition with
                            //     equal number of lanes or flagged city transition), or
                            // 3. a city junction
                            // with multiple or a single target lane: Perform lane matching
                            extendedLog?.Invoke(new { _ = "regular node" });

                            // min/max compatible outer similar lane indices
                            int minNextCompatibleOuterSimilarIndex = -1;
                            int maxNextCompatibleOuterSimilarIndex = -1;
                            if (nextIncomingDir == ArrowDirection.Turn) {
                                minNextCompatibleOuterSimilarIndex = 0;
                                maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                extendedLog?.Invoke(new { _ = "u-turn:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                            } else if (nodeIsRealJunction) {

                                extendedLog?.Invoke(new { _ = "next is real junction" });

                                // at junctions: try to match distinct lanes
                                if (nextCompatibleLaneCount > prevSimilarLaneCount
                                    && prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                    // merge inner lanes
                                    minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
                                    maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                    extendedLog?.Invoke(new { _ = "merge inner lanes:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                } else if (nextCompatibleLaneCount < prevSimilarLaneCount
                                           && prevSimilarLaneCount % nextCompatibleLaneCount == 0) {
                                    // symmetric split
                                    int splitFactor =
                                        prevSimilarLaneCount / nextCompatibleLaneCount;
                                    minNextCompatibleOuterSimilarIndex =
                                        maxNextCompatibleOuterSimilarIndex =
                                            prevOuterSimilarLaneIndex / splitFactor;

                                    extendedLog?.Invoke(new { _ = "symmetric split:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                } else {
                                    // 1-to-n (split inner lane) or 1-to-1 (direct lane matching)
                                    minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
                                    maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;

                                    extendedLog?.Invoke(new { _ = "1-to-n (split inner lane) or 1-to-1 (direct lane matching):",
                                        minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });
                                }

                                bool straightLaneChangesAllowed =
                                    nextIncomingDir == ArrowDirection.Forward && laneChangesAllowed;

                                extendedLog?.Invoke(new { laneChangesAllowed, straightLaneChangesAllowed });

                                if (!straightLaneChangesAllowed) {
                                    if (nextHasBusLane && !prevHasBusLane) {
                                        // allow vehicles on the bus lane AND on the next lane to merge on this lane
                                        maxNextCompatibleOuterSimilarIndex = Math.Min(
                                            nextCompatibleLaneCount - 1,
                                            maxNextCompatibleOuterSimilarIndex + 1);

                                        extendedLog?.Invoke(new { _ = "allow vehicles on the bus lane AND on the next lane to merge on this lane:",
                                            minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                    } else if (!nextHasBusLane && prevHasBusLane) {
                                        // allow vehicles to enter the bus lane
                                        minNextCompatibleOuterSimilarIndex = Math.Max(
                                            0, minNextCompatibleOuterSimilarIndex - 1);

                                        extendedLog?.Invoke(new { _ = "allow vehicles to enter the bus lane:",
                                            minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                    }
                                } else {
                                    // vehicles may change lanes when going straight
                                    minNextCompatibleOuterSimilarIndex--;
                                    maxNextCompatibleOuterSimilarIndex++;

                                    extendedLog?.Invoke(new { _ = "vehicles may change lanes when going straight:",
                                        minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                }
                            } else if (prevSimilarLaneCount == nextCompatibleLaneCount) {
                                // equal lane count: consider all available lanes
                                minNextCompatibleOuterSimilarIndex = 0;
                                maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                extendedLog?.Invoke(new { _ = "equal lane count:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                            } else {
                                // lane continuation point: lane merging/splitting
                                extendedLog?.Invoke(new { _ = "lane continuation point: lane merging/splitting" });

                                bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
                                bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0

                                extendedLog?.Invoke(new { sym1, sym2 });

                                if (prevSimilarLaneCount < nextCompatibleLaneCount) {

                                    extendedLog?.Invoke(new { _ = "lane merging:", prevSimilarLaneCount, nextCompatibleLaneCount });

                                    // lane merging
                                    if (sym1 == sym2) {
                                        // merge outer lanes
                                        // nextCompatibleLaneCount - prevSimilarLaneCount is always > 0
                                        int a = (nextCompatibleLaneCount - prevSimilarLaneCount) >> 1;

                                        extendedLog?.Invoke(new { _ = "merge outer lanes.", a });

                                        if (prevSimilarLaneCount == 1) {
                                            minNextCompatibleOuterSimilarIndex = 0;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            extendedLog?.Invoke(new { _ = "prevSimilarLaneCount == 1:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else if (prevOuterSimilarLaneIndex == 0) {
                                            minNextCompatibleOuterSimilarIndex = 0;
                                            maxNextCompatibleOuterSimilarIndex = a;

                                            extendedLog?.Invoke(new { _ = "prevOuterSimilarLaneIndex == 0:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            extendedLog?.Invoke(new { _ = "prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else {
                                            minNextCompatibleOuterSimilarIndex =
                                                maxNextCompatibleOuterSimilarIndex =
                                                    prevOuterSimilarLaneIndex + a;

                                            extendedLog?.Invoke(new { _ = "default case:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });
                                        }
                                    } else {
                                        // criss-cross merge
                                        // nextCompatibleLaneCount - prevSimilarLaneCount - 1 is always >= 0
                                        int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) >> 1;

                                        // nextCompatibleLaneCount - prevSimilarLaneCount + 1 is always >= 2
                                        int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) >> 1;

                                        extendedLog?.Invoke(new { _ = "criss-cross merge:", a, b });

                                        if (prevSimilarLaneCount == 1) {
                                            minNextCompatibleOuterSimilarIndex = 0;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            extendedLog?.Invoke(new { _ = "prevSimilarLaneCount == 1:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else if (prevOuterSimilarLaneIndex == 0) {
                                            minNextCompatibleOuterSimilarIndex = 0;
                                            maxNextCompatibleOuterSimilarIndex = b;

                                            extendedLog?.Invoke(new { _ = "prevOuterSimilarLaneIndex == 0:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            extendedLog?.Invoke(new { _ = "prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                        } else {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
                                            maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + b;

                                            extendedLog?.Invoke(new { _ = "default criss-cross case:",
                                                minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });
                                        }
                                    }
                                } else {
                                    // at lane splits: distribute traffic evenly (1-to-n, n-to-n)
                                    // prevOuterSimilarIndex is always > nextCompatibleLaneCount
                                    extendedLog?.Invoke(new { _ = "at lane splits: distribute traffic evenly (1-to-n, n-to-n)" });

                                    if (sym1 == sym2) {
                                        // split outer lanes
                                        // prevSimilarLaneCount - nextCompatibleLaneCount is always > 0
                                        int a = (prevSimilarLaneCount - nextCompatibleLaneCount) >> 1;

                                        // a is always <= prevSimilarLaneCount
                                        minNextCompatibleOuterSimilarIndex =
                                            maxNextCompatibleOuterSimilarIndex =
                                                prevOuterSimilarLaneIndex - a;

                                        extendedLog?.Invoke(new { _ = "split outer lanes:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                                    } else {
                                        // split outer lanes, criss-cross inner lanes
                                        // prevSimilarLaneCount - nextCompatibleLaneCount - 1 is always >= 0
                                        int a = (prevSimilarLaneCount - nextCompatibleLaneCount - 1) >> 1;

                                        minNextCompatibleOuterSimilarIndex =
                                            (a - 1 >= prevOuterSimilarLaneIndex)
                                                ? 0
                                                : prevOuterSimilarLaneIndex - a - 1;
                                        maxNextCompatibleOuterSimilarIndex =
                                            (a >= prevOuterSimilarLaneIndex)
                                                ? 0
                                                : prevOuterSimilarLaneIndex - a;

                                        extendedLog?.Invoke(new { _ = "split outer lanes, criss-cross inner lanes:",
                                            minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });
                                    }
                                }
                            }

                            extendedLog?.Invoke(new { _ = "pre-final bounds:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                            minNextCompatibleOuterSimilarIndex = Math.Max(
                                0,
                                Math.Min(
                                    minNextCompatibleOuterSimilarIndex,
                                    nextCompatibleLaneCount - 1));
                            maxNextCompatibleOuterSimilarIndex = Math.Max(
                                0,
                                Math.Min(
                                    maxNextCompatibleOuterSimilarIndex,
                                    nextCompatibleLaneCount - 1));

                            if (minNextCompatibleOuterSimilarIndex > maxNextCompatibleOuterSimilarIndex) {
                                minNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex;
                            }

                            extendedLog?.Invoke(new { _ = "final bounds:", minNextCompatibleOuterSimilarIndex, maxNextCompatibleOuterSimilarIndex });

                            // find best matching lane(s)
                            for (int nextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex;
                                 nextCompatibleOuterSimilarIndex <= maxNextCompatibleOuterSimilarIndex;
                                 ++nextCompatibleOuterSimilarIndex) {
                                int nextTransitionIndex = FindLaneWithMaxOuterIndex(
                                    compatibleLaneIndicesSortedByOuterSimilarIndex,
                                    nextCompatibleOuterSimilarIndex);

                                extendedLog?.Invoke(new { _ = "best matching lane iteration:", nextCompatibleOuterSimilarIndex, nextTransitionIndex });

                                if (nextTransitionIndex < 0) {
                                    continue;
                                }

                                // calculate lane distance
                                byte compatibleLaneDist = 0;

                                if (nextIncomingDir == ArrowDirection.Turn) {
                                    compatibleLaneDist = (byte)GlobalConfig
                                                               .Instance.PathFinding
                                                               .UturnLaneDistance;
                                } else if (!nodeIsRealJunction &&
                                           ((!nodeIsJunction && !nodeIsTransition) ||
                                            nextCompatibleLaneCount == prevSimilarLaneCount)) {
                                    // relative lane distance (positive: change to more outer lane,
                                    // negative: change to more inner lane)
                                    int relLaneDist =
                                        nextCompatibleOuterSimilarIndices[nextTransitionIndex] -
                                        prevOuterSimilarLaneIndex;
                                    compatibleLaneDist = (byte)Math.Abs(relLaneDist);
                                }

                                // skip lanes having lane connections
                                // nextCompatibleTransitionDatas is only for roads.
                                if (LaneConnectionManager.Instance.Road.HasOutgoingConnections(
                                    nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                    isNodeStartNodeOfNextSegment)) {
                                    int laneConnectionTransIndex =
                                        compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];

                                    if (laneConnectionTransIndex >= 0) {
                                        nextLaneConnectionTransitionDatas[laneConnectionTransIndex]
                                            .distance = compatibleLaneDist;
                                    }

                                    extendedLog?.Invoke(new {
                                        _ = $"Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId})",
                                        __ = "has outgoing lane connections. Skip for now but set",
                                        compatibleLaneDist, ___ = "if", laneConnectionTransIndex, ____ = ">= 0" });

                                    continue; // disregard lane since it has outgoing connections
                                }

                                if (nextIncomingDir == ArrowDirection.Turn && // u-turn
                                    !nodeIsEndOrOneWayOut && // not a dead end
                                                             // incoming lane is not innermost lane
                                    nextCompatibleOuterSimilarIndex != maxNextCompatibleOuterSimilarIndex) {
                                    // force u-turns to happen on the innermost lane
                                    ++compatibleLaneDist;
                                    nextCompatibleTransitionDatas[nextTransitionIndex].type =
                                        LaneEndTransitionType.Relaxed;

                                    extendedLog?.Invoke(new {
                                        _ = $"Next lane ({nextCompatibleTransitionDatas[nextTransitionIndex].laneId}) is avoided u-turn.",
                                        __ = $"Incrementing compatible lane distance to {compatibleLaneDist}" });
                                }

                                extendedLog?.Invoke(new { _ = "->", compatibleLaneDist });

                                nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;

                                if (onHighway && !nodeIsRealJunction && compatibleLaneDist > 1) {
                                    // under normal circumstances vehicles should not change more
                                    // than one lane on highways at one time
                                    nextCompatibleTransitionDatas[nextTransitionIndex].type
                                        = LaneEndTransitionType.Relaxed;

                                    extendedLog?.Invoke(new { _ = "-> under normal circumstances vehicles should not change " +
                                        "more than one lane on highways at one time: setting type to Relaxed" });

                                } else if (applyHighwayRulesAtSegment) {
                                    UpdateHighwayLaneArrows(
                                        nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                        isNodeStartNodeOfNextSegment,
                                        nextIncomingDir);
                                }

                                if (numNextCompatibleTransitionDataIndices < MAX_NUM_TRANSITIONS) {
                                    nextCompatibleTransitionDataIndices[numNextCompatibleTransitionDataIndices++] =
                                        nextTransitionIndex;
                                } else {
                                    Log.Warning(
                                        "nextCompatibleTransitionDataIndices overflow @ source lane " +
                                        $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                }
                            } // foreach lane
                        } // highway/city rules if/else
                    } // compatible lanes found

                    // build final array
                    var nextTransitionDatas = new LaneTransitionData[
                        numNextRelaxedTransitionDatas +
                        numNextCompatibleTransitionDataIndices +
                        numNextLaneConnectionTransitionDatas +
                        numNextForcedTransitionDatas];
                    int j = 0;

                    for (int i = 0; i < numNextCompatibleTransitionDataIndices; ++i) {
                        nextTransitionDatas[j++] = nextCompatibleTransitionDatas[nextCompatibleTransitionDataIndices[i]];
                    }

                    for (int i = 0; i < numNextLaneConnectionTransitionDatas; ++i) {
                        nextTransitionDatas[j++] = nextLaneConnectionTransitionDatas[i];
                    }

                    for (int i = 0; i < numNextRelaxedTransitionDatas; ++i) {
                        nextTransitionDatas[j++] = nextRelaxedTransitionDatas[i];
                    }

                    for (int i = 0; i < numNextForcedTransitionDatas; ++i) {
                        nextTransitionDatas[j++] = nextForcedTransitionDatas[i];
                    }

                    extendedLog?.Invoke(new { _ = "build array for ", nextSegmentId, nextTransitionDatas = nextTransitionDatas.ArrayToString() });

                    backwardRouting.AddTransitions(nextTransitionDatas);

                    extendedLog?.Invoke(new { _ = "updated incoming/outgoing lanes for next segment iteration:", totalIncomingLanes, totalOutgoingLanes });

                    if (nextSegmentId != prevSegmentId) {
                        totalIncomingLanes += incomingCarLanes;
                        totalOutgoingLanes += outgoingCarLanes;
                    }
                } else {
                    // invalid segment
                    extendedLog?.Invoke(new { _ = "valid segment check NOT passed for ", nextSegmentId, segmentIndex });
                }

                if (iterateViaGeometry) {
                    ref NetSegment nextSegment2 = ref nextSegmentId.ToSegment();
                    nextSegmentId = Shortcuts.LHT
                        ? nextSegment2.GetLeftSegment(nodeId)
                        : nextSegment2.GetRightSegment(nodeId);

                    if (nextSegmentId == prevSegmentId || nextSegmentId == 0) {
                        // we reached the first segment again
                        break;
                    }
                }
            } // foreach segment

            // update backward routing
            LaneEndBackwardRoutings[GetLaneEndRoutingIndex(prevLaneId, isNodeStartNodeOfPrevSegment)] = backwardRouting;

            // update forward routing
            LaneTransitionData[] newTransitions = backwardRouting.transitions;
            if (newTransitions != null) {
                for (int i = 0; i < newTransitions.Length; ++i) {
                    uint sourceIndex = GetLaneEndRoutingIndex(
                        newTransitions[i].laneId,
                        newTransitions[i].startNode);

                    LaneTransitionData forwardTransition = new() {
                        laneId = prevLaneId,
                        laneIndex = (byte)prevLaneIndex,
                        type = newTransitions[i].type,
                        group = newTransitions[i].group,
                        distance = newTransitions[i].distance,
                        segmentId = prevSegmentId,
                        startNode = isNodeStartNodeOfPrevSegment,
                    };

                    LaneEndForwardRoutings[sourceIndex].AddTransition(forwardTransition);

                    extendedLog?.Invoke(new { _ = "adding transition to forward routing of ", prevLaneId, prevLaneIndex },
                        new { _ = $"@ seg. {newTransitions[i].segmentId} @ node {newTransitions[i].startNode}", sourceIndex },
                        forwardTransition.ToString(),
                        "\nNew forward routing:",
                        LaneEndForwardRoutings[sourceIndex].ToString());

                }
            }

            if (logRouting) {
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                    "FINISHED calculating routing data for array index {4}: {5}",
                    prevSegmentId,
                    prevLaneIndex,
                    prevLaneId,
                    isNodeStartNodeOfPrevSegment,
                    GetLaneEndRoutingIndex(prevLaneId, isNodeStartNodeOfPrevSegment),
                    backwardRouting);
            }
        }

        /// <summary>
        /// remove all backward routings from this lane and forward routings pointing to this lane
        /// </summary>
        protected void ResetLaneRoutings(uint laneId, bool startNode) {
            uint index = GetLaneEndRoutingIndex(laneId, startNode);
            LaneTransitionData[] oldBackwardTransitions = LaneEndBackwardRoutings[index].transitions;

            if (oldBackwardTransitions != null) {
                for (int i = 0; i < oldBackwardTransitions.Length; ++i) {
                    uint sourceIndex = GetLaneEndRoutingIndex(
                        oldBackwardTransitions[i].laneId,
                        oldBackwardTransitions[i].startNode);
                    LaneEndForwardRoutings[sourceIndex].RemoveTransition(laneId);
                }
            }

            LaneEndBackwardRoutings[index].Reset();
        }

        private void UpdateHighwayLaneArrows(uint laneId, bool startNode, ArrowDirection dir) {
            LaneArrows? prevHighwayArrows = Flags.GetHighwayLaneArrowFlags(laneId);
            var newHighwayArrows = LaneArrows.None;

            if (prevHighwayArrows != null) {
                newHighwayArrows = (LaneArrows)prevHighwayArrows;
            }

            switch (dir) {
                case ArrowDirection.Right:
                    newHighwayArrows |= LaneArrows.Left;
                    break;
                case ArrowDirection.Left:
                    newHighwayArrows |= LaneArrows.Right;
                    break;
                case ArrowDirection.Forward:
                    newHighwayArrows |= LaneArrows.Forward;
                    break;
            }

            if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != LaneArrows.None) {
                Flags.SetHighwayLaneArrowFlags(laneId, newHighwayArrows, false);
            }
        }

        public uint GetLaneEndRoutingIndex(uint laneId, bool startNode) {
            return laneId + (startNode ? 0u : NetManager.MAX_LANE_COUNT);
        }

        public int CalcInnerSimilarLaneIndex(ushort segmentId, int laneIndex) {
            return CalcInnerSimilarLaneIndex(segmentId.ToSegment().Info.m_lanes[laneIndex]);
        }

        public int CalcInnerSimilarLaneIndex(NetInfo.Lane laneInfo) {
            // note: m_direction is correct here
            return (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0
                       ? laneInfo.m_similarLaneIndex
                       : laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1;
        }

        public int CalcOuterSimilarLaneIndex(ushort segmentId, int laneIndex) {
            return CalcOuterSimilarLaneIndex(segmentId.ToSegment().Info.m_lanes[laneIndex]);
        }

        public int CalcOuterSimilarLaneIndex(NetInfo.Lane laneInfo) {
            // note: m_direction is correct here
            return (byte)(laneInfo.m_direction & NetInfo.Direction.Forward) != 0
                       ? laneInfo.m_similarLaneCount - laneInfo.m_similarLaneIndex - 1
                       : laneInfo.m_similarLaneIndex;
        }

        protected int FindLaneWithMaxOuterIndex(int[] indicesSortedByOuterIndex,
                                                int targetOuterLaneIndex) {
            return indicesSortedByOuterIndex[
                Math.Max(0, Math.Min(targetOuterLaneIndex, indicesSortedByOuterIndex.Length - 1))];
        }

        protected int FindLaneByOuterIndex(LaneTransitionData[] laneTransitions,
                                           int num,
                                           ushort segmentId,
                                           int targetOuterLaneIndex) {
            for (int i = 0; i < num; ++i) {
                int outerIndex = CalcOuterSimilarLaneIndex(segmentId, laneTransitions[i].laneIndex);
                if (outerIndex == targetOuterLaneIndex) {
                    return i;
                }
            }

            return -1;
        }

        protected int FindLaneByInnerIndex(LaneTransitionData[] laneTransitions,
                                           int num,
                                           ushort segmentId,
                                           int targetInnerLaneIndex) {
            for (int i = 0; i < num; ++i) {
                int innerIndex = CalcInnerSimilarLaneIndex(segmentId, laneTransitions[i].laneIndex);
                if (innerIndex == targetInnerLaneIndex) {
                    return i;
                }
            }

            return -1;
        }

        protected bool IsOutgoingLane(ushort segmentId, bool startNode, int laneIndex) {
            return IsIncomingOutgoingLane(segmentId, startNode, laneIndex, false);
        }

        protected bool IsIncomingLane(ushort segmentId, bool startNode, int laneIndex) {
            return IsIncomingOutgoingLane(segmentId, startNode, laneIndex, true);
        }

        protected bool IsIncomingOutgoingLane(ushort segmentId,
                                              bool startNode,
                                              int laneIndex,
                                              bool incoming) {
            ref NetSegment segment = ref segmentId.ToSegment();
            bool segIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            NetInfo.Direction dir = startNode ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
            dir = incoming ^ segIsInverted ? NetInfo.InvertDirection(dir) : dir;

            NetInfo.Direction finalDir = segment.Info.m_lanes[laneIndex].m_finalDirection;

            return (finalDir & dir) != NetInfo.Direction.None;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == seg.segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.HandleInvalidSegment({seg.segmentId}) called.");
            }

            Flags.RemoveHighwayLaneArrowFlagsAtSegment(seg.segmentId);
            ResetRoutingData(seg.segmentId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == seg.segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.HandleValidSegment({seg.segmentId}) called.");
            }

            ResetRoutingData(seg.segmentId);
            RequestRecalculation(seg.segmentId);
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();

            RecalculateAll();
        }

        /// <summary>
        /// if the direction of the given lanes do not match then similar lane index must be reversed.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1117:Parameters should be on same line or separate lines", Justification = "beauty")]
        private bool ShouldReverseSimilarLaneIndex(
            bool segmentInvert1, NetInfo.Lane laneInfo1, bool startNode1,
            bool segmentInvert2, NetInfo.Lane laneInfo2, bool startNode2) {
#if DEBUG
            bool logRouting = DebugSwitch.Routing.Get();
#else
            const bool logRouting = false;
#endif
            bool both1 = laneInfo1.m_finalDirection == NetInfo.Direction.Both;
            bool both2 = laneInfo2.m_finalDirection == NetInfo.Direction.Both;
            bool backward1 = laneInfo1.IsGoingBackward();
            bool backward2 = laneInfo2.IsGoingBackward();

            bool reverse;
            if (!both1 && !both2) {
                // both lanes are one-way or station tracks
                // [https://github.com/CitiesSkylinesMods/TMPE/issues/1486#issuecomment-1075699771] what about station tracks connecting to unidirectional tracks?
                reverse = false;
            } else {
                // at least one lane is non-station bidirectional

                reverse = startNode1 == startNode2; // Reverse if segments are facing each other
                reverse ^= segmentInvert1 != segmentInvert2; // Reverse if segments are different directions
                reverse ^= backward1 != backward2;  // Reverse if lanes are in different directions
                // Reversing two times is like not reversing at all. So every time one of the conditions above are met we toggle reverse using the ^= operator.
            }

            if (logRouting) {
                Log._Debug(
                    $"ShouldReverseSimilarLaneIndex() : reverse={reverse} " +
                    $"segmentInvert1={segmentInvert1} startNode1={startNode1} backward1={backward1} both1={both1}\n" +
                    $"segmentInvert2={segmentInvert2}  startNode2={startNode2} backward2={backward2} both2={both2}\n");
            }

            return reverse;
        }
    }
}
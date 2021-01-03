namespace TrafficManager.Manager.Impl {
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

    public class RoutingManager
        : AbstractGeometryObservingManager,
          IRoutingManager
    {
        private RoutingManager() { }

        public static readonly RoutingManager Instance = new RoutingManager();

        private const NetInfo.LaneType ROUTED_LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        private const VehicleInfo.VehicleType ROUTED_VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Metro |
            VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus;

        private const VehicleInfo.VehicleType ARROW_VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        private const byte MAX_NUM_TRANSITIONS = 64;

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

        private bool segmentsUpdated;

        private readonly ulong[] updatedSegmentBuckets = new ulong[576];

        private readonly object updateLock = new object();

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            string buf = $"Segment routings:\n";

            for (var i = 0; i < SegmentRoutings.Length; ++i) {
                if (!Services.NetService.IsSegmentValid((ushort)i)) {
                    continue;
                }

                buf += $"Segment {i}: {SegmentRoutings[i]}\n";
            }

            buf += $"\nLane end backward routings:\n";

            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneAndItsSegmentValid(laneId)) {
                    continue;
                }

                buf += $"Lane {laneId} @ start: {LaneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, true)]}\n";
                buf += $"Lane {laneId} @ end: {LaneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, false)]}\n";
            }

            buf += $"\nLane end forward routings:\n";

            for (uint laneId = 0; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneAndItsSegmentValid(laneId)) {
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

                if (Services.SimulationService.SimulationPaused ||
                    Services.SimulationService.ForcedSimulationPaused) {
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
                ushort startNodeId = Services.NetService.GetSegmentNodeId(segmentId, true);
                Services.NetService.IterateNodeSegments(
                    startNodeId,
                    (ushort otherSegmentId, ref NetSegment otherSeg) => {
                        RequestRecalculation(otherSegmentId, false);
                        return true;
                    });

                ushort endNodeId = Services.NetService.GetSegmentNodeId(segmentId, false);
                Services.NetService.IterateNodeSegments(
                    endNodeId,
                    (ushort otherSegmentId, ref NetSegment otherSeg) => {
                        RequestRecalculation(otherSegmentId, false);
                        return true;
                    });
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
#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get() &&
                         (DebugSettings.SegmentId <= 0 || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug($"RoutingManager.RecalculateSegment({segmentId}) called.");
            }

            if (!Services.NetService.IsSegmentValid(segmentId)) {
                if (logRouting) {
                    Log._Debug($"RoutingManager.RecalculateSegment({segmentId}): " +
                               "Segment is invalid. Skipping recalculation");
                }
                return;
            }

            RecalculateSegmentRoutingData(segmentId);

            Services.NetService.IterateSegmentLanes(
                segmentId,
                (uint laneId,
                 ref NetLane lane,
                 NetInfo.Lane laneInfo,
                 ushort segId,
                 ref NetSegment segment,
                 byte laneIndex) => {
                    RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, true);
                    RecalculateLaneEndRoutingData(segmentId, laneIndex, laneId, false);
                    return true;
                });
        }

        protected void ResetIncomingHighwayLaneArrows(ushort segmentId) {
            ushort[] nodeIds = new ushort[2]; //TODO remove due to unnecessary allocation

            ref NetSegment segment = ref segmentId.ToSegment();
            nodeIds[0] = segment.m_startNode;
            nodeIds[1] = segment.m_endNode;

#if DEBUG
            bool logRouting = DebugSwitch.RoutingBasicLog.Get()
                              && (DebugSettings.SegmentId <= 0
                                  || DebugSettings.SegmentId == segmentId);
#else
            const bool logRouting = false;
#endif
            if (logRouting) {
                Log._Debug("RoutingManager.ResetRoutingData: Identify nodes connected to " +
                           $"{segmentId}: nodeIds={nodeIds.ArrayToString()}");
            }

            // reset highway lane arrows on all incoming lanes
            foreach (ushort nodeId in nodeIds) {
                if (nodeId == 0) {
                    continue;
                }

                Services.NetService.IterateNodeSegments(
                    nodeId,
                    (ushort segId, ref NetSegment netSegment) => {
                        if (segId == segmentId) {
                            return true;
                        }

                        Services.NetService.IterateSegmentLanes(
                            segId,
                            (uint laneId,
                             ref NetLane lane,
                             NetInfo.Lane laneInfo,
                             ushort sId,
                             ref NetSegment seg,
                             byte laneIndex) => {
                                if (IsIncomingLane(
                                    segId,
                                    seg.m_startNode == nodeId,
                                    laneIndex)) {
                                    Flags.RemoveHighwayLaneArrowFlags(laneId);
                                }

                                return true;
                            });
                        return true;
                    });
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

            Services.NetService.IterateSegmentLanes(
                segmentId,
                (uint laneId,
                 ref NetLane lane,
                 NetInfo.Lane laneInfo,
                 ushort segId,
                 ref NetSegment segment,
                 byte laneIndex) => {
                    if (extendedLogRouting) {
                        Log._Debug($"RoutingManager.ResetRoutingData: Resetting lane {laneId}, " +
                                   $"idx {laneIndex} @ seg. {segmentId}");
                    }

                    ResetLaneRoutings(laneId, true);
                    ResetLaneRoutings(laneId, false);

                    return true;
                });
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

        protected void RecalculateLaneEndRoutingData(ushort segmentId,
                                                     int laneIndex,
                                                     uint laneId,
                                                     bool startNode) {
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
                Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                           $"{laneIndex}, {laneId}, {startNode}) called");
            }

            ResetLaneRoutings(laneId, startNode);

            if (!IsOutgoingLane(segmentId, startNode, laneIndex)) {
                if (extendedLogRouting) {
                    Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                               $"{laneIndex}, {laneId}, {startNode}): Lane is not an outgoing lane");
                }

                return;
            }

            ref NetSegment segment = ref segmentId.ToSegment();
            NetInfo prevSegmentInfo = segment.Info;
            bool prevSegIsInverted = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;

            bool lht = Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft;

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment prevSeg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId];
            ExtSegmentEnd prevEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)];

            ushort prevSegmentId = segmentId;
            int prevLaneIndex = laneIndex;
            uint prevLaneId = laneId;
            ushort nextNodeId = prevEnd.nodeId;

            NetInfo.Lane prevLaneInfo = prevSegmentInfo.m_lanes[prevLaneIndex];
            if (!prevLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES)) {
                return;
            }

            LaneEndRoutingData backwardRouting = new LaneEndRoutingData();
            backwardRouting.routed = true;

            int prevSimilarLaneCount = prevLaneInfo.m_similarLaneCount;
            int prevInnerSimilarLaneIndex = CalcInnerSimilarLaneIndex(prevSegmentId, prevLaneIndex);
            int prevOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(prevSegmentId, prevLaneIndex);
            bool prevHasBusLane = prevSeg.buslane;

            bool nextIsJunction = false;
            bool nextIsTransition = false;
            bool nextIsEndOrOneWayOut = false;
            bool nextHasTrafficLights = false;
            bool nextHasPrioritySigns =
                Constants.ManagerFactory.TrafficPriorityManager.HasNodePrioritySign(nextNodeId);
            bool nextIsRealJunction = false;
            ushort buildingId = 0;

            ref NetNode nextNode = ref nextNodeId.ToNode();
            nextIsJunction = (nextNode.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None;
            nextIsTransition = (nextNode.m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
            nextHasTrafficLights = (nextNode.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None;
            nextIsEndOrOneWayOut = (nextNode.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
            nextIsRealJunction = nextNode.CountSegments() >= 3;
            buildingId = NetNode.FindOwnerBuilding(nextNodeId, 32f);

            bool isTollBooth = buildingId != 0
                && buildingId.ToBuilding().Info.m_buildingAI is TollBoothAI;

            bool nextIsSimpleJunction = false;
            bool nextIsSplitJunction = false;

            if (Options.highwayRules && !nextHasTrafficLights && !nextHasPrioritySigns) {
                // determine if junction is a simple junction (highway rules only apply to simple junctions)
                int numOutgoing = 0;
                int numIncoming = 0;

                for (int i = 0; i < 8; ++i) {
                    ushort segId = nextNode.GetSegment(i);
                    if (segId == 0) {
                        continue;
                    }

                    bool start = (bool)Constants.ServiceFactory.NetService.IsStartNode(segId, nextNodeId);
                    ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segId, start)];

                    if (segEnd.incoming) {
                        ++numIncoming;
                    }

                    if (segEnd.outgoing) {
                        ++numOutgoing;
                    }
                }

                nextIsSimpleJunction = numOutgoing == 1 || numIncoming == 1;
                nextIsSplitJunction = numOutgoing > 1;
            }

            // bool isNextRealJunction = prevSegGeo.CountOtherSegments(startNode) > 1;
            bool nextAreOnlyOneWayHighways =
                Constants.ManagerFactory.ExtSegmentEndManager.CalculateOnlyHighways(
                    prevEnd.segmentId,
                    prevEnd.startNode);

            // determine if highway rules should be applied
            bool onHighway = Options.highwayRules && nextAreOnlyOneWayHighways &&
                             prevEnd.outgoing && prevSeg.oneWay && prevSeg.highway;
            bool applyHighwayRules = onHighway && nextIsSimpleJunction;
            bool applyHighwayRulesAtJunction = applyHighwayRules && nextIsRealJunction;
            bool iterateViaGeometry = applyHighwayRulesAtJunction &&
                                      prevLaneInfo.CheckType(
                                          ROUTED_LANE_TYPES,
                                          ARROW_VEHICLE_TYPES);
            // start with u-turns at highway junctions
            ushort nextSegmentId = iterateViaGeometry ? segmentId : (ushort)0;

            if (extendedLogRouting) {
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                    "prevSegment={4}. Starting exploration with nextSegment={5} @ nextNodeId={6} " +
                    "-- onHighway={7} applyHighwayRules={8} applyHighwayRulesAtJunction={9} " +
                    "Options.highwayRules={10} nextIsSimpleJunction={11} nextAreOnlyOneWayHighways={12} " +
                    "prevEndGeo.OutgoingOneWay={13} prevSegGeo.IsHighway()={14} iterateViaGeometry={15}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    segmentId,
                    nextSegmentId,
                    nextNodeId,
                    onHighway,
                    applyHighwayRules,
                    applyHighwayRulesAtJunction,
                    Options.highwayRules,
                    nextIsSimpleJunction,
                    nextAreOnlyOneWayHighways,
                    prevEnd.outgoing && prevSeg.oneWay,
                    prevSeg.highway,
                    iterateViaGeometry);
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                    "prevSegIsInverted={4} leftHandDrive={5}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    prevSegIsInverted,
                    lht);
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                    "prevSimilarLaneCount={4} prevInnerSimilarLaneIndex={5} prevOuterSimilarLaneIndex={6} " +
                    "prevHasBusLane={7}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    prevSimilarLaneCount,
                    prevInnerSimilarLaneIndex,
                    prevOuterSimilarLaneIndex,
                    prevHasBusLane);
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): nextIsJunction={4} " +
                    "nextIsEndOrOneWayOut={5} nextHasTrafficLights={6} nextIsSimpleJunction={7} " +
                    "nextIsSplitJunction={8} isNextRealJunction={9}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    nextIsJunction,
                    nextIsEndOrOneWayOut,
                    nextHasTrafficLights,
                    nextIsSimpleJunction,
                    nextIsSplitJunction,
                    nextIsRealJunction);
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): nextNodeId={4} " +
                    "buildingId={5} isTollBooth={6}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    nextNodeId,
                    buildingId,
                    isTollBooth);
            }

            // running number of next incoming lanes (number is updated at each segment iteration)
            int totalIncomingLanes = 0;

            // running number of next outgoing lanes (number is updated at each segment iteration)
            int totalOutgoingLanes = 0;

            for (int k = 0; k < 8; ++k) {
                if (!iterateViaGeometry) {
                    int kCopy = k;
                    nextSegmentId = nextNode.GetSegment(kCopy);

                    if (nextSegmentId == 0) {
                        continue;
                    }
                }

                int outgoingVehicleLanes = 0;
                int incomingVehicleLanes = 0;

                ref NetSegment nextSegment = ref nextSegmentId.ToSegment();
                bool isNextStartNodeOfNextSegment = nextSegment.m_startNode == nextNodeId;

                NetInfo nextSegmentInfo = nextSegment.Info;
                bool nextSegIsInverted =
                    (nextSegment.m_flags & NetSegment.Flags.Invert) !=
                    NetSegment.Flags.None;
                uint nextFirstLaneId = nextSegment.m_lanes;

                bool nextIsHighway =
                    Constants.ManagerFactory.ExtSegmentManager.CalculateIsHighway(nextSegmentId);
                bool nextHasBusLane =
                    Constants.ManagerFactory.ExtSegmentManager.CalculateHasBusLane(nextSegmentId);

                if (extendedLogRouting) {
                    Log._DebugFormat(
                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): Exploring " +
                        "nextSegmentId={4}",
                        segmentId,
                        laneIndex,
                        laneId,
                        startNode,
                        nextSegmentId);
                    Log._DebugFormat(
                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                        "isNextStartNodeOfNextSegment={4} nextSegIsInverted={5} nextFirstLaneId={6} " +
                        "nextIsHighway={7} nextHasBusLane={8} totalOutgoingLanes={9} totalIncomingLanes={10}",
                        segmentId,
                        laneIndex,
                        laneId,
                        startNode,
                        isNextStartNodeOfNextSegment,
                        nextSegIsInverted,
                        nextFirstLaneId,
                        nextIsHighway,
                        nextHasBusLane,
                        totalOutgoingLanes,
                        totalIncomingLanes);
                }

                // determine next segment direction by evaluating the geometry information
                ArrowDirection nextIncomingDir = segEndMan.GetDirection(ref prevEnd, nextSegmentId);
                bool isNextSegmentValid = nextIncomingDir != ArrowDirection.None;

                if (extendedLogRouting) {
                    Log._DebugFormat(
                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                        "prevSegment={4}. Exploring nextSegment={5} -- nextFirstLaneId={6} " +
                        "-- nextIncomingDir={7} valid={8}",
                        segmentId,
                        laneIndex,
                        laneId,
                        startNode,
                        segmentId,
                        nextSegmentId,
                        nextFirstLaneId,
                        nextIncomingDir,
                        isNextSegmentValid);
                }

                NetInfo.Direction nextDir = isNextStartNodeOfNextSegment
                                                ? NetInfo.Direction.Backward
                                                : NetInfo.Direction.Forward;
                NetInfo.Direction nextDir2 =
                    !nextSegIsInverted ? nextDir : NetInfo.InvertDirection(nextDir);

                LaneTransitionData[] nextRelaxedTransitionDatas = null;
                byte numNextRelaxedTransitionDatas = 0;
                LaneTransitionData[] nextCompatibleTransitionDatas = null;
                int[] nextCompatibleOuterSimilarIndices = null;
                byte numNextCompatibleTransitionDatas = 0;
                LaneTransitionData[] nextLaneConnectionTransitionDatas = null;
                byte numNextLaneConnectionTransitionDatas = 0;
                LaneTransitionData[] nextForcedTransitionDatas = null;
                byte numNextForcedTransitionDatas = 0;
                int[] nextCompatibleTransitionDataIndices = null;
                byte numNextCompatibleTransitionDataIndices = 0;
                int[] compatibleLaneIndexToLaneConnectionIndex = null;

                if (isNextSegmentValid) {
                    nextRelaxedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    nextCompatibleTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    nextLaneConnectionTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    nextForcedTransitionDatas = new LaneTransitionData[MAX_NUM_TRANSITIONS];
                    nextCompatibleOuterSimilarIndices = new int[MAX_NUM_TRANSITIONS];
                    nextCompatibleTransitionDataIndices = new int[MAX_NUM_TRANSITIONS];
                    compatibleLaneIndexToLaneConnectionIndex = new int[MAX_NUM_TRANSITIONS];
                }

                uint nextLaneId = nextFirstLaneId;
                byte nextLaneIndex = 0;

                // ushort compatibleLaneIndicesMask = 0;
                while (nextLaneIndex < nextSegmentInfo.m_lanes.Length && nextLaneId != 0u) {
                    // determine valid lanes based on lane arrows
                    NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextLaneIndex];

                    if (extendedLogRouting) {
                        Log._DebugFormat(
                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                            "prevSegment={4}. Exploring nextSegment={5}, lane {6}, idx {7}",
                            segmentId,
                            laneIndex,
                            laneId,
                            startNode,
                            segmentId,
                            nextSegmentId,
                            nextLaneId,
                            nextLaneIndex);
                    }

                    // next is compatible lane
                    if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ROUTED_VEHICLE_TYPES) &&
                        (prevLaneInfo.m_vehicleType & nextLaneInfo.m_vehicleType) != VehicleInfo.VehicleType.None
                        /*(nextLaneInfo.m_vehicleType & prevLaneInfo.m_vehicleType) != VehicleInfo.VehicleType.None &&
                        (nextLaneInfo.m_laneType & prevLaneInfo.m_laneType) != NetInfo.LaneType.None*/)
                    {
                        if (extendedLogRouting) {
                            Log._Debug(
                                $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                $"{laneIndex}, {laneId}, {startNode}): vehicle type check passed for " +
                                $"nextLaneId={nextLaneId}, idx={nextLaneIndex}");
                        }

                        // next is incoming lane
                        if ((nextLaneInfo.m_finalDirection & nextDir2) != NetInfo.Direction.None) {
                            if (extendedLogRouting) {
                                Log._Debug(
                                    $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                    $"{laneIndex}, {laneId}, {startNode}): lane direction check passed " +
                                    $"for nextLaneId={nextLaneId}, idx={nextLaneIndex}");
                            }

                            ++incomingVehicleLanes;

                            if (extendedLogRouting) {
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "increasing number of incoming lanes at nextLaneId={4}, idx={5}: " +
                                    "isNextValid={6}, nextLaneInfo.m_finalDirection={7}, nextDir2={8}: " +
                                    "incomingVehicleLanes={9}, outgoingVehicleLanes={10} ",
                                    segmentId,
                                    laneIndex,
                                    laneId,
                                    startNode,
                                    nextLaneId,
                                    nextLaneIndex,
                                    isNextSegmentValid,
                                    nextLaneInfo.m_finalDirection,
                                    nextDir2,
                                    incomingVehicleLanes,
                                    outgoingVehicleLanes);
                            }

                            if (isNextSegmentValid) {
                                // calculate current similar lane index starting from outer lane
                                int nextOuterSimilarLaneIndex = CalcOuterSimilarLaneIndex(nextSegmentId, nextLaneIndex);

                                // int nextInnerSimilarLaneIndex = CalcInnerSimilarLaneIndex(nextSegmentId, nextLaneIndex);
                                var isCompatibleLane = false;
                                var transitionType = LaneEndTransitionType.Invalid;

                                // check for lane connections
                                bool nextHasOutgoingConnections =
                                    LaneConnectionManager.Instance.HasConnections(
                                        nextLaneId,
                                        isNextStartNodeOfNextSegment);
                                bool nextIsConnectedWithPrev = true;

                                if (nextHasOutgoingConnections) {
                                    nextIsConnectedWithPrev =
                                        LaneConnectionManager.Instance.AreLanesConnected(
                                            prevLaneId,
                                            nextLaneId,
                                            startNode);
                                }

                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "checking lane connections of nextLaneId={4}, idx={5}: " +
                                        "isNextStartNodeOfNextSegment={6}, nextSegmentId={7}, " +
                                        "nextHasOutgoingConnections={8}, nextIsConnectedWithPrev={9}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        nextLaneId,
                                        nextLaneIndex,
                                        isNextStartNodeOfNextSegment,
                                        nextSegmentId,
                                        nextHasOutgoingConnections,
                                        nextIsConnectedWithPrev);
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "connection information for nextLaneId={4}, idx={5}: " +
                                        "nextOuterSimilarLaneIndex={6}, nextHasOutgoingConnections={7}, " +
                                        "nextIsConnectedWithPrev={8}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        nextLaneId,
                                        nextLaneIndex,
                                        nextOuterSimilarLaneIndex,
                                        nextHasOutgoingConnections,
                                        nextIsConnectedWithPrev);
                                }

                                int currentLaneConnectionTransIndex = -1;

                                if (nextHasOutgoingConnections) {
                                    // check for lane connections
                                    if (nextIsConnectedWithPrev) {
                                        // lane is connected with previous lane
                                        if (numNextLaneConnectionTransitionDatas < MAX_NUM_TRANSITIONS) {
                                            currentLaneConnectionTransIndex =
                                                numNextLaneConnectionTransitionDatas;

                                            nextLaneConnectionTransitionDatas[numNextLaneConnectionTransitionDatas++].Set(
                                                nextLaneId,
                                                nextLaneIndex,
                                                LaneEndTransitionType.LaneConnection,
                                                nextSegmentId,
                                                isNextStartNodeOfNextSegment);
                                        } else {
                                            Log.Warning(
                                                $"nextTransitionDatas overflow @ source lane {prevLaneId}, " +
                                                $"idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                        }

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): nextLaneId={4}, idx={5} has outgoing connections " +
                                                "and is connected with previous lane. adding as lane connection lane.",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextLaneId,
                                                nextLaneIndex);
                                        }
                                    } else {
                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): nextLaneId={4}, idx={5} has outgoing connections " +
                                                "but is NOT connected with previous lane",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextLaneId,
                                                nextLaneIndex);
                                        }
                                    }
                                }

                                if (isTollBooth) {
                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "nextNodeId={4}, buildingId={5} is a toll booth. Preventing lane changes.",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            nextNodeId,
                                            buildingId);
                                    }

                                    if (nextOuterSimilarLaneIndex == prevOuterSimilarLaneIndex) {
                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                                "nextLaneId={4}, idx={5} is associated with a toll booth " +
                                                "(buildingId={6}). adding as Default.",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextLaneId,
                                                nextLaneIndex,
                                                buildingId);
                                        }

                                        isCompatibleLane = true;
                                        transitionType = LaneEndTransitionType.Default;
                                    }
                                } else if (!nextIsJunction) {
                                    if (extendedLogRouting) {
                                        Log._Debug(
                                            $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                            $"{laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, " +
                                            $"idx={nextLaneIndex} is not a junction. adding as Default.");
                                    }

                                    isCompatibleLane = true;
                                    transitionType = LaneEndTransitionType.Default;
                                } else if (nextLaneInfo.CheckType(ROUTED_LANE_TYPES, ARROW_VEHICLE_TYPES)) {
                                    // check for lane arrows
                                    LaneArrows nextLaneArrows =
                                        LaneArrowManager.Instance.GetFinalLaneArrows(nextLaneId);
                                    bool hasLeftArrow = (nextLaneArrows & LaneArrows.Left) != LaneArrows.None;
                                    bool hasRightArrow = (nextLaneArrows & LaneArrows.Right) != LaneArrows.None;
                                    bool hasForwardArrow =
                                        (nextLaneArrows & LaneArrows.Forward) != LaneArrows.None ||
                                        (nextLaneArrows & LaneArrows.LeftForwardRight) ==
                                        LaneArrows.None;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "start lane arrow check for nextLaneId={4}, idx={5}: hasLeftArrow={6}, " +
                                            "hasForwardArrow={7}, hasRightArrow={8}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            nextLaneId,
                                            nextLaneIndex,
                                            hasLeftArrow,
                                            hasForwardArrow,
                                            hasRightArrow);
                                    }

                                    if (applyHighwayRules || // highway rules enabled
                                        (nextIncomingDir == ArrowDirection.Right && hasLeftArrow) || // valid incoming right
                                        (nextIncomingDir == ArrowDirection.Left && hasRightArrow) || // valid incoming left
                                        (nextIncomingDir == ArrowDirection.Forward && hasForwardArrow) || // valid incoming straight
                                        (nextIncomingDir == ArrowDirection.Turn
                                         && (!nextIsRealJunction
                                             || nextIsEndOrOneWayOut
                                             || ((lht && hasRightArrow)
                                                 || (!lht && hasLeftArrow))))) // valid turning lane
                                    {
                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): lane arrow check passed for nextLaneId={4}, " +
                                                "idx={5}. adding as default lane.",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextLaneId,
                                                nextLaneIndex);
                                        }

                                        isCompatibleLane = true;
                                        transitionType = LaneEndTransitionType.Default;
                                    } else if (nextIsConnectedWithPrev) {
                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): lane arrow check FAILED for nextLaneId={4}, " +
                                                "idx={5}. adding as relaxed lane.",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextLaneId,
                                                nextLaneIndex);
                                        }

                                        // lane can be used by all vehicles that may disregard lane arrows
                                        transitionType = LaneEndTransitionType.Relaxed;

                                        if (numNextRelaxedTransitionDatas < MAX_NUM_TRANSITIONS) {
                                            nextRelaxedTransitionDatas[
                                                numNextRelaxedTransitionDatas++].Set(
                                                nextLaneId,
                                                nextLaneIndex,
                                                transitionType,
                                                nextSegmentId,
                                                isNextStartNodeOfNextSegment,
                                                GlobalConfig.Instance.PathFinding.IncompatibleLaneDistance);
                                        } else {
                                            Log.Warning(
                                                $"nextTransitionDatas overflow @ source lane {prevLaneId}, " +
                                                $"idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                        }
                                    }
                                } else if (!nextHasOutgoingConnections) {
                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                            "{2}, {3}): nextLaneId={4}, idx={5} is used by vehicles " +
                                            "that do not follow lane arrows. adding as default.",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            nextLaneId,
                                            nextLaneIndex);
                                    }

                                    // routed vehicle that does not follow lane arrows (trains, trams,
                                    // metros, monorails)
                                    transitionType = LaneEndTransitionType.Default;

                                    if (numNextForcedTransitionDatas < MAX_NUM_TRANSITIONS) {
                                        nextForcedTransitionDatas[numNextForcedTransitionDatas].Set(
                                            nextLaneId,
                                            nextLaneIndex,
                                            transitionType,
                                            nextSegmentId,
                                            isNextStartNodeOfNextSegment);

                                        if (!nextIsRealJunction) {
                                            // simple forced lane transition: set lane distance
                                            nextForcedTransitionDatas[numNextForcedTransitionDatas]
                                                .distance = (byte)Math.Abs(
                                                prevOuterSimilarLaneIndex - nextOuterSimilarLaneIndex);
                                        }

                                        ++numNextForcedTransitionDatas;
                                    } else {
                                        Log.Warning("nextForcedTransitionDatas overflow @ source lane " +
                                                    $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                    }
                                }

                                if (isCompatibleLane) {
                                    if (extendedLogRouting) {
                                        Log._Debug(
                                            $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                            $"{laneIndex}, {laneId}, {startNode}): adding nextLaneId=" +
                                            $"{nextLaneId}, idx={nextLaneIndex} as compatible lane now.");
                                    }

                                    if (numNextCompatibleTransitionDatas < MAX_NUM_TRANSITIONS) {
                                        nextCompatibleOuterSimilarIndices[numNextCompatibleTransitionDatas] =
                                            nextOuterSimilarLaneIndex;

                                        compatibleLaneIndexToLaneConnectionIndex[numNextCompatibleTransitionDatas] =
                                            currentLaneConnectionTransIndex;

                                        //compatibleLaneIndicesMask |= POW2MASKS[numNextCompatibleTransitionDatas];
                                        nextCompatibleTransitionDatas[numNextCompatibleTransitionDatas++].Set(
                                            nextLaneId,
                                            nextLaneIndex,
                                            transitionType,
                                            nextSegmentId,
                                            isNextStartNodeOfNextSegment);
                                    } else {
                                        Log.Warning(
                                            "nextCompatibleTransitionDatas overflow @ source lane " +
                                            $"{prevLaneId}, idx {prevLaneIndex} @ seg. {prevSegmentId}");
                                    }
                                } else {
                                    if (extendedLogRouting) {
                                        Log._Debug(
                                            $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                            $"{laneIndex}, {laneId}, {startNode}): nextLaneId={nextLaneId}, " +
                                            $"idx={nextLaneIndex} is NOT compatible.");
                                    }
                                }
                            }
                        } else {
                            if (extendedLogRouting) {
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "lane direction check NOT passed for nextLaneId={4}, idx={5}: " +
                                    "isNextValid={6}, nextLaneInfo.m_finalDirection={7}, nextDir2={8}",
                                    segmentId,
                                    laneIndex,
                                    laneId,
                                    startNode,
                                    nextLaneId,
                                    nextLaneIndex,
                                    isNextSegmentValid,
                                    nextLaneInfo.m_finalDirection,
                                    nextDir2);
                            }

                            if ((nextLaneInfo.m_finalDirection &
                                 NetInfo.InvertDirection(nextDir2)) != NetInfo.Direction.None)
                            {
                                ++outgoingVehicleLanes;
                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "increasing number of outgoing lanes at nextLaneId={4}, idx={5}: " +
                                        "isNextValid={6}, nextLaneInfo.m_finalDirection={7}, nextDir2={8}: " +
                                        "incomingVehicleLanes={9}, outgoingVehicleLanes={10}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        nextLaneId,
                                        nextLaneIndex,
                                        isNextSegmentValid,
                                        nextLaneInfo.m_finalDirection,
                                        nextDir2,
                                        incomingVehicleLanes,
                                        outgoingVehicleLanes);
                                }
                            }
                        }
                    } else {
                        if (extendedLogRouting) {
                            Log._DebugFormat(
                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                "vehicle type check NOT passed for nextLaneId={4}, idx={5}: " +
                                "prevLaneInfo.m_vehicleType={6}, nextLaneInfo.m_vehicleType={7}, " +
                                "prevLaneInfo.m_laneType={8}, nextLaneInfo.m_laneType={9}",
                                segmentId,
                                laneIndex,
                                laneId,
                                startNode,
                                nextLaneId,
                                nextLaneIndex,
                                prevLaneInfo.m_vehicleType,
                                nextLaneInfo.m_vehicleType,
                                prevLaneInfo.m_laneType,
                                nextLaneInfo.m_laneType);
                        }
                    }

                    nextLaneId = nextLaneId.ToLane().m_nextLane;
                    ++nextLaneIndex;
                } // foreach lane


                if (extendedLogRouting) {
                    Log._Debug(
                        $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, " +
                        $"{laneId}, {startNode}): isNextValid={isNextSegmentValid} Compatible lanes: " +
                        nextCompatibleTransitionDatas?.ArrayToString());
                }

                if (isNextSegmentValid) {
                    bool laneChangesAllowed
                        = Options.junctionRestrictionsEnabled
                          && JunctionRestrictionsManager.Instance.IsLaneChangingAllowedWhenGoingStraight(
                                 nextSegmentId, isNextStartNodeOfNextSegment);
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

                        if (extendedLogRouting) {
                            Log._DebugFormat(
                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): found " +
                                "compatible lanes! compatibleLaneIndicesSortedByOuterSimilarIndex={4}, " +
                                "laneDiff={5}, applyHighwayRulesAtSegment={6}",
                                segmentId,
                                laneIndex,
                                laneId,
                                startNode,
                                compatibleLaneIndicesSortedByOuterSimilarIndex.ArrayToString(),
                                laneDiff,
                                applyHighwayRulesAtSegment);
                        }

                        if (applyHighwayRulesAtJunction) {
                            // we reached a highway junction where more than two segments are connected to each other
                            if (extendedLogRouting) {
                                Log._Debug(
                                    $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, {laneIndex}, " +
                                    $"{laneId}, {startNode}): applying highway rules at junction");
                            }

                            // number of lanes that were processed in earlier segment iterations
                            // (either all incoming or all outgoing)
                            int numLanesSeen = Math.Max(totalIncomingLanes, totalOutgoingLanes);

                            int minNextInnerSimilarIndex = -1;
                            int maxNextInnerSimilarIndex = -1;

                            // this lane will be referred as the "stay" lane with zero distance
                            int refNextInnerSimilarIndex = -1;

#if DEBUGHWJUNCTIONROUTING
                            if (extendedLogRouting) {
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "applying highway rules at junction",
                                    segmentId, laneIndex, laneId, startNode);
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "totalIncomingLanes={4}, totalOutgoingLanes={5}, numLanesSeen={6} " +
                                    "laneChangesAllowed={7}",
                                    segmentId, laneIndex, laneId, startNode, totalIncomingLanes,
                                    totalOutgoingLanes, numLanesSeen, laneChangesAllowed);
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "prevInnerSimilarLaneIndex={4}, prevSimilarLaneCount={5}, " +
                                    "nextCompatibleLaneCount={6}",
                                    segmentId, laneIndex, laneId, startNode, prevInnerSimilarLaneIndex,
                                    prevSimilarLaneCount, nextCompatibleLaneCount);
                            }
#endif

                            if (nextIsSplitJunction) {
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
                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "highway rules at junction: lane splitting junction. " +
                                        "minNextInnerSimilarIndex={4}, maxNextInnerSimilarIndex={5}",
                                        segmentId, laneIndex, laneId, startNode, minNextInnerSimilarIndex,
                                        maxNextInnerSimilarIndex);
                                }
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
                                        && maxNextInnerSimilarIndex < nextCompatibleLaneCount - 1)
                                    {
                                        // we reached the outermost lane on the previous segment but
                                        // there are still lanes to go on the next segment: allow merging
                                        maxNextInnerSimilarIndex = nextCompatibleLaneCount - 1;
                                    }
                                }

#if DEBUGHWJUNCTIONROUTING
                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, " +
                                        "{3}): highway rules at junction: lane merging/unknown junction. " +
                                        "minNextInnerSimilarIndex={4}, maxNextInnerSimilarIndex={5}",
                                        segmentId, laneIndex, laneId, startNode, minNextInnerSimilarIndex,
                                        maxNextInnerSimilarIndex);
                                }
#endif
                            }

                            if (minNextInnerSimilarIndex >= 0) {
#if DEBUGHWJUNCTIONROUTING
                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "minNextInnerSimilarIndex >= 0. nextCompatibleTransitionDatas={4}",
                                        segmentId, laneIndex, laneId, startNode,
                                        nextCompatibleTransitionDatas.ArrayToString());
                                }
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
                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                            "{2}, {3}): highway junction iteration: " +
                                            "nextInnerSimilarIndex={4}, nextTransitionIndex={5}",
                                            segmentId, laneIndex, laneId, startNode, nextInnerSimilarIndex,
                                            nextTransitionIndex);
                                    }
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
                                    if (LaneConnectionManager.Instance.HasConnections(
                                        nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                        isNextStartNodeOfNextSegment))
                                    {
                                        int laneConnectionTransIndex =
                                            compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];

                                        if (laneConnectionTransIndex >= 0) {
                                            nextLaneConnectionTransitionDatas[
                                                laneConnectionTransIndex].distance = compatibleLaneDist;
                                        }

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): Next lane ({4}) has outgoing lane connections. " +
                                                "Skip for now but set compatibleLaneDist={5} if " +
                                                "laneConnectionTransIndex={6} >= 0.",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                                compatibleLaneDist,
                                                laneConnectionTransIndex);
                                        }

                                        // disregard lane since it has outgoing connections
                                        continue;
                                    }

                                    nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;
#if DEBUGHWJUNCTIONROUTING
                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, " +
                                            "{3}): highway junction iteration: compatibleLaneDist={4}",
                                            segmentId, laneIndex, laneId, startNode,
                                            compatibleLaneDist);
                                    }
#endif

                                    UpdateHighwayLaneArrows(
                                        nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                        isNextStartNodeOfNextSegment,
                                        nextIncomingDir);

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
                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "highway junction iterations finished: nextCompatibleTransitionDataIndices={4}",
                                        segmentId, laneIndex, laneId, startNode,
                                        nextCompatibleTransitionDataIndices.ArrayToString());
                                }
#endif
                            }
                        } else {
                            // This is
                            // 1. a highway lane splitting/merging point,
                            // 2. a city or highway lane continuation point (simple transition with
                            //     equal number of lanes or flagged city transition), or
                            // 3. a city junction
                            // with multiple or a single target lane: Perform lane matching
                            if (extendedLogRouting) {
                                Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                           $"{laneIndex}, {laneId}, {startNode}): regular node");
                            }

                            // min/max compatible outer similar lane indices
                            int minNextCompatibleOuterSimilarIndex = -1;
                            int maxNextCompatibleOuterSimilarIndex = -1;
                            if (nextIncomingDir == ArrowDirection.Turn) {
                                minNextCompatibleOuterSimilarIndex = 0;
                                maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "u-turn: minNextCompatibleOuterSimilarIndex={4}, " +
                                        "maxNextCompatibleOuterSimilarIndex={5}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        minNextCompatibleOuterSimilarIndex,
                                        maxNextCompatibleOuterSimilarIndex);
                                }
                            } else if (nextIsRealJunction) {
                                if (extendedLogRouting) {
                                    Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                               $"{laneIndex}, {laneId}, {startNode}): next is real junction");
                                }

                                // at junctions: try to match distinct lanes
                                if (nextCompatibleLaneCount > prevSimilarLaneCount
                                    && prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1)
                                {
                                    // merge inner lanes
                                    minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
                                    maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "merge inner lanes: minNextCompatibleOuterSimilarIndex={4}, " +
                                            "maxNextCompatibleOuterSimilarIndex={5}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            minNextCompatibleOuterSimilarIndex,
                                            maxNextCompatibleOuterSimilarIndex);
                                    }
                                } else if (nextCompatibleLaneCount < prevSimilarLaneCount
                                           && prevSimilarLaneCount % nextCompatibleLaneCount == 0)
                                {
                                    // symmetric split
                                    int splitFactor =
                                        prevSimilarLaneCount / nextCompatibleLaneCount;
                                    minNextCompatibleOuterSimilarIndex =
                                        maxNextCompatibleOuterSimilarIndex =
                                            prevOuterSimilarLaneIndex / splitFactor;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                            "{2}, {3}): symmetric split: minNextCompatibleOuterSimilarIndex={4}, " +
                                            "maxNextCompatibleOuterSimilarIndex={5}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            minNextCompatibleOuterSimilarIndex,
                                            maxNextCompatibleOuterSimilarIndex);
                                    }
                                } else {
                                    // 1-to-n (split inner lane) or 1-to-1 (direct lane matching)
                                    minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;
                                    maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                            "{2}, {3}): 1-to-n (split inner lane) or 1-to-1 (direct " +
                                            "lane matching): minNextCompatibleOuterSimilarIndex={4}, " +
                                            "maxNextCompatibleOuterSimilarIndex={5}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            minNextCompatibleOuterSimilarIndex,
                                            maxNextCompatibleOuterSimilarIndex);
                                    }
                                }

                                bool straightLaneChangesAllowed =
                                    nextIncomingDir == ArrowDirection.Forward && laneChangesAllowed;

                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "laneChangesAllowed={4} straightLaneChangesAllowed={5}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        laneChangesAllowed,
                                        straightLaneChangesAllowed);
                                }

                                if (!straightLaneChangesAllowed) {
                                    if (nextHasBusLane && !prevHasBusLane) {
                                        // allow vehicles on the bus lane AND on the next lane to merge on this lane
                                        maxNextCompatibleOuterSimilarIndex = Math.Min(
                                            nextCompatibleLaneCount - 1,
                                            maxNextCompatibleOuterSimilarIndex + 1);

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): allow vehicles on the bus lane AND on the " +
                                                "next lane to merge on this lane: " +
                                                "minNextCompatibleOuterSimilarIndex={4}, " +
                                                "maxNextCompatibleOuterSimilarIndex={5}",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                minNextCompatibleOuterSimilarIndex,
                                                maxNextCompatibleOuterSimilarIndex);
                                        }
                                    } else if (!nextHasBusLane && prevHasBusLane) {
                                        // allow vehicles to enter the bus lane
                                        minNextCompatibleOuterSimilarIndex = Math.Max(
                                            0, minNextCompatibleOuterSimilarIndex - 1);

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): allow vehicles to enter the bus lane: " +
                                                "minNextCompatibleOuterSimilarIndex={4}, " +
                                                "maxNextCompatibleOuterSimilarIndex={5}",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                minNextCompatibleOuterSimilarIndex,
                                                maxNextCompatibleOuterSimilarIndex);
                                        }
                                    }
                                } else {
                                    // vehicles may change lanes when going straight
                                    minNextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex - 1;
                                    maxNextCompatibleOuterSimilarIndex = maxNextCompatibleOuterSimilarIndex + 1;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, " +
                                            "{3}): vehicles may change lanes when going straight: " +
                                            "minNextCompatibleOuterSimilarIndex={4}, " +
                                            "maxNextCompatibleOuterSimilarIndex={5}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            minNextCompatibleOuterSimilarIndex,
                                            maxNextCompatibleOuterSimilarIndex);
                                    }
                                }
                            } else if (prevSimilarLaneCount == nextCompatibleLaneCount) {
                                // equal lane count: consider all available lanes
                                minNextCompatibleOuterSimilarIndex = 0;
                                maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "equal lane count: minNextCompatibleOuterSimilarIndex={4}, " +
                                        "maxNextCompatibleOuterSimilarIndex={5}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        minNextCompatibleOuterSimilarIndex,
                                        maxNextCompatibleOuterSimilarIndex);
                                }
                            } else {
                                // lane continuation point: lane merging/splitting
                                if (extendedLogRouting) {
                                    Log._Debug(
                                        $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                        $"{laneIndex}, {laneId}, {startNode}): lane continuation point: " +
                                        "lane merging/splitting");
                                }

                                bool sym1 = (prevSimilarLaneCount & 1) == 0; // mod 2 == 0
                                bool sym2 = (nextCompatibleLaneCount & 1) == 0; // mod 2 == 0
                                if (extendedLogRouting) {
                                    Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                               $"{laneIndex}, {laneId}, {startNode}): sym1={sym1}, sym2={sym2}");
                                }

                                if (prevSimilarLaneCount < nextCompatibleLaneCount) {
                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, " +
                                            "{3}): lane merging (prevSimilarLaneCount={4} < " +
                                            "nextCompatibleLaneCount={5})",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            prevSimilarLaneCount,
                                            nextCompatibleLaneCount);
                                    }

                                    // lane merging
                                    if (sym1 == sym2) {
                                        // merge outer lanes
                                        // nextCompatibleLaneCount - prevSimilarLaneCount is always > 0
                                        int a = (nextCompatibleLaneCount - prevSimilarLaneCount) >> 1;

                                        if (extendedLogRouting) {
                                            Log._Debug(
                                                $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                                $"{laneIndex}, {laneId}, {startNode}): merge outer lanes. a={a}");
                                        }

                                        if (prevSimilarLaneCount == 1) {
                                            minNextCompatibleOuterSimilarIndex = 0;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevSimilarLaneCount == 1: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else if (prevOuterSimilarLaneIndex == 0) {
                                            minNextCompatibleOuterSimilarIndex = 0;
                                            maxNextCompatibleOuterSimilarIndex = a;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevOuterSimilarLaneIndex == 0: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else {
                                            minNextCompatibleOuterSimilarIndex =
                                                maxNextCompatibleOuterSimilarIndex =
                                                    prevOuterSimilarLaneIndex + a;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): default case: minNextCompatibleOuterSimilarIndex" +
                                                    "={4}, maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        }
                                    } else {
                                        // criss-cross merge
                                        // nextCompatibleLaneCount - prevSimilarLaneCount - 1 is always >= 0
                                        int a = (nextCompatibleLaneCount - prevSimilarLaneCount - 1) >> 1;

                                        // nextCompatibleLaneCount - prevSimilarLaneCount + 1 is always >= 2
                                        int b = (nextCompatibleLaneCount - prevSimilarLaneCount + 1) >> 1;

                                        if (extendedLogRouting) {
                                            Log._Debug(
                                                $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                                $"{laneIndex}, {laneId}, {startNode}): criss-cross merge: " +
                                                $"a={a}, b={b}");
                                        }

                                        if (prevSimilarLaneCount == 1) {
                                            minNextCompatibleOuterSimilarIndex = 0;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;
                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevSimilarLaneCount == 1: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else if (prevOuterSimilarLaneIndex == 0) {
                                            minNextCompatibleOuterSimilarIndex = 0;
                                            maxNextCompatibleOuterSimilarIndex = b;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevOuterSimilarLaneIndex == 0: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else if (prevOuterSimilarLaneIndex == prevSimilarLaneCount - 1) {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;

                                            // always >=0
                                            maxNextCompatibleOuterSimilarIndex = nextCompatibleLaneCount - 1;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): prevOuterSimilarLaneIndex == " +
                                                    "prevSimilarLaneCount - 1: minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        } else {
                                            minNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + a;
                                            maxNextCompatibleOuterSimilarIndex = prevOuterSimilarLaneIndex + b;

                                            if (extendedLogRouting) {
                                                Log._DebugFormat(
                                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                    "{2}, {3}): default criss-cross case: " +
                                                    "minNextCompatibleOuterSimilarIndex={4}, " +
                                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                                    segmentId,
                                                    laneIndex,
                                                    laneId,
                                                    startNode,
                                                    minNextCompatibleOuterSimilarIndex,
                                                    maxNextCompatibleOuterSimilarIndex);
                                            }
                                        }
                                    }
                                } else {
                                    // at lane splits: distribute traffic evenly (1-to-n, n-to-n)
                                    // prevOuterSimilarIndex is always > nextCompatibleLaneCount
                                    if (extendedLogRouting) {
                                        Log._Debug(
                                            $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                            $"{laneIndex}, {laneId}, {startNode}): at lane splits: " +
                                            "distribute traffic evenly (1-to-n, n-to-n)");
                                    }

                                    if (sym1 == sym2) {
                                        // split outer lanes
                                        // prevSimilarLaneCount - nextCompatibleLaneCount is always > 0
                                        int a = (prevSimilarLaneCount - nextCompatibleLaneCount) >> 1;

                                        // a is always <= prevSimilarLaneCount
                                        minNextCompatibleOuterSimilarIndex =
                                            maxNextCompatibleOuterSimilarIndex =
                                                prevOuterSimilarLaneIndex - a;

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): split outer lanes: " +
                                                "minNextCompatibleOuterSimilarIndex={4}, " +
                                                "maxNextCompatibleOuterSimilarIndex={5}",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                minNextCompatibleOuterSimilarIndex,
                                                maxNextCompatibleOuterSimilarIndex);
                                        }
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

                                        if (extendedLogRouting) {
                                            Log._DebugFormat(
                                                "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, " +
                                                "{2}, {3}): split outer lanes, criss-cross inner lanes: " +
                                                "minNextCompatibleOuterSimilarIndex={4}, " +
                                                "maxNextCompatibleOuterSimilarIndex={5}",
                                                segmentId,
                                                laneIndex,
                                                laneId,
                                                startNode,
                                                minNextCompatibleOuterSimilarIndex,
                                                maxNextCompatibleOuterSimilarIndex);
                                        }
                                    }
                                }
                            }

                            if (extendedLogRouting) {
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "pre-final bounds: minNextCompatibleOuterSimilarIndex={4}, " +
                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                    segmentId,
                                    laneIndex,
                                    laneId,
                                    startNode,
                                    minNextCompatibleOuterSimilarIndex,
                                    maxNextCompatibleOuterSimilarIndex);
                            }

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

                            if (extendedLogRouting) {
                                Log._DebugFormat(
                                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                    "final bounds: minNextCompatibleOuterSimilarIndex={4}, " +
                                    "maxNextCompatibleOuterSimilarIndex={5}",
                                    segmentId,
                                    laneIndex,
                                    laneId,
                                    startNode,
                                    minNextCompatibleOuterSimilarIndex,
                                    maxNextCompatibleOuterSimilarIndex);
                            }

                            // find best matching lane(s)
                            for (int nextCompatibleOuterSimilarIndex = minNextCompatibleOuterSimilarIndex;
                                 nextCompatibleOuterSimilarIndex <= maxNextCompatibleOuterSimilarIndex;
                                 ++nextCompatibleOuterSimilarIndex)
                            {
                                int nextTransitionIndex = FindLaneWithMaxOuterIndex(
                                    compatibleLaneIndicesSortedByOuterSimilarIndex,
                                    nextCompatibleOuterSimilarIndex);

                                if (extendedLogRouting) {
                                    Log._DebugFormat(
                                        "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                        "best matching lane iteration -- nextCompatibleOuterSimilarIndex={4} " +
                                        "=> nextTransitionIndex={5}",
                                        segmentId,
                                        laneIndex,
                                        laneId,
                                        startNode,
                                        nextCompatibleOuterSimilarIndex,
                                        nextTransitionIndex);
                                }

                                if (nextTransitionIndex < 0) {
                                    continue;
                                }

                                // calculate lane distance
                                byte compatibleLaneDist = 0;

                                if (nextIncomingDir == ArrowDirection.Turn) {
                                    compatibleLaneDist = (byte)GlobalConfig
                                                               .Instance.PathFinding
                                                               .UturnLaneDistance;
                                } else if (!nextIsRealJunction &&
                                           ((!nextIsJunction && !nextIsTransition) ||
                                            nextCompatibleLaneCount == prevSimilarLaneCount)) {
                                    // relative lane distance (positive: change to more outer lane,
                                    // negative: change to more inner lane)
                                    int relLaneDist =
                                        nextCompatibleOuterSimilarIndices[nextTransitionIndex] -
                                        prevOuterSimilarLaneIndex;
                                    compatibleLaneDist = (byte)Math.Abs(relLaneDist);
                                }

                                // skip lanes having lane connections
                                if (LaneConnectionManager.Instance.HasConnections(
                                    nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                    isNextStartNodeOfNextSegment))
                                {
                                    int laneConnectionTransIndex =
                                        compatibleLaneIndexToLaneConnectionIndex[nextTransitionIndex];

                                    if (laneConnectionTransIndex >= 0) {
                                        nextLaneConnectionTransitionDatas[laneConnectionTransIndex]
                                            .distance = compatibleLaneDist;
                                    }

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "Next lane ({4}) has outgoing lane connections. Skip for now but " +
                                            "set compatibleLaneDist={5} if laneConnectionTransIndex={6} >= 0.",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                            compatibleLaneDist,
                                            laneConnectionTransIndex);
                                    }

                                    continue; // disregard lane since it has outgoing connections
                                }

                                if (nextIncomingDir == ArrowDirection.Turn && // u-turn
                                    !nextIsEndOrOneWayOut && // not a dead end
                                    // incoming lane is not innermost lane
                                    nextCompatibleOuterSimilarIndex != maxNextCompatibleOuterSimilarIndex)
                                {
                                    // force u-turns to happen on the innermost lane
                                    ++compatibleLaneDist;
                                    nextCompatibleTransitionDatas[nextTransitionIndex].type =
                                        LaneEndTransitionType.Relaxed;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "Next lane ({4}) is avoided u-turn. Incrementing compatible " +
                                            "lane distance to {5}",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode,
                                            nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                            compatibleLaneDist);
                                    }
                                }

                                if (extendedLogRouting) {
                                    Log._Debug(
                                        $"RoutingManager.RecalculateLaneEndRoutingData({segmentId}, " +
                                        $"{laneIndex}, {laneId}, {startNode}): -> " +
                                        $"compatibleLaneDist={compatibleLaneDist}");
                                }

                                nextCompatibleTransitionDatas[nextTransitionIndex].distance = compatibleLaneDist;

                                if (onHighway && !nextIsRealJunction && compatibleLaneDist > 1) {
                                    // under normal circumstances vehicles should not change more
                                    // than one lane on highways at one time
                                    nextCompatibleTransitionDatas[nextTransitionIndex].type
                                        = LaneEndTransitionType.Relaxed;

                                    if (extendedLogRouting) {
                                        Log._DebugFormat(
                                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                                            "-> under normal circumstances vehicles should not change " +
                                            "more than one lane on highways at one time: setting type to Relaxed",
                                            segmentId,
                                            laneIndex,
                                            laneId,
                                            startNode);
                                    }
                                } else if (applyHighwayRulesAtSegment) {
                                    UpdateHighwayLaneArrows(
                                        nextCompatibleTransitionDatas[nextTransitionIndex].laneId,
                                        isNextStartNodeOfNextSegment,
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

                    if (extendedLogRouting) {
                        Log._DebugFormat(
                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): build " +
                            "array for nextSegment={4}: nextTransitionDatas={5}",
                            segmentId,
                            laneIndex,
                            laneId,
                            startNode,
                            nextSegmentId,
                            nextTransitionDatas.ArrayToString());
                    }

                    backwardRouting.AddTransitions(nextTransitionDatas);

                    if (extendedLogRouting) {
                        Log._DebugFormat(
                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                            "updated incoming/outgoing lanes for next segment iteration: " +
                            "totalIncomingLanes={4}, totalOutgoingLanes={5}",
                            segmentId,
                            laneIndex,
                            laneId,
                            startNode,
                            totalIncomingLanes,
                            totalOutgoingLanes);
                    }
                } // valid segment

                if (nextSegmentId != prevSegmentId) {
                    totalIncomingLanes += incomingVehicleLanes;
                    totalOutgoingLanes += outgoingVehicleLanes;
                }

                if (iterateViaGeometry) {
                    ref NetSegment nextSegment2 = ref nextSegmentId.ToSegment();
                    nextSegmentId = Constants.ServiceFactory.SimulationService.TrafficDrivesOnLeft
                        ? nextSegment2.GetLeftSegment(nextNodeId)
                        : nextSegment2.GetRightSegment(nextNodeId);

                    if (nextSegmentId == prevSegmentId || nextSegmentId == 0) {
                        // we reached the first segment again
                        break;
                    }
                }
            } // foreach segment

            // update backward routing
            LaneEndBackwardRoutings[GetLaneEndRoutingIndex(laneId, startNode)] = backwardRouting;

            // update forward routing
            LaneTransitionData[] newTransitions = backwardRouting.transitions;
            if (newTransitions != null) {
                for (int i = 0; i < newTransitions.Length; ++i) {
                    uint sourceIndex = GetLaneEndRoutingIndex(
                        newTransitions[i].laneId,
                        newTransitions[i].startNode);

                    LaneTransitionData forwardTransition = new LaneTransitionData {
                        laneId = laneId,
                        laneIndex = (byte)laneIndex,
                        type = newTransitions[i].type,
                        distance = newTransitions[i].distance,
                        segmentId = segmentId,
                        startNode = startNode,
                    };

                    LaneEndForwardRoutings[sourceIndex].AddTransition(forwardTransition);

                    if (extendedLogRouting) {
                        Log._DebugFormat(
                            "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                            "adding transition to forward routing of laneId={4}, idx={5} @ seg. " +
                            "{6} @ node {7} (sourceIndex={8}): {9}\n\nNew forward routing:\n{10}",
                            segmentId,
                            laneIndex,
                            laneId,
                            startNode,
                            laneId,
                            laneIndex,
                            newTransitions[i].segmentId,
                            newTransitions[i].startNode,
                            sourceIndex,
                            forwardTransition,
                            LaneEndForwardRoutings[sourceIndex]);
                    }
                }
            }

            if (logRouting) {
                Log._DebugFormat(
                    "RoutingManager.RecalculateLaneEndRoutingData({0}, {1}, {2}, {3}): " +
                    "FINISHED calculating routing data for array index {4}: {5}",
                    segmentId,
                    laneIndex,
                    laneId,
                    startNode,
                    GetLaneEndRoutingIndex(laneId, startNode),
                    backwardRouting);
            }
        }

        /// <summary>
        /// remove all backward routings from this lane and forward routings pointing to this lane
        /// </summary>
        /// <param name="laneId"></param>
        /// <param name="startNode"></param>
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

            // Log._Debug($"RoutingManager.RecalculateLaneEndRoutingData: highway rules -- next lane " +
            //           $"{laneId} obeys highway rules. Setting highway lane arrows to {newHighwayArrows}. " +
            //           $"prevHighwayArrows={prevHighwayArrows}");

            if (newHighwayArrows != prevHighwayArrows && newHighwayArrows != LaneArrows.None) {
                Flags.SetHighwayLaneArrowFlags(laneId, newHighwayArrows, false);
            }
        }

        // private int GetSegmentNodeIndex(ushort nodeId, ushort segmentId) {
        //    int i = -1;
        //    Services.NetService.IterateNodeSegments(
        //        nodeId,
        //        (ushort segId, ref NetSegment segment) => {
        //            ++i;
        //            if (segId == segmentId) {
        //                return false;
        //            }
        //
        //            return true;
        //        });
        //    return i;
        // }

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

        [UsedImplicitly]
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
    }
}
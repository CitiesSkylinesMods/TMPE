namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic;
    using TrafficManager.Geometry;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using static TrafficManager.Util.Shortcuts;
    using static CSUtil.Commons.TernaryBoolUtil;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Lifecycle;
    using TrafficManager.API.Traffic.Enums;

    public class JunctionRestrictionsManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.SegmentNodeConf>>,
          IJunctionRestrictionsManager
    {
        public static JunctionRestrictionsManager Instance { get; } =
            new JunctionRestrictionsManager();

        private readonly SegmentJunctionRestrictions[] invalidSegmentRestrictions;

        /// <summary>
        /// Holds junction restrictions for each segment end
        /// </summary>
        private readonly SegmentJunctionRestrictions[] segmentRestrictions;

        private JunctionRestrictionsManager() {
            segmentRestrictions = new SegmentJunctionRestrictions[NetManager.MAX_SEGMENT_COUNT];
            invalidSegmentRestrictions = new SegmentJunctionRestrictions[NetManager.MAX_SEGMENT_COUNT];
        }

        private void AddInvalidSegmentJunctionRestrictions(ushort segmentId,
                                                           bool startNode,
                                                           ref JunctionRestrictions restrictions) {
            if (startNode) {
                invalidSegmentRestrictions[segmentId].startNodeRestrictions = restrictions;
            } else {
                invalidSegmentRestrictions[segmentId].endNodeRestrictions = restrictions;
            }
        }

        protected override void HandleSegmentEndReplacement(SegmentEndReplacement replacement,
                                                            ref ExtSegmentEnd segEnd) {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ISegmentEndId oldSegmentEndId = replacement.oldSegmentEndId;
            ISegmentEndId newSegmentEndId = replacement.newSegmentEndId;

            JunctionRestrictions restrictions;
            if (oldSegmentEndId.StartNode) {
                restrictions = invalidSegmentRestrictions[oldSegmentEndId.SegmentId].startNodeRestrictions;
                invalidSegmentRestrictions[oldSegmentEndId.SegmentId].startNodeRestrictions.Reset();
            } else {
                restrictions = invalidSegmentRestrictions[oldSegmentEndId.SegmentId].endNodeRestrictions;
                invalidSegmentRestrictions[oldSegmentEndId.SegmentId].endNodeRestrictions.Reset();
            }

            UpdateDefaults(
                ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(newSegmentEndId.SegmentId, newSegmentEndId.StartNode)],
                ref restrictions,
                ref segEnd.nodeId.ToNode());

            Log._Debug(
                $"JunctionRestrictionsManager.HandleSegmentEndReplacement({replacement}): " +
                $"Segment replacement detected: {oldSegmentEndId.SegmentId} -> {newSegmentEndId.SegmentId} " +
                $"@ {newSegmentEndId.StartNode}");

            SetSegmentJunctionRestrictions(newSegmentEndId.SegmentId, newSegmentEndId.StartNode, restrictions);
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                ExtSegment seg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[i];
                if (seg.valid) {
                    HandleValidSegment(ref seg);
                }
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Junction restrictions:");

            for (int i = 0; i < segmentRestrictions.Length; ++i) {
                if (segmentRestrictions[i].IsDefault()) {
                    continue;
                }

                Log._Debug($"Segment {i}: {segmentRestrictions[i]}");
            }
        }

        private bool MayHaveJunctionRestrictions(ushort nodeId) {
            ref NetNode netNode = ref nodeId.ToNode();

            Log._Debug($"JunctionRestrictionsManager.MayHaveJunctionRestrictions({nodeId}): " +
                       $"flags={netNode.m_flags}");

            return netNode.m_flags.IsFlagSet(NetNode.Flags.Junction | NetNode.Flags.Bend)
                && netNode.IsValid();
        }

        public bool HasJunctionRestrictions(ushort nodeId) {
            ref NetNode netNode = ref nodeId.ToNode();
            if (!netNode.IsValid()) {
                return false;
            }

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = netNode.GetSegment(i);
                if (segmentId != 0) {
                    bool startNode = segmentId.ToSegment().m_startNode == nodeId;
                    bool isDefault = startNode
                        ? segmentRestrictions[segmentId].startNodeRestrictions.IsDefault()
                        : segmentRestrictions[segmentId].endNodeRestrictions.IsDefault();

                    if (!isDefault) {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RemoveJunctionRestrictions(ushort nodeId) {
            Log._Debug($"JunctionRestrictionsManager.RemoveJunctionRestrictions({nodeId}) called.");

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    if (segmentId.ToSegment().m_startNode == nodeId) {
                        segmentRestrictions[segmentId].startNodeRestrictions.Reset(false);
                    } else {
                        segmentRestrictions[segmentId].endNodeRestrictions.Reset(false);
                    }
                }
            }
        }

        [UsedImplicitly]
        public void RemoveJunctionRestrictionsIfNecessary() {
            for (uint nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
                RemoveJunctionRestrictionsIfNecessary((ushort)nodeId);
            }
        }

        public void RemoveJunctionRestrictionsIfNecessary(ushort nodeId) {
            if (!MayHaveJunctionRestrictions(nodeId)) {
                RemoveJunctionRestrictions(nodeId);
            }
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            HandleInvalidSegment(ref seg, false);
            HandleInvalidSegment(ref seg, true);
        }

        private void HandleInvalidSegment(ref ExtSegment seg, bool startNode) {
            JunctionRestrictions restrictions = startNode
                                                ? segmentRestrictions[seg.segmentId].startNodeRestrictions
                                                : segmentRestrictions[seg.segmentId].endNodeRestrictions;

            if (!restrictions.IsDefault()) {
                AddInvalidSegmentJunctionRestrictions(seg.segmentId, startNode, ref restrictions);
            }

            segmentRestrictions[seg.segmentId].Reset(startNode, true);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
            UpdateDefaults(ref seg);
        }

        /// <summary>
        /// called when deserializing or when policy changes.
        /// TODO [issue #1116]: publish segment changes? if so it should be done only when policy changes not when deserializing.
        /// </summary>
        public void UpdateAllDefaults() {
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                UpdateDefaults(ref extSegmentManager.ExtSegments[segmentId]);
            }
        }

        private void UpdateDefaults(ref ExtSegment seg) {
            ushort segmentId = seg.segmentId;
            ref NetSegment netSegment = ref segmentId.ToSegment();
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            UpdateDefaults(
                ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, true)],
                ref segmentRestrictions[segmentId].startNodeRestrictions,
                ref netSegment.m_startNode.ToNode());

            UpdateDefaults(
                ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, false)],
                ref segmentRestrictions[segmentId].endNodeRestrictions,
                ref netSegment.m_endNode.ToNode());
        }

        private void UpdateDefaults(ref ExtSegmentEnd segEnd,
                                    ref JunctionRestrictions restrictions,
                                    ref NetNode node) {
            if (!IsUturnAllowedConfigurable(segEnd.segmentId, segEnd.startNode, ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowUTurn);
            }

            if (!IsNearTurnOnRedAllowedConfigurable(segEnd.segmentId, segEnd.startNode, ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowNearTurnOnRed);
            }

            if (!IsFarTurnOnRedAllowedConfigurable(segEnd.segmentId, segEnd.startNode, ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowFarTurnOnRed);
            }

            if (!IsLaneChangingAllowedWhenGoingStraightConfigurable(
                    segEnd.segmentId,
                    segEnd.startNode,
                    ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowForwardLaneChange);
            }

            if (!IsEnteringBlockedJunctionAllowedConfigurable(
                    segEnd.segmentId,
                    segEnd.startNode,
                    ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowEnterWhenBlocked);
            }

            if (!IsPedestrianCrossingAllowedConfigurable(
                    segEnd.segmentId,
                    segEnd.startNode,
                    ref node)) {
                restrictions.ClearValue(JunctionRestrictionFlags.AllowPedestrianCrossing);
            }

            restrictions.SetDefault(JunctionRestrictionFlags.AllowUTurn, GetDefaultUturnAllowed(
                segEnd.segmentId,
                segEnd.startNode,
                ref node));
            restrictions.SetDefault(JunctionRestrictionFlags.AllowNearTurnOnRed, GetDefaultNearTurnOnRedAllowed(
                segEnd.segmentId,
                segEnd.startNode,
                ref node));
            restrictions.SetDefault(JunctionRestrictionFlags.AllowFarTurnOnRed, GetDefaultFarTurnOnRedAllowed(
                segEnd.segmentId,
                segEnd.startNode,
                ref node));
            restrictions.SetDefault(JunctionRestrictionFlags.AllowForwardLaneChange,
                GetDefaultLaneChangingAllowedWhenGoingStraight(
                    segEnd.segmentId,
                    segEnd.startNode,
                    ref node));
            restrictions.SetDefault(JunctionRestrictionFlags.AllowEnterWhenBlocked, GetDefaultEnteringBlockedJunctionAllowed(
                segEnd.segmentId,
                segEnd.startNode,
                ref node));
            restrictions.SetDefault(JunctionRestrictionFlags.AllowPedestrianCrossing, GetDefaultPedestrianCrossingAllowed(
                segEnd.segmentId,
                segEnd.startNode,
                ref node));

#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._DebugFormat(
                    "JunctionRestrictionsManager.UpdateDefaults({0}, {1}): Set defaults: " +
                    "defaultUturnAllowed={2}, defaultNearTurnOnRedAllowed={3}, " +
                    "defaultFarTurnOnRedAllowed={4}, defaultStraightLaneChangingAllowed={5}, " +
                    "defaultEnterWhenBlockedAllowed={6}, defaultPedestrianCrossingAllowed={7}",
                    segEnd.segmentId,
                    segEnd.startNode,
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowUTurn),
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowNearTurnOnRed),
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowFarTurnOnRed),
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowForwardLaneChange),
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowEnterWhenBlocked),
                    restrictions.GetDefault(JunctionRestrictionFlags.AllowPedestrianCrossing));
            }
#endif
            Notifier.Instance.OnNodeModified(segEnd.nodeId, this);
        }

        public bool IsUturnAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            bool ret =
                (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition |
                                 NetNode.Flags.End | NetNode.Flags.Bend |
                                 NetNode.Flags.OneWayOut)) != NetNode.Flags.None
                && node.Info?.m_class?.m_service != ItemClass.Service.Beautification
                && !Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId].oneWay;
#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._DebugFormat(
                    "JunctionRestrictionsManager.IsUturnAllowedConfigurable({0}, {1}): ret={2}, " +
                    "flags={3}, service={4}, seg.oneWay={5}",
                    segmentId,
                    startNode,
                    ret,
                    node.m_flags,
                    node.Info?.m_class?.m_service,
                    Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId].oneWay);
            }
#endif
            return ret;
        }

        public bool GetDefaultUturnAllowed(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            if (!Constants.ManagerFactory.JunctionRestrictionsManager.IsUturnAllowedConfigurable(
                    segmentId,
                    startNode,
                    ref node)) {
                // vanilla one-way or no other segment with matching vehicle categories
                bool res = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None ||
                           ((node.m_flags2 & NetNode.Flags2.PedestrianStreetTransition) != 0 &&!HasOtherSegmentsWithCategories(segmentId, ref node));
                if (logLogic) {
                    Log._Debug(
                        $"JunctionRestrictionsManager.GetDefaultUturnAllowed({segmentId}, " +
                        $"{startNode}): Setting is not configurable. res={res}, flags={node.m_flags}");
                }

                return res;
            }

            bool ret = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None ||
                       ((node.m_flags2 & NetNode.Flags2.PedestrianStreetTransition) != 0 &&!HasOtherSegmentsWithCategories(segmentId, ref node));

            if (!ret && SavedGameOptions.Instance.allowUTurns) {
                ret = (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) !=
                      NetNode.Flags.None;
            }

            if (logLogic) {
                Log._Debug(
                    $"JunctionRestrictionsManager.GetDefaultUturnAllowed({segmentId}, " +
                    $"{startNode}): Setting is configurable. ret={ret}, flags={node.m_flags}");
            }

            return ret;
        }

        private bool HasOtherSegmentsWithCategories(ushort ignoreSegment, ref NetNode node) {
            VehicleInfo.VehicleCategory category = ignoreSegment.ToSegment().Info.m_vehicleCategories;
            for (int i = 0; i < Constants.MAX_SEGMENTS_OF_NODE; i++) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId == 0 || segmentId == ignoreSegment) {
                    continue;
                }
                ref NetSegment segment = ref segmentId.ToSegment();
                if ((segment.Info.m_vehicleCategories & category) == category) {
                    return true;
                }
            }
            return false;
        }

        public bool IsUturnAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowUTurn, startNode);
        }

        public bool IsNearTurnOnRedAllowedConfigurable(ushort segmentId,
                                                       bool startNode,
                                                       ref NetNode node) {
            return IsTurnOnRedAllowedConfigurable(true, segmentId, startNode, ref node);
        }

        public bool IsFarTurnOnRedAllowedConfigurable(ushort segmentId,
                                                      bool startNode,
                                                      ref NetNode node) {
            return IsTurnOnRedAllowedConfigurable(false, segmentId, startNode, ref node);
        }

        public bool IsTurnOnRedAllowedConfigurable(bool near,
                                                   ushort segmentId,
                                                   bool startNode,
                                                   ref NetNode node) {
            ITurnOnRedManager turnOnRedMan = Constants.ManagerFactory.TurnOnRedManager;
            int index = turnOnRedMan.GetIndex(segmentId, startNode);
            bool lht = LHT;
            bool ret =
                (node.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None &&
                (((lht == near) && turnOnRedMan.TurnOnRedSegments[index].leftSegmentId != 0) ||
                ((!lht == near) && turnOnRedMan.TurnOnRedSegments[index].rightSegmentId != 0));
#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._Debug(
                    $"JunctionRestrictionsManager.IsTurnOnRedAllowedConfigurable({near}, " +
                    $"{segmentId}, {startNode}): ret={ret}");
            }
#endif

            return ret;
        }

        public bool GetDefaultNearTurnOnRedAllowed(ushort segmentId,
                                                   bool startNode,
                                                   ref NetNode node) {
            return GetDefaultTurnOnRedAllowed(true, segmentId, startNode, ref node);
        }

        public bool GetDefaultFarTurnOnRedAllowed(ushort segmentId,
                                                  bool startNode,
                                                  ref NetNode node) {
            return GetDefaultTurnOnRedAllowed(false, segmentId, startNode, ref node);
        }

        public bool GetDefaultTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            if (!IsTurnOnRedAllowedConfigurable(near, segmentId, startNode, ref node)) {
                if (logLogic) {
                    Log._Debug(
                        $"JunctionRestrictionsManager.IsTurnOnRedAllowedConfigurable({near}, " +
                        $"{segmentId}, {startNode}): Setting is not configurable. res=false");
                }

                return false;
            }

            bool ret = near ? SavedGameOptions.Instance.allowNearTurnOnRed : SavedGameOptions.Instance.allowFarTurnOnRed;
            if (logLogic) {
                Log._Debug(
                    $"JunctionRestrictionsManager.GetTurnOnRedAllowed({near}, {segmentId}, " +
                    $"{startNode}): Setting is configurable. ret={ret}, flags={node.m_flags}");
            }

            return ret;
        }

        public bool IsTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) {
            return near
                       ? IsNearTurnOnRedAllowed(segmentId, startNode)
                       : IsFarTurnOnRedAllowed(segmentId, startNode);
        }

        public bool IsNearTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode);
        }

        public bool IsFarTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode);
        }

        public bool IsLaneChangingAllowedWhenGoingStraightConfigurable(
            ushort segmentId,
            bool startNode,
            ref NetNode node) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            bool isOneWay = segMan.ExtSegments[segmentId].oneWay;
            bool ret =
                (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Transition)) !=
                NetNode.Flags.None &&
                node.Info?.m_class?.m_service != ItemClass.Service.Beautification &&
                !(isOneWay && segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)]
                                       .outgoing) && node.CountSegments() > 2;
#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._DebugFormat(
                    "JunctionRestrictionsManager.IsLaneChangingAllowedWhenGoingStraightConfigurable" +
                    "({0}, {1}): ret={2}, flags={3}, service={4}, outgoingOneWay={5}, " +
                    "node.CountSegments()={6}",
                    segmentId,
                    startNode,
                    ret,
                    node.m_flags,
                    node.Info?.m_class?.m_service,
                    isOneWay && segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)].outgoing,
                    node.CountSegments());
            }
#endif
            return ret;
        }

        public bool GetDefaultLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            if (!Constants.ManagerFactory.JunctionRestrictionsManager
                          .IsLaneChangingAllowedWhenGoingStraightConfigurable(
                              segmentId,
                              startNode,
                              ref node)) {
                if (logLogic) {
                    Log._Debug(
                        "JunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight" +
                        $"({segmentId}, {startNode}): Setting is not configurable. res=false");
                }

                return false;
            }

            bool ret = SavedGameOptions.Instance.allowLaneChangesWhileGoingStraight;

            if (logLogic) {
                Log._Debug(
                    "JunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight" +
                    $"({segmentId}, {startNode}): Setting is configurable. ret={ret}");
            }

            return ret;
        }

        public bool IsLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowForwardLaneChange, startNode);
        }

        public bool IsEnteringBlockedJunctionAllowedConfigurable(
            ushort segmentId,
            bool startNode,
            ref NetNode node) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            bool isOneWay = segMan.ExtSegments[segmentId].oneWay;
            bool ret = (node.m_flags & NetNode.Flags.Junction) != NetNode.Flags.None &&
                       node.Info?.m_class?.m_service != ItemClass.Service.Beautification &&
                       !(isOneWay
                         && segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)].outgoing);

#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._DebugFormat(
                    "JunctionRestrictionsManager.IsEnteringBlockedJunctionAllowedConfigurable" +
                    "({0}, {1}): ret={2}, flags={3}, service={4}, outgoingOneWay={5}",
                    segmentId,
                    startNode,
                    ret,
                    node.m_flags,
                    node.Info?.m_class?.m_service,
                    isOneWay && segEndMan.ExtSegmentEnds[segEndMan.GetIndex(segmentId, startNode)].outgoing);
            }
#endif
            return ret;
        }

        public bool GetDefaultEnteringBlockedJunctionAllowed(
            ushort segmentId,
            bool startNode,
            ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }

            if (!IsEnteringBlockedJunctionAllowedConfigurable(segmentId, startNode, ref node)) {
                bool res =
                    (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.OneWayOut |
                                     NetNode.Flags.OneWayIn)) != NetNode.Flags.Junction ||
                    node.CountSegments() == 2;
                if (logLogic) {
                    Log._DebugFormat(
                        "JunctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed" +
                        "({0}, {1}): Setting is not configurable. res={2}, flags={3}, " +
                        "node.CountSegments()={4}",
                        segmentId,
                        startNode,
                        res,
                        node.m_flags,
                        node.CountSegments());
                }

                return res;
            }

            bool ret;
            if (SavedGameOptions.Instance.allowEnterBlockedJunctions) {
                ret = true;
            } else {
                ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;
                int numOutgoing = 0;
                int numIncoming = 0;
                node.CountLanes(
                    nodeId,
                    0,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    VehicleInfo.VehicleType.Car,
                    VehicleInfo.VehicleCategory.All,
                    true,
                    ref numOutgoing,
                    ref numIncoming);
                ret = numOutgoing == 1 || numIncoming == 1;
            }

            if (logLogic) {
                Log._Debug(
                    "JunctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed" +
                    $"({segmentId}, {startNode}): Setting is configurable. ret={ret}");
            }

            return ret;
        }

        public bool IsEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode);
        }

        public bool IsPedestrianCrossingAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) {
            bool ret = (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Bend)) != NetNode.Flags.None
                       && node.Info?.m_class?.m_service != ItemClass.Service.Beautification
                       && !node.Info.IsPedestrianZoneRoad() && (segmentId.ToSegment().Info.m_connectGroup & NetInfo.ConnectGroup.Pedestrian) == 0;
#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._Debug(
                    "JunctionRestrictionsManager.IsPedestrianCrossingAllowedConfigurable" +
                    $"({segmentId}, {startNode}): ret={ret}, flags={node.m_flags}, " +
                    $"service={node.Info?.m_class?.m_service}, " +
                    $"nodePedestrianZone={node.Info.IsPedestrianZoneRoad()}, " +
                    $"segmentConnectGroup={segmentId.ToSegment().Info.m_connectGroup}");
            }
#endif
            return ret;
        }

        public bool GetDefaultPedestrianCrossingAllowed(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            if (!IsPedestrianCrossingAllowedConfigurable(segmentId, startNode, ref node)) {
                if (logLogic) {
                    Log._Debug(
                        "JunctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed" +
                        $"({segmentId}, {startNode}): Setting is not configurable. res=true");
                }

                return true;
            }

            if (SavedGameOptions.Instance.NoDoubleCrossings &&
                node.m_flags.IsFlagSet(NetNode.Flags.Junction) &&
                !node.m_flags.IsFlagSet(NetNode.Flags.Untouchable) &&
                node.CountSegments() == 2) {

                // there are only two segments so left segment is the same as right.
                ushort otherSegmentID = startNode
                    ? segmentId.ToSegment().m_startLeftSegment
                    : segmentId.ToSegment().m_endLeftSegment;

                NetInfo info1 = segmentId.ToSegment().Info;
                NetInfo info2 = otherSegmentID.ToSegment().Info;
                bool hasPedestrianLanes1 = info1.m_hasPedestrianLanes;
                bool hasPedestrianLanes2 = info2.m_hasPedestrianLanes;

                // if only one of them has pedestrian lane then
                // only the segment with pedestrian lanes need crossings
                // also if neither have pedestrian lanes then none need crossing.
                if (!hasPedestrianLanes1)
                    return false;
                if (!hasPedestrianLanes2)
                    return true;

                float sizeDiff = info1.m_halfWidth - info2.m_halfWidth;
                if (sizeDiff == 0)
                    return true; //if same size then both will get crossings.

                // at bridge/tunnel entrances, pedestrian crossing is on ground road.
                bool isRoad1 = info1.m_netAI is RoadAI;
                bool isRoad2 = info2.m_netAI is RoadAI;
                if (isRoad1 && !isRoad2)
                    return true; // only this segment needs pedestrian crossing.
                if (isRoad2 && !isRoad1)
                    return false; // only the other segment needs pedestrian crossing.

                if (sizeDiff > 0)
                    return false; // only the smaller segment needs pedestrian crossing.
            }

            // crossing is allowed at junctions and at untouchable nodes (for example: spiral
            // underground parking)
            bool ret = (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Untouchable)) !=
                       NetNode.Flags.None;

            if (logLogic) {
                Log._Debug(
                    $"JunctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed({segmentId}, " +
                    $"{startNode}): Setting is configurable. ret={ret}, flags={node.m_flags}");
            }

            return ret;
        }

        public bool IsPedestrianCrossingAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetValueOrDefault(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode);
        }

        public TernaryBool GetUturnAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowUTurn, startNode);
        }

        public TernaryBool GetNearTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode);
        }

        public TernaryBool GetFarTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode);
        }

        public TernaryBool GetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) {
            return near
                       ? GetNearTurnOnRedAllowed(segmentId, startNode)
                       : GetFarTurnOnRedAllowed(segmentId, startNode);
        }

        public TernaryBool GetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowForwardLaneChange, startNode);
        }

        public TernaryBool GetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode);
        }

        public TernaryBool GetPedestrianCrossingAllowed(ushort segmentId, bool startNode) {
            return segmentRestrictions[segmentId].GetTernaryBool(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode);
        }

        public bool ToggleUturnAllowed(ushort segmentId, bool startNode) {
            return SetUturnAllowed(segmentId, startNode, !IsUturnAllowed(segmentId, startNode));
        }

        public bool ToggleNearTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return ToggleTurnOnRedAllowed(true, segmentId, startNode);
        }

        public bool ToggleFarTurnOnRedAllowed(ushort segmentId, bool startNode) {
            return ToggleTurnOnRedAllowed(false, segmentId, startNode);
        }

        public bool ToggleTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) {
            return SetTurnOnRedAllowed(near, segmentId, startNode, !IsTurnOnRedAllowed(near, segmentId, startNode));
        }

        public bool ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) {
            return SetLaneChangingAllowedWhenGoingStraight(
                segmentId,
                startNode,
                !IsLaneChangingAllowedWhenGoingStraight(segmentId, startNode));
        }

        public bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) {
            return SetEnteringBlockedJunctionAllowed(
                segmentId,
                startNode,
                !IsEnteringBlockedJunctionAllowed(segmentId, startNode));
        }

        public bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) {
            return SetPedestrianCrossingAllowed(
                segmentId,
                startNode,
                !IsPedestrianCrossingAllowed(segmentId, startNode));
        }

        public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) {
            return SetUturnAllowed(segmentId, startNode, ToTernaryBool(value));
        }

        public bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, bool value) {
            return SetNearTurnOnRedAllowed(segmentId, startNode, ToTernaryBool(value));
        }

        public bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, bool value) {
            return SetFarTurnOnRedAllowed(segmentId, startNode, ToTernaryBool(value));
        }

        public bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, bool value) {
            return SetTurnOnRedAllowed(near, segmentId, startNode, ToTernaryBool(value));
        }

        public bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value) {
            return SetLaneChangingAllowedWhenGoingStraight(
                segmentId,
                startNode,
                ToTernaryBool(value));
        }

        public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) {
            return SetEnteringBlockedJunctionAllowed(
                segmentId,
                startNode,
                ToTernaryBool(value));
        }

        public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) {
            return SetPedestrianCrossingAllowed(
                segmentId,
                startNode,
                ToTernaryBool(value));
        }

        public bool ClearSegmentEnd(ushort segmentId, bool startNode) {
            bool ret = true;
            ret |= SetPedestrianCrossingAllowed(segmentId, startNode, TernaryBool.Undefined);
            ret |= SetEnteringBlockedJunctionAllowed(segmentId, startNode, TernaryBool.Undefined);
            ret |= SetLaneChangingAllowedWhenGoingStraight(segmentId, startNode, TernaryBool.Undefined);
            ret |= SetFarTurnOnRedAllowed(segmentId, startNode, TernaryBool.Undefined);
            ret |= SetNearTurnOnRedAllowed(segmentId, startNode, TernaryBool.Undefined);
            ret |= SetUturnAllowed(segmentId, startNode, TernaryBool.Undefined);
            return ret;
        }

        public bool ClearNode(ushort nodeId) {
            bool ret = false;
            ref NetNode node = ref nodeId.ToNode();
            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId != 0) {
                    bool startNode = segmentId.ToSegment().IsStartNode(nodeId);
                    ret |= ClearSegmentEnd(segmentId, startNode);
                }
            }
            return ret;
        }

        private void SetSegmentJunctionRestrictions(ushort segmentId, bool startNode, JunctionRestrictions restrictions) {
            if (restrictions.HasValue(JunctionRestrictionFlags.AllowUTurn)) {
                SetUturnAllowed(segmentId, startNode, restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowUTurn));
            }

            if (restrictions.HasValue(JunctionRestrictionFlags.AllowNearTurnOnRed)) {
                SetNearTurnOnRedAllowed(segmentId, startNode, restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowNearTurnOnRed));
            }

            if (restrictions.HasValue(JunctionRestrictionFlags.AllowFarTurnOnRed)) {
                SetFarTurnOnRedAllowed(segmentId, startNode, restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowFarTurnOnRed));
            }

            if (restrictions.HasValue(JunctionRestrictionFlags.AllowForwardLaneChange)) {
                SetLaneChangingAllowedWhenGoingStraight(
                    segmentId,
                    startNode,
                    restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowForwardLaneChange));
            }

            if (restrictions.HasValue(JunctionRestrictionFlags.AllowEnterWhenBlocked)) {
                SetEnteringBlockedJunctionAllowed(
                    segmentId,
                    startNode,
                    restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowEnterWhenBlocked));
            }

            if (restrictions.HasValue(JunctionRestrictionFlags.AllowPedestrianCrossing)) {
                SetPedestrianCrossingAllowed(
                    segmentId,
                    startNode,
                    restrictions.GetValueOrDefault(JunctionRestrictionFlags.AllowPedestrianCrossing));
            }
        }

        private static ref NetNode GetNode(ushort segmentId, bool startNode) =>
            ref segmentId.ToSegment().GetNodeId(startNode).ToNode();

        #region Set<Traffic Rule>Allowed: TernaryBool

        public bool SetUturnAllowed(ushort segmentId, bool startNode, TernaryBool value) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }
            if(GetUturnAllowed(segmentId, startNode) == value) {
                return true;
            }
            if(!IsUturnAllowedConfigurable(segmentId, startNode, ref GetNode(segmentId, startNode))) {
                return false;
            }

            if (value == TernaryBool.False && Constants.ManagerFactory.LaneConnectionManager.HasUturnConnections(
                    segmentId,
                    startNode)) {
                return false;
            }

            segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowUTurn, startNode, value);
            OnSegmentChange(
                segmentId,
                startNode,
                ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                true);
            return true;
        }

        public bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value) {
            return SetTurnOnRedAllowed(true, segmentId, startNode, value);
        }

        public bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value) {
            return SetTurnOnRedAllowed(false, segmentId, startNode, value);
        }

        public bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, TernaryBool value) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }
            if (GetTurnOnRedAllowed(near, segmentId, startNode) == value) {
                return true;
            }
            if (!IsTurnOnRedAllowedConfigurable(near, segmentId, startNode, ref GetNode(segmentId, startNode))) {
                return false;
            }

            if (value == TernaryBool.False && Constants.ManagerFactory.LaneConnectionManager.HasUturnConnections(
                    segmentId,
                    startNode)) {
                return false;
            }

            if (near) {
                segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowNearTurnOnRed, startNode, value);
            } else {
                segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowFarTurnOnRed, startNode, value);
            }
            OnSegmentChange(segmentId, startNode, ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId], true);
            return true;
        }

        public bool SetLaneChangingAllowedWhenGoingStraight(
            ushort segmentId,
            bool startNode,
            TernaryBool value) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }
            if (GetLaneChangingAllowedWhenGoingStraight(segmentId, startNode) == value) {
                return true;
            }
            if (!IsLaneChangingAllowedWhenGoingStraightConfigurable(segmentId, startNode, ref GetNode(segmentId, startNode))) {
                return false;
            }

            segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowForwardLaneChange, startNode, value);
            OnSegmentChange(
                segmentId,
                startNode,
                ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                true);
            return true;
        }

        public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, TernaryBool value) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }
            if (GetEnteringBlockedJunctionAllowed(segmentId, startNode) == value) {
                return true;
            }
            if (!IsEnteringBlockedJunctionAllowedConfigurable(segmentId, startNode, ref GetNode(segmentId, startNode))) {
                return false;
            }

            segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowEnterWhenBlocked, startNode, value);

            // recalculation not needed here because this is a simulation-time feature
            OnSegmentChange(
                segmentId,
                startNode,
                ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                false);
            return true;
        }

        public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, TernaryBool value) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                return false;
            }
            if(GetPedestrianCrossingAllowed(segmentId, startNode) == value) {
                return true;
            }
            if(!IsPedestrianCrossingAllowedConfigurable(segmentId, startNode, ref GetNode(segmentId, startNode))) {
                return false;
            }

            segmentRestrictions[segmentId].SetValue(JunctionRestrictionFlags.AllowPedestrianCrossing, startNode, value);
            OnSegmentChange(
                segmentId,
                startNode,
                ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                true);
            return true;
        }

#endregion

        private void OnSegmentChange(ushort segmentId,
                                     bool startNode,
                                     ref ExtSegment seg,
                                     bool requireRecalc) {
            HandleValidSegment(ref seg);

            if (requireRecalc) {
                RoutingManager.Instance.RequestRecalculation(segmentId);
                if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                    ExtSegmentManager.Instance.PublishSegmentChanges(segmentId);
                }
            }

            Notifier.Instance.OnNodeModified(segmentId.ToSegment().GetNodeId(startNode), this);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < segmentRestrictions.Length; ++i) {
                segmentRestrictions[i].Reset(startNode: null, resetDefaults: true);
            }

            for (int i = 0; i < invalidSegmentRestrictions.Length; ++i) {
                invalidSegmentRestrictions[i].Reset(startNode: null, resetDefaults: true);
            }
        }

        public bool LoadData(List<Configuration.SegmentNodeConf> data) {
            bool success = true;
            Log.Info($"Loading junction restrictions. {data.Count} elements");

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            foreach (Configuration.SegmentNodeConf segNodeConf in data) {
                try {
                    ref NetSegment netSegment = ref segNodeConf.segmentId.ToSegment();

                    if (!netSegment.IsValid()) {
                        continue;
                    }

#if DEBUGLOAD
                    Log._Debug($"JunctionRestrictionsManager.LoadData: Loading junction restrictions for segment {segNodeConf.segmentId}: startNodeFlags={segNodeConf.startNodeFlags} endNodeFlags={segNodeConf.endNodeFlags}");
#endif
                    if (segNodeConf.startNodeFlags != null) {
                        ushort startNodeId = netSegment.m_startNode;
                        if (startNodeId != 0) {
                            Configuration.SegmentNodeFlags flags = segNodeConf.startNodeFlags;
                            ref NetNode startNode = ref startNodeId.ToNode();

                            if (flags.uturnAllowed != null &&
                                        IsUturnAllowedConfigurable(
                                            segNodeConf.segmentId,
                                            true,
                                            ref startNode)) {
                                SetUturnAllowed(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.uturnAllowed);
                            }

                            if (flags.turnOnRedAllowed != null &&
                                IsNearTurnOnRedAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    true,
                                    ref startNode)) {
                                SetNearTurnOnRedAllowed(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.turnOnRedAllowed);
                            }

                            if (flags.farTurnOnRedAllowed != null &&
                                IsFarTurnOnRedAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    true,
                                    ref startNode)) {
                                SetFarTurnOnRedAllowed(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.farTurnOnRedAllowed);
                            }

                            if (flags.straightLaneChangingAllowed != null &&
                                IsLaneChangingAllowedWhenGoingStraightConfigurable(
                                    segNodeConf.segmentId,
                                    true,
                                    ref startNode)) {
                                SetLaneChangingAllowedWhenGoingStraight(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.straightLaneChangingAllowed);
                            }

                            if (flags.enterWhenBlockedAllowed != null &&
                                IsEnteringBlockedJunctionAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    true,
                                    ref startNode)) {
                                SetEnteringBlockedJunctionAllowed(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.enterWhenBlockedAllowed);
                            }

                            if (flags.pedestrianCrossingAllowed != null &&
                                IsPedestrianCrossingAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    true,
                                    ref startNode)) {
                                SetPedestrianCrossingAllowed(
                                    segNodeConf.segmentId,
                                    true,
                                    (bool)flags.pedestrianCrossingAllowed);
                            }
                        } else {
                            Log.Warning(
                                "JunctionRestrictionsManager.LoadData(): Could not get segment " +
                                $"end geometry for segment {segNodeConf.segmentId} @ start node");
                        }
                    }

                    if (segNodeConf.endNodeFlags != null) {
                        ushort endNodeId = netSegment.m_endNode;
                        if (endNodeId != 0) {
                            Configuration.SegmentNodeFlags flags1 = segNodeConf.endNodeFlags;
                            ref NetNode node = ref endNodeId.ToNode();

                            if (flags1.uturnAllowed != null &&
                                IsUturnAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetUturnAllowed(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.uturnAllowed);
                            }

                            if (flags1.straightLaneChangingAllowed != null &&
                                IsLaneChangingAllowedWhenGoingStraightConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetLaneChangingAllowedWhenGoingStraight(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.straightLaneChangingAllowed);
                            }

                            if (flags1.enterWhenBlockedAllowed != null &&
                                IsEnteringBlockedJunctionAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetEnteringBlockedJunctionAllowed(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.enterWhenBlockedAllowed);
                            }

                            if (flags1.pedestrianCrossingAllowed != null &&
                                IsPedestrianCrossingAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetPedestrianCrossingAllowed(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.pedestrianCrossingAllowed);
                            }

                            if (flags1.turnOnRedAllowed != null &&
                                IsNearTurnOnRedAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetNearTurnOnRedAllowed(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.turnOnRedAllowed);
                            }

                            if (flags1.farTurnOnRedAllowed != null &&
                                IsFarTurnOnRedAllowedConfigurable(
                                    segNodeConf.segmentId,
                                    false,
                                    ref node)) {
                                SetFarTurnOnRedAllowed(
                                    segNodeConf.segmentId,
                                    false,
                                    (bool)flags1.farTurnOnRedAllowed);
                            }
                        } else {
                            Log.Warning(
                                "JunctionRestrictionsManager.LoadData(): Could not get segment " +
                                $"end geometry for segment {segNodeConf.segmentId} @ end node");
                        }
                    }
                } catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"Error loading junction restrictions @ segment {segNodeConf.segmentId}: " + e);
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.SegmentNodeConf> SaveData(ref bool success) {
            var ret = new List<Configuration.SegmentNodeConf>();
            NetManager netManager = Singleton<NetManager>.instance;

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
                try {
                    ref NetSegment netSegment = ref ((ushort)segmentId).ToSegment();

                    if (!netSegment.IsValid()) {
                        continue;
                    }

                    Configuration.SegmentNodeFlags startNodeFlags = null;
                    Configuration.SegmentNodeFlags endNodeFlags = null;

                    ushort startNodeId = netSegment.m_startNode;

                    if (startNodeId.ToNode().IsValid()) {
                        JunctionRestrictions endFlags = segmentRestrictions[segmentId].startNodeRestrictions;

                        if (!endFlags.IsDefault()) {
                            startNodeFlags = new Configuration.SegmentNodeFlags();

                            startNodeFlags.uturnAllowed =
                                TernaryBoolUtil.ToOptBool(GetUturnAllowed((ushort)segmentId, true));
                            startNodeFlags.turnOnRedAllowed = TernaryBoolUtil.ToOptBool(
                                GetNearTurnOnRedAllowed((ushort)segmentId, true));
                            startNodeFlags.farTurnOnRedAllowed = TernaryBoolUtil.ToOptBool(
                                GetFarTurnOnRedAllowed((ushort)segmentId, true));
                            startNodeFlags.straightLaneChangingAllowed = TernaryBoolUtil.ToOptBool(
                                GetLaneChangingAllowedWhenGoingStraight((ushort)segmentId, true));
                            startNodeFlags.enterWhenBlockedAllowed = TernaryBoolUtil.ToOptBool(
                                GetEnteringBlockedJunctionAllowed((ushort)segmentId, true));
                            startNodeFlags.pedestrianCrossingAllowed = TernaryBoolUtil.ToOptBool(
                                GetPedestrianCrossingAllowed((ushort)segmentId, true));

#if DEBUGSAVE
                            Log._Debug($"JunctionRestrictionsManager.SaveData: Saving start node "+
                            $"junction restrictions for segment {segmentId}: {startNodeFlags}");
#endif
                        }
                    }

                    ushort endNodeId = netSegment.m_endNode;

                    if (endNodeId.ToNode().IsValid()) {
                        JunctionRestrictions restrictions = segmentRestrictions[segmentId].endNodeRestrictions;

                        if (!restrictions.IsDefault()) {
                            endNodeFlags = new Configuration.SegmentNodeFlags();

                            endNodeFlags.uturnAllowed =
                                TernaryBoolUtil.ToOptBool(
                                    GetUturnAllowed((ushort)segmentId, false));
                            endNodeFlags.turnOnRedAllowed = TernaryBoolUtil.ToOptBool(
                                GetNearTurnOnRedAllowed((ushort)segmentId, false));
                            endNodeFlags.farTurnOnRedAllowed = TernaryBoolUtil.ToOptBool(
                                GetFarTurnOnRedAllowed((ushort)segmentId, false));
                            endNodeFlags.straightLaneChangingAllowed = TernaryBoolUtil.ToOptBool(
                                GetLaneChangingAllowedWhenGoingStraight((ushort)segmentId, false));
                            endNodeFlags.enterWhenBlockedAllowed = TernaryBoolUtil.ToOptBool(
                                GetEnteringBlockedJunctionAllowed((ushort)segmentId, false));
                            endNodeFlags.pedestrianCrossingAllowed = TernaryBoolUtil.ToOptBool(
                                GetPedestrianCrossingAllowed((ushort)segmentId, false));

#if DEBUGSAVE
                            Log._Debug($"JunctionRestrictionsManager.SaveData: Saving end node junction "+
                            $"restrictions for segment {segmentId}: {endNodeFlags}");
#endif
                        }
                    }

                    if (startNodeFlags == null && endNodeFlags == null) {
                        continue;
                    }

                    var conf = new Configuration.SegmentNodeConf((ushort)segmentId);

                    conf.startNodeFlags = startNodeFlags;
                    conf.endNodeFlags = endNodeFlags;

#if DEBUGSAVE
                    Log._Debug($"Saving segment-at-node flags for seg. {segmentId}");
#endif
                    ret.Add(conf);
                } catch (Exception e) {
                    Log.Error(
                        $"Exception occurred while saving segment node flags @ {segmentId}: {e}");
                    success = false;
                }
            }

            return ret;
        }

        private struct SegmentJunctionRestrictions {
            public JunctionRestrictions startNodeRestrictions;
            public JunctionRestrictions endNodeRestrictions;

            public bool GetValueOrDefault(JunctionRestrictionFlags flags, bool startNode) {
                return (startNode ? startNodeRestrictions : endNodeRestrictions).GetValueOrDefault(flags);
            }

            public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags, bool startNode) {
                return (startNode ? startNodeRestrictions : endNodeRestrictions).GetTernaryBool(flags);
            }

            public void SetValue(JunctionRestrictionFlags flags, bool startNode, TernaryBool value) {
                if (startNode)
                    startNodeRestrictions.SetValue(flags, value);
                else
                    endNodeRestrictions.SetValue(flags, value);
            }

            public bool IsDefault() {
                return startNodeRestrictions.IsDefault() && endNodeRestrictions.IsDefault();
            }

            public void Reset(bool? startNode = null, bool resetDefaults = true) {
                if (startNode == null || (bool)startNode) {
                    startNodeRestrictions.Reset(resetDefaults);
                }

                if (startNode == null || !(bool)startNode) {
                    endNodeRestrictions.Reset(resetDefaults);
                }
            }

            public override string ToString() {
                return "[SegmentJunctionRestrictions\n" +
                        $"\tstartNodeRestrictions = {startNodeRestrictions}\n" +
                        $"\tendNodeRestrictions = {endNodeRestrictions}\n" +
                        "SegmentJunctionRestrictions]";
            }
        }

        private struct JunctionRestrictions {

            private JunctionRestrictionFlags values;

            private JunctionRestrictionFlags mask;

            private JunctionRestrictionFlags defaults;


            public void ClearValue(JunctionRestrictionFlags flags) {
                values &= ~flags;
                mask &= ~flags;
            }

            public void SetDefault(JunctionRestrictionFlags flags, bool value) {
                if (value)
                    defaults |= flags;
                else
                    defaults &= ~flags;
            }

            public bool GetDefault(JunctionRestrictionFlags flags) {
                return (defaults & flags) == flags;
            }

            public bool HasValue(JunctionRestrictionFlags flags) {
                return (mask & flags) == flags;
            }

            public TernaryBool GetTernaryBool(JunctionRestrictionFlags flags) {
                return (mask & flags) == flags
                        ? (values & flags) == flags
                            ? TernaryBool.True
                            : TernaryBool.False
                        : TernaryBool.Undefined;
            }

            public bool GetValueOrDefault(JunctionRestrictionFlags flags) {
                return ((values & flags & mask) | (defaults & flags & ~mask)) == flags;
            }

            public void SetValue(JunctionRestrictionFlags flags, TernaryBool value) {
                switch (value) {
                    case TernaryBool.True:
                        values |= flags;
                        mask |= flags;
                        break;

                    case TernaryBool.False:
                        values &= ~flags;
                        mask |= flags;
                        break;

                    case TernaryBool.Undefined:
                        values &= ~flags;
                        mask &= ~flags;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(value));
                }
            }

            public bool IsDefault() {
                return ((values & mask) | (defaults & ~mask)) == defaults;
            }

            public void Reset(bool resetDefaults = true) {
                values = mask = default;

                if (resetDefaults) {
                    defaults = default;
                }
            }

            public override string ToString() {
                return string.Format(
                    $"[JunctionRestrictions\n\tvalues = {values}\n\tmask = {mask}\n" +
                    $"defaults = {defaults}\n" +
                    "JunctionRestrictions]");
            }
        }
    }
}
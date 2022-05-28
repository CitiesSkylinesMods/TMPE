namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Hook;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic;
    using TrafficManager.Geometry;
    using TrafficManager.Network.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using static TrafficManager.Util.Shortcuts;
    using static CSUtil.Commons.TernaryBoolUtil;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using TrafficManager.Lifecycle;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl.LaneConnection;
    using static TrafficManager.API.Hook.IJunctionRestrictionsHook;

    public class JunctionRestrictionsManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.SegmentNodeConf>>,
          IJunctionRestrictionsManager,
          IJunctionRestrictionsHook
    {
        public static JunctionRestrictionsManager Instance { get; } =
            new JunctionRestrictionsManager();

        private readonly SegmentJunctionRestrictions[] orphanedRestrictions;

        /// <summary>
        /// Holds junction restrictions for each segment end
        /// </summary>
        private readonly SegmentJunctionRestrictions[] segmentRestrictions;

        public event Action<FlagsHookArgs> GetDefaultsHook;
        public event Action<FlagsHookArgs> GetConfigurableHook;

        private JunctionRestrictionsManager() {
            segmentRestrictions = new SegmentJunctionRestrictions[NetManager.MAX_SEGMENT_COUNT];
            orphanedRestrictions = new SegmentJunctionRestrictions[NetManager.MAX_SEGMENT_COUNT];
        }

        public void InvalidateFlags(ushort segmentId, bool startNode, JunctionRestrictionFlags flags) {

            if (segmentId.ToSegment().IsValid()) {
                SegmentEndId segmentEndId = segmentId.AtNode(startNode);
                GetJunctionRestrictions(segmentEndId).Invalidate(segmentEndId);
            }
        }

        public void InvalidateFlags(ushort nodeId, JunctionRestrictionFlags flags) {

            if (nodeId.ToNode().IsValid()) {
                foreach (var segmentId in ExtNodeManager.Instance.GetNodeSegmentIds(nodeId, ClockDirection.Clockwise)) {
                    InvalidateFlags(segmentId, segmentId.ToSegment().IsStartNode(nodeId), flags);
                }
            }
        }

        private ref JunctionRestrictions GetJunctionRestrictions(SegmentEndId segmentEndId) {
            return ref (segmentEndId.StartNode
                        ? ref segmentRestrictions[segmentEndId].startNodeRestrictions
                        : ref segmentRestrictions[segmentEndId].endNodeRestrictions);
        }

        private void AddOrphanedSegmentJunctionRestrictions(ushort segmentId,
                                                           bool startNode,
                                                           JunctionRestrictions restrictions) {
            if (startNode) {
                orphanedRestrictions[segmentId].startNodeRestrictions = restrictions;
            } else {
                orphanedRestrictions[segmentId].endNodeRestrictions = restrictions;
            }
        }

        protected override void HandleSegmentEndReplacement(SegmentEndReplacement replacement,
                                                            ref ExtSegmentEnd segEnd) {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ISegmentEndId oldSegmentEndId = replacement.oldSegmentEndId;
            ISegmentEndId newSegmentEndId = replacement.newSegmentEndId;

            JunctionRestrictions restrictions;
            if (oldSegmentEndId.StartNode) {
                restrictions = orphanedRestrictions[oldSegmentEndId.SegmentId].startNodeRestrictions;
                orphanedRestrictions[oldSegmentEndId.SegmentId].startNodeRestrictions.Reset(oldSegmentEndId.FromApi());
            } else {
                restrictions = orphanedRestrictions[oldSegmentEndId.SegmentId].endNodeRestrictions;
                orphanedRestrictions[oldSegmentEndId.SegmentId].endNodeRestrictions.Reset(oldSegmentEndId.FromApi());
            }

            GetJunctionRestrictions(newSegmentEndId.FromApi()).Invalidate(newSegmentEndId.FromApi());

            Log._Debug(
                $"JunctionRestrictionsManager.HandleSegmentEndReplacement({replacement}): " +
                $"Segment replacement detected: {oldSegmentEndId.SegmentId} -> {newSegmentEndId.SegmentId} " +
                $"@ {newSegmentEndId.StartNode}");

            SetSegmentJunctionRestrictions(newSegmentEndId.FromApi(), restrictions);
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ExtSegment seg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId];
                if (seg.valid) {
                    HandleValidSegment(ref seg);
                }
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Junction restrictions:");

            for (ushort segmentId = 0; segmentId < segmentRestrictions.Length; ++segmentId) {
                if (segmentRestrictions[segmentId].IsDefault(segmentId)) {
                    continue;
                }

                Log._Debug($"Segment {segmentId}: {segmentRestrictions[segmentId]}");
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
            if (!nodeId.ToNode().IsValid()) {
                return false;
            }

            for (int i = 0; i < 8; ++i) {
                var segmentEndId = nodeId.GetSegmentEnd(i);
                if (segmentEndId != default) {

                    if (!GetJunctionRestrictions(segmentEndId).IsDefault(segmentEndId)) {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RemoveJunctionRestrictions(ushort nodeId) {
            Log._Debug($"JunctionRestrictionsManager.RemoveJunctionRestrictions({nodeId}) called.");

            for (int i = 0; i < 8; ++i) {
                var segmentEndId = nodeId.GetSegmentEnd(i);
                if (segmentEndId != default) {
                    GetJunctionRestrictions(segmentEndId).Reset(segmentEndId, false);
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

            var segmentEndId = seg.segmentId.AtNode(startNode);

            JunctionRestrictions restrictions = startNode
                                                ? segmentRestrictions[seg.segmentId].startNodeRestrictions
                                                : segmentRestrictions[seg.segmentId].endNodeRestrictions;

            if (!restrictions.IsDefault(segmentEndId)) {
                AddOrphanedSegmentJunctionRestrictions(seg.segmentId, startNode, restrictions);
            }

            segmentRestrictions[seg.segmentId].Reset(segmentEndId);
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
            segmentRestrictions[seg.segmentId].Invalidate(seg.segmentId);
        }

        /// <summary>
        /// called when deserailizing or when policy changes.
        /// TODO [issue #1116]: publish segment changes? if so it should be done only when policy changes not when deserializing.
        /// </summary>
        public void UpdateAllDefaults() {

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {

                if (!segmentId.ToSegment().IsValid()) {
                    continue;
                }
                segmentRestrictions[segmentId].Invalidate(segmentId);
            }
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
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

        /// <summary>
        /// This is necessary because we can't change the signature of methods known to be patched.
        /// It will go away once the Node Controller mods are updated.
        /// </summary>
        private static class CalculationContext {

            [ThreadStatic]
            private static bool? _isConfigurable;

            public static bool? IsConfigurable {
                get => _isConfigurable;
                set {
                    if (value.HasValue && _isConfigurable.HasValue)
                        throw new InvalidOperationException("JunctionRestrictionsManager.CalculationContext used recursively");

                    _isConfigurable = value;
                }
            }
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultUturnAllowed(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            var isConfigurable = CalculationContext.IsConfigurable ?? IsUturnAllowedConfigurable(segmentId, startNode, ref node);
            if (!isConfigurable) {
                bool res = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) !=
                           NetNode.Flags.None;
                if (logLogic) {
                    Log._Debug(
                        $"JunctionRestrictionsManager.GetDefaultUturnAllowed({segmentId}, " +
                        $"{startNode}): Setting is not configurable. res={res}, flags={node.m_flags}");
                }

                return res;
            }

            bool ret = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) !=
                       NetNode.Flags.None;

            if (!ret && Options.allowUTurns) {
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

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool IsNearTurnOnRedAllowedConfigurable(ushort segmentId,
                                                       bool startNode,
                                                       ref NetNode node) {
            return IsTurnOnRedAllowedConfigurable(true, segmentId, startNode, ref node);
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool IsFarTurnOnRedAllowedConfigurable(ushort segmentId,
                                                      bool startNode,
                                                      ref NetNode node) {
            return IsTurnOnRedAllowedConfigurable(false, segmentId, startNode, ref node);
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
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

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultNearTurnOnRedAllowed(ushort segmentId,
                                                   bool startNode,
                                                   ref NetNode node) {
            return GetDefaultTurnOnRedAllowed(true, segmentId, startNode, ref node);
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultFarTurnOnRedAllowed(ushort segmentId,
                                                  bool startNode,
                                                  ref NetNode node) {
            return GetDefaultTurnOnRedAllowed(false, segmentId, startNode, ref node);
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            var isConfigurable = CalculationContext.IsConfigurable ?? IsTurnOnRedAllowedConfigurable(near, segmentId, startNode, ref node);
            if (!isConfigurable) {
                if (logLogic) {
                    Log._Debug(
                        $"JunctionRestrictionsManager.IsTurnOnRedAllowedConfigurable({near}, " +
                        $"{segmentId}, {startNode}): Setting is not configurable. res=false");
                }

                return false;
            }

            bool ret = near ? Options.allowNearTurnOnRed : Options.allowFarTurnOnRed;
            if (logLogic) {
                Log._Debug(
                    $"JunctionRestrictionsManager.GetTurnOnRedAllowed({near}, {segmentId}, " +
                    $"{startNode}): Setting is configurable. ret={ret}, flags={node.m_flags}");
            }

            return ret;
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
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

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            var isConfigurable = CalculationContext.IsConfigurable ?? IsLaneChangingAllowedWhenGoingStraightConfigurable(segmentId, startNode, ref node);
            if (!isConfigurable) {
                if (logLogic) {
                    Log._Debug(
                        "JunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight" +
                        $"({segmentId}, {startNode}): Setting is not configurable. res=false");
                }

                return false;
            }

            bool ret = Options.allowLaneChangesWhileGoingStraight;

            if (logLogic) {
                Log._Debug(
                    "JunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight" +
                    $"({segmentId}, {startNode}): Setting is configurable. ret={ret}");
            }

            return ret;
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
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

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
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

            var isConfigurable = CalculationContext.IsConfigurable ?? IsEnteringBlockedJunctionAllowedConfigurable(segmentId, startNode, ref node);
            if (!isConfigurable) {
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
            if (Options.allowEnterBlockedJunctions) {
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

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool IsPedestrianCrossingAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) {
            bool ret = (node.m_flags & (NetNode.Flags.Junction | NetNode.Flags.Bend)) != NetNode.Flags.None
                       && node.Info?.m_class?.m_service != ItemClass.Service.Beautification;
#if DEBUG
            if (DebugSwitch.JunctionRestrictions.Get()) {
                Log._Debug(
                    "JunctionRestrictionsManager.IsPedestrianCrossingAllowedConfigurable" +
                    $"({segmentId}, {startNode}): ret={ret}, flags={node.m_flags}, " +
                    $"service={node.Info?.m_class?.m_service}");
            }
#endif
            return ret;
        }

        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)]
        public bool GetDefaultPedestrianCrossingAllowed(ushort segmentId, bool startNode, ref NetNode node) {
#if DEBUG
            bool logLogic = DebugSwitch.JunctionRestrictions.Get();
#else
            const bool logLogic = false;
#endif

            var isConfigurable = CalculationContext.IsConfigurable ?? IsPedestrianCrossingAllowedConfigurable(segmentId, startNode, ref node);
            if (!isConfigurable) {
                if (logLogic) {
                    Log._Debug(
                        "JunctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed" +
                        $"({segmentId}, {startNode}): Setting is not configurable. res=true");
                }

                return true;
            }

            if (Options.NoDoubleCrossings &&
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

                // at bridge/tunnel entracnes, pedestrian crossing is on ground road.
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

        public bool ClearSegmentEnd(ushort segmentId, bool startNode) {

            if (!segmentId.ToSegment().IsValid())
                return false;

            var segmentEndId = segmentId.AtNode(startNode);
            return GetJunctionRestrictions(segmentEndId).ClearValue(segmentEndId, JunctionRestrictionFlags.All);
        }

        private void SetSegmentJunctionRestrictions(SegmentEndId segmentEndId, JunctionRestrictions restrictions) {
            GetJunctionRestrictions(segmentEndId).Copy(segmentEndId, restrictions);
        }

        private static ref NetNode GetNode(ushort segmentId, bool startNode) =>
            ref segmentId.ToSegment().GetNodeId(startNode).ToNode();

        private void OnSegmentChange(SegmentEndId segmentEndId,
                                     bool requireRecalc) {

            ref var seg = ref ExtSegmentManager.Instance.ExtSegments[segmentEndId.SegmentId];

            HandleValidSegment(ref seg);

            if (requireRecalc) {
                RoutingManager.Instance.RequestRecalculation(segmentEndId.SegmentId);
                if (TMPELifecycle.Instance.MayPublishSegmentChanges()) {
                    ExtSegmentManager.Instance.PublishSegmentChanges(segmentEndId.SegmentId);
                }
            }

            Notifier.Instance.OnNodeModified(segmentEndId.GetNodeId(), this);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (ushort segmentId = 0; segmentId < segmentRestrictions.Length; ++segmentId) {
                segmentRestrictions[segmentId].Reset((ushort)segmentId);
            }

            for (ushort segmentId = 0; segmentId < orphanedRestrictions.Length; ++segmentId) {
                orphanedRestrictions[segmentId].Reset(segmentId);
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

                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowUTurn, flags.uturnAllowed);
                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowNearTurnOnRed, flags.turnOnRedAllowed);
                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowFarTurnOnRed, flags.farTurnOnRedAllowed);
                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowForwardLaneChange, flags.straightLaneChangingAllowed);
                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowEnterWhenBlocked, flags.enterWhenBlockedAllowed);
                            SetValue(segNodeConf.segmentId, true, JunctionRestrictionFlags.AllowPedestrianCrossing, flags.pedestrianCrossingAllowed);
                        } else {
                            Log.Warning(
                                "JunctionRestrictionsManager.LoadData(): Could not get segment " +
                                $"end geometry for segment {segNodeConf.segmentId} @ start node");
                        }
                    }

                    if (segNodeConf.endNodeFlags != null) {
                        ushort endNodeId = netSegment.m_endNode;
                        if (endNodeId != 0) {
                            Configuration.SegmentNodeFlags flags = segNodeConf.endNodeFlags;
                            ref NetNode node = ref endNodeId.ToNode();

                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowUTurn, flags.uturnAllowed);
                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowNearTurnOnRed, flags.turnOnRedAllowed);
                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowFarTurnOnRed, flags.farTurnOnRedAllowed);
                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowForwardLaneChange, flags.straightLaneChangingAllowed);
                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowEnterWhenBlocked, flags.enterWhenBlockedAllowed);
                            SetValue(segNodeConf.segmentId, false, JunctionRestrictionFlags.AllowPedestrianCrossing, flags.pedestrianCrossingAllowed);
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

            for (ushort segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; segmentId++) {
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

                        if (!endFlags.IsDefault(segmentId.AtStartNode())) {
                            startNodeFlags = new Configuration.SegmentNodeFlags();

                            startNodeFlags.uturnAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowUTurn);
                            startNodeFlags.turnOnRedAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowNearTurnOnRed);
                            startNodeFlags.farTurnOnRedAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowFarTurnOnRed);
                            startNodeFlags.straightLaneChangingAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowForwardLaneChange);
                            startNodeFlags.enterWhenBlockedAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowEnterWhenBlocked);
                            startNodeFlags.pedestrianCrossingAllowed = GetValue(segmentId, true, JunctionRestrictionFlags.AllowPedestrianCrossing);

#if DEBUGSAVE
                            Log._Debug($"JunctionRestrictionsManager.SaveData: Saving start node "+
                            $"junction restrictions for segment {segmentId}: {startNodeFlags}");
#endif
                        }
                    }

                    ushort endNodeId = netSegment.m_endNode;

                    if (endNodeId.ToNode().IsValid()) {
                        JunctionRestrictions restrictions = segmentRestrictions[segmentId].endNodeRestrictions;

                        if (!restrictions.IsDefault(segmentId.AtEndNode())) {
                            endNodeFlags = new Configuration.SegmentNodeFlags();

                            endNodeFlags.uturnAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowUTurn);
                            endNodeFlags.turnOnRedAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowNearTurnOnRed);
                            endNodeFlags.farTurnOnRedAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowFarTurnOnRed);
                            endNodeFlags.straightLaneChangingAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowForwardLaneChange);
                            endNodeFlags.enterWhenBlockedAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowEnterWhenBlocked);
                            endNodeFlags.pedestrianCrossingAllowed = GetValue(segmentId, false, JunctionRestrictionFlags.AllowPedestrianCrossing);

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

        public bool IsConfigurable(ushort segmentId, bool startNode, JunctionRestrictionFlags flags)
            => segmentRestrictions[segmentId].IsConfigurable(segmentId.AtNode(startNode), flags);

        public bool GetDefaultValue(ushort segmentId, bool startNode, JunctionRestrictionFlags flags)
            => segmentRestrictions[segmentId].GetDefaultValue(segmentId.AtNode(startNode), flags);

        public bool ToggleValue(ushort segmentId, bool startNode, JunctionRestrictionFlags flags) {

            if (flags == default || ((int)flags & ((int)flags - 1)) != 0)
                return false;

            return SetValue(segmentId, startNode, flags, !GetValueOrDefault(segmentId, startNode, flags));
        }

        public bool SetValue(ushort segmentId, bool startNode, JunctionRestrictionFlags flags, bool? value)
            => segmentId.ToSegment().IsValid() && flags != default && segmentRestrictions[segmentId].SetValue(segmentId.AtNode(startNode), flags, value);

        public bool? GetValue(ushort segmentId, bool startNode, JunctionRestrictionFlags flags)
            => segmentRestrictions[segmentId].GetValue(segmentId.AtNode(startNode), flags);

        public bool GetValueOrDefault(ushort segmentId, bool startNode, JunctionRestrictionFlags flags)
            => segmentRestrictions[segmentId].GetValueOrDefault(segmentId.AtNode(startNode), flags);

        bool IJunctionRestrictionsManager.IsUturnAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn);
        bool IJunctionRestrictionsManager.IsNearTurnOnRedAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed);
        bool IJunctionRestrictionsManager.IsFarTurnOnRedAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed);
        bool IJunctionRestrictionsManager.IsTurnOnRedAllowedConfigurable(bool near, ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed);
        bool IJunctionRestrictionsManager.IsLaneChangingAllowedWhenGoingStraightConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange);
        bool IJunctionRestrictionsManager.IsEnteringBlockedJunctionAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked);
        bool IJunctionRestrictionsManager.IsPedestrianCrossingAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node) => IsConfigurable(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing);
        bool IJunctionRestrictionsManager.GetDefaultUturnAllowed(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn);
        bool IJunctionRestrictionsManager.GetDefaultNearTurnOnRedAllowed(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed);
        bool IJunctionRestrictionsManager.GetDefaultFarTurnOnRedAllowed(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed);
        bool IJunctionRestrictionsManager.GetDefaultTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed);
        bool IJunctionRestrictionsManager.GetDefaultLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange);
        bool IJunctionRestrictionsManager.GetDefaultEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked);
        bool IJunctionRestrictionsManager.GetDefaultPedestrianCrossingAllowed(ushort segmentId, bool startNode, ref NetNode node) => GetDefaultValue(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleUturnAllowed(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleNearTurnOnRedAllowed(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleFarTurnOnRedAllowed(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool ToggleEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool TogglePedestrianCrossingAllowed(ushort segmentId, bool startNode) => ToggleValue(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetUturnAllowed(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, bool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing, value);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetUturnAllowed(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetNearTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetFarTurnOnRedAllowed(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool SetPedestrianCrossingAllowed(ushort segmentId, bool startNode, TernaryBool value) => SetValue(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing, ToOptBool(value));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetUturnAllowed(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetNearTurnOnRedAllowed(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetFarTurnOnRedAllowed(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public TernaryBool GetPedestrianCrossingAllowed(ushort segmentId, bool startNode) => ToTernaryBool(GetValue(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing));
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsUturnAllowed(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowUTurn);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsNearTurnOnRedAllowed(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowNearTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsFarTurnOnRedAllowed(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowFarTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsTurnOnRedAllowed(bool near, ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, near ? JunctionRestrictionFlags.AllowNearTurnOnRed : JunctionRestrictionFlags.AllowFarTurnOnRed);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowForwardLaneChange);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowEnterWhenBlocked);
        [Obsolete("If you must call this method, please go through IJunctionRestrictionsManager.", true)] public bool IsPedestrianCrossingAllowed(ushort segmentId, bool startNode) => GetValueOrDefault(segmentId, startNode, JunctionRestrictionFlags.AllowPedestrianCrossing);

        private struct SegmentJunctionRestrictions {
            public JunctionRestrictions startNodeRestrictions;
            public JunctionRestrictions endNodeRestrictions;

            public bool GetValueOrDefault(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                return (segmentEndId.StartNode ? startNodeRestrictions : endNodeRestrictions).GetValueOrDefault(segmentEndId, flags);
            }

            public bool? GetValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                return (segmentEndId.StartNode ? startNodeRestrictions : endNodeRestrictions).GetValue(segmentEndId, flags);
            }

            public bool GetDefaultValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                return (segmentEndId.StartNode ? startNodeRestrictions : endNodeRestrictions).GetDefaultValue(segmentEndId, flags);
            }

            public bool IsConfigurable(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                return (segmentEndId.StartNode ? startNodeRestrictions : endNodeRestrictions).IsConfigurable(segmentEndId, flags);
            }

            public bool SetValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, bool? value) {
                if (segmentEndId.StartNode)
                    return startNodeRestrictions.SetValue(segmentEndId, flags, value);
                else
                    return endNodeRestrictions.SetValue(segmentEndId, flags, value);
            }

            public bool IsDefault(ushort segmentId) {
                return startNodeRestrictions.IsDefault(segmentId.AtStartNode()) && endNodeRestrictions.IsDefault(segmentId.AtEndNode());
            }

            public void Reset(SegmentEndId segmentEndId) {
                if (segmentEndId.StartNode)
                    startNodeRestrictions.Reset(segmentEndId);
                else
                    endNodeRestrictions.Reset(segmentEndId);
            }

            public void Reset(ushort segmentId) {
                Reset(segmentId.AtStartNode());
                Reset(segmentId.AtEndNode());
            }

            public void Invalidate(ushort segmentId) {
                startNodeRestrictions.Invalidate(segmentId.AtStartNode());
                endNodeRestrictions.Invalidate(segmentId.AtEndNode());
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

            private JunctionRestrictionFlags configurables;

            private JunctionRestrictionFlags valid;

            public bool ClearValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) => SetValue(segmentEndId, flags, null);

            private void SetDefault(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, bool value) {
                if (value)
                    defaults |= flags;
                else
                    defaults &= ~flags;
            }

            private void SetConfigurable(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, bool value) {
                if (value)
                    configurables |= flags;
                else
                    configurables &= ~flags;
            }

            public bool GetDefaultValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                Recalculate(segmentEndId);
                return (defaults & flags) == flags;
            }

            public bool IsConfigurable(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                Recalculate(segmentEndId);
                return (configurables & flags) == flags;
            }

            public bool HasValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                return (mask & flags) == flags;
            }

            public bool? GetValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {

                return (mask & flags) != flags ? null
                        : (values & flags) == flags ? true
                        : (values & flags) == 0 ? false
                        : null;
            }

            public bool GetValueOrDefault(SegmentEndId segmentEndId, JunctionRestrictionFlags flags) {
                Recalculate(segmentEndId);
                return ((values & flags & mask) | (defaults & flags & ~mask)) == flags;
            }

            public JunctionRestrictionFlags GetFlagsWithDefaults(SegmentEndId segmentEndId) {
                return (values & mask) | (defaults & ~mask);
            }

            private const JunctionRestrictionFlags routingRecalculationFlags = JunctionRestrictionFlags.All & ~JunctionRestrictionFlags.AllowEnterWhenBlocked;

            private bool ValidateSet(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, JunctionRestrictionFlags newValues, JunctionRestrictionFlags newMask) {

                const JunctionRestrictionFlags uTurnCheckFlags
                            = JunctionRestrictionFlags.AllowNearTurnOnRed
                                | JunctionRestrictionFlags.AllowFarTurnOnRed
                                | JunctionRestrictionFlags.AllowUTurn;

                if ((newMask & flags & uTurnCheckFlags) != 0
                        && (newValues & newMask & flags & uTurnCheckFlags) == 0
                        && LaneConnectionManager.Instance.HasUturnConnections(segmentEndId.SegmentId, segmentEndId.StartNode))
                    return false;

                return true;
            }

            public bool SetValue(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, bool? value) {

                Recalculate(segmentEndId);

                var newValues = value == true ? (values | flags) : (values & ~flags);
                var newMask = value.HasValue ? (mask | flags) : (mask & ~flags);

                return Set(segmentEndId, flags, newValues, newMask);
            }

            private bool Set(SegmentEndId segmentEndId, JunctionRestrictionFlags flags, JunctionRestrictionFlags newValues, JunctionRestrictionFlags newMask) {

                Recalculate(segmentEndId);

                var changingValues = newValues ^ values;
                var changingMask = newMask ^ mask;

                if (changingValues == 0 && changingMask == 0)
                    return true;

                if ((configurables & changingValues) != changingValues || (configurables & changingMask) != changingMask)
                    return false;

                if (!ValidateSet(segmentEndId, flags, newValues, newMask))
                    return false;

                values = newValues & flags;
                mask = newMask & flags;

                return true;
            }

            public void Copy(SegmentEndId segmentEndId, JunctionRestrictions other) {
                Recalculate(segmentEndId);
                Set(segmentEndId, other.mask & configurables, other.values, other.mask);
            }

            public bool IsDefault(SegmentEndId segmentEndId) {
                Recalculate(segmentEndId);
                return ((values & mask) | (defaults & ~mask)) == defaults;
            }

            private delegate bool Calculator(ushort segmentId, bool startNode, ref NetNode node);

            /// <summary>
            /// This is needed because the methods are annoted to produce a compiler error if referenced directly.
            /// </summary>
            /// <param name="methodName"></param>
            /// <returns></returns>
            private static Calculator DelegateTo(string methodName) {
                return (Calculator)Delegate.CreateDelegate(typeof(Calculator), Instance, methodName);
            }

            private static Dictionary<JunctionRestrictionFlags, Calculator> configurableCalculators = new Dictionary<JunctionRestrictionFlags, Calculator>() {
                { JunctionRestrictionFlags.AllowUTurn, DelegateTo(nameof(IsUturnAllowedConfigurable)) },
                { JunctionRestrictionFlags.AllowNearTurnOnRed, DelegateTo(nameof(IsNearTurnOnRedAllowedConfigurable)) },
                { JunctionRestrictionFlags.AllowFarTurnOnRed, DelegateTo(nameof(IsFarTurnOnRedAllowedConfigurable)) },
                { JunctionRestrictionFlags.AllowForwardLaneChange, DelegateTo(nameof(IsLaneChangingAllowedWhenGoingStraightConfigurable)) },
                { JunctionRestrictionFlags.AllowEnterWhenBlocked, DelegateTo(nameof(IsEnteringBlockedJunctionAllowedConfigurable)) },
                { JunctionRestrictionFlags.AllowPedestrianCrossing, DelegateTo(nameof(IsPedestrianCrossingAllowedConfigurable)) },
            };

            private static Dictionary<JunctionRestrictionFlags, Calculator> defaultCalculators = new Dictionary<JunctionRestrictionFlags, Calculator>() {
                { JunctionRestrictionFlags.AllowUTurn, DelegateTo(nameof(GetDefaultUturnAllowed)) },
                { JunctionRestrictionFlags.AllowNearTurnOnRed, DelegateTo(nameof(GetDefaultNearTurnOnRedAllowed)) },
                { JunctionRestrictionFlags.AllowFarTurnOnRed, DelegateTo(nameof(GetDefaultFarTurnOnRedAllowed)) },
                { JunctionRestrictionFlags.AllowForwardLaneChange, DelegateTo(nameof(GetDefaultLaneChangingAllowedWhenGoingStraight)) },
                { JunctionRestrictionFlags.AllowEnterWhenBlocked, DelegateTo(nameof(GetDefaultEnteringBlockedJunctionAllowed)) },
                { JunctionRestrictionFlags.AllowPedestrianCrossing, DelegateTo(nameof(GetDefaultPedestrianCrossingAllowed)) },
            };

            private void Recalculate(SegmentEndId segmentEndId) {

                var recalculateFlags = JunctionRestrictionFlags.All & ~valid;
                if (recalculateFlags != default) {
                    ref var node = ref segmentEndId.GetNodeId().ToNode();

                    JunctionRestrictionFlags newConfigurables = default;

                    foreach (var c in configurableCalculators) {
                        if ((recalculateFlags & c.Key) != 0) {
                            var result = c.Value(segmentEndId.SegmentId, segmentEndId.StartNode, ref node);
                            if (result)
                                newConfigurables |= c.Key;
                            else
                                newConfigurables &= ~c.Key;
                        }
                    }

                    if (Instance.GetConfigurableHook != null) {
                        var args = new FlagsHookArgs(segmentEndId.SegmentId, segmentEndId.StartNode, mask, newConfigurables);
                        Instance.GetConfigurableHook(args);
                        newConfigurables = args.Result;
                    }

                    JunctionRestrictionFlags newDefaults = default;

                    foreach (var c in defaultCalculators) {
                        if ((recalculateFlags & c.Key) != 0) {
                            try {
                                CalculationContext.IsConfigurable = (newConfigurables & c.Key) != 0;
                                var result = c.Value(segmentEndId.SegmentId, segmentEndId.StartNode, ref node);
                                if (result)
                                    newDefaults |= c.Key;
                                else
                                    newDefaults &= ~c.Key;
                            }
                            finally {
                                CalculationContext.IsConfigurable = null;
                            }
                        }
                    }

                    if (Instance.GetDefaultsHook != null) {
                        var args = new FlagsHookArgs(segmentEndId.SegmentId, segmentEndId.StartNode, mask, newDefaults);
                        Instance.GetDefaultsHook(args);
                        newDefaults = args.Result;
                    }

                    configurables = (configurables & ~recalculateFlags) | (newConfigurables & recalculateFlags);
                    defaults = (defaults & ~recalculateFlags) | (newDefaults & recalculateFlags);

                    var clearFlags = mask & ~configurables;
                    if (clearFlags != default) {
                        values &= configurables;
                        mask &= configurables;

                        Instance.OnSegmentChange(segmentEndId, (clearFlags & routingRecalculationFlags) != 0);
                    }
                }
            }

            public void Reset(SegmentEndId segmentEndId, bool resetDefaults = true) {
                values = mask = default;

                if (resetDefaults) {
                    valid = default;
                }
            }

            public void Invalidate(SegmentEndId segmentEndId) {
                if (valid != default) {
                    valid = default;
                    Notifier.Instance.OnNodeModified(segmentEndId.GetNodeId(), Instance);
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

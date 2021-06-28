namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for a LANE.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlayLaneSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        public readonly ushort SegmentId;

        public readonly uint LaneId;
        public readonly byte LaneIndex;
        public readonly NetInfo.Lane LaneInfo;
        private readonly int SortedLaneIndex;

        public OverlayLaneSpeedlimitHandle(ushort segmentId,
                                           uint laneId,
                                           byte laneIndex,
                                           NetInfo.Lane laneInfo,
                                           int sortedLaneIndex) {
            this.SegmentId = segmentId;
            this.LaneId = laneId;
            this.LaneIndex = laneIndex;
            this.LaneInfo = laneInfo;
            this.SortedLaneIndex = sortedLaneIndex;
        }

        /// <summary>
        /// Called when mouse is down, and when mouse is not in parent tool window area.
        /// The show per lane mode is active.
        /// </summary>
        /// <param name="action">The speed limit to set or clear.</param>
        /// <param name="target">Destination (lanes or defaults).</param>
        /// <param name="multiSegmentMode">Whether action affects entire street.</param>
        public void Click(in SetSpeedLimitAction action,
                          SetSpeedLimitTarget target,
                          bool multiSegmentMode) {
            NetManager netManager = Singleton<NetManager>.instance;
            NetSegment[] segmentsBuffer = netManager.m_segments.m_buffer;

            Apply(
                segmentId: this.SegmentId,
                laneIndex: this.LaneIndex,
                laneId: this.LaneId,
                netInfo: segmentsBuffer[this.SegmentId].Info,
                laneInfo: this.LaneInfo,
                action: action,
                target: target);

            if (multiSegmentMode) {
                ClickMultiSegment(action, target);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        /// <param name="speedLimitToSet">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action,
                                       SetSpeedLimitTarget target) {
            if (new RoundaboutMassEdit().TraverseLoop(this.SegmentId, out var segmentList)) {
                IEnumerable<LanePos> lanes = FollowRoundaboutLane(
                    segmentList,
                    this.SegmentId,
                    this.SortedLaneIndex);

                foreach (LanePos lane in lanes) {
                    // the speed limit for this lane has already been set.
                    if (lane.laneId == this.LaneId) {
                        continue;
                    }

                    SpeedLimitsTool.SetSpeedLimit(lane, action);
                }
            } else {
                int slIndexCopy = this.SortedLaneIndex;

                // Apply this to each lane
                bool LaneVisitorFn(SegmentLaneTraverser.SegmentLaneVisitData data) {
                    if (data.SegVisitData.Initial) {
                        return true;
                    }

                    if (slIndexCopy != data.SortedLaneIndex) {
                        return true;
                    }

                    ushort segmentId = data
                                       .SegVisitData
                                       .CurSeg
                                       .segmentId;

                    // netinfo is a class, ref is not necessary
                    NetInfo segmentInfo = segmentId.ToSegment().Info;

                    NetInfo.Lane curLaneInfo = segmentInfo.m_lanes[data.CurLanePos.laneIndex];

                    Apply(
                        segmentId: segmentId,
                        laneIndex: data.CurLanePos.laneIndex,
                        laneId: data.CurLanePos.laneId,
                        netInfo: segmentInfo,
                        laneInfo: curLaneInfo,
                        action: action,
                        target: target);

                    return true;
                }

                SegmentLaneTraverser.Traverse(
                    initialSegmentId: this.SegmentId,
                    direction: SegmentTraverser.TraverseDirection.AnyDirection,
                    side: SegmentTraverser.TraverseSide.AnySide,
                    laneStopCrit: SegmentLaneTraverser.LaneStopCriterion.LaneCount,
                    segStopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                    laneVisitor: LaneVisitorFn);
            }
        } // end Click MultiSegment

        /// <summary>
        /// iterates through the given roundabout <paramref name="segmentList"/> returning an enumeration
        /// of all lanes with a matching <paramref name="sortedLaneIndex"/> based on <paramref name="segmentId0"/>
        /// </summary>
        /// <param name="segmentList">input list of roundabout segments (must be oneway, and in the same direction).</param>
        /// <param name="segmentId0">The segment to match lane agaisnt</param>
        /// <param name="sortedLaneIndex">Index.</param>
        private IEnumerable<LanePos> FollowRoundaboutLane(
                    List<ushort> segmentList,
                    ushort segmentId0,
                    int sortedLaneIndex) {
            bool invert0 = segmentId0.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);

            int count0 = Shortcuts.netService.GetSortedLanes(
               segmentId: segmentId0,
               segment: ref segmentId0.ToSegment(),
               startNode: null,
               laneTypeFilter: SpeedLimitManager.LANE_TYPES,
               vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
               sort: false).Count;

            foreach (ushort segmentId in segmentList) {
                bool invert = segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);
                IList<LanePos> lanes = Shortcuts.netService.GetSortedLanes(
                    segmentId: segmentId,
                    segment: ref segmentId.ToSegment(),
                    startNode: null,
                    laneTypeFilter: SpeedLimitManager.LANE_TYPES,
                    vehicleTypeFilter: SpeedLimitManager.VEHICLE_TYPES,
                    reverse: invert != invert0,
                    sort: true);
                int index = sortedLaneIndex;

                // if lane count does not match, assume segments are connected from outer side of the roundabout.
                if (invert0) {
                    int diff = lanes.Count - count0;
                    index += diff;
                }

                if (index >= 0 && index < lanes.Count) {
                    yield return lanes[index];
                }
            } // foreach
        }

        /// <summary>
        /// Based on target value, applies speed limit to a lane or default for that road type.
        /// </summary>
        /// <param name="netInfo">Used for setting default speed limit for all roads if this type.</param>
        /// <param name="laneInfo">Used for setting override for one lane.</param>
        /// <param name="action">What limit setting to apply on click.</param>
        /// <param name="target">Where to apply the limit setting.</param>
        private static void Apply(ushort segmentId,
                                  uint laneIndex,
                                  uint laneId,
                                  NetInfo netInfo,
                                  NetInfo.Lane laneInfo,
                                  SetSpeedLimitAction action,
                                  SetSpeedLimitTarget target) {
            switch (target) {
                case SetSpeedLimitTarget.LaneOverride:
                    SpeedLimitManager.Instance.SetLaneSpeedLimit(segmentId, laneIndex, laneInfo, laneId, action);
                    break;
                case SetSpeedLimitTarget.LaneDefault:
                    if (action.Override.HasValue) {
                        // SpeedLimitManager.Instance.FixCurrentSpeedLimits(netInfo);
                        SpeedLimitManager.Instance.SetCustomNetInfoSpeedLimit(
                            info: netInfo,
                            customSpeedLimit: action.Override.Value.GameUnits);
                    }

                    // TODO: The speed limit manager only supports default speed limit overrides per road type, not per lane
                    break;
                default:
                    Log.Error(
                        $"Target for LANE speed handle click is not supported {nameof(target)}");
                    throw new NotSupportedException();
            }
        }
    } // end struct
} // end namespace
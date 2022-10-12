namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using System.Collections.Generic;
    using ColossalFramework;
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for a LANE.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlayLaneSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        private readonly ushort segmentId_;

        private readonly uint laneId_;

        public uint LaneId => this.laneId_;

        private readonly byte laneIndex_;
        private readonly NetInfo.Lane laneInfo_;

        private readonly int sortedLaneIndex_;

        public int SortedLaneIndex => this.sortedLaneIndex_;

        public OverlayLaneSpeedlimitHandle(ushort segmentId,
                                           uint laneId,
                                           byte laneIndex,
                                           NetInfo.Lane laneInfo,
                                           int sortedLaneIndex) {
            this.segmentId_ = segmentId;
            this.laneId_ = laneId;
            this.laneIndex_ = laneIndex;
            this.laneInfo_ = laneInfo;
            this.sortedLaneIndex_ = sortedLaneIndex;
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
            Apply(
                segmentId: this.segmentId_,
                laneIndex: this.laneIndex_,
                laneId: this.laneId_,
                netInfo: this.segmentId_.ToSegment().Info,
                laneInfo: this.laneInfo_,
                action: action,
                target: target);

            if (multiSegmentMode) {
                this.ClickMultiSegment(action, target);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        private void ClickMultiSegment(SetSpeedLimitAction action,
                                       SetSpeedLimitTarget target) {
            if (new RoundaboutMassEdit().TraverseLoop(this.segmentId_, out var segmentList)) {
                IEnumerable<LanePos> lanes = this.FollowRoundaboutLane(
                    segmentList,
                    this.segmentId_,
                    this.sortedLaneIndex_);

                foreach (LanePos lane in lanes) {
                    // the speed limit for this lane has already been set.
                    if (lane.laneId == this.laneId_) {
                        continue;
                    }

                    SpeedLimitsTool.SetSpeedLimit(lane, action);
                }
            } else {
                int slIndexCopy = this.sortedLaneIndex_;

                // Apply this to each lane
                bool LaneVisitorFn(SegmentLaneTraverser.SegmentLaneVisitData data) {
                    if (data.SegVisitData.Initial) {
                        return true;
                    }

                    if (slIndexCopy != data.SortedLaneIndex) {
                        return true;
                    }

                    ushort segmentId = data.SegVisitData.CurSeg.segmentId;

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
                    initialSegmentId: this.segmentId_,
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
        /// of all lanes with a matching <paramref name="sortedLaneIndex"/> based on <paramref name="segmentId0"/>.
        /// </summary>
        /// <param name="segmentList">input list of roundabout segments (must be oneway, and in the same direction).</param>
        /// <param name="segmentId0">The segment to match lane against.</param>
        /// <param name="sortedLaneIndex">Index.</param>
        internal IEnumerable<LanePos> FollowRoundaboutLane(
            List<ushort> segmentList,
            ushort segmentId0,
            int sortedLaneIndex) {

            bool invert0 = segmentId0.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);

            int count0 = segmentId0.ToSegment().GetSortedLanes(
                null,
                SpeedLimitManager.LANE_TYPES,
                SpeedLimitManager.VEHICLE_TYPES,
                sort: false).Count;

            foreach (ushort segmentId in segmentList) {
                bool invert = segmentId.ToSegment().m_flags.IsFlagSet(NetSegment.Flags.Invert);

                var lanes = segmentId.ToSegment().GetSortedLanes(
                    null,
                    SpeedLimitManager.LANE_TYPES,
                    SpeedLimitManager.VEHICLE_TYPES,
                    reverse: invert != invert0);

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
                    // TODO: The speed limit manager only supports default speed limit overrides per road type, not per lane
                    OverlaySegmentSpeedlimitHandle.ApplyDefaultSpeedLimit(segmentId, netInfo, action);
                    break;
                default:
                    Log.Error(
                        $"Target for LANE speed handle click is not supported {nameof(target)}");
                    throw new NotSupportedException();
            }
        }
    } // end struct
} // end namespace
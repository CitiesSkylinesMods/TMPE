namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework;
    using GenericGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
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
        public void Click(in SetSpeedLimitAction action, bool multiSegmentMode) {
            SpeedLimitManager.Instance.SetSpeedLimit(
                segmentId: this.SegmentId,
                laneIndex: this.LaneIndex,
                laneInfo: this.LaneInfo,
                laneId: this.LaneId,
                action: action);

            if (multiSegmentMode) {
                ClickMultiSegment(action);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        /// <param name="speedLimitToSet">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action) {
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

                    Constants.ServiceFactory.NetService.ProcessSegment(
                        segmentId: data.SegVisitData.CurSeg.segmentId,
                        handler: (ushort curSegmentId, ref NetSegment curSegment) => {
                            NetInfo.Lane curLaneInfo =
                                curSegment.Info.m_lanes[data.CurLanePos.laneIndex];

                            SpeedLimitManager.Instance.SetSpeedLimit(
                                segmentId: curSegmentId,
                                laneIndex: data.CurLanePos.laneIndex,
                                laneInfo: curLaneInfo,
                                laneId: data.CurLanePos.laneId,
                                action: action);
                            return true;
                        });

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
    } // end struct
} // end namespace
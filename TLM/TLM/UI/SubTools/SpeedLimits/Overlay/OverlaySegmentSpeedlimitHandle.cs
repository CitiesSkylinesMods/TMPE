namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for SEGMENT.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlaySegmentSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        private readonly ushort segmentId_;

        public ushort SegmentId => this.segmentId_;

        public OverlaySegmentSpeedlimitHandle(ushort segmentId) {
            this.segmentId_ = segmentId;
        }

        /// <summary>
        /// Called when mouse is down, and when mouse is not in parent tool window area.
        /// The show per lane mode is disabled and editing per segment.
        /// </summary>
        /// <param name="action">What speed limit to set or clear.</param>
        /// <param name="target">The speed limit destination object (override or default, segment or lane).</param>
        /// <param name="multiSegmentMode">True if user holds Shift to edit the road.</param>
        public void Click(in SetSpeedLimitAction action,
                          SetSpeedLimitTarget target,
                          bool multiSegmentMode) {
            NetInfo netInfo = this.segmentId_.ToSegment().Info;

            Apply(
                segmentId: this.segmentId_,
                finalDir: NetInfo.Direction.Forward,
                netInfo: netInfo,
                action: action,
                target: target);
            Apply(
                segmentId: this.segmentId_,
                finalDir: NetInfo.Direction.Backward,
                netInfo: netInfo,
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
        /// <param name="action">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action,
                                       SetSpeedLimitTarget target) {
            if (new RoundaboutMassEdit().TraverseLoop(this.segmentId_, out var segmentList)) {
                foreach (ushort segId in segmentList) {
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segId, action);
                }

                return;
            }

            // Called for each lane in the traversed street
            bool ForEachSegmentFun(SegmentLaneTraverser.SegmentLaneVisitData data) {
                if (data.SegVisitData.Initial) {
                    return true;
                }

                ushort otherSegmentId = data.SegVisitData.CurSeg.segmentId;
                NetInfo otherSegmentInfo = otherSegmentId.ToSegment().Info;
                byte laneIndex = data.CurLanePos.laneIndex;
                NetInfo.Lane laneInfo = otherSegmentInfo.m_lanes[laneIndex];

                Apply(
                    segmentId: otherSegmentId,
                    finalDir: laneInfo.m_finalDirection,
                    netInfo: otherSegmentInfo,
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
                laneVisitor: ForEachSegmentFun);
        } // end Click MultiSegment

        /// <summary>Based on target value, applies speed limit to a segmet or default for that road type.</summary>
        /// <param name="netInfo">For defaults, will set default speed limit for that road type.</param>
        private static void Apply(ushort segmentId,
                                  NetInfo.Direction finalDir,
                                  NetInfo netInfo,
                                  SetSpeedLimitAction action,
                                  SetSpeedLimitTarget target) {
            switch (target) {
                case SetSpeedLimitTarget.SegmentOverride:
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segmentId, finalDir, action);
                    break;
                case SetSpeedLimitTarget.SegmentDefault:
                case SetSpeedLimitTarget.LaneDefault:
                    ApplyDefaultSpeedLimit(segmentId, netInfo, action);
                    break;
                default:
                    Log.Error($"Target for SEGMENT speed handle click is not supported {nameof(target)}");
                    throw new NotSupportedException();
            }
        }

        internal static void ApplyDefaultSpeedLimit(ushort segmentId,
                                                  [NotNull] NetInfo netInfo,
                                                  SetSpeedLimitAction action) {
            switch (action.Type) {
                case SetSpeedLimitAction.ActionType.SetOverride:
                case SetSpeedLimitAction.ActionType.Unlimited:
                    bool displayMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                    SpeedValue value = action.GuardedValue.Override;
                    Log._Debug($"Setting speed limit for netinfo '{netInfo.name}' seg={segmentId} to={value.FormatStr(displayMph)}");
                    SpeedLimitManager.Instance.SetCustomNetinfoSpeedLimit(
                        netinfo: netInfo,
                        customSpeedLimit: value.GameUnits);
                    break;
                case SetSpeedLimitAction.ActionType.ResetToDefault:
                    Log._Debug($"Resetting custom default speed for netinfo '{netInfo.name}'");
                    SpeedLimitManager.Instance.ResetCustomNetinfoSpeedLimit(netInfo);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    } // end struct
} // end namespace
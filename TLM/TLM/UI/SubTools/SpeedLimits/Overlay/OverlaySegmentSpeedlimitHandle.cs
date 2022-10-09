namespace TrafficManager.UI.SubTools.SpeedLimits.Overlay {
    using System;
    using System.Diagnostics.CodeAnalysis;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Describes a recently rendered speed icon on the speed limits overlay for SEGMENT.
    /// It is created while rendering, and if mouse is hovering over it, it is added to the list.
    /// Click is handled separately away from the rendering code.
    /// </summary>
    public readonly struct OverlaySegmentSpeedlimitHandle {
        /// <summary>Segment id where the speedlimit sign was displayed.</summary>
        [SuppressMessage("Usage", "RAS0002:Readonly field for a non-readonly struct", Justification = "Primitive value.")]
        private readonly ushort segmentId_;

        public OverlaySegmentSpeedlimitHandle(ushort segmentId)
            => this.segmentId_ = segmentId;

        public ushort SegmentId => this.segmentId_;

        /// <summary>
        /// Called when mouse is down, and when mouse is not in parent tool window area.
        /// The show per lane mode is disabled and editing per segment.
        /// </summary>
        /// <param name="action">What speed limit to set or clear.</param>
        /// <param name="target">The speed limit destination object (override or default, segment or lane).</param>
        /// <param name="multiSegmentMode">True if user holds Shift to edit the road.</param>
        public void Click(
            in SetSpeedLimitAction action,
            SetSpeedLimitTarget target,
            bool multiSegmentMode) {

            // Must not be used in Lane modes.
            if (target is SetSpeedLimitTarget.LaneDefault or SetSpeedLimitTarget.LaneOverride) {
                string msg = $"Unsupported target for SEGMENT speed handle click: {nameof(target)}";
                Log.Error(msg);
                throw new NotSupportedException(msg);
            }

            if (multiSegmentMode) {

                this.ClickMultiSegment(action, target);

            } else {

                NetInfo netInfo = this.segmentId_.ToSegment().Info;

                Apply(
                    segmentId: this.segmentId_,
                    netInfo: netInfo,
                    action: action,
                    target: target);
            }
        }

        /// <summary>
        /// Called if speed limit icon was clicked in segment display mode,
        /// but also multisegment mode was enabled (like holding Shift).
        /// </summary>
        /// <param name="action">The active speed limit on the palette.</param>
        private void ClickMultiSegment(SetSpeedLimitAction action, SetSpeedLimitTarget target) {

            // Special case for roundabouts
            // TO-DO: https://github.com/CitiesSkylinesMods/TMPE/issues/1469
            if (new RoundaboutMassEdit().TraverseLoop(this.segmentId_, out var segmentList)) {
                foreach (ushort segId in segmentList) {
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segId, action);
                }

                return;
            }

            // Called for each segment in the traversed route
            bool ForEachSegmentFun(SegmentTraverser.SegmentVisitData data) {
                ushort segmentId = data.CurSeg.segmentId;
                ref NetSegment netSegment = ref segmentId.ToSegment();

                if (!netSegment.AnyApplicableLane(
                    SpeedLimitManager.LANE_TYPES,
                    SpeedLimitManager.VEHICLE_TYPES)) {

                    return false;
                }

                Apply(
                    segmentId: segmentId,
                    netInfo: netSegment.Info,
                    action: action,
                    target: target);

                return true;
            }

            SegmentTraverser.Traverse(
                initialSegmentId: this.segmentId_,
                direction: SegmentTraverser.TraverseDirection.AnyDirection,
                side: SegmentTraverser.TraverseSide.AnySide,
                stopCrit: SegmentTraverser.SegmentStopCriterion.Junction,
                visitorFun: ForEachSegmentFun);
        }

        /// <summary>Based on target value, applies speed limit to a segment or default for that road type.</summary>
        /// <param name="netInfo">For defaults, will set default speed limit for that road type.</param>
        private static void Apply(
            ushort segmentId,
            NetInfo netInfo,
            SetSpeedLimitAction action,
            SetSpeedLimitTarget target) {

            switch (target) {
                case SetSpeedLimitTarget.SegmentOverride:
                    SpeedLimitManager.Instance.SetSegmentSpeedLimit(segmentId, action);
                    break;
                case SetSpeedLimitTarget.SegmentDefault:
                    ApplyDefaultSpeedLimit(segmentId, netInfo, action);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Invalid target specified.");
            }
        }

        internal static void ApplyDefaultSpeedLimit(
            ushort segmentId,
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
                    throw new ArgumentOutOfRangeException("Invalid action type specified.");
            }
        }
    }
}
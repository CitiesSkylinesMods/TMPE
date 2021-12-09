namespace TrafficManager.UI.SubTools.SpeedLimits {
    using TrafficManager.State;

    /// <summary>
    /// Defines where <see cref="SetSpeedLimitAction"/> is applied.
    /// </summary>
    public enum SetSpeedLimitTarget {
        /// <summary>The speed limit will be set or cleared for override per segment.</summary>
        SegmentOverride,

        /// <summary>The speed limit will be set or cleared for override per lane.</summary>
        LaneOverride,

        /// <summary>The speed limit will be set or cleared for default per road type.</summary>
        SegmentDefault,

        /// <summary>The speed limit will be set or cleared for default per road type per lane.</summary>
        LaneDefault,
    }
}
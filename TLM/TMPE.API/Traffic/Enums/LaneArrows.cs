namespace TrafficManager.API.Traffic.Enums {
    using System;
    using JetBrains.Annotations;

    [Flags]
    public enum LaneArrows {
        // compatible with NetLane.Flags
        None = 0,
        Forward = 16,
        Left = 32,
        Right = 64,

        [UsedImplicitly]
        LeftForward = Left + Forward,

        [UsedImplicitly]
        LeftRight = Left + Right,

        [UsedImplicitly]
        ForwardRight = Forward + Right,
        LeftForwardRight = Left + Forward + Right
    }
}
namespace TrafficManager.API.Traffic.Enums {
    using System;
    using JetBrains.Annotations;

    /// <summary>
    /// compatible with NetLane.Flags
    /// </summary>
    [Flags]
    public enum LaneArrows {
        None = 0,
        Forward = NetLane.Flags.Forward,
        Left = NetLane.Flags.Left,
        Right = NetLane.Flags.Right,
        LeftForward = Left | Forward,
        LeftRight = Left | Right,
        ForwardRight = Forward | Right,
        LeftForwardRight = Left | Forward | Right,
    }
}
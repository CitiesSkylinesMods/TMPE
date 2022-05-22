namespace TrafficManager.API.UI {
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    /// <summary>
    /// gets the texture for overlay sprite for each traffic rule according to the current theme.
    /// </summary>
    public interface IRoadSignTheme {
        Texture2D Crossing(bool allowewd);
        Texture2D EnterBlockedJunction(bool allowewd);
        Texture2D LaneChange(bool allowewd);
        Texture2D LeftOnRed(bool allowewd);
        Texture2D RightOnRed(bool allowewd);
        Texture2D UTurn(bool allowewd);
        Texture2D Priority(PriorityType p);
        Texture2D Parking(bool p);
        Texture2D VehicleRestriction(ExtVehicleType type, bool allow);
        Texture2D TrafficLights(bool enabled);
        Texture2D TimedTrafficLights(bool paused);
    }
}

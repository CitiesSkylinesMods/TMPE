namespace TrafficManager.API.UI {
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    /// <summary>
    /// gets the texture for overlay sprite for each traffic rule according to the current theme.
    /// </summary>
    public interface ITheme {
        Texture2D JunctionRestriction(JunctionRestrictionFlags rule, bool allowed);

        Texture2D Parking(bool allowed);

        Texture2D Priority(PriorityType p);

        Texture2D VehicleRestriction(ExtVehicleType type, bool allow);

        Texture2D TrafficLightIcon(ushort nodeId);
    }
}

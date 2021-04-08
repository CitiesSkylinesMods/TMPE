namespace TrafficManager.API.Manager {
    public interface IManagerFactory {
        IAdvancedParkingManager AdvancedParkingManager { get; }
        ICustomSegmentLightsManager CustomSegmentLightsManager { get; }
        IExtBuildingManager ExtBuildingManager { get; }
        IExtCitizenInstanceManager ExtCitizenInstanceManager { get; }
        IExtCitizenManager ExtCitizenManager { get; }
        IExtNodeManager ExtNodeManager { get; }
        IExtPathManager ExtPathManager { get; }
        IExtSegmentManager ExtSegmentManager { get; }
        IExtSegmentEndManager ExtSegmentEndManager { get; }
        IExtVehicleManager ExtVehicleManager { get; }
        IJunctionRestrictionsManager JunctionRestrictionsManager { get; }
        ILaneArrowManager LaneArrowManager { get; }
        ILaneConnectionManager LaneConnectionManager { get; }
        IGeometryManager GeometryManager { get; }
        IOptionsManager OptionsManager { get; }
        IParkingRestrictionsManager ParkingRestrictionsManager { get; }
        IRoutingManager RoutingManager { get; }
        ISegmentEndManager SegmentEndManager { get; }
        ISpeedLimitManager SpeedLimitManager { get; }
        ITrafficLightManager TrafficLightManager { get; }
        ITrafficLightSimulationManager TrafficLightSimulationManager { get; }
        ITrafficMeasurementManager TrafficMeasurementManager { get; }
        ITrafficPriorityManager TrafficPriorityManager { get; }
        ITurnOnRedManager TurnOnRedManager { get; }
        IUtilityManager UtilityManager { get; }
        IVehicleBehaviorManager VehicleBehaviorManager { get; }
        IVehicleRestrictionsManager VehicleRestrictionsManager { get; }
    }
}
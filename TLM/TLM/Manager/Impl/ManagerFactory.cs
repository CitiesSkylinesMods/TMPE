namespace TrafficManager.Manager.Impl {
    using TrafficManager.API.Manager;

    public class ManagerFactory : IManagerFactory {
        public static IManagerFactory Instance = new ManagerFactory();

        public IAdvancedParkingManager AdvancedParkingManager =>
            Impl.AdvancedParkingManager.Instance;

        public ICustomSegmentLightsManager CustomSegmentLightsManager =>
            Impl.CustomSegmentLightsManager.Instance;

        public IExtBuildingManager ExtBuildingManager => Impl.ExtBuildingManager.Instance;

        public IExtCitizenInstanceManager ExtCitizenInstanceManager =>
            Impl.ExtCitizenInstanceManager.Instance;

        public IExtCitizenManager ExtCitizenManager => Impl.ExtCitizenManager.Instance;

        public IExtLaneManager ExtLaneManager => Impl.ExtLaneManager.Instance;

        public IExtNodeManager ExtNodeManager => Impl.ExtNodeManager.Instance;

        public IExtPathManager ExtPathManager => Impl.ExtPathManager.Instance;

        public IExtSegmentManager ExtSegmentManager => Impl.ExtSegmentManager.Instance;

        public IExtSegmentEndManager ExtSegmentEndManager => Impl.ExtSegmentEndManager.Instance;

        public IExtVehicleManager ExtVehicleManager => Impl.ExtVehicleManager.Instance;

        public IJunctionRestrictionsManager JunctionRestrictionsManager =>
            Impl.JunctionRestrictionsManager.Instance;

        public ILaneArrowManager LaneArrowManager => Impl.LaneArrowManager.Instance;

        public ILaneConnectionManager LaneConnectionManager => Impl.LaneConnection.LaneConnectionManager.Instance;

        public IGeometryManager GeometryManager => Impl.GeometryManager.Instance;

        public IOptionsManager OptionsManager => Impl.OptionsManager.Instance;

        public IParkingRestrictionsManager ParkingRestrictionsManager =>
            Impl.ParkingRestrictionsManager.Instance;

        public IRoutingManager RoutingManager => Impl.RoutingManager.Instance;

        public ISegmentEndManager SegmentEndManager => Impl.SegmentEndManager.Instance;

        public ISpeedLimitManager SpeedLimitManager => Impl.SpeedLimitManager.Instance;

        public ITrafficLightManager TrafficLightManager => Impl.TrafficLightManager.Instance;

        public ITrafficLightSimulationManager TrafficLightSimulationManager =>
            Impl.TrafficLightSimulationManager.Instance;

        public ITrafficMeasurementManager TrafficMeasurementManager =>
            Impl.TrafficMeasurementManager.Instance;

        public ITrafficPriorityManager TrafficPriorityManager =>
            Impl.TrafficPriorityManager.Instance;

        public ITurnOnRedManager TurnOnRedManager => Impl.TurnOnRedManager.Instance;

        public IUtilityManager UtilityManager => Impl.UtilityManager.Instance;

        public IVehicleBehaviorManager VehicleBehaviorManager =>
            Impl.VehicleBehaviorManager.Instance;

        public IVehicleRestrictionsManager VehicleRestrictionsManager =>
            Impl.VehicleRestrictionsManager.Instance;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager.Manager {
	public interface IManagerFactory {
		IAdvancedParkingManager AdvancedParkingManager { get; }
		ICustomSegmentLightsManager CustomSegmentLightsManager { get; }
		IExtBuildingManager ExtBuildingManager { get; }
		IExtCitizenInstanceManager ExtCitizenInstanceManager { get; }
		IExtCitizenManager ExtCitizenManager { get; }
		IJunctionRestrictionsManager JunctionRestrictionsManager { get; }
		ILaneArrowManager LaneArrowManager { get; }
		ILaneConnectionManager LaneConnectionManager { get; }
		IOptionsManager OptionsManager { get; }
		IParkingRestrictionsManager ParkingRestrictionsManager { get; }
		IRoutingManager RoutingManager { get; }
		ISegmentEndManager SegmentEndManager { get; }
		ISpeedLimitManager SpeedLimitManager { get; }
		ITrafficLightManager TrafficLightManager { get; }
		ITrafficLightSimulationManager TrafficLightSimulationManager { get; }
		ITrafficMeasurementManager TrafficMeasurementManager { get; }
		ITrafficPriorityManager TrafficPriorityManager { get; }
		IUtilityManager UtilityManager { get; }
		IVehicleBehaviorManager VehicleBehaviorManager { get; }
		IVehicleRestrictionsManager VehicleRestrictionsManager { get; }
		IVehicleStateManager VehicleStateManager { get; }
	}
}

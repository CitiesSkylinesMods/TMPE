using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager.Manager.Impl {
	public class ManagerFactory : IManagerFactory {
		public static IManagerFactory Instance = new ManagerFactory();

		public IAdvancedParkingManager AdvancedParkingManager {
			get {
				return Impl.AdvancedParkingManager.Instance;
			}
		}

		public ICustomSegmentLightsManager CustomSegmentLightsManager {
			get {
				return Impl.CustomSegmentLightsManager.Instance;
			}
		}

		public IEmergencyBehaviorManager EmergencyBehaviorManager {
			get {
				return Impl.EmergencyBehaviorManager.Instance;
			}
		}

		public IExtBuildingManager ExtBuildingManager {
			get {
				return Impl.ExtBuildingManager.Instance;
			}
		}

		public IExtCitizenInstanceManager ExtCitizenInstanceManager {
			get {
				return Impl.ExtCitizenInstanceManager.Instance;
			}
		}

		public IExtCitizenManager ExtCitizenManager {
			get {
				return Impl.ExtCitizenManager.Instance;
			}
		}

		public IExtNodeManager ExtNodeManager {
			get {
				return Impl.ExtNodeManager.Instance;
			}
		}

		public IExtPathManager ExtPathManager {
			get {
				return Impl.ExtPathManager.Instance;
			}
		}

		public IExtSegmentManager ExtSegmentManager {
			get {
				return Impl.ExtSegmentManager.Instance;
			}
		}

		public IExtSegmentEndManager ExtSegmentEndManager {
			get {
				return Impl.ExtSegmentEndManager.Instance;
			}
		}

		public IExtVehicleManager ExtVehicleManager {
			get {
				return Impl.ExtVehicleManager.Instance;
			}
		}

		public IJunctionRestrictionsManager JunctionRestrictionsManager {
			get {
				return Impl.JunctionRestrictionsManager.Instance;
			}
		}

		public ILaneArrowManager LaneArrowManager {
			get {
				return Impl.LaneArrowManager.Instance;
			}
		}

		public ILaneConnectionManager LaneConnectionManager {
			get {
				return Impl.LaneConnectionManager.Instance;
			}
		}

		public IGeometryManager GeometryManager {
			get {
				return Impl.GeometryManager.Instance;
			}
		}

		public IOptionsManager OptionsManager {
			get {
				return Impl.OptionsManager.Instance;
			}
		}

		public IParkingRestrictionsManager ParkingRestrictionsManager {
			get {
				return Impl.ParkingRestrictionsManager.Instance;
			}
		}

		public IRoutingManager RoutingManager {
			get {
				return Impl.RoutingManager.Instance;
			}
		}

		public ISegmentEndManager SegmentEndManager {
			get {
				return Impl.SegmentEndManager.Instance;
			}
		}

		public ISpeedLimitManager SpeedLimitManager {
			get {
				return Impl.SpeedLimitManager.Instance;
			}
		}

		public ITrafficLightManager TrafficLightManager {
			get {
				return Impl.TrafficLightManager.Instance;
			}
		}

		public ITrafficLightSimulationManager TrafficLightSimulationManager {
			get {
				return Impl.TrafficLightSimulationManager.Instance;
			}
		}

		public ITrafficMeasurementManager TrafficMeasurementManager {
			get {
				return Impl.TrafficMeasurementManager.Instance;
			}
		}

		public ITrafficPriorityManager TrafficPriorityManager {
			get {
				return Impl.TrafficPriorityManager.Instance;
			}
		}

		public IUtilityManager UtilityManager {
			get {
				return Impl.UtilityManager.Instance;
			}
		}

		public IVehicleBehaviorManager VehicleBehaviorManager {
			get {
				return Impl.VehicleBehaviorManager.Instance;
			}
		}

		public IVehicleRestrictionsManager VehicleRestrictionsManager {
			get {
				return Impl.VehicleRestrictionsManager.Instance;
			}
		}
	}
}

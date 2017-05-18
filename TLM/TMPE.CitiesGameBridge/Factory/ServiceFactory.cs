using System;
using GenericGameBridge.Factory;
using GenericGameBridge.Service;

namespace CitiesGameBridge.Factory {
	public class ServiceFactory : IServiceFactory {
		public static readonly IServiceFactory Instance = new ServiceFactory();

		private ServiceFactory() {

		}

		public IBuildingService BuildingService {
			get {
				return Service.BuildingService.Instance;
			}
		}

		public ICitizenService CitizenService {
			get {
				return Service.CitizenService.Instance;
			}
		}

		public INetService NetService {
			get {
				return Service.NetService.Instance;
			}
		}

		public ISimulationService SimulationService {
			get {
				return Service.SimulationService.Instance;
			}
		}

		public IVehicleService VehicleService {
			get {
				return Service.VehicleService.Instance;
			}
		}
	}
}

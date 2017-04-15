using GenericGameBridge.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GenericGameBridge.Service;
using CitiesGameBridge.Service;

namespace CitiesGameBridge.Factory {
	public class ServiceFactory : IServiceFactory {
		public static readonly IServiceFactory Instance = new ServiceFactory();

		private ServiceFactory() {

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

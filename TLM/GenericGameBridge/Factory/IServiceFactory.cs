using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericGameBridge.Factory {
	public interface IServiceFactory {
		INetService NetService { get; }
		ISimulationService SimulationService { get; }
		IVehicleService VehicleService { get; }
	}
}

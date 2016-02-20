using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	[Flags]
	public enum ExtVehicleType {
		None = 0,
		PassengerCar = 1,
		Bus = 2,
		Taxi = 4,
		CargoTruck = 8,
		Service = 16,
		Emergency = 32,
		PassengerTrain = 64,
		CargoTrain = 128,
		Tram = 256,
		Bicycle = 512,
		Pedestrian = 1024,
		CargoVehicle = CargoTruck | CargoTrain,
		PublicTransport = Bus | Taxi | Tram,
		RoadPublicTransport = Bus | Taxi,
		RoadVehicle = PassengerCar | Bus | Taxi | CargoTruck | Service | Emergency,
		RailVehicle = PassengerTrain | CargoTrain,
		NonTransportRoadVehicle = RoadVehicle & ~PublicTransport
	}
}

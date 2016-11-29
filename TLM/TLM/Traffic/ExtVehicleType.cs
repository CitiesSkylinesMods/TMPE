using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Traffic {
	[Flags]
	public enum ExtVehicleType {
		None = 0,
		PassengerCar = 1,
		Bus = 1 << 1,
		Taxi = 1 << 2,
		CargoTruck = 1 << 3,
		Service = 1 << 4,
		Emergency = 1 << 5,
		PassengerTrain = 1 << 6,
		CargoTrain = 1 << 7,
		Tram = 1 << 8,
		Bicycle = 1 << 9,
		Pedestrian = 1 << 10,
		PassengerShip = 1 << 11,
		CargoShip = 1 << 12,
		PassengerPlane = 1 << 13,
		Helicopter = 1 << 14,
		Plane = PassengerPlane,
		Ship = PassengerShip | CargoShip,
		CargoVehicle = CargoTruck | CargoTrain | CargoShip,
		PublicTransport = Bus | Taxi | Tram | PassengerTrain,
		RoadPublicTransport = Bus | Taxi,
		RoadVehicle = PassengerCar | Bus | Taxi | CargoTruck | Service | Emergency,
		RailVehicle = PassengerTrain | CargoTrain,
		NonTransportRoadVehicle = RoadVehicle & ~PublicTransport
	}
}

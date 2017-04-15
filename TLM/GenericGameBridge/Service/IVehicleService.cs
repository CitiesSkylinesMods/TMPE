using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericGameBridge.Service {
	public delegate bool VehicleHandler(ushort vehicleId, ref Vehicle vehicle);

	public interface IVehicleService {
		bool CheckVehicleFlags(ushort vehicleId, Vehicle.Flags flagMask, Vehicle.Flags? expectedResult = default(Vehicle.Flags?));
		bool CheckVehicleFlags2(ushort vehicleId, Vehicle.Flags2 flagMask, Vehicle.Flags2? expectedResult = default(Vehicle.Flags2?));
		bool IsVehicleValid(ushort vehicleId);
		void ProcessVehicle(ushort vehicleId, VehicleHandler handler);
		void ProcessVehicle(ushort vehicleId, ref Vehicle vehicle, VehicleHandler handler);
	}
}

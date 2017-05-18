using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;

namespace CitiesGameBridge.Service {
	public class VehicleService : IVehicleService {
		public static readonly IVehicleService Instance = new VehicleService();

		private VehicleService() {

		}

		public bool CheckVehicleFlags(ushort vehicleId, Vehicle.Flags flagMask, Vehicle.Flags? expectedResult = default(Vehicle.Flags?)) {
			bool ret = false;
			ProcessVehicle(vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
				ret = LogicUtil.CheckFlags((uint)vehicle.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool CheckVehicleFlags2(ushort vehicleId, Vehicle.Flags2 flagMask, Vehicle.Flags2? expectedResult = default(Vehicle.Flags2?)) {
			bool ret = false;
			ProcessVehicle(vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
				ret = LogicUtil.CheckFlags((uint)vehicle.m_flags2, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool IsVehicleValid(ushort vehicleId) {
			return CheckVehicleFlags(vehicleId, Vehicle.Flags.Created | Vehicle.Flags.Deleted, Vehicle.Flags.Created);
		}

		public void ProcessVehicle(ushort vehicleId, VehicleHandler handler) {
			ProcessVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], handler);
		}

		public void ProcessVehicle(ushort vehicleId, ref Vehicle vehicle, VehicleHandler handler) {
			handler(vehicleId, ref vehicle);
		}
	}
}

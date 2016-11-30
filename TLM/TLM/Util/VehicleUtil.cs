using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;

namespace TrafficManager.Util {
	public static class VehicleUtil {
		public delegate void VehicleHandler(ushort vehicleId, ref Vehicle vehicle);
		public delegate void ParkedVehicleHandler(ushort parkedVehicleId, ref VehicleParked parkedVehicle);

		public static void ProcessVehicle(ushort vehicleId, VehicleHandler handler) {
			ProcessVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], handler);
		}

		public static void ProcessVehicle(ushort vehicleId, ref Vehicle vehicle, VehicleHandler handler) {
			handler(vehicleId, ref vehicle);
		}

		public static void ProcessParkedVehicle(ushort parkedVehicleId, ParkedVehicleHandler handler) {
			ProcessParkedVehicle(parkedVehicleId, ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId], handler);
		}

		public static void ProcessParkedVehicle(ushort parkedVehicleId, ref VehicleParked parkedVehicle, ParkedVehicleHandler handler) {
			handler(parkedVehicleId, ref parkedVehicle);
		}
	}
}

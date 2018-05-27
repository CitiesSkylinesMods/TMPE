using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IVehicleStateManager {
		// TODO define me!
		// TODO documentation
		void SetNextVehicleIdOnSegment(ushort vehicleId, ushort nextVehicleId);
		void SetPreviousVehicleIdOnSegment(ushort vehicleId, ushort previousVehicleId);
		
		/// <summary>
		/// Handles a released vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData);
	}
}

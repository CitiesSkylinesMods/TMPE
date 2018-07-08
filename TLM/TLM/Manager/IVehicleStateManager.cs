using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Manager {
	public interface IVehicleStateManager {
		// TODO define me!
		// TODO documentation
		VehicleState[] VehicleStates { get; }
		void SetNextVehicleIdOnSegment(ushort vehicleId, ushort nextVehicleId);
		void SetPreviousVehicleIdOnSegment(ushort vehicleId, ushort previousVehicleId);
	}
}

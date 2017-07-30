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
	}
}

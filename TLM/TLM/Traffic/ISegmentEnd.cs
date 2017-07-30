using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;

namespace TrafficManager.Traffic {
	public interface ISegmentEnd : ISegmentEndId {
		[Obsolete]
		ushort NodeId { get; }
		ushort FirstRegisteredVehicleId { get; set; } // TODO private set

		void Update();
		void Destroy();
		IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped = true, bool debug = false);
		int GetRegisteredVehicleCount();
	}
}

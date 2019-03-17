using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	[Obsolete("should be removed when implementing issue #240")]
	public interface ISegmentEnd : ISegmentEndId {
		[Obsolete]
		ushort NodeId { get; }
		//ushort FirstRegisteredVehicleId { get; set; } // TODO private set

		void Update();
		void Destroy();
		IDictionary<ushort, uint>[] MeasureOutgoingVehicles(bool includeStopped = true, bool debug = false);
		uint GetRegisteredVehicleCount();
	}
}

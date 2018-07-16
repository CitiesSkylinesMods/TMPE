using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	// TODO why do we need this?
	[Flags]
	public enum ExtVehicleFlags {
		None = 0,
		Created = 1,
		Spawned = 1 << 1
	}
}

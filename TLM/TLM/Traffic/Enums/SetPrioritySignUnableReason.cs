using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	public enum SetPrioritySignUnableReason {
		None,
		NoJunction,
		HasTimedLight,
		InvalidSegment,
		NotIncoming
	}
}

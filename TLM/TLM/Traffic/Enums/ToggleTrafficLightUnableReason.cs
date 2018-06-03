using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	public enum ToggleTrafficLightUnableReason {
		None,
		NoJunction,
		HasTimedLight,
		InsufficientSegments
	}
}

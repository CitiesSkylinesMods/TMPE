using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.TrafficLight {
	public enum LightMode {
		Simple = 1, // <^>
		SingleLeft = 2, // <, ^>
		SingleRight = 3, // <^, >
		All = 4 // <, ^, >
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Enums {
	[Flags]
	public enum LaneArrows { // compatible with NetLane.Flags
		None = 0,
		Forward = 16,
		Left = 32,
		Right = 64,
		LeftForward = 48,
		LeftRight = 96,
		ForwardRight = 80,
		LeftForwardRight = 112
	}
}

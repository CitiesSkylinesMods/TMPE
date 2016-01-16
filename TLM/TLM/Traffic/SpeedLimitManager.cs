using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic {
	class SpeedLimitManager {
		public static readonly List<ushort> AvailableSpeedLimits;

		static SpeedLimitManager() {
			AvailableSpeedLimits = new List<ushort>();
			AvailableSpeedLimits.Add(0);
			AvailableSpeedLimits.Add(10);
			AvailableSpeedLimits.Add(20);
			AvailableSpeedLimits.Add(30);
			AvailableSpeedLimits.Add(40);
			AvailableSpeedLimits.Add(50);
			AvailableSpeedLimits.Add(60);
			AvailableSpeedLimits.Add(70);
			AvailableSpeedLimits.Add(80);
			AvailableSpeedLimits.Add(90);
			AvailableSpeedLimits.Add(100);
			AvailableSpeedLimits.Add(120);
			AvailableSpeedLimits.Add(130);
		}
	}
}

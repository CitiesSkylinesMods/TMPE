using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Util {
	public static class MathUtil {
		public static float RandomizeFloat(Randomizer rng, float lower, float upper) {
			return ((float)rng.UInt32(0, 10001) / 10000f) * (upper - lower) + lower;
		}
	}
}

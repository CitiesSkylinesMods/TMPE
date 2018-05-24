using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State.ConfigData {
	public class DynamicLaneSelection {
		/// <summary>
		/// Maximum allowed reserved space on previous vehicle lane
		/// </summary>
		public float MaxReservedSpace = 0.5f;

		/// <summary>
		/// Maximum allowed reserved space on previous vehicle lane (for reckless drivers)
		/// </summary>
		public float MaxRecklessReservedSpace = 10f;

		/// <summary>
		/// Lane speed randomization interval
		/// </summary>
		public float LaneSpeedRandInterval = 5f;

		/// <summary>
		/// Maximum number of considered lane changes
		/// </summary>
		public int MaxOptLaneChanges = 2;

		/// <summary>
		/// Maximum allowed speed difference for safe lane changes
		/// </summary>
		public float MaxUnsafeSpeedDiff = 0.4f;

		/// <summary>
		/// Minimum required speed improvement for safe lane changes
		/// </summary>
		public float MinSafeSpeedImprovement = 25f;

		/// <summary>
		/// Minimum required traffic flow improvement for safe lane changes
		/// </summary>
		public float MinSafeTrafficImprovement = 20f;

		/// <summary>
		/// Minimum relative speed (in %) where volume measurement starts
		/// </summary>
		public ushort VolumeMeasurementRelSpeedThreshold = 50;
	}
}

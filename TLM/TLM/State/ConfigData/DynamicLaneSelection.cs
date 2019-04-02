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

		// ---

		/// <summary>
		/// Minimum maximum allowed reserved space on previous vehicle lane (for regular drivers)
		/// </summary>
		public float MinMaxReservedSpace = 0f;

		/// <summary>
		/// Maximum value for Maximum allowed reserved space on previous vehicle lane (for regular drivers)
		/// </summary>
		public float MaxMaxReservedSpace = 5f;

		/// <summary>
		/// Minimum maximum allowed reserved space on previous vehicle lane (for reckless drivers)
		/// </summary>
		public float MinMaxRecklessReservedSpace = 10f;

		/// <summary>
		/// Maximum maximum allowed reserved space on previous vehicle lane (for reckless drivers)
		/// </summary>
		public float MaxMaxRecklessReservedSpace = 50f;

		/// <summary>
		/// Minimum lane speed randomization interval
		/// </summary>
		public float MinLaneSpeedRandInterval = 0f;

		/// <summary>
		/// Maximum lane speed randomization interval
		/// </summary>
		public float MaxLaneSpeedRandInterval = 25f;

		/// <summary>
		/// Maximum number of considered lane changes
		/// </summary>
		public int MinMaxOptLaneChanges = 1;

		/// <summary>
		/// Maximum number of considered lane changes
		/// </summary>
		public int MaxMaxOptLaneChanges = 3;

		/// <summary>
		/// Minimum maximum allowed speed difference for safe lane changes (in game units)
		/// </summary>
		public float MinMaxUnsafeSpeedDiff = 0.1f;

		/// <summary>
		/// Maximum maximum allowed speed difference for safe lane changes (in game units)
		/// </summary>
		public float MaxMaxUnsafeSpeedDiff = 1f;

		/// <summary>
		/// Minimum minimum required speed improvement for safe lane changes (in km/h)
		/// </summary>
		public float MinMinSafeSpeedImprovement = 5f;

		/// <summary>
		/// Maximum minimum required speed improvement for safe lane changes (in km/h)
		/// </summary>
		public float MaxMinSafeSpeedImprovement = 30f;

		/// <summary>
		/// Minimum minimum required traffic flow improvement for safe lane changes (in %)
		/// </summary>
		public float MinMinSafeTrafficImprovement = 5f;

		/// <summary>
		/// Maximum minimum required traffic flow improvement for safe lane changes (in %)
		/// </summary>
		public float MaxMinSafeTrafficImprovement = 30f;
	}
}

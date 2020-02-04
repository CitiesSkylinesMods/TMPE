namespace TrafficManager.State.ConfigData {
    using TrafficManager.API.Traffic.Data;

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

        /*
         * max. reserved space:
         *   low = egoistic
         *   high = altruistic
         */

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

        /*
         * lane speed randomization interval:
         *    low = altruistic (driver sees the true lane speed)
         *    high = egoistic (driver imagines to be in the slowest queue, http://www.bbc.com/future/story/20130827-why-other-queues-move-faster)
         */

        /// <summary>
        /// Minimum lane speed randomization interval
        /// </summary>
        public float MinLaneSpeedRandInterval = 0f;

        /// <summary>
        /// Maximum lane speed randomization interval
        /// </summary>
        public float MaxLaneSpeedRandInterval = 25f;

        /*
         * max. considered lane changes:
         *    low = altruistic
         *    high = egoistic
         */

        /// <summary>
        /// Maximum number of considered lane changes
        /// </summary>
        public int MinMaxOptLaneChanges = 1;

        /// <summary>
        /// Maximum number of considered lane changes
        /// </summary>
        public int MaxMaxOptLaneChanges = 3;

        /*
         * max. allowed speed difference for unsafe lane changes
         *    low = altruistic
         *    high = egoistic
         */

        /// <summary>
        /// Minimum maximum allowed speed difference for unsafe lane changes (in game units)
        /// </summary>
        public float MinMaxUnsafeSpeedDiff = 0.1f;

        /// <summary>
        /// Maximum maximum allowed speed difference for unsafe lane changes (in game units)
        /// </summary>
        public float MaxMaxUnsafeSpeedDiff = 1f;

        /*
         * min. required speed improvement for safe lane changes
         *    low = egoistic
         *    high = altruistic
         */

        /// <summary>
        /// Minimum minimum required speed improvement for safe lane changes (in game units).
        /// Set to 5 km/h in game units.
        /// </summary>
        public readonly SpeedValue MinMinSafeSpeedImprovement = SpeedValue.FromKmph(5);

        /// <summary>
        /// Maximum minimum required speed improvement for safe lane changes (in game units)
        /// Set to 30 km/h in game units.
        /// </summary>
        public readonly SpeedValue MaxMinSafeSpeedImprovement = SpeedValue.FromKmph(30);

        /*
         * min. required traffic flow improvement for safe lane changes
         *    low = egoistic
         *    high = altruistic
         */

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
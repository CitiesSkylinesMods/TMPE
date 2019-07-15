namespace TrafficManager.State.ConfigData {
    public class AdvancedVehicleAI {
        /// <summary>
        /// Junction randomization for randomized lane selection
        /// </summary>
        public uint LaneRandomizationJunctionSel = 3;

        /// <summary>
        /// Cost factor for lane randomization
        /// </summary>
        public float LaneRandomizationCostFactor = 1f;

        /// <summary>
        /// minimum base lane changing cost
        /// </summary>
        public float LaneChangingBaseMinCost = 1.1f;

        /// <summary>
        /// maximum base lane changing cost
        /// </summary>
        public float LaneChangingBaseMaxCost = 1.5f;

        /// <summary>
        /// base cost for changing lanes in front of junctions
        /// </summary>
        public float LaneChangingJunctionBaseCost = 2f;

        /// <summary>
        /// base cost for traversing junctions
        /// </summary>
        public float JunctionBaseCost = 0.1f;

        /// <summary>
        /// > 1 lane changing cost factor
        /// </summary>
        public float MoreThanOneLaneChangingCostFactor = 2f;

        /// <summary>
        /// Relative factor for lane traffic cost calculation
        /// </summary>
        public float TrafficCostFactor = 4f;

        /// <summary>
        /// lane density random interval
        /// </summary>
        public float LaneDensityRandInterval = 20f;

        /// <summary>
        /// Threshold for resetting traffic buffer
        /// </summary>
        public uint MaxTrafficBuffer = 10;
    }
}
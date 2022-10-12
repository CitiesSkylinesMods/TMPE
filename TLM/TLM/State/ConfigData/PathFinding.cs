namespace TrafficManager.State.ConfigData {
    public class PathFinding {
        /// <summary>
        /// penalty for buses not driving on bus lanes
        /// </summary>
        public float PublicTransportLanePenalty = 10f;

        /// <summary>
        /// reward for public transport staying on transport lane
        /// </summary>
        public float PublicTransportLaneReward = 0.1f;

        /// <summary>
        /// maximum penalty for heavy vehicles driving on an inner lane
        /// </summary>
        public float HeavyVehicleMaxInnerLanePenalty = 0.5f;

        /// <summary>
        /// Junction randomization for randomized lane selection
        /// </summary>
        public uint HeavyVehicleInnerLanePenaltySegmentSel = 3;

        /// <summary>
        /// artificial lane distance for vehicles that change to lanes which have an incompatible lane arrow configuration
        /// </summary>
        public byte IncompatibleLaneDistance = 2;

        /// <summary>
        /// artificial lane distance for u-turns
        /// </summary>
        public int UturnLaneDistance = 2;

        /// <summary>
        /// Maximum walking distance
        /// </summary>
        public float MaxWalkingDistance = 2500f;

        /// <summary>
        /// Minimum penalty for entering public transport vehicles
        /// </summary>
        public float PublicTransportTransitionMinPenalty = 0f;

        /// <summary>
        /// Maximum penalty for entering public transport vehicles
        /// </summary>
        public float PublicTransportTransitionMaxPenalty = 100f;

        /// <summary>
        /// Allows Buses to drive through districts with Old Town policy (AfterDark DLC)
        /// </summary>
        public bool AllowBusInOldTownDistricts = false;

        /// <summary>
        /// Allows Taxis to drive through districts with Old Town policy (AfterDark DLC)
        /// </summary>
        public bool AllowTaxiInOldTownDistricts = false;
    }
}
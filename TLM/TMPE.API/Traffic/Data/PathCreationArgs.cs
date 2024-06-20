namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Auto)]
    public struct PathCreationArgs {
        /// <summary>
        /// Extended path type
        /// </summary>
        public ExtPathType extPathType;

        /// <summary>
        /// Extended vehicle type
        /// </summary>
        public ExtVehicleType extVehicleType;

        /// <summary>
        /// (optional) vehicle id
        /// </summary>
        public ushort vehicleId;

        /// <summary>
        /// is entity alredy spawned?
        /// </summary>
        public bool spawned;

        /// <summary>
        /// Current build index
        /// </summary>
        public uint buildIndex;

        /// <summary>
        /// Start position (first alternative)
        /// </summary>
        public PathUnit.Position startPosA;

        /// <summary>
        /// Start position (second alternative, opposite road side)
        /// </summary>
        public PathUnit.Position startPosB;

        /// <summary>
        /// End position (first alternative)
        /// </summary>
        public PathUnit.Position endPosA;

        /// <summary>
        /// End position (second alternative, opposite road side)
        /// </summary>
        public PathUnit.Position endPosB;

        /// <summary>
        /// (optional) position of the parked vehicle
        /// </summary>
        public PathUnit.Position vehiclePosition;

        /// <summary>
        /// Allowed set of lane types
        /// </summary>
        public NetInfo.LaneType laneTypes;

        /// <summary>
        /// Allowed set of vehicle types
        /// </summary>
        public VehicleInfo.VehicleType vehicleTypes;

        /// <summary>
        /// Allowed set of vehicle categories
        /// </summary>
        public VehicleInfo.VehicleCategory vehicleCategories;

        /// <summary>
        /// Maximum allowed path length
        /// </summary>
        public float maxLength;

        /// <summary>
        /// Is the path calculated for a heavy vehicle?
        /// </summary>
        public bool isHeavyVehicle;

        /// <summary>
        /// Is the path calculated for a vehicle with a combustion engine?
        /// </summary>
        public bool hasCombustionEngine;

        /// <summary>
        /// Should blocked segments be ignored?
        /// </summary>
        public bool ignoreBlocked;

        /// <summary>
        /// Should flooded segments be ignored?
        /// </summary>
        public bool ignoreFlooded;

        /// <summary>
        /// Should path costs be ignored?
        /// </summary>
        public bool ignoreCosts;

        /// <summary>
        /// Should random parking apply?
        /// </summary>
        public bool randomParking;

        /// <summary>
        /// Should the path be stable (and not randomized)?
        /// </summary>
        public bool stablePath;

        /// <summary>
        /// Is this a high priority path?
        /// </summary>
        public bool skipQueue;
    }
}
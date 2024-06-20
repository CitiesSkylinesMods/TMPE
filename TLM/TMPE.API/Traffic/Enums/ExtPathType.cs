namespace TrafficManager.API.Traffic.Enums {
    public enum ExtPathType : byte {
        /// <summary>
        /// Mixed path
        /// </summary>
        None = 0,

        /// <summary>
        /// Walking path
        /// </summary>
        WalkingOnly = 1,

        /// <summary>
        /// Driving path
        /// </summary>
        DrivingOnly = 2,
    }
}
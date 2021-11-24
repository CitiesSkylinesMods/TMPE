namespace TrafficManager.Util.Extensions {
    public static class VehicleExtensions {
        /// <summary>
        /// Checks if the vehicle is Created, but not Deleted.
        /// </summary>
        /// <param name="vehicle">vehicle</param>
        /// <returns>True if the vehicle is valid, otherwise false.</returns>
        public static bool IsValid(this ref Vehicle vehicle) =>
            vehicle.m_flags.CheckFlags(
                required: Vehicle.Flags.Created,
                forbidden: Vehicle.Flags.Deleted);
    }
}

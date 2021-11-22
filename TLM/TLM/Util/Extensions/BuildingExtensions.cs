namespace TrafficManager.Util.Extensions {
    public static class BuildingExtensions {
        /// <summary>
        /// Checks if the building is Created, but neither Collapsed nor Deleted.
        /// </summary>
        /// <param name="building">building</param>
        /// <returns>True if the building is valid, otherwise false.</returns>
        public static bool IsValid(this ref Building building) =>
            building.m_flags.CheckFlags(
                required: Building.Flags.Created,
                forbidden: Building.Flags.Collapsed | Building.Flags.Deleted);
    }
}

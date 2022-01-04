namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    public static class BuildingExtensions {
        private static Building[] _buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;

        internal static ref Building ToBuilding(this ushort buildingId) => ref _buildingBuffer[buildingId];

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

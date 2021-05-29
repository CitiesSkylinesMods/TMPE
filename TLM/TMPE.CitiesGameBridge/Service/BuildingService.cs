namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using GenericGameBridge.Service;
    using System;

    public class BuildingService : IBuildingService {
        public static readonly IBuildingService Instance = new BuildingService();

        private BuildingService() { }

        /// <summary>
        /// Check if a building id is valid.
        /// </summary>
        /// 
        /// <param name="buildingId">The id of the building to check.</param>
        /// 
        /// <returns>Returns <c>true</c> if valid, otherwise <c>false</c>.</returns>
        public bool IsBuildingValid(ushort buildingId) {
            return CheckBuildingFlags(
                buildingId,
                Building.Flags.Created | Building.Flags.Collapsed | Building.Flags.Deleted,
                Building.Flags.Created);
        }

        /// <summary>
        /// Check building flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="buildingId">The id of the building to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckBuildingFlags(ushort buildingId,
                                       Building.Flags flagMask,
                                       Building.Flags? expectedResult = null) {

            Building.Flags result = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId].m_flags & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }
    }
}
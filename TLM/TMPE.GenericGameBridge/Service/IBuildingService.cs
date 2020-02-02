namespace GenericGameBridge.Service {
    public delegate bool BuildingHandler(ushort buildingId, ref Building building);

    public interface IBuildingService {
        bool CheckBuildingFlags(ushort buildingId,
                                Building.Flags flagMask,
                                Building.Flags? expectedResult = default);

        bool IsBuildingValid(ushort buildingId);

        void ProcessBuilding(ushort buildingId, BuildingHandler handler);

        void ProcessBuilding(ushort buildingId, ref Building building, BuildingHandler handler);
    }
}
namespace GenericGameBridge.Service {
    public interface IBuildingService {
        bool CheckBuildingFlags(ushort buildingId,
                                Building.Flags flagMask,
                                Building.Flags? expectedResult = default);

        bool IsBuildingValid(ushort buildingId);
    }
}
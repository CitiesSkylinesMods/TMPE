namespace GenericGameBridge.Service {
    public interface ICitizenService {
        bool CheckCitizenFlags(uint citizenId,
                               Citizen.Flags flagMask,
                               Citizen.Flags? expectedResult = default);

        bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                       CitizenInstance.Flags flagMask,
                                       CitizenInstance.Flags? expectedResult =
                                           default);

        bool IsCitizenInstanceValid(ushort citizenInstanceId);
    }
}
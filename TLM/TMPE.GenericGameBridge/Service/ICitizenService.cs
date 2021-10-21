namespace GenericGameBridge.Service {
    public interface ICitizenService {
        bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                       CitizenInstance.Flags flagMask,
                                       CitizenInstance.Flags? expectedResult =
                                           default);

        bool IsCitizenInstanceValid(ushort citizenInstanceId);
    }
}
namespace GenericGameBridge.Service {
    public interface ICitizenService {
        bool CheckCitizenFlags(uint citizenId,
                               Citizen.Flags flagMask,
                               Citizen.Flags? expectedResult = default);

        bool IsCitizenValid(uint citizenId);

        bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                       CitizenInstance.Flags flagMask,
                                       CitizenInstance.Flags? expectedResult =
                                           default);

        bool IsCitizenInstanceValid(ushort citizenInstanceId);

        /// <summary>
        /// Despawns and releases the given citizen instance.
        /// </summary>
        /// <param name="citizenInstanceId">Citizen instance id to release</param>
        void ReleaseCitizenInstance(ushort citizenInstanceId);
    }
}
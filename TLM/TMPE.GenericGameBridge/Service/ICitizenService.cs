namespace GenericGameBridge.Service {
    public delegate bool CitizenHandler(uint citizenId, ref Citizen citizen);

    public delegate bool CitizenInstanceHandler(ushort citizenInstanceId,
                                                ref CitizenInstance citizenInstance);

    public interface ICitizenService {
        bool CheckCitizenFlags(uint citizenId,
                               Citizen.Flags flagMask,
                               Citizen.Flags? expectedResult = default);

        bool IsCitizenValid(uint citizenId);

        void ProcessCitizen(uint citizenId, CitizenHandler handler);

        bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                       CitizenInstance.Flags flagMask,
                                       CitizenInstance.Flags? expectedResult =
                                           default);

        bool IsCitizenInstanceValid(ushort citizenInstanceId);

        void ProcessCitizenInstance(ushort citizenInstanceId, CitizenInstanceHandler handler);

        /// <summary>
        /// Despawns and releases the given citizen instance.
        /// </summary>
        /// <param name="citizenInstanceId">Citizen instance id to release</param>
        void ReleaseCitizenInstance(ushort citizenInstanceId);
    }
}
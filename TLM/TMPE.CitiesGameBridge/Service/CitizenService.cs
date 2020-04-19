namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class CitizenService : ICitizenService {
        public static readonly ICitizenService Instance = new CitizenService();

        private CitizenService() { }

        /// <summary>
        /// Check citizen flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="citizenId">The id of the citizen to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckCitizenFlags(uint citizenId,
                                      Citizen.Flags flagMask,
                                      Citizen.Flags? expectedResult = null) {

            Citizen.Flags result =
                Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags
                & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        /// <summary>
        /// Check citizen instance flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="citizenInstanceId">The id of the citizen instance to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                              CitizenInstance.Flags flagMask,
                                              CitizenInstance.Flags? expectedResult = null) {

            CitizenInstance.Flags result =
                Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId].m_flags
                & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        // TODO: check collapsed flag?
        public bool IsCitizenInstanceValid(ushort citizenInstanceId) {
            return CheckCitizenInstanceFlags(
                citizenInstanceId,
                CitizenInstance.Flags.Created | CitizenInstance.Flags.Deleted,
                CitizenInstance.Flags.Created);
        }

        // TODO: check collapsed flag?
        public bool IsCitizenValid(uint citizenId) {
            return CheckCitizenFlags(citizenId, Citizen.Flags.Created);
        }

        public void ProcessCitizen(uint citizenId, CitizenHandler handler) {
            handler(
                citizenId,
                ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId]);
        }

        public void ProcessCitizenInstance(ushort citizenInstanceId,
                                           CitizenInstanceHandler handler) {
            handler(
                citizenInstanceId,
                ref Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId]);
        }

        public void ReleaseCitizenInstance(ushort citizenInstanceId) {
            Singleton<CitizenManager>.instance.ReleaseCitizenInstance(citizenInstanceId);
        }
    }
}
namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class CitizenService : ICitizenService {
        public static readonly ICitizenService Instance = new CitizenService();

        private CitizenService() { }

        public bool CheckCitizenFlags(uint citizenId,
                                      Citizen.Flags flagMask,
                                      Citizen.Flags? expectedResult = default) {
            bool ret = false;
            ProcessCitizen(
                citizenId,
                (uint cId, ref Citizen citizen) => {
                    ret = LogicUtil.CheckFlags(
                        (uint)citizen.m_flags,
                        (uint)flagMask,
                        (uint?)expectedResult);
                    return true;
                });
            return ret;
        }

        public bool CheckCitizenInstanceFlags(ushort citizenInstanceId,
                                              CitizenInstance.Flags flagMask,
                                              CitizenInstance.Flags? expectedResult =
                                                  default) {
            bool ret = false;
            ProcessCitizenInstance(
                citizenInstanceId,
                (ushort ciId, ref CitizenInstance citizenInstance) => {
                    ret = LogicUtil.CheckFlags(
                        (uint)citizenInstance.m_flags,
                        (uint)flagMask,
                        (uint?)expectedResult);
                    return true;
                });
            return ret;
        }

        public bool IsCitizenInstanceValid(ushort citizenInstanceId) {
            return CheckCitizenInstanceFlags(
                citizenInstanceId,
                CitizenInstance.Flags.Created | CitizenInstance.Flags.Deleted,
                CitizenInstance.Flags.Created);
        }

        public bool IsCitizenValid(uint citizenId) {
            return CheckCitizenFlags(citizenId, Citizen.Flags.Created);
        }

        public void ProcessCitizen(uint citizenId, CitizenHandler handler) {
            ProcessCitizen(
                citizenId,
                ref Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId],
                handler);
        }

        public void ProcessCitizen(uint citizenId, ref Citizen citizen, CitizenHandler handler) {
            handler(citizenId, ref citizen);
        }

        public void ProcessCitizenInstance(ushort citizenInstanceId,
                                           CitizenInstanceHandler handler) {
            ProcessCitizenInstance(
                citizenInstanceId,
                ref Singleton<CitizenManager>.instance.m_instances.m_buffer[citizenInstanceId],
                handler);
        }

        public void ProcessCitizenInstance(ushort citizenInstanceId,
                                           ref CitizenInstance citizenInstance,
                                           CitizenInstanceHandler handler) {
            handler(citizenInstanceId, ref citizenInstance);
        }

        public void ReleaseCitizenInstance(ushort citizenInstanceId) {
            Singleton<CitizenManager>.instance.ReleaseCitizenInstance(citizenInstanceId);
        }
    }
}
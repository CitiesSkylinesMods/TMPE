namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class CitizenService : ICitizenService {
        public static readonly ICitizenService Instance = new CitizenService();

        private CitizenService() { }
    }
}
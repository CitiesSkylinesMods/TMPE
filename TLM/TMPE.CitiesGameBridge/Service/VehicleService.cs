namespace CitiesGameBridge.Service {
    using GenericGameBridge.Service;

    public class VehicleService : IVehicleService {
        public static readonly IVehicleService Instance = new VehicleService();

        private VehicleService() { }
    }
}
namespace CitiesGameBridge.Service {
    using GenericGameBridge.Service;

    public class VehicleService : IVehicleService {
        public static readonly IVehicleService Instance = new VehicleService();

        private VehicleService() { }

        public int MaxVehicleCount => VehicleManager.instance.m_vehicles.m_buffer.Length;
    }
}
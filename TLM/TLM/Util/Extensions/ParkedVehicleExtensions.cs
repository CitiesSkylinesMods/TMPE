namespace TrafficManager.Util.Extensions
{
    using ColossalFramework;

    public static class ParkedVehicleExtensions
    {
        private static readonly VehicleParked[] _parkedVehiclesBuffer = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer;

        public static ref VehicleParked ToParkedVehicle(this ushort parkedVehicleId) => ref _parkedVehiclesBuffer[parkedVehicleId];

        public static ref VehicleParked ToParkedVehicle(this uint parkedVehicleId) => ref _parkedVehiclesBuffer[parkedVehicleId];

        public static bool IsCreated(this ref VehicleParked parkedVehicle) =>
            ((VehicleParked.Flags)parkedVehicle.m_flags).IsFlagSet(VehicleParked.Flags.Created);

        public static void AssignToCitizenAndMakeVisible(this ushort parkedVehicleId,
                                                           uint citizenId) {
            CitizenManager.instance.m_citizens.m_buffer[citizenId].SetParkedVehicle(citizenId, parkedVehicleId);
            _parkedVehiclesBuffer[parkedVehicleId].m_flags &= (ushort)(VehicleParked.Flags.All & ~VehicleParked.Flags.Parking);
        }
    }
}


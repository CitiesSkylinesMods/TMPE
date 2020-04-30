namespace CitiesGameBridge.Service {
    using ColossalFramework;
    using CSUtil.Commons;
    using GenericGameBridge.Service;

    public class VehicleService : IVehicleService {
        public static readonly IVehicleService Instance = new VehicleService();

        private VehicleService() { }

        public int MaxVehicleCount => VehicleManager.instance.m_vehicles.m_buffer.Length;

        /// <summary>
        /// Check vehicle flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="vehicleId">The id of the vehicle to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckVehicleFlags(ushort vehicleId,
                                      Vehicle.Flags flagMask,
                                      Vehicle.Flags? expectedResult = null) {

            Vehicle.Flags result =
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags
                & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        /// <summary>
        /// Check vehicle flags2 contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="vehicleId">The id of the vehicle to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckVehicleFlags2(ushort vehicleId,
                                       Vehicle.Flags2 flagMask,
                                       Vehicle.Flags2? expectedResult = null) {

            Vehicle.Flags2 result =
                Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId].m_flags2
                & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }

        public bool IsVehicleValid(ushort vehicleId) {
            return CheckVehicleFlags(
                vehicleId,
                Vehicle.Flags.Created | Vehicle.Flags.Deleted,
                Vehicle.Flags.Created);
        }

        public void ProcessParkedVehicle(ushort parkedVehicleId, ParkedVehicleHandler handler) {
            handler(
                parkedVehicleId,
                ref Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId]);
        }

        public void ProcessVehicle(ushort vehicleId, VehicleHandler handler) {
            handler(
                vehicleId,
                ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId]);
        }

        public void ReleaseParkedVehicle(ushort parkedVehicleId) {
            Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
        }

        public void ReleaseVehicle(ushort vehicleId) {
            Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
        }
    }
}
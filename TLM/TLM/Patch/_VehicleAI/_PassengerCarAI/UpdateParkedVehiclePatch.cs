namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;

    [UsedImplicitly]
    [HarmonyPatch(typeof(PassengerCarAI), "UpdateParkedVehicle")]
    public static class UpdateParkedVehiclePatch {

        [UsedImplicitly]
        public static bool Prefix(ushort parkedID, ref VehicleParked parkedData) {
            uint ownerCitizenId = parkedData.m_ownerCitizen;
            ushort homeId = 0;

            if (ownerCitizenId != 0u) {
                homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[ownerCitizenId].m_homeBuilding;
            }

            // NON-STOCK CODE START
            if (!AdvancedParkingManager.Instance.TryMoveParkedVehicle(
                    parkedID,
                    ref parkedData,
                    parkedData.m_position,
                    GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                    homeId)) {
                Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedID);
            }
            // NON-STOCK CODE STOP
            return false;
        }
    }
}
namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch(typeof(PassengerCarAI), "UpdateParkedVehicle")]
    public static class UpdateParkedVehiclePatch {

        [UsedImplicitly]
        public static bool Prefix(ushort parkedID, ref VehicleParked parkedData) {
            uint ownerCitizenId = parkedData.m_ownerCitizen;
            ushort homeId = 0;

            // NON-STOCK CODE START
            if (ownerCitizenId != 0u) {
                homeId = CitizenManager.instance.m_citizens.m_buffer[ownerCitizenId].m_homeBuilding;
            }

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
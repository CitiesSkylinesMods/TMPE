namespace TrafficManager.Patch._VehicleManager {
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(typeof(VehicleManager), "ReleaseVehicle")]
    [UsedImplicitly]
    public static class ReleaseVehiclePatch {
        /// <summary>
        /// Notifies the vehicle state manager about a released vehicle.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static void Prefix(VehicleManager __instance, ushort vehicle) {
            Constants.ManagerFactory.ExtVehicleManager.OnReleaseVehicle(
                vehicle,
                ref __instance.m_vehicles.m_buffer[vehicle]);
        }
    }
}
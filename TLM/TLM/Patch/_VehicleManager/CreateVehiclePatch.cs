namespace TrafficManager.Patch._VehicleManager {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(VehicleManager), "CreateVehicle")]
    [UsedImplicitly]
    public static class CreateVehiclePatch {
        /// <summary>
        /// Restricts the set of vehicle types allowed to spawn before hitting the vehicle limit.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static bool Prefix(VehicleManager __instance, ref ushort vehicle, VehicleInfo info) {
            if (__instance.m_vehicleCount > __instance.m_vehicles.m_buffer.Length - 5)
            {
                // prioritize service vehicles and public transport when hitting the vehicle limit
                ItemClass.Service service = info.GetService();
                if (service == ItemClass.Service.Residential ||
                    service == ItemClass.Service.Industrial ||
                    service == ItemClass.Service.Commercial ||
                    service == ItemClass.Service.Office) {
                    vehicle = 0;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Notifies the vehicle state manager about a created vehicle.
        /// </summary>
        [HarmonyPostfix]
        [UsedImplicitly]
        public static void Postfix(VehicleManager __instance, bool __result, ref ushort vehicle) {
            if (__result) {
                Constants.ManagerFactory.ExtVehicleManager.OnCreateVehicle(
                    vehicle,
                    ref vehicle.ToVehicle()); // NON-STOCK CODE
            }
        }
    }
}
using HarmonyLib;
using TrafficManager.Manager.Impl;
namespace TrafficManager.Patch._Vehicle {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.Spawn))]
    public static class SpawnPatch {
        /// <summary>
        /// Notifies the vehicle state manager about a spawned vehicle.
        /// </summary>
        public static void Postfix(ushort vehicleID, ref Vehicle __instance ) {
            ExtVehicleManager.Instance.OnSpawnVehicle(vehicleID, ref __instance);
        }
    }
}
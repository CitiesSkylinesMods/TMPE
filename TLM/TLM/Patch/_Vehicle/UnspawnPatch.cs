using HarmonyLib;
using TrafficManager.Manager.Impl;
namespace TrafficManager.Patch._Vehicle {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.Unspawn))]
    public static class UnspawnPatch {
        /// <summary>
        /// Notifies the vehicle state manager about an Unspawned vehicle.
        /// </summary>
        public static void Prefix(ushort vehicleID, ref Vehicle __instance) {
            ExtVehicleManager.Instance.OnDespawnVehicle(vehicleID, ref __instance);
        }
    }
}
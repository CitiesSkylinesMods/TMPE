namespace TrafficManager.Patch._VehicleAI._BusAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._RoadBaseAI;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<BusAI>();

        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [HarmonyPrefix]
        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Bus;
            CreatePathPatch.VehicleID = vehicleID;

            // override vanilla values.
            CreatePathPatch.StablePath = true;
        }
    }
}

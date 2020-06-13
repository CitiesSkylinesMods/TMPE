namespace TrafficManager.Patch._VehicleAI._CargoTruckAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using System.Reflection.Emit;
    using System.Collections.Generic;


    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<CargoTruckAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = ExtVehicleType.CargoVehicle; // TODO [issue #] why not cargo truck? why not store the return value from method above?
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }

        [UsedImplicitly]
        [HarmonyPriority(Priority.Low)] // so that if this code is redundant, it would result in assertion failure.
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            return StartPathFindCommons.ReplaceMaxPosTranspiler(instructions);
        }
    }
}

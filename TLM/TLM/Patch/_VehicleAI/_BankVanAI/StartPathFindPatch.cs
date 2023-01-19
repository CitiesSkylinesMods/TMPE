namespace TrafficManager.Patch._VehicleAI._BankVanAI {
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Patch._PathManager;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<BankVanAI>();

        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.ExtVehicleType = ExtVehicleType.Service;
            CreatePathPatch.VehicleID = vehicleID;
        }

        [UsedImplicitly]
        [HarmonyPriority(Priority.Low)] // so that if this code is redundant, it would result in warning log.
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            return StartPathFindCommons.ReplaceMaxPosTranspiler(instructions);
        }
    }
}

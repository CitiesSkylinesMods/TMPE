namespace TrafficManager.Patch._VehicleAI._PostVanAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using System.Collections.Generic;
    using System.Reflection.Emit;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<PostVanAI>();

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

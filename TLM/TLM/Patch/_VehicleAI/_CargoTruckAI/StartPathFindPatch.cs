namespace TrafficManager.Patch._VehicleAI._CargoTruckAI{
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.Patch._PathManager;
    using System.Reflection;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using ColossalFramework;
    using System.Reflection.Emit;
    using System.Collections.Generic;
    using CSUtil.Commons;
    using static TrafficManager.Util.TranspilerUtil;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.Util;

    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<CargoTruckAI>();

        // TODO [issue #] Should this be done in TMPE?
        // see https://github.com/CitiesSkylinesMods/TMPE/issues/895#issuecomment-643111138
        const float vanilaMaxPos_ = 4800f; // 25 tiles compatible value
        const float newMaxPos_ = 8000f; // 81 tiles compatible value.

        /// <summary>
        /// Notifies the extended citizen manager about a citizen that arrived at their destination if the Parking AI is active.
        /// </summary>
        [UsedImplicitly]
        public static void Prefix(ushort vehicleID, ref Vehicle vehicleData) {
            ExtVehicleManager.Instance.OnStartPathFind(vehicleID, ref vehicleData, null);
            CreatePathPatch.ExtVehicleType = ExtVehicleType.CargoVehicle; // TODO [issue #] why not cargo truck? why not store the return value from method above?
            CreatePathPatch.ExtPathType = ExtPathType.None;
            CreatePathPatch.VehicleID = vehicleID;
        }

        [UsedImplicitly]
        [HarmonyPriority(Priority.Low)] // so that if this code is redundant, then the assertion bellow fails.
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            // TODO replace startPos with endpod if krzychu said it is necessary.
            int n = 0;
            foreach (var instruction in instructions) {
                bool is_ldfld_minCornerOffset =
                    instruction.opcode == OpCodes.Ldc_R4 && ((float)instruction.operand) == vanilaMaxPos_;
                if (is_ldfld_minCornerOffset) {
                    n++;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, operand: newMaxPos_);
                }else {
                    yield return instruction;
                }
            }

            // if another mod has already made such replacement then this assertion fails and we would know :)
            Shortcuts.Assert(n > 0, "n>0"); 
            Log._Debug($"CargoTruckAI.StartPathFindPatch.Transpiler() successfully " +
                $"replaced {n} instances of ldc.r4 {vanilaMaxPos_} with {newMaxPos_}");
            yield break;
        }
    }
}

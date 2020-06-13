namespace TrafficManager.Patch._VehicleAI {
    using System.Reflection;
    using TrafficManager.Util;
    using UnityEngine;
    using HarmonyLib;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using CSUtil.Commons;

    public static class StartPathFindCommons {
        // protected override bool StartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget)        delegate bool TargetDelegate(out uint unit, ref Randomizer randomizer, uint buildIndex,
        delegate bool TargetDelegate(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget);
        public static MethodBase TargetMethod<T>() {
            return TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(T), "StartPathFind");
        }

        // TODO [issue #] Should this be done in TMPE?
        // see https://github.com/CitiesSkylinesMods/TMPE/issues/895#issuecomment-643111138
        const float vanilaMaxPos_ = 4800f; // 25 tiles compatible value
        const float newMaxPos_ = 8000f; // 81 tiles compatible value.

        public static IEnumerable<CodeInstruction> ReplaceMaxPosTranspiler(IEnumerable<CodeInstruction> instructions) {
            int n = 0;
            foreach (var instruction in instructions) {
                bool is_ldfld_minCornerOffset =
                    instruction.opcode == OpCodes.Ldc_R4 && ((float)instruction.operand) == vanilaMaxPos_;
                if (is_ldfld_minCornerOffset) {
                    n++;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, operand: newMaxPos_);
                } else {
                    yield return instruction;
                }
            }

            // if another mod has already made such replacement then this assertion fails and we would know :)
            Shortcuts.Assert(n > 0, "n>0");
            Log._Debug($"StartPathFindCommons.ReplaceMaxPosTranspiler() successfully " +
                $"replaced {n} instances of ldc.r4 {vanilaMaxPos_} with {newMaxPos_}");
            yield break;
        }
    }
}

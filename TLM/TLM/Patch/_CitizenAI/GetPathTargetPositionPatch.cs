namespace TrafficManager.Patch._CitizenAI {
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using HarmonyLib;
    using TrafficManager.Util.Extensions;

    [HarmonyPatch(typeof(CitizenAI), "GetPathTargetPosition")]
    public static class GetPathTargetPositionPatch {
        [HarmonyPriority(Priority.Low)]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var mGetGap = AccessTools.Method(typeof(GetPathTargetPositionPatch), nameof(GetGap));
            foreach (var instruction in instructions) {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float value && value == 128) {
                    // we don't replace LDC_R4 64 but rather pass it to GetGap. This way it would be more compatible with other mods in future should they try to modify the same line.
                    yield return new CodeInstruction(OpCodes.Ldloc, 4); // path position
                    yield return new CodeInstruction(OpCodes.Call, mGetGap);
                }
            }
        }

        private static float GetGap(float gap /*128f*/, PathUnit.Position pathPos) {
            NetInfo info = pathPos.m_segment.ToSegment().Info;
            return Max(gap, info.m_minCornerOffset, info.m_halfWidth * 2);
        }

        private static float Max(float a, float b, float c) {
            float max = a > b ? a : b;
            return max > c ? max : c;
        }
    }
}

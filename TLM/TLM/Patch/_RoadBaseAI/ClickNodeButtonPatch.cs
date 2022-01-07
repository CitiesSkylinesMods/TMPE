namespace TrafficManager.Patch._RoadBaseAI {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using TrafficManager.UI;
    using TrafficManager.UI.SubTools;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch(typeof(RoadBaseAI), nameof(RoadBaseAI.ClickNodeButton))]
    public class ClickNodeButtonPatch {

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {

            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            int minus1OpIndex = TranspilerUtil.SearchInstruction(codes, new CodeInstruction(OpCodes.Ldc_I4_M1), 0);
            //check index and previous instruction
            if(minus1OpIndex != -1 && codes[minus1OpIndex + 1].opcode.Equals(OpCodes.Bne_Un)) {
                int ldArg0Index = TranspilerUtil.SearchInstruction(codes, new CodeInstruction(OpCodes.Ldarg_0), minus1OpIndex);
                //check index and previous instruction
                if(ldArg0Index != -1 && codes[ldArg0Index - 1].opcode.Equals(OpCodes.Stfld)) {
                    int targetIndex = minus1OpIndex + 2;//move index to first item to remove
                    // replace all instruction between targetIndex and Ldarg_0
                    codes.RemoveRange(targetIndex, ldArg0Index - targetIndex);

                    var newInstructions = new[] {
                        new CodeInstruction(OpCodes.Ldarg_1), // load node id
                        new CodeInstruction(OpCodes.Call, typeof(ClickNodeButtonPatch).GetMethod(nameof(ClickNodeButtonPatch.ToggleTrafficLight))),
                    };
                    codes.InsertRange(targetIndex, newInstructions);
                } else {
                    throw new Exception("Could not found Ldarg_0 Instruction or instruction was already patched!");
                }
            } else {
                throw new Exception("Could not found Ldc_I4_M1 Instruction or previous instruction was already patched!");
            }

            return codes;
        }

        public static void ToggleTrafficLight(ushort nodeId) {
            ToggleTrafficLightsTool toggleTool =
                ModUI.GetTrafficManagerTool()?.GetSubTool(ToolMode.ToggleTrafficLight) as ToggleTrafficLightsTool;
            toggleTool?.ToggleTrafficLight(nodeId, ref nodeId.ToNode(), false);
        }
    }
    /*
     *  Replace
        -------------------------
        data.m_flags ^= NetNode.Flags.TrafficLights;
        data.m_flags |= NetNode.Flags.CustomTrafficLights;
        -------------------------

        IL_0039: ldarg.2
        IL_003A: dup
        IL_003B: ldfld     valuetype NetNode/Flags NetNode::m_flags
        IL_0040: ldc.i4    8388608
        IL_0045: xor
        IL_0046: stfld     valuetype NetNode/Flags NetNode::m_flags
        IL_004B: ldarg.2
        IL_004C: dup
        IL_004D: ldfld     valuetype NetNode/Flags NetNode::m_flags
        IL_0052: ldc.i4    -2147483648
        IL_0057: or
        IL_0058: stfld     valuetype NetNode/Flags NetNode::m_flags

        With
        -------------------------
        Replacement(nodeId);
        -------------------------

        IL_002e: ldarg.1      // nodeId
        IL_002f: call         void ClickNodeButtonPatch::ToggleTrafficLight(bool)
     */
}

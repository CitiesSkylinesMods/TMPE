namespace TrafficManager.Patch._RoadBaseAI {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UI;
    using UI.SubTools;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch(typeof(RoadBaseAI), nameof(RoadBaseAI.ClickNodeButton))]
    public class ClickNodeButtonPatch {

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {

            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            int minus1OpIndex = TranspilerUtil.SearchInstruction(codes, new CodeInstruction(OpCodes.Ldc_I4_M1), 0);
            //check index and previous instruction
            if (minus1OpIndex != -1 && codes[minus1OpIndex + 1].opcode.Equals(OpCodes.Bne_Un)) {
                int ldArg0Index = TranspilerUtil.SearchInstruction(codes, new CodeInstruction(OpCodes.Ldarg_0), minus1OpIndex);
                //check index and previous instruction
                if (ldArg0Index != -1 && codes[ldArg0Index -1].opcode.Equals(OpCodes.Stfld)) {
                    int targetIndex = minus1OpIndex + 2;//move index to first item to remove
                    // replace all instruction between targetIndex and Ldarg_0
                    codes.RemoveRange(targetIndex, ldArg0Index - targetIndex);
                    codes.InsertRange(targetIndex, GetReplacementInstructions());
                } else {
                    throw new Exception("Could not found Ldarg_0 Instruction or instruction was already patched!");
                }
            } else {
                throw new Exception("Could not found Ldc_I4_M1 Instruction or previous instruction was already patched!");
            }

            return codes;
        }

        private static List<CodeInstruction> GetReplacementInstructions() {
            MethodBase getTMTool = typeof(ModUI).GetMethod(nameof(ModUI.GetTrafficManagerTool));
            MethodBase getSubTool = typeof(TrafficManagerTool).GetMethod(nameof(TrafficManagerTool.GetSubTool));
            Type toggleTLTool = typeof(ToggleTrafficLightsTool);
            MethodBase toggleTL = toggleTLTool.GetMethod(nameof(ToggleTrafficLightsTool.ToggleTrafficLight));

            return new List<CodeInstruction> {
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Call, getTMTool),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Callvirt, getSubTool),
                new CodeInstruction(OpCodes.Castclass, toggleTLTool),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Callvirt, toggleTL)
            };
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
        ToggleTrafficLightsTool toggleTool = (ToggleTrafficLightsTool)ModUI.GetTrafficManagerTool(true)
                                                                           .GetSubTool(ToolMode.ToggleTrafficLight);
        toggleTool.ToggleTrafficLight(nodeId, ref data, false);
        -------------------------

        IL_002e: ldc.i4.1
        IL_002f: call         class TrafficManager.UI.TrafficManagerTool TrafficManager.UI.ModUI::GetTrafficManagerTool(bool)
        IL_0034: ldc.i4.1
        IL_0035: callvirt     instance class TrafficManager.UI.LegacySubTool TrafficManager.UI.TrafficManagerTool::GetSubTool(valuetype TrafficManager.UI.ToolMode)
        IL_003a: castclass    TrafficManager.UI.SubTools.ToggleTrafficLightsTool

        IL_003f: ldarg.1      // nodeId
        IL_0040: ldarg.2      // data
        IL_0041: ldc.i4.0
        IL_0042: callvirt     instance void TrafficManager.UI.SubTools.ToggleTrafficLightsTool::ToggleTrafficLight(unsigned int16, valuetype ['Assembly-CSharp']NetNode&, bool)

     */
}
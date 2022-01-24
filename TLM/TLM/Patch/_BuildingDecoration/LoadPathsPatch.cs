namespace TrafficManager.Patches._BuildingDecoration {
    using System.Collections.Generic;
    using HarmonyLib;
    using TrafficManager.Util;
    using System;
    using System.Reflection.Emit;
    using JetBrains.Annotations;

    //public static void LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
    [HarmonyPatch(typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths))]
    [UsedImplicitly]
    public class LoadPathsPatch {
        ///<summary>Called after intersection is built</summary>
        internal static void AfterIntersectionBuilt(BuildingInfo info) {
            if (!Shortcuts.InSimulationThread())
                return; // only rendering

            var newSegmentIds = NetManager.instance.m_tempSegmentBuffer.ToArray();
            PlaceIntersectionUtil.ApplyTrafficRules(info, newSegmentIds);
        }

        // code from: https://github.com/Strdate/SmartIntersections/blob/master/SmartIntersections/Patch/LoadPathsPatch.cs
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            var fTempSegmentBuffer = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempSegmentBuffer))
                ?? throw new Exception("Could not find NetManager.m_tempSegmentBuffer");
            var mSize = AccessTools.DeclaredField(fTempSegmentBuffer.FieldType, nameof(FastList<ushort>.m_size))
                ?? throw new Exception("Could not find m_tempSegmentBuffer.m_size");
            var mAfterIntersectionBuilt = AccessTools.DeclaredMethod(
                typeof(LoadPathsPatch), nameof(AfterIntersectionBuilt))
                ?? throw new Exception("Could not find AfterIntersectionBuilt()");

            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);

            bool predicate(int i) =>
                codes[i].opcode == OpCodes.Blt &&
                codes[i - 1].opcode == OpCodes.Ldfld && codes[i - 1].operand == mSize &&
                codes[i - 2].opcode == OpCodes.Ldfld && codes[i - 2].operand == fTempSegmentBuffer;
            int index = TranspilerUtil.SearchGeneric(codes, predicate, index: 0, counter: 1);
            index += 1; // index to insert instructions. (end of the loop)

            var newInstructions = new[] {
                new CodeInstruction(OpCodes.Ldarg_0), // load argument info
                new CodeInstruction(OpCodes.Call, mAfterIntersectionBuilt),
            };

            TranspilerUtil.InsertInstructions(codes, newInstructions, index);
            return codes;
        }
    }
}

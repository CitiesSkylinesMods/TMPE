namespace TrafficManager.Patches._BuildingDecoration {
    using System.Collections.Generic;
    using HarmonyLib;
    using TrafficManager.Util;
    using System;
    using System.Reflection.Emit;

    //public static void LoadPaths(BuildingInfo info, ushort buildingID, ref Building data, float elevation)
    [HarmonyPatch(typeof(BuildingDecoration), nameof(BuildingDecoration.LoadPaths))]
    public class LoadPathsPatch {
        ///<summary>Called after intersection is built</summary> 
        internal static void AfterIntersectionBuilt(BuildingInfo info) {
            if (!Shortcuts.InSimulationThread())
                return; // only rendering

            var newSegmentIds = NetManager.instance.m_tempSegmentBuffer.ToArray();
            PlaceIntersectionUtil.ApplyTrafficRules(info, newSegmentIds);
        }

        // code from: https://github.com/Strdate/SmartIntersections/blob/master/SmartIntersections/Patch/LoadPathsPatch.cs
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            var fTempNodeBuffer = AccessTools.DeclaredField(typeof(NetManager), nameof(NetManager.m_tempNodeBuffer))
                ?? throw new Exception("cound not find NetManager.m_tempNodeBuffer");
            var mClear = AccessTools.DeclaredMethod(fTempNodeBuffer.FieldType, nameof(FastList<ushort>.Clear))
                ?? throw new Exception("cound not find m_tempNodeBuffer.Clear");
            var mAfterIntersectionBuilt = AccessTools.DeclaredMethod(
                typeof(LoadPathsPatch), nameof(AfterIntersectionBuilt))
                ?? throw new Exception("cound not find AfterIntersectionBuilt()");

            List<CodeInstruction> codes = TranspilerUtil.ToCodeList(instructions);
            bool comp(int i) =>
                codes[i].opcode == OpCodes.Ldfld && codes[i].operand == fTempNodeBuffer &&
                codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 1].operand == mClear;
            int index = TranspilerUtil.SearchGeneric(codes, comp, index: 0, counter: 2);
            index -= 1; // index to insert instructions.

            var newInstructions = new[] {
                new CodeInstruction(OpCodes.Ldarg_0), // load argument info
                new CodeInstruction(OpCodes.Call, mAfterIntersectionBuilt),
            };

            TranspilerUtil.InsertInstructions(codes, newInstructions, index);
            return codes;
        }
    }
}

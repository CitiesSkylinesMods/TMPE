namespace TrafficManager.Patch._VehicleAI._TramBaseAI {
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CalculateSegmentPositionPatch {
        private delegate void CalculatePositionDelegate(ushort vehicleID,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position position,
                                                        uint laneID,
                                                        byte offset,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(typeof(TramBaseAI), "CalculateSegmentPosition");

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            return VehicleAICommons.TranspileCalculateSegmentPosition(instructions, noSlowDriving: true, extendedDefinition: false);
        }
    }
}
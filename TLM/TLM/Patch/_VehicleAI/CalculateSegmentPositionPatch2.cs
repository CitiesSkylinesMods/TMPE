namespace TrafficManager.Patch._VehicleAI {
    using System.Collections.Generic;
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class CalculateSegmentPositionPatch2 {
        private delegate void CalculatePositionDelegate(ushort vehicleId,
                                                        ref Vehicle vehicleData,
                                                        PathUnit.Position nextPosition,
                                                        PathUnit.Position position,
                                                        uint laneId,
                                                        byte offset,
                                                        PathUnit.Position prevPos,
                                                        uint prevLaneId,
                                                        byte prevOffset,
                                                        int index,
                                                        out Vector3 pos,
                                                        out Vector3 dir,
                                                        out float maxSpeed);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<CalculatePositionDelegate>(typeof(VehicleAI), "CalculateSegmentPosition");

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            return VehicleAICommons.TranspileCalculateSegmentPosition(instructions, noSlowDriving: false, extendedDefinition: true);
        }
    }
}
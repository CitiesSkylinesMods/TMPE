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
    public static class SimulationStepPatch {

        private delegate void TargetDelegate(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(TramBaseAI), nameof(TramBaseAI.SimulationStep));

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler( ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            return PatchCommons.TranspileTramTrainSimulationStep(il, instructions);
        }

    }
}
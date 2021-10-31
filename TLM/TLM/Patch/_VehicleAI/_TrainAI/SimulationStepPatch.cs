namespace TrafficManager.Patch._VehicleAI._TrainAI {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class SimulationStepPatch {

        private delegate void TargetDelegate(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(TrainAI), nameof(TrainAI.SimulationStep));

        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(ILGenerator il, IEnumerable<CodeInstruction> instructions) {
            return PatchCommons.TranspileTramTrainSimulationStep(il, instructions);
        }
    }
}
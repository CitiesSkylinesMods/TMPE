namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System.Collections.Generic;
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public static class SimulationStepPatch {

        private delegate void TargetDelegate(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(PassengerCarAI), nameof(PassengerCarAI.SimulationStep));

        /// <summary>
        /// Adds MayDespawn call method for disable despawning feature
        /// </summary>
        /// <param name="instructions">Instructions of method</param>
        /// <returns></returns>
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            return PatchCommons.ReplaceSimulationStepIsCongestedCheck(instructions);
        }
    }
}
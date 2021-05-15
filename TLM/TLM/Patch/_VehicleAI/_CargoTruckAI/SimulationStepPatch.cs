namespace TrafficManager.Patch._VehicleAI._CargoTruckAI {
    using System.Collections.Generic;
    using HarmonyLib;
    using JetBrains.Annotations;

    [HarmonyPatch(typeof(CargoTruckAI), nameof(CargoTruckAI.SimulationStep))]
    public static class SimulationStepPatch {

        /// <summary>
        /// Adds MayDespawn call for disable despawning feature
        /// </summary>
        /// <param name="instructions">Instructions of method</param>
        /// <returns></returns>
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            return PatchCommons.ReplaceSimulationStepIsCongestedCheck(instructions);
        }
    }
}
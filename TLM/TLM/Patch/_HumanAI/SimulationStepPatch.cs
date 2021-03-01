namespace TrafficManager.Patch._HumanAI {
    using System.Reflection;
    using HarmonyLib;
    using JetBrains.Annotations;

    [UsedImplicitly]
    // [HarmonyPatch]
    public class SimulationStepPatch {

        public static MethodBase TargetMethod() => null;

        public static bool Prefix() {
            //todo
            return false;
        }
    }
}
namespace TrafficManager.Patch._CommonBuildingAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using System.Reflection;

    [HarmonyPatch]
    [UsedImplicitly]
    public class SimulationStepPatch
    {
        [UsedImplicitly]
        public static MethodBase TargetMethod()
        {
            return HarmonyLib.AccessTools.DeclaredMethod(
                typeof(CommonBuildingAI),
                "SimulationStep",
                new[] { typeof(ushort), typeof(Building).MakeByRefType() }) ??
                throw new System.Exception("_CommonBuildingAI.SimulationStepPatch failed to find TargetMethod");
        }

        /// <summary>
        /// Decreases parking space and public transport demand before each simulation step if the Parking AI is active.
        /// </summary>
        [UsedImplicitly]
        public static void Prefix(ushort buildingID, ref Building data)
        {
            Constants.ManagerFactory.ExtBuildingManager.OnBeforeSimulationStep(buildingID, ref data);
        }
    }
}
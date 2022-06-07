namespace TrafficManager.Patch._VehicleAI {
    using HarmonyLib;
    using JetBrains.Annotations;
    using System.Reflection;

    [HarmonyPatch]
    class InvalidPathPatch {
        [UsedImplicitly]
        static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(VehicleAI), "InvalidPath");

        [UsedImplicitly]
        static void Prefix(ref Vehicle vehicleData) {
            if (vehicleData.Info?.m_vehicleAI is TrainAI or TramBaseAI) {
                vehicleData.m_targetPos2.w = 0;
                vehicleData.m_targetPos0 = vehicleData.m_targetPos2;
                vehicleData.m_targetPos1 = vehicleData.m_targetPos2;
                vehicleData.m_targetPos3 = vehicleData.m_targetPos2;
            }
        }
    }
}

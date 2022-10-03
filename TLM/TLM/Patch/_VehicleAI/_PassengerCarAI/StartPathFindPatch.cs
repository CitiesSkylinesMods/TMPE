namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System.Reflection;
    using Connection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using UnityEngine;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<PassengerCarAI>();

        private static GetDriverInstanceDelegate GetDriverInstance;

        [UsedImplicitly]
        public static void Prepare() {
            GetDriverInstance = GameConnectionManager.Instance.PassengerCarAIConnection.GetDriverInstance;
        }

        [UsedImplicitly]
        public static bool Prefix(ref bool __result,
                                  VehicleInfo ___m_info,
                                  PassengerCarAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  Vector3 startPos,
                                  Vector3 endPos,
                                  bool startBothWays,
                                  bool endBothWays,
                                  bool undergroundTarget) {
            ushort driverInstanceId = GetDriverInstance(__instance, vehicleID, ref vehicleData);
            __result = driverInstanceId != 0
                   && Constants.ManagerFactory.VehicleBehaviorManager.StartPassengerCarPathFind(
                       vehicleID,
                       ref vehicleData,
                       ___m_info,
                       driverInstanceId,
                       ref CitizenManager.instance.m_instances.m_buffer[driverInstanceId],
                       ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId],
                       startPos,
                       endPos,
                       startBothWays,
                       endBothWays,
                       undergroundTarget,
                       false,
                       false,
                       false);

            return false;
        }
    }
}
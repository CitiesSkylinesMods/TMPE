namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System.Reflection;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using UnityEngine;

    [UsedImplicitly]
    [HarmonyPatch]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.TargetMethod<PassengerCarAI>();

        [HarmonyDelegate(typeof(PassengerCarAI), "GetDriverInstance", MethodDispatchType.Call)]
        public delegate ushort GetDriverInstance(ushort vehicleID, ref Vehicle vehicleData);

        [UsedImplicitly]
        public static bool Prefix(ref bool __result,
                                  VehicleInfo ___m_info,
                                  GetDriverInstance getDriverInstance,
                                  //Harmony magic END
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  Vector3 startPos,
                                  Vector3 endPos,
                                  bool startBothWays,
                                  bool endBothWays,
                                  bool undergroundTarget) {
            ushort driverInstanceId = getDriverInstance(vehicleID, ref vehicleData);
            __result = driverInstanceId != 0
                   && Constants.ManagerFactory.VehicleBehaviorManager.StartPassengerCarPathFind(
                       vehicleID,
                       ref vehicleData,
                       ___m_info,
                       driverInstanceId,
                       ref Singleton<CitizenManager>.instance.m_instances.m_buffer[driverInstanceId],
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
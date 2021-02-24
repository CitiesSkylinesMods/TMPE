namespace TrafficManager.Patch._CitizenAI {
    using System.Reflection;
    using _VehicleAI;
    using API.Manager;
    using HarmonyLib;
    using JetBrains.Annotations;
    using UnityEngine;

    [HarmonyPatch]
    [UsedImplicitly]
    public class StartPathFindPatch {

        [UsedImplicitly]
        public static MethodBase TargetMethod() => StartPathFindCommons.GetCitizenAITargetMethod();

        [UsedImplicitly]
        public static bool Prefix(ref bool __result,
                                  ushort instanceID,
                                  ref CitizenInstance citizenData,
                                  Vector3 startPos,
                                  Vector3 endPos,
                                  VehicleInfo vehicleInfo,
                                  bool enableTransport,
                                  bool ignoreCost) {

            IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtCitizenManager extCitizenManager = Constants.ManagerFactory.ExtCitizenManager;
            __result = extCitizenInstanceManager.StartPathFind(
                instanceID,
                ref citizenData,
                ref extCitizenInstanceManager.ExtInstances[instanceID],
                ref extCitizenManager.ExtCitizens[citizenData.m_citizen],
                startPos,
                endPos,
                vehicleInfo,
                enableTransport,
                ignoreCost);

            return false;
        }
    }
}
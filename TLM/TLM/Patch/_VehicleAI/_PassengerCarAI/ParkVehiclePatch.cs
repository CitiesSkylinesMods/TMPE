namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch(typeof(PassengerCarAI), "ParkVehicle")]
    public static class ParkVehiclePatch {

        [UsedImplicitly]
        public static bool Prefix(ref bool __result,
                                  // Harmony magic END
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  PathUnit.Position pathPos,
                                  uint nextPath,
                                  int nextPositionIndex,
                                  out byte segmentOffset) {
             CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            uint maxUnitCount = citizenManager.m_units.m_size;

            uint driverCitizenId = 0u;
            ushort driverCitizenInstanceId = 0;
            ushort targetBuildingId = 0; // NON-STOCK CODE
            uint curCitizenUnitId = vehicleData.m_citizenUnits;
            int numIterations = 0;

            while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
                ref CitizenUnit currentCitizenUnit = ref curCitizenUnitId.ToCitizenUnit();
                for (int i = 0; i < 5; i++) {
                    uint citizenId = currentCitizenUnit.GetCitizen(i);
                    if (citizenId == 0u) {
                        continue;
                    }

                    driverCitizenInstanceId = citizenId.ToCitizen().m_instance;
                    if (driverCitizenInstanceId == 0) {
                        continue;
                    }

                    // NON-STOCK CODE START
                    ref CitizenInstance driverCitizenInstance = ref ((ushort)driverCitizenInstanceId).ToCitizenInstance();
                    driverCitizenId = driverCitizenInstance.m_citizen;
                    targetBuildingId = driverCitizenInstance.m_targetBuilding;
                    // NON-STOCK CODE END

                    break;
                }

                curCitizenUnitId = currentCitizenUnit.m_nextUnit;
                if (++numIterations > maxUnitCount) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                  $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

            __result =  Constants.ManagerFactory.VehicleBehaviorManager.ParkPassengerCar(
                vehicleID,
                ref vehicleData,
                vehicleData.Info,
                driverCitizenId,
                ref driverCitizenId.ToCitizen(),
                driverCitizenInstanceId,
                ref driverCitizenInstanceId.ToCitizenInstance(),
                ref ExtCitizenInstanceManager.Instance.ExtInstances[driverCitizenInstanceId],
                targetBuildingId,
                pathPos,
                nextPath,
                nextPositionIndex,
                out segmentOffset);
            return false;
        }
    }
}
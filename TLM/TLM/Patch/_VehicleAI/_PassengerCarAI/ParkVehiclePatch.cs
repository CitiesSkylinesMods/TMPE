namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;

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

            uint driverCitizenId = 0u;
            ushort driverCitizenInstanceId = 0;
            ushort targetBuildingId = 0; // NON-STOCK CODE
            uint curCitizenUnitId = vehicleData.m_citizenUnits;
            int numIterations = 0;

            while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
                uint nextUnit = citizenManager.m_units.m_buffer[curCitizenUnitId].m_nextUnit;
                for (int i = 0; i < 5; i++) {
                    uint citizenId = citizenManager.m_units.m_buffer[curCitizenUnitId].GetCitizen(i);
                    if (citizenId == 0u) {
                        continue;
                    }

                    driverCitizenInstanceId = citizenManager.m_citizens.m_buffer[citizenId].m_instance;
                    if (driverCitizenInstanceId == 0) {
                        continue;
                    }

                    driverCitizenId = citizenManager.m_instances.m_buffer[driverCitizenInstanceId].m_citizen;

                    // NON-STOCK CODE START
                    targetBuildingId = citizenManager.m_instances.m_buffer[driverCitizenInstanceId].m_targetBuilding;
                    // NON-STOCK CODE END

                    break;
                }

                curCitizenUnitId = nextUnit;
                if (++numIterations > CitizenManager.MAX_UNIT_COUNT) {
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
                ref citizenManager.m_citizens.m_buffer[driverCitizenId],
                driverCitizenInstanceId,
                ref citizenManager.m_instances.m_buffer[driverCitizenInstanceId],
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
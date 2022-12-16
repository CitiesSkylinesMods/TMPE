using TrafficManager.Util.Extensions;

namespace TrafficManager.Patch._CitizenAI._ResidentAI {
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using Connection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class GetVehicleInfoPatch {
        private delegate VehicleInfo TargetDelegate(ushort instanceID,
                                                    ref CitizenInstance citizenData,
                                                    bool forceProbability,
                                                    out VehicleInfo trailer);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(ResidentAI), "GetVehicleInfo");

        private static GetTaxiProbabilityResidentDelegate GetTaxiProbability;
        private static GetBikeProbabilityResidentDelegate GetBikeProbability;
        private static GetCarProbabilityResidentDelegate GetCarProbability;
        private static GetElectricCarProbabilityResidentDelegate GetElectricCarProbability;

        [UsedImplicitly]
        public static void Prepare() {
            GetTaxiProbability = GameConnectionManager.Instance.ResidentAIConnection.GetTaxiProbability;
            GetBikeProbability = GameConnectionManager.Instance.ResidentAIConnection.GetBikeProbability;
            GetCarProbability = GameConnectionManager.Instance.ResidentAIConnection.GetCarProbability;
            GetElectricCarProbability = GameConnectionManager.Instance.ResidentAIConnection.GetElectricCarProbability;
        }

        [UsedImplicitly]
        public static bool Prefix(ResidentAI __instance,
                                   ref VehicleInfo __result,
                                   ushort instanceID,
                                   ref CitizenInstance citizenData,
                                   bool forceProbability,
                                   out VehicleInfo trailer) {
#if DEBUG
            bool citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceID)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == citizenData.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == citizenData.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == citizenData.m_targetBuilding);
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
#else
            var logParkingAi = false;
#endif
            trailer = null;

            if (citizenData.m_citizen == 0u) {
                __result = null;
                return false;
            }

            // NON-STOCK CODE START
            bool forceTaxi = false;
            if (SavedGameOptions.Instance.parkingAI) {
                if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceID]
                                             .pathMode == ExtPathMode.TaxiToTarget) {
                    forceTaxi = true;
                }
            }

            // NON-STOCK CODE END
            Citizen.AgeGroup ageGroup =
                Constants.ManagerFactory.ExtCitizenManager.GetAgeGroup(citizenData.Info.m_agePhase);

            int carProb;
            int bikeProb;
            int taxiProb;

            // NON-STOCK CODE START
            if (forceTaxi) {
                carProb = 0;
                bikeProb = 0;
                taxiProb = 100;
            } else // NON-STOCK CODE END
            if (forceProbability
                || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) !=
                CitizenInstance.Flags.None) {
                carProb = 100;
                bikeProb = 0;
                taxiProb = 0;
            } else {
                carProb = GetCarProbability(__instance, instanceID, ref citizenData, ageGroup);
                bikeProb = GetBikeProbability(__instance, instanceID, ref citizenData, ageGroup);
                taxiProb = GetTaxiProbability(__instance, instanceID, ref citizenData, ageGroup);
            }

            Randomizer randomizer = new Randomizer(citizenData.m_citizen);
            bool useCar = randomizer.Int32(100u) < carProb;
            bool useBike = !useCar && randomizer.Int32(100u) < bikeProb;
            bool useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
            bool useElectricCar = false;

            bool forceElectric = false;
            if (useCar) {
                int electricProb = GetElectricCarProbability(__instance,
                                                             instanceID,
                                                             ref citizenData,
                                                             __instance.m_info.m_agePhase);
                useElectricCar = randomizer.Int32(100u) < electricProb;
                // NON-STOCK CODE START
                forceElectric = useElectricCar && electricProb == 100;
                // NON-STOCK CODE END
            }

            ItemClass.Service service = ItemClass.Service.Residential;
            ItemClass.SubService subService = useElectricCar
                                 ? ItemClass.SubService.ResidentialLowEco
                                 : ItemClass.SubService.ResidentialLow;
            if (useTaxi) {
                service = ItemClass.Service.PublicTransport;
                subService = ItemClass.SubService.PublicTransportTaxi;
            }

            // NON-STOCK CODE START
            VehicleInfo carInfo = null;
            if (SavedGameOptions.Instance.parkingAI && useCar) {
                ref Citizen citizen = ref CitizenManager.instance.m_citizens.m_buffer[citizenData.m_citizen];
                ushort parkedVehicleId = citizen.m_parkedVehicle;

                if (parkedVehicleId != 0) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"CustomResidentAI.CustomGetVehicleInfo({instanceID}): " +
                        $"Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}.");

                    ref VehicleParked parkedVehicle = ref parkedVehicleId.ToParkedVehicle();
                    carInfo = parkedVehicle.Info;
                    if (forceElectric && carInfo.m_class.m_subService != ItemClass.SubService.ResidentialLowEco) {
                        Log._DebugIf(logParkingAi,
                                     () => $"CustomResidentAI.CustomGetVehicleInfo({instanceID}): " +
                                                         $"Force electric! Parked vehicle {parkedVehicleId} is not electric vehicle, swap with electric one.");

                        if (AdvancedParkingManager.SwapParkedVehicleWithElectric(
                                logParkingAi: logParkingAi,
                                citizenId: citizenData.m_citizen,
                                citizen: ref citizen,
                                position: parkedVehicle.m_position,
                                rotation: parkedVehicle.m_rotation,
                                electricVehicleInfo: out VehicleInfo electricVehicleInfo)) {
                            carInfo = electricVehicleInfo;
                        }
                    } else {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"CustomResidentAI.CustomGetVehicleInfo({instanceID}): " +
                                  "Reuse existing vehicle info");
                        carInfo = parkedVehicle.Info;
                    }
                }
            }

            if (carInfo == null && (useCar || useTaxi)) {
                // NON-STOCK CODE END
                carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                    ref randomizer,
                    service,
                    subService,
                    ItemClass.Level.Level1);
            }

            if (useBike) {
                ItemClass.Level ageGroupLvl = ageGroup != Citizen.AgeGroup.Child
                                      ? ItemClass.Level.Level2
                                      : ItemClass.Level.Level1;
                VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                    ref randomizer,
                    ItemClass.Service.Residential,
                    ItemClass.SubService.ResidentialHigh,
                    ageGroupLvl);

                if (bikeInfo != null) {
                    __result = bikeInfo;
                    return false;
                }
            }

            if ((useCar || useTaxi) && carInfo != null) {
                __result = carInfo;
                return false;
            }

            __result = null;
            return false;
        }
    }
}
namespace TrafficManager.Patch._CitizenAI._TouristAI {
    using System.Reflection;
    using API.Traffic.Enums;
    using ColossalFramework.Math;
    using Connection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using Util;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch]
    public class GetVehicleInfoPatch {
        private delegate VehicleInfo TargetDelegate(ushort instanceID,
                                                    ref CitizenInstance citizenData,
                                                    bool forceProbability,
                                                    out VehicleInfo trailer);
        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(TouristAI), "GetVehicleInfo");

        private static GetTaxiProbabilityDelegate GetTaxiProbability;
        private static GetBikeProbabilityDelegate GetBikeProbability;
        private static GetCarProbabilityDelegate GetCarProbability;
        private static GetElectricCarProbabilityDelegate GetElectricCarProbability;
        private static GetCamperProbabilityDelegate GetCamperProbability;

        [UsedImplicitly]
        public static void Prepare() {
            GetTaxiProbability = GameConnectionManager.Instance.TouristAIConnection.GetTaxiProbability;
            GetBikeProbability = GameConnectionManager.Instance.TouristAIConnection.GetBikeProbability;
            GetCarProbability = GameConnectionManager.Instance.TouristAIConnection.GetCarProbability;
            GetElectricCarProbability = GameConnectionManager.Instance.TouristAIConnection.GetElectricCarProbability;
            GetCamperProbability = GameConnectionManager.Instance.TouristAIConnection.GetCamperProbability;
        }

        [UsedImplicitly]
        public static bool Prefix(TouristAI __instance,
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
                if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceID].pathMode ==
                    ExtPathMode.TaxiToTarget) {
                    forceTaxi = true;
                }
            }

            Citizen[] citizensBuf = CitizenManager.instance.m_citizens.m_buffer;
            // NON-STOCK CODE END
            Citizen.Wealth wealthLevel = citizensBuf[citizenData.m_citizen].WealthLevel;
            int carProb;
            int bikeProb;
            int taxiProb;

            // NON-STOCK CODE START
            if (forceTaxi) {
                carProb = 0;
                bikeProb = 0;
                taxiProb = 100;
            } else // NON-STOCK CODE END
            if (forceProbability || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) !=
                CitizenInstance.Flags.None) {
                carProb = 100;
                bikeProb = 0;
                taxiProb = 0;
            } else {
                carProb = GetCarProbability(__instance, citizenData.m_frame1.m_position);
                bikeProb = GetBikeProbability(__instance);
                taxiProb = GetTaxiProbability(__instance, instanceID, ref citizenData);
            }

            Randomizer randomizer = new Randomizer(citizenData.m_citizen);
            bool useCar = randomizer.Int32(100u) < carProb;
            bool useBike = !useCar && randomizer.Int32(100u) < bikeProb;
            bool useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
            bool useCamper = false;
            bool useElectricCar = false;

            if (useCar) {
                int camperProb = GetCamperProbability(__instance, wealthLevel);
                useCamper = randomizer.Int32(100u) < camperProb;

                if (!useCamper) {
                    int electricProb = GetElectricCarProbability(__instance, wealthLevel);
                    useElectricCar = randomizer.Int32(100u) < electricProb;
                }
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
            if (SavedGameOptions.Instance.parkingAI && useCar && !useTaxi) {
                ushort parkedVehicleId = citizensBuf[citizenData.m_citizen].m_parkedVehicle;
                if (parkedVehicleId != 0) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"CustomTouristAI.CustomGetVehicleInfo({instanceID}): " +
                              $"Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. " +
                              $"Reusing vehicle info.");
                    carInfo = parkedVehicleId.ToParkedVehicle().Info;
                }
            }

            if (carInfo == null && (useCar || useTaxi)) {
                // NON-STOCK CODE END
                if (useCamper) {
                    Randomizer randomizer2 = randomizer;
                    carInfo = VehicleManager.instance.GetRandomVehicleInfo(
                        ref randomizer,
                        service,
                        subService,
                        ItemClass.Level.Level2);
                    if (carInfo == null || carInfo.m_vehicleAI is CarTrailerAI) {
                        trailer = carInfo;
                        randomizer = randomizer2;
                        carInfo = VehicleManager.instance.GetRandomVehicleInfo(
                            ref randomizer,
                            service,
                            subService,
                            ItemClass.Level.Level1);
                    }
                } else {
                    carInfo = VehicleManager.instance.GetRandomVehicleInfo(
                        ref randomizer,
                        service,
                        subService,
                        ItemClass.Level.Level1);
                }
            }

            if (useBike) {
                VehicleInfo bikeInfo = VehicleManager.instance.GetRandomVehicleInfo(
                    ref randomizer,
                    ItemClass.Service.Residential,
                    ItemClass.SubService.ResidentialHigh,
                    ItemClass.Level.Level2);
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
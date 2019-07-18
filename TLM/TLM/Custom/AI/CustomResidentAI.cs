namespace TrafficManager.Custom.AI {
    using System.Runtime.CompilerServices;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using State;
    using State.ConfigData;
    using Traffic.Enums;

    [TargetType(typeof(ResidentAI))]
    public class CustomResidentAI : ResidentAI {
        [RedirectMethod]
        [UsedImplicitly]
        public string CustomGetLocalizedStatus(ushort instanceId,
                                               ref CitizenInstance data,
                                               out InstanceID target) {
            var ret = Constants.ManagerFactory.ExtCitizenInstanceManager.GetResidentLocalizedStatus(
                instanceId,
                ref data,
                out var addCustomStatus,
                out target);

            // NON-STOCK CODE START
            if (Options.parkingAI && addCustomStatus) {
                ret = AdvancedParkingManager.Instance.EnrichLocalizedCitizenStatus(
                    ret,
                    ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceId],
                    ref ExtCitizenManager.Instance.ExtCitizens[data.m_citizen]);
            }

            // NON-STOCK CODE END
            return ret;
        }

        [RedirectMethod]
        [UsedImplicitly]
        public VehicleInfo CustomGetVehicleInfo(ushort instanceId,
                                                ref CitizenInstance citizenData,
                                                bool forceCar,
                                                out VehicleInfo trailer) {
#if DEBUG
            var citizenDebug = (DebugSettings.CitizenInstanceId == 0
                            || DebugSettings.CitizenInstanceId == instanceId)
                           && (DebugSettings.CitizenId == 0
                               || DebugSettings.CitizenId == citizenData.m_citizen)
                           && (DebugSettings.SourceBuildingId == 0
                               || DebugSettings.SourceBuildingId == citizenData.m_sourceBuilding)
                           && (DebugSettings.TargetBuildingId == 0
                               || DebugSettings.TargetBuildingId == citizenData.m_targetBuilding);
            var logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
#else
            var logParkingAi = false;
#endif
            trailer = null;

            if (citizenData.m_citizen == 0u) {
                return null;
            }

            // NON-STOCK CODE START
            var forceTaxi = false;
            if (Options.parkingAI) {
                if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceId]
                                             .pathMode == ExtPathMode.TaxiToTarget) {
                    forceTaxi = true;
                }
            }

            // NON-STOCK CODE END
            var ageGroup =
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
            if (forceCar
                || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) !=
                CitizenInstance.Flags.None) {
                carProb = 100;
                bikeProb = 0;
                taxiProb = 0;
            } else {
                carProb = GetCarProbability(instanceId, ref citizenData, ageGroup);
                bikeProb = GetBikeProbability(instanceId, ref citizenData, ageGroup);
                taxiProb = GetTaxiProbability(instanceId, ref citizenData, ageGroup);
            }

            var randomizer = new Randomizer(citizenData.m_citizen);
            var useCar = randomizer.Int32(100u) < carProb;
            var useBike = !useCar && randomizer.Int32(100u) < bikeProb;
            var useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
            var useElectricCar = false;

            if (useCar) {
                var electricProb = GetElectricCarProbability(
                    instanceId,
                    ref citizenData,
                    m_info.m_agePhase);
                useElectricCar = randomizer.Int32(100u) < electricProb;
            }

            var service = ItemClass.Service.Residential;
            var subService = useElectricCar
                                 ? ItemClass.SubService.ResidentialLowEco
                                 : ItemClass.SubService.ResidentialLow;
            if (useTaxi) {
                service = ItemClass.Service.PublicTransport;
                subService = ItemClass.SubService.PublicTransportTaxi;
            }

            // NON-STOCK CODE START
            VehicleInfo carInfo = null;
            if (Options.parkingAI && useCar) {
                var parkedVehicleId = Singleton<CitizenManager>
                                      .instance.m_citizens.m_buffer[citizenData.m_citizen]
                                      .m_parkedVehicle;

                if (parkedVehicleId != 0) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"CustomResidentAI.CustomGetVehicleInfo({instanceId}): " +
                        $"Citizen instance {instanceId} owns a parked vehicle {parkedVehicleId}. " +
                        $"Reusing vehicle info.");
                    carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
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
                var ageGroupLvl = ageGroup != Citizen.AgeGroup.Child
                                      ? ItemClass.Level.Level2
                                      : ItemClass.Level.Level1;
                var bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                    ref randomizer,
                    ItemClass.Service.Residential,
                    ItemClass.SubService.ResidentialHigh,
                    ageGroupLvl);

                if (bikeInfo != null) {
                    return bikeInfo;
                }
            }

            if ((useCar || useTaxi) && carInfo != null) {
                return carInfo;
            }

            return null;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetTaxiProbability(ushort instanceId, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
            Log._DebugOnlyError("CustomResidentAI.GetTaxiProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetBikeProbability(ushort instanceId, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
            Log._DebugOnlyError("CustomResidentAI.GetBikeProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetCarProbability(ushort instanceId, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
            Log._DebugOnlyError("CustomResidentAI.GetCarProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetElectricCarProbability(ushort instanceId,
                                              ref CitizenInstance citizenData,
                                              Citizen.AgePhase agePhase) {
            Log._DebugOnlyError("CustomResidentAI.GetElectricCarProbability called!");
            return 20;
        }
    }
}
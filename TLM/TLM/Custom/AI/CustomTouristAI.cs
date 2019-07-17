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

    [TargetType(typeof(TouristAI))]
    public class CustomTouristAI : TouristAI {
        [RedirectMethod]
        [UsedImplicitly]
        public string CustomGetLocalizedStatus(ushort instanceId, ref CitizenInstance data, out InstanceID target) {
            var ret = Constants.ManagerFactory.ExtCitizenInstanceManager.GetTouristLocalizedStatus(
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
                if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceId].pathMode == ExtPathMode.TaxiToTarget) {
                    forceTaxi = true;
                }
            }

            // NON-STOCK CODE END
            var wealthLevel = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].WealthLevel;
            int carProb;
            int bikeProb;
            int taxiProb;

            // NON-STOCK CODE START
            if (forceTaxi) {
                carProb = 0;
                bikeProb = 0;
                taxiProb = 100;
            } else // NON-STOCK CODE END
            if (forceCar || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) != CitizenInstance.Flags.None) {
                carProb = 100;
                bikeProb = 0;
                taxiProb = 0;
            } else {
                carProb = GetCarProbability();
                bikeProb = GetBikeProbability();
                taxiProb = GetTaxiProbability();
            }

            var randomizer = new Randomizer(citizenData.m_citizen);
            var useCar = randomizer.Int32(100u) < carProb;
            var useBike = !useCar && randomizer.Int32(100u) < bikeProb;
            var useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
            var useCamper = false;
            var useElectricCar = false;

            if (useCar) {
                var camperProb = GetCamperProbability(wealthLevel);
                useCamper = randomizer.Int32(100u) < camperProb;

                if (!useCamper) {
                    var electricProb = GetElectricCarProbability(wealthLevel);
                    useElectricCar = randomizer.Int32(100u) < electricProb;
                }
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
            if (Options.parkingAI && useCar && !useTaxi) {
                var parkedVehicleId = Singleton<CitizenManager>
                                      .instance.m_citizens.m_buffer[citizenData.m_citizen]
                                      .m_parkedVehicle;
                if (parkedVehicleId != 0) {
                    Log._DebugIf(
                        logParkingAi,
                        $"CustomTouristAI.CustomGetVehicleInfo({instanceId}): " +
                        $"Citizen instance {instanceId} owns a parked vehicle {parkedVehicleId}. " +
                        $"Reusing vehicle info.");
                    carInfo = Singleton<VehicleManager>
                              .instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
                }
            }

            if (carInfo == null && (useCar || useTaxi)) {
                // NON-STOCK CODE END
                if (useCamper) {
                    var randomizer2 = randomizer;
                    carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                        ref randomizer,
                        service,
                        subService,
                        ItemClass.Level.Level2);
                    if (carInfo == null || carInfo.m_vehicleAI is CarTrailerAI) {
                        trailer = carInfo;
                        randomizer = randomizer2;
                        carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                            ref randomizer,
                            service,
                            subService,
                            ItemClass.Level.Level1);
                    }
                } else {
                    carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                        ref randomizer,
                        service,
                        subService,
                        ItemClass.Level.Level1);
                }
            }

            if (useBike) {
                var bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                    ref randomizer,
                    ItemClass.Service.Residential,
                    ItemClass.SubService.ResidentialHigh,
                    ItemClass.Level.Level2);
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
        private int GetTaxiProbability() {
            Log._DebugOnlyError("CustomTouristAI.GetTaxiProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetBikeProbability() {
            Log._DebugOnlyError("CustomTouristAI.GetBikeProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetCarProbability() {
            Log._DebugOnlyError("CustomTouristAI.GetCarProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetElectricCarProbability(Citizen.Wealth wealth) {
            Log._DebugOnlyError("CustomTouristAI.GetElectricCarProbability called!");
            return 20;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private int GetCamperProbability(Citizen.Wealth wealth) {
            Log._DebugOnlyError("CustomTouristAI.GetCamperProbability called!");
            return 20;
        }
    }
}
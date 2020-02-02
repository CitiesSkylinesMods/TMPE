namespace TrafficManager.Custom.AI {
    using System.Runtime.CompilerServices;
    using TrafficManager.API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;

    [TargetType(typeof(TouristAI))]
    public class CustomTouristAI : TouristAI {
        [RedirectMethod]
        [UsedImplicitly]
        public string CustomGetLocalizedStatus(ushort instanceId, ref CitizenInstance data, out InstanceID target) {
            string ret = Constants.ManagerFactory.ExtCitizenInstanceManager.GetTouristLocalizedStatus(
                instanceId,
                ref data,
                out bool addCustomStatus,
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
            bool citizenDebug = (DebugSettings.CitizenInstanceId == 0
                                || DebugSettings.CitizenInstanceId == instanceId)
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
                return null;
            }

            // NON-STOCK CODE START
            bool forceTaxi = false;
            if (Options.parkingAI) {
                if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceId].pathMode == ExtPathMode.TaxiToTarget) {
                    forceTaxi = true;
                }
            }

            // NON-STOCK CODE END
            Citizen.Wealth wealthLevel = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].WealthLevel;
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

            Randomizer randomizer = new Randomizer(citizenData.m_citizen);
            bool useCar = randomizer.Int32(100u) < carProb;
            bool useBike = !useCar && randomizer.Int32(100u) < bikeProb;
            bool useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
            bool useCamper = false;
            bool useElectricCar = false;

            if (useCar) {
                int camperProb = GetCamperProbability(wealthLevel);
                useCamper = randomizer.Int32(100u) < camperProb;

                if (!useCamper) {
                    int electricProb = GetElectricCarProbability(wealthLevel);
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
            if (Options.parkingAI && useCar && !useTaxi) {
                ushort parkedVehicleId = Singleton<CitizenManager>
                                      .instance.m_citizens.m_buffer[citizenData.m_citizen]
                                      .m_parkedVehicle;
                if (parkedVehicleId != 0) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"CustomTouristAI.CustomGetVehicleInfo({instanceId}): " +
                        $"Citizen instance {instanceId} owns a parked vehicle {parkedVehicleId}. " +
                        $"Reusing vehicle info.");
                    carInfo = Singleton<VehicleManager>
                              .instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
                }
            }

            if (carInfo == null && (useCar || useTaxi)) {
                // NON-STOCK CODE END
                if (useCamper) {
                    Randomizer randomizer2 = randomizer;
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
                VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
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
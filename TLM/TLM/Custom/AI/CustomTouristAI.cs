using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using TrafficManager.RedirectionFramework.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Enums;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	using State.ConfigData;

	[TargetType(typeof(TouristAI))]
	public class CustomTouristAI : TouristAI {
		[RedirectMethod]
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			bool addCustomStatus = false;
			String ret = Constants.ManagerFactory.ExtCitizenInstanceManager.GetTouristLocalizedStatus(instanceID, ref data, out addCustomStatus, out target);

			// NON-STOCK CODE START
			if (Options.parkingAI && addCustomStatus) {
				ret = AdvancedParkingManager.Instance.EnrichLocalizedCitizenStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[data.m_citizen]);
			}
			// NON-STOCK CODE END

			return ret;
		}

		[RedirectMethod]
		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceCar, out VehicleInfo trailer) {
#if DEBUG
			bool citDebug = (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceID) &&
				(DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenData.m_citizen) &&
				(DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == citizenData.m_sourceBuilding) &&
				(DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == citizenData.m_targetBuilding)
			;
			bool debug = DebugSwitch.BasicParkingAILog.Get() && citDebug;
			bool fineDebug = DebugSwitch.ExtendedParkingAILog.Get() && citDebug;
#endif

			trailer = null;

			if (citizenData.m_citizen == 0u) {
				return null;
			}

			// NON-STOCK CODE START
			bool forceTaxi = false;
			if (Options.parkingAI) {
				if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceID].pathMode == ExtPathMode.TaxiToTarget) {
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
			} else
			// NON-STOCK CODE END
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
				int camperProb = this.GetCamperProbability(wealthLevel);
				useCamper = randomizer.Int32(100u) < camperProb;

				if (!useCamper) {
					int electricProb = GetElectricCarProbability(wealthLevel);
					useElectricCar = randomizer.Int32(100u) < electricProb;
				}
			}

			ItemClass.Service service = ItemClass.Service.Residential;
			ItemClass.SubService subService = useElectricCar ? ItemClass.SubService.ResidentialLowEco : ItemClass.SubService.ResidentialLow;
			if (useTaxi) {
				service = ItemClass.Service.PublicTransport;
				subService = ItemClass.SubService.PublicTransportTaxi;
			}
			// NON-STOCK CODE START
			VehicleInfo carInfo = null;
			if (Options.parkingAI && useCar && !useTaxi) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomTouristAI.CustomGetVehicleInfo({instanceID}): Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
#endif
					carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}

			if (carInfo == null && (useCar || useTaxi)) {
				// NON-STOCK CODE END
				if (useCamper) {
					Randomizer randomizer2 = randomizer;
					carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level2);
					if (carInfo == null || carInfo.m_vehicleAI is CarTrailerAI) {
						trailer = carInfo;
						randomizer = randomizer2;
						carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level1);
					}
				} else {
					carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level1);
				}
			}

			if (useBike) {
				VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialHigh, ItemClass.Level.Level2);
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
		private int GetTaxiProbability() {
			Log.Error("CustomTouristAI.GetTaxiProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetBikeProbability() {
			Log.Error("CustomTouristAI.GetBikeProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCarProbability() {
			Log.Error("CustomTouristAI.GetCarProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetElectricCarProbability(Citizen.Wealth wealth) {
			Log.Error("CustomTouristAI.GetElectricCarProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCamperProbability(Citizen.Wealth wealth) {
			Log.Error("CustomTouristAI.GetCamperProbability called!");
			return 20;
		}
	}
}

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
using TrafficManager.UI;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizen;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	using State.ConfigData;

	[TargetType(typeof(ResidentAI))]
	public class CustomResidentAI : ResidentAI {
		[RedirectMethod]
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			bool addCustomStatus = false;
			String ret = Constants.ManagerFactory.ExtCitizenInstanceManager.GetResidentLocalizedStatus(instanceID, ref data, out addCustomStatus, out target);

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
			bool citDebug = (GlobalConfig.Instance.Debug.CitizenInstanceId == 0 || GlobalConfig.Instance.Debug.CitizenInstanceId == instanceID) &&
				(GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenData.m_citizen) &&
				(GlobalConfig.Instance.Debug.SourceBuildingId == 0 || GlobalConfig.Instance.Debug.SourceBuildingId == citizenData.m_sourceBuilding) &&
				(GlobalConfig.Instance.Debug.TargetBuildingId == 0 || GlobalConfig.Instance.Debug.TargetBuildingId == citizenData.m_targetBuilding)
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

			Citizen.AgeGroup ageGroup = Constants.ManagerFactory.ExtCitizenManager.GetAgeGroup(citizenData.Info.m_agePhase);

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
				carProb = GetCarProbability(instanceID, ref citizenData, ageGroup);
				bikeProb = GetBikeProbability(instanceID, ref citizenData, ageGroup);
				taxiProb = GetTaxiProbability(instanceID, ref citizenData, ageGroup);
			}
			Randomizer randomizer = new Randomizer(citizenData.m_citizen);
			bool useCar = randomizer.Int32(100u) < carProb;
			bool useBike = !useCar && randomizer.Int32(100u) < bikeProb;
			bool useTaxi = !useCar && !useBike && randomizer.Int32(100u) < taxiProb;
			bool useElectricCar = false;
			if (useCar) {
				int electricProb = GetElectricCarProbability(instanceID, ref citizenData, this.m_info.m_agePhase);
				useElectricCar = randomizer.Int32(100u) < electricProb;
			}

			ItemClass.Service service = ItemClass.Service.Residential;
			ItemClass.SubService subService = useElectricCar ? ItemClass.SubService.ResidentialLowEco : ItemClass.SubService.ResidentialLow;
			if (useTaxi) {
				service = ItemClass.Service.PublicTransport;
				subService = ItemClass.SubService.PublicTransportTaxi;
			}
			// NON-STOCK CODE START
			VehicleInfo carInfo = null;
			if (Options.parkingAI && useCar) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
#if DEBUG
					if (debug)
						Log._Debug($"CustomResidentAI.CustomGetVehicleInfo({instanceID}): Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
#endif
					carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}

			if (carInfo == null && (useCar || useTaxi)) {
				// NON-STOCK CODE END
				carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level1);
			}

			if (useBike) {
				VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialHigh, (ageGroup != Citizen.AgeGroup.Child) ? ItemClass.Level.Level2 : ItemClass.Level.Level1);
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
		private int GetTaxiProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetTaxiProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetBikeProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetBikeProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCarProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetCarProbability called!");
			return 20;
		}

		[RedirectReverse]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetElectricCarProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgePhase agePhase) {
			Log.Error("CustomResidentAI.GetElectricCarProbability called!");
			return 20;
		}
	}
}

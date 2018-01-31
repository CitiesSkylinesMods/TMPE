using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSUtil.Commons;
using CSUtil.Commons.Benchmark;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.UI;
using UnityEngine;
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	public class CustomResidentAI : ResidentAI {
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			if ((data.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}
			CitizenManager citMan = Singleton<CitizenManager>.instance;
			uint citizenId = data.m_citizen;
			bool isStudent = false;
			ushort homeId = 0;
			ushort workId = 0;
			ushort vehicleId = 0;
			if (citizenId != 0u) {
				homeId = citMan.m_citizens.m_buffer[citizenId].m_homeBuilding;
				workId = citMan.m_citizens.m_buffer[citizenId].m_workBuilding;
				vehicleId = citMan.m_citizens.m_buffer[citizenId].m_vehicle;
				isStudent = ((citMan.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Student) != Citizen.Flags.None);
			}
			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}
			bool isOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool hangsAround = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;
			String ret = "";
			if (vehicleId != 0) {
				VehicleManager instance2 = Singleton<VehicleManager>.instance;
				VehicleInfo info = instance2.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (info.m_class.m_service == ItemClass.Service.Residential && info.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (info.m_vehicleAI.GetOwnerID(vehicleId, ref instance2.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizenId) {
						if (isOutsideConnection) {
							target = InstanceID.Empty;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}
						
						if (targetBuilding == homeId) {
							target = InstanceID.Empty;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_HOME");
						} else if (targetBuilding == workId) {
							target = InstanceID.Empty;
							return Locale.Get((!isStudent) ? "CITIZEN_STATUS_DRIVINGTO_WORK" : "CITIZEN_STATUS_DRIVINGTO_SCHOOL");
						} else {
							target = InstanceID.Empty;
							target.Building = targetBuilding;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
						}
					}
				} else if (info.m_class.m_service == ItemClass.Service.PublicTransport || info.m_class.m_service == ItemClass.Service.Disaster) {
					if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != CitizenInstance.Flags.None) {
						target = InstanceID.Empty;
						return Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
					}
					if (isOutsideConnection) {
						target = InstanceID.Empty;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
					}
					if (targetBuilding == homeId) {
						target = InstanceID.Empty;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_HOME");
					}
					if (targetBuilding == workId) {
						target = InstanceID.Empty;
						return Locale.Get((!isStudent) ? "CITIZEN_STATUS_TRAVELLINGTO_WORK" : "CITIZEN_STATUS_TRAVELLINGTO_SCHOOL");
					}
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
				}
			}
			if (isOutsideConnection) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
			}

			if (targetBuilding == homeId) {
				if (hangsAround) {
					target = InstanceID.Empty;
					return Locale.Get("CITIZEN_STATUS_AT_HOME");
				}
				target = InstanceID.Empty;
				ret = Locale.Get("CITIZEN_STATUS_GOINGTO_HOME");
			} else if (targetBuilding == workId) {
				if (hangsAround) {
					target = InstanceID.Empty;
					return Locale.Get((!isStudent) ? "CITIZEN_STATUS_AT_WORK" : "CITIZEN_STATUS_AT_SCHOOL");
				}
				target = InstanceID.Empty;
				ret = Locale.Get((!isStudent) ? "CITIZEN_STATUS_GOINGTO_WORK" : "CITIZEN_STATUS_GOINGTO_SCHOOL");
			} else {
				if (hangsAround) {
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					return Locale.Get("CITIZEN_STATUS_VISITING");
				}
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				ret = Locale.Get("CITIZEN_STATUS_GOINGTO");
			}

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "EnrichLocalizedCitizenStatus")) {
#endif
				if (Options.prohibitPocketCars) {
					ret = AdvancedParkingManager.Instance.EnrichLocalizedCitizenStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID]);
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END
			return ret;
		}

		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceCar) {
			if (citizenData.m_citizen == 0u) {
				return null;
			}

			// NON-STOCK CODE START
			bool forceTaxi = false;
#if BENCHMARK
			using (var bm = new Benchmark(null, "forceTaxi")) {
#endif
				if (Options.prohibitPocketCars) {
					if (ExtCitizenInstanceManager.Instance.ExtInstances[instanceID].pathMode == ExtPathMode.TaxiToTarget) {
						forceTaxi = true;
					}
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			Citizen.AgeGroup ageGroup = CustomCitizenAI.GetAgeGroup(citizenData.Info.m_agePhase);

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
#if BENCHMARK
			using (var bm = new Benchmark(null, "find-parked-vehicle")) {
#endif
				if (Options.prohibitPocketCars && useCar) {
					ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
					if (parkedVehicleId != 0) {
#if DEBUG
						if (GlobalConfig.Instance.Debug.Switches[2])
							Log._Debug($"CustomResidentAI.CustomGetVehicleInfo({instanceID}): Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
#endif
						carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
					}
				}
#if BENCHMARK
			}
#endif

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

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetTaxiProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetTaxiProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetBikeProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetBikeProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCarProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			Log.Error("CustomResidentAI.GetCarProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetElectricCarProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgePhase agePhase) {
			Log.Error("CustomResidentAI.GetElectricCarProbability called!");
			return 20;
		}
	}
}

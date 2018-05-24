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
using static TrafficManager.Traffic.Data.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	public class CustomTouristAI : TouristAI {
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			bool addCustomStatus = false;
			String ret = GetStockLocalizedStatus(instanceID, ref data, out addCustomStatus, out target);

			// NON-STOCK CODE START
#if BENCHMARK
			using (var bm = new Benchmark(null, "EnrichLocalizedCitizenStatus")) {
#endif
				if (Options.prohibitPocketCars && addCustomStatus) {
					ret = AdvancedParkingManager.Instance.EnrichLocalizedCitizenStatus(ret, ref ExtCitizenInstanceManager.Instance.ExtInstances[instanceID], ref ExtCitizenManager.Instance.ExtCitizens[data.m_citizen]);
				}
#if BENCHMARK
			}
#endif
			// NON-STOCK CODE END

			return ret;
		}

		private String GetStockLocalizedStatus(ushort instanceID, ref CitizenInstance data, out bool addCustomStatus, out InstanceID target) {
			if ((data.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None) {
				target = InstanceID.Empty;
				addCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint citizenId = data.m_citizen;
			ushort vehicleId = 0;
			if (citizenId != 0u) {
				vehicleId = instance.m_citizens.m_buffer[citizenId].m_vehicle;
			}

			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				addCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}

			if ((data.m_flags & CitizenInstance.Flags.TargetIsNode) != 0) {
				if (vehicleId != 0) {
					VehicleManager vehManager = Singleton<VehicleManager>.instance;
					VehicleInfo info = vehManager.m_vehicles.m_buffer[vehicleId].Info;
					if (info.m_class.m_service == ItemClass.Service.Residential && info.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
						if (info.m_vehicleAI.GetOwnerID(vehicleId, ref vehManager.m_vehicles.m_buffer[vehicleId]).Citizen == citizenId) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							addCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_DRIVINGTO");
						}
					} else if (info.m_class.m_service == ItemClass.Service.PublicTransport || info.m_class.m_service == ItemClass.Service.Disaster) {
						ushort transportLine = Singleton<NetManager>.instance.m_nodes.m_buffer[targetBuilding].m_transportLine;
						if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != 0) {
							target = InstanceID.Empty;
							addCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
						}
						if (vehManager.m_vehicles.m_buffer[vehicleId].m_transportLine != transportLine) {
							target = InstanceID.Empty;
							target.NetNode = targetBuilding;
							addCustomStatus = true;
							return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
						}
					}
				}

				if ((data.m_flags & CitizenInstance.Flags.OnTour) != 0) {
					target = InstanceID.Empty;
					target.NetNode = targetBuilding;
					addCustomStatus = true;
					return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_VISITING");
				}

				target = InstanceID.Empty;
				target.NetNode = targetBuilding;
				addCustomStatus = true;
				return ColossalFramework.Globalization.Locale.Get("CITIZEN_STATUS_GOINGTO");
			}

			bool isOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool hangsAround = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;
			
			if (vehicleId != 0) {
				VehicleManager vehManager = Singleton<VehicleManager>.instance;
				VehicleInfo vehicleInfo = vehManager.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (vehicleInfo.m_class.m_service == ItemClass.Service.Residential && vehicleInfo.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (vehicleInfo.m_vehicleAI.GetOwnerID(vehicleId, ref vehManager.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizenId) {
						if (isOutsideConnection) {
							target = InstanceID.Empty;
							addCustomStatus = true;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}

						target = InstanceID.Empty;
						target.Building = targetBuilding;
						addCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
					}
				} else if (vehicleInfo.m_class.m_service == ItemClass.Service.PublicTransport || vehicleInfo.m_class.m_service == ItemClass.Service.Disaster) {
					if (isOutsideConnection) {
						target = InstanceID.Empty;
						addCustomStatus = true;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
					}
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					addCustomStatus = true;
					return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
				}
			}
			if (isOutsideConnection) {
				target = InstanceID.Empty;
				addCustomStatus = true;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
			}

			if (hangsAround) {
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				addCustomStatus = false;
				return Locale.Get("CITIZEN_STATUS_VISITING");
			}

			target = InstanceID.Empty;
			target.Building = targetBuilding;
			addCustomStatus = true;
			return Locale.Get("CITIZEN_STATUS_GOINGTO");
		}

		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceCar, out VehicleInfo trailer) {
#if DEBUG
			bool citDebug = GlobalConfig.Instance.Debug.CitizenId == 0 || GlobalConfig.Instance.Debug.CitizenId == citizenData.m_citizen;
			bool debug = GlobalConfig.Instance.Debug.Switches[2] && citDebug;
			bool fineDebug = GlobalConfig.Instance.Debug.Switches[4] && citDebug;
#endif

			trailer = null;

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
#if BENCHMARK
			using (var bm = new Benchmark(null, "find-parked-vehicle")) {
#endif
				if (Options.prohibitPocketCars && useCar && !useTaxi) {
					ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
					if (parkedVehicleId != 0) {
#if DEBUG
						if (debug)
							Log._Debug($"CustomTouristAI.CustomGetVehicleInfo({instanceID}): Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
#endif
						carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
					}
				}
#if BENCHMARK
			}
#endif

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

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetTaxiProbability() {
			Log.Error("CustomTouristAI.GetTaxiProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetBikeProbability() {
			Log.Error("CustomTouristAI.GetBikeProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCarProbability() {
			Log.Error("CustomTouristAI.GetCarProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetElectricCarProbability(Citizen.Wealth wealth) {
			Log.Error("CustomTouristAI.GetElectricCarProbability called!");
			return 20;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int GetCamperProbability(Citizen.Wealth wealth) {
			Log.Error("CustomTouristAI.GetCamperProbability called!");
			return 20;
		}
	}
}

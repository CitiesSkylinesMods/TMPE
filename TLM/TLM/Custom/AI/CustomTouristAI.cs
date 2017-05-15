using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager;
using TrafficManager.State;
using TrafficManager.Traffic;
using static TrafficManager.Traffic.ExtCitizenInstance;

namespace TrafficManager.Custom.AI {
	public class CustomTouristAI : TouristAI {
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			if ((data.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != CitizenInstance.Flags.None) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint citizen = data.m_citizen;
			ushort vehicleId = 0;
			if (citizen != 0u) {
				vehicleId = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_vehicle;
			}
			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_CONFUSED");
			}
			bool flag = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool flag2 = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;
			String ret = "";
			if (vehicleId != 0) {
				VehicleManager instance2 = Singleton<VehicleManager>.instance;
				VehicleInfo info = instance2.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (info.m_class.m_service == ItemClass.Service.Residential && info.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (info.m_vehicleAI.GetOwnerID(vehicleId, ref instance2.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizen) {
						if (flag) {
							target = InstanceID.Empty;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}

						target = InstanceID.Empty;
						target.Building = targetBuilding;
						return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
					}
				} else if (info.m_class.m_service == ItemClass.Service.PublicTransport || info.m_class.m_service == ItemClass.Service.Disaster) {
					if (flag) {
						target = InstanceID.Empty;
						return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
					}
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
				}
			}
			if (flag) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
			}
			if (flag2) {
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				return Locale.Get("CITIZEN_STATUS_VISITING");
			}
			target = InstanceID.Empty;
			target.Building = targetBuilding;
			ret = Locale.Get("CITIZEN_STATUS_GOINGTO");

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);
				ret = CustomHumanAI.EnrichLocalizedStatus(ret, extInstance);
			}
			// NON-STOCK CODE END
			return ret;
		}

		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceCar) {
			if (citizenData.m_citizen == 0u) {
				return null;
			}

			// NON-STOCK CODE START
			bool forceTaxi = false;
			if (Options.prohibitPocketCars) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);
				if (extInstance.PathMode == ExtPathMode.TaxiToTarget) {
					forceTaxi = true;
				}
			}
			// NON-STOCK CODE END

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
			bool useBike = randomizer.Int32(100u) < bikeProb;
			bool useTaxi = randomizer.Int32(100u) < taxiProb;
			ItemClass.Service service = ItemClass.Service.Residential;
			ItemClass.SubService subService = ItemClass.SubService.ResidentialLow;
			if (!useCar && useTaxi) {
				service = ItemClass.Service.PublicTransport;
				subService = ItemClass.SubService.PublicTransportTaxi;
			}
			// NON-STOCK CODE START
			VehicleInfo carInfo = null;
			if (Options.prohibitPocketCars && useCar && !useTaxi) {
				ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceID);
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
#if DEBUG
					if (GlobalConfig.Instance.DebugSwitches[2])
						Log._Debug($"CustomResidentAI.GetVehicleInfo: Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
#endif
					carInfo = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}
			if (carInfo == null && (useCar || useTaxi))
			// NON-STOCK CODE END
				carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level1);
			VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialHigh, ItemClass.Level.Level2);
			if (useBike && bikeInfo != null) {
				return bikeInfo;
			}
			if ((useCar || useTaxi) && carInfo != null) {
				return carInfo;
			}
			return null;
		}

		private int GetTaxiProbability() {
			Log.Error("CustomTouristAI.GetTaxiProbability called!");
			return 20;
		}

		private int GetBikeProbability() {
			Log.Error("CustomTouristAI.GetBikeProbability called!");
			return 20;
		}

		private int GetCarProbability() {
			Log.Error("CustomTouristAI.GetCarProbability called!");
			return 20;
		}
	}
}

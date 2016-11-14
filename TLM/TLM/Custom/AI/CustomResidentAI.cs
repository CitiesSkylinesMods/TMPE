using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	public class CustomResidentAI : HumanAI {
		public string CustomGetLocalizedStatus(ushort instanceID, ref CitizenInstance data, out InstanceID target) {
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint citizen = data.m_citizen;
			bool isStudent = false;
			ushort homeId = 0;
			ushort workId = 0;
			ushort vehicleId = 0;
			if (citizen != 0u) {
				homeId = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_homeBuilding;
				workId = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_workBuilding;
				vehicleId = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_vehicle;
				isStudent = ((instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_flags & Citizen.Flags.Student) != Citizen.Flags.None);
			}
			ushort targetBuilding = data.m_targetBuilding;
			if (targetBuilding == 0) {
				target = InstanceID.Empty;
				return Locale.Get("CITIZEN_STATUS_CONFUSED") + " :-(";
			}
			bool isOutsideConnection = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)targetBuilding].m_flags & Building.Flags.IncomingOutgoing) != Building.Flags.None;
			bool hangsAround = data.m_path == 0u && (data.m_flags & CitizenInstance.Flags.HangAround) != CitizenInstance.Flags.None;
			if (vehicleId != 0) {
				VehicleManager instance2 = Singleton<VehicleManager>.instance;
				VehicleInfo info = instance2.m_vehicles.m_buffer[(int)vehicleId].Info;
				if (info.m_class.m_service == ItemClass.Service.Residential && info.m_vehicleType != VehicleInfo.VehicleType.Bicycle) {
					if (info.m_vehicleAI.GetOwnerID(vehicleId, ref instance2.m_vehicles.m_buffer[(int)vehicleId]).Citizen == citizen) {
						if (isOutsideConnection) {
							target = InstanceID.Empty;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
						}
						if (targetBuilding == homeId) {
							target = InstanceID.Empty;
							return Locale.Get("CITIZEN_STATUS_DRIVINGTO_HOME");
						}
						if (targetBuilding == workId) {
							target = InstanceID.Empty;
							return Locale.Get((!isStudent) ? "CITIZEN_STATUS_DRIVINGTO_WORK" : "CITIZEN_STATUS_DRIVINGTO_SCHOOL");
						}
						target = InstanceID.Empty;
						target.Building = targetBuilding;
						return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
					}
				} else if (info.m_class.m_service == ItemClass.Service.PublicTransport) {
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
				return Locale.Get("CITIZEN_STATUS_GOINGTO_HOME");
			} else if (targetBuilding == workId) {
				if (hangsAround) {
					target = InstanceID.Empty;
					return Locale.Get((!isStudent) ? "CITIZEN_STATUS_AT_WORK" : "CITIZEN_STATUS_AT_SCHOOL");
				}
				target = InstanceID.Empty;
				return Locale.Get((!isStudent) ? "CITIZEN_STATUS_GOINGTO_WORK" : "CITIZEN_STATUS_GOINGTO_SCHOOL");
			} else {
				if (hangsAround) {
					target = InstanceID.Empty;
					target.Building = targetBuilding;
					return Locale.Get("CITIZEN_STATUS_VISITING");
				}
				target = InstanceID.Empty;
				target.Building = targetBuilding;
				return Locale.Get("CITIZEN_STATUS_GOINGTO");
			}
		}

		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceProbability) {
			return CustomResidentAI.GetVehicleInfo(instanceID, ref citizenData, forceProbability);
		}

		public static VehicleInfo GetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceProbability) {
			if (citizenData.m_citizen == 0u) {
				return null;
			}

			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
					if (Options.debugSwitches[2])
						Log._Debug($"CustomResidentAI.GetVehicleInfo: Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
					return Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}
			// NON-STOCK CODE END

			Citizen.AgeGroup ageGroup = CustomCitizenAI.GetAgeGroup(citizenData.Info.m_agePhase);

			int carProb;
			int bikeProb;
			int taxiProb;
			if (forceProbability || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) != CitizenInstance.Flags.None) {
				carProb = 100;
				bikeProb = 0;
				taxiProb = 0;
			} else {
				carProb = CustomResidentAI.GetCarProbability(instanceID, ref citizenData, ageGroup);
				bikeProb = CustomResidentAI.GetBikeProbability(instanceID, ref citizenData, ageGroup);
				taxiProb = CustomResidentAI.GetTaxiProbability(instanceID, ref citizenData, ageGroup);
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
			VehicleInfo carInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, service, subService, ItemClass.Level.Level1);
			VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialHigh, (ageGroup != Citizen.AgeGroup.Child) ? ItemClass.Level.Level2 : ItemClass.Level.Level1);
			if (useBike && bikeInfo != null) {
				return bikeInfo;
			}
			if ((useCar || useTaxi) && carInfo != null) {
				return carInfo;
			}
			return null;
		}

		private static int GetTaxiProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			switch (ageGroup) {
				case Citizen.AgeGroup.Child:
					return 0;
				case Citizen.AgeGroup.Teen:
					return 2;
				case Citizen.AgeGroup.Young:
					return 2;
				case Citizen.AgeGroup.Adult:
					return 4;
				case Citizen.AgeGroup.Senior:
					return 6;
				default:
					return 0;
			}
		}

		private static int GetBikeProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			CitizenManager citizenManager = Singleton<CitizenManager>.instance;
			uint citizenId = citizenData.m_citizen;
			ushort homeId = citizenManager.m_citizens.m_buffer[(int)((UIntPtr)citizenId)].m_homeBuilding;
			int bikeEncouragement = 0;
			if (homeId != 0) {
				Vector3 position = Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)homeId].m_position;
				DistrictManager districtManager = Singleton<DistrictManager>.instance;
				byte district = districtManager.GetDistrict(position);
				DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[(int)district].m_cityPlanningPolicies;
				if ((cityPlanningPolicies & DistrictPolicies.CityPlanning.EncourageBiking) != DistrictPolicies.CityPlanning.None) {
					bikeEncouragement = 10;
				}
			}
			switch (ageGroup) {
				case Citizen.AgeGroup.Child:
					return 40 + bikeEncouragement;
				case Citizen.AgeGroup.Teen:
					return 30 + bikeEncouragement;
				case Citizen.AgeGroup.Young:
					return 20 + bikeEncouragement;
				case Citizen.AgeGroup.Adult:
					return 10 + bikeEncouragement;
				case Citizen.AgeGroup.Senior:
					return 0 + bikeEncouragement;
				default:
					return 0;
			}
		}

		private static int GetCarProbability(ushort instanceID, ref CitizenInstance citizenData, Citizen.AgeGroup ageGroup) {
			switch (ageGroup) {
				case Citizen.AgeGroup.Child:
					return 0;
				case Citizen.AgeGroup.Teen:
					return 5;
				case Citizen.AgeGroup.Young:
					return Options.prohibitPocketCars ? 100 : 15;
				case Citizen.AgeGroup.Adult:
					return Options.prohibitPocketCars ? 100 : 20;
				case Citizen.AgeGroup.Senior:
					return Options.prohibitPocketCars ? 100 : 10;
				default:
					return 0;
			}
		}
	}
}

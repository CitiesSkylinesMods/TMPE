using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	public class CustomResidentAI : HumanAI {
		public VehicleInfo GetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceProbability) {
			if (citizenData.m_citizen == 0u) {
				return null;
			}
			
			// NON-STOCK CODE START
			if (Options.prohibitPocketCars) {
				ushort parkedVehicleId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
				if (parkedVehicleId != 0) {
					Log._Debug($"CustomResidentAI.GetVehicleInfo: Citizen instance {instanceID} owns a parked vehicle. Reusing vehicle info.");
					return Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}
			// NON-STOCK CODE END

			Citizen.AgeGroup ageGroup;
			switch (this.m_info.m_agePhase) {
				case Citizen.AgePhase.Child:
					ageGroup = Citizen.AgeGroup.Child;
					break;
				case Citizen.AgePhase.Teen0:
				case Citizen.AgePhase.Teen1:
					ageGroup = Citizen.AgeGroup.Teen;
					break;
				case Citizen.AgePhase.Young0:
				case Citizen.AgePhase.Young1:
				case Citizen.AgePhase.Young2:
					ageGroup = Citizen.AgeGroup.Young;
					break;
				case Citizen.AgePhase.Adult0:
				case Citizen.AgePhase.Adult1:
				case Citizen.AgePhase.Adult2:
				case Citizen.AgePhase.Adult3:
					ageGroup = Citizen.AgeGroup.Adult;
					break;
				case Citizen.AgePhase.Senior0:
				case Citizen.AgePhase.Senior1:
				case Citizen.AgePhase.Senior2:
				case Citizen.AgePhase.Senior3:
					ageGroup = Citizen.AgeGroup.Senior;
					break;
				default:
					ageGroup = Citizen.AgeGroup.Adult;
					break;
			}
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
					return 15;
				case Citizen.AgeGroup.Adult:
					return 20;
				case Citizen.AgeGroup.Senior:
					return 10;
				default:
					return 0;
			}
		}
	}
}

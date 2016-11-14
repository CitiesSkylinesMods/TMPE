using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;

namespace TrafficManager.Custom.AI {
	public class CustomTouristAI : TouristAI {
		public VehicleInfo CustomGetVehicleInfo(ushort instanceID, ref CitizenInstance citizenData, bool forceProbability) {
			return CustomTouristAI.GetVehicleInfo(instanceID, ref citizenData, forceProbability);
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
						Log._Debug($"CustomTouristAI.GetVehicleInfo: Citizen instance {instanceID} owns a parked vehicle {parkedVehicleId}. Reusing vehicle info.");
					return Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[parkedVehicleId].Info;
				}
			}
			// NON-STOCK CODE END

			int carProb;
			int bikeProb;
			int taxiProb;
			if (forceProbability || (citizenData.m_flags & CitizenInstance.Flags.BorrowCar) != CitizenInstance.Flags.None) {
				carProb = 100;
				bikeProb = 0;
				taxiProb = 0;
			} else {
				carProb = CustomTouristAI.GetCarProbability();
				bikeProb = CustomTouristAI.GetBikeProbability();
				taxiProb = CustomTouristAI.GetTaxiProbability();
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
			VehicleInfo bikeInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(ref randomizer, ItemClass.Service.Residential, ItemClass.SubService.ResidentialHigh, ItemClass.Level.Level2);
			if (useBike && bikeInfo != null) {
				return bikeInfo;
			}
			if ((useCar || useTaxi) && carInfo != null) {
				return carInfo;
			}
			return null;
		}

		private static int GetTaxiProbability() {
			return 20;
		}

		private static int GetBikeProbability() {
			return 20;
		}

		private static int GetCarProbability() {
			return 20;
		}
	}
}

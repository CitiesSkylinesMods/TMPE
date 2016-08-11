using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Geometry;
using UnityEngine;
using TrafficManager.Traffic;

namespace TrafficManager.Custom.AI {
	class CustomCitizenAI : CitizenAI {
		public static readonly int[] FREE_TRANSPORT_USAGE_PROBABILITY = { 90, 80, 50 };
		public static readonly int[] TRANSPORT_USAGE_PROBABILITY = { 50, 40, 30 };
		public static readonly int[] DAY_TAXI_USAGE_PROBABILITY = { 5, 10, 60 };
		public static readonly int[] NIGHT_TAXI_USAGE_PROBABILITY = { 10, 40, 70 };

		// CitizenAI
		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
			SimulationManager simManager = Singleton<SimulationManager>.instance;
			Citizen.Wealth wealthLevel = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].WealthLevel;
			bool useTaxi = false;
			bool useCar = false;
			bool useBike = false;
			bool usePublicTransport = false;

			bool randomParking = false;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						if ((simManager.m_isNightTime && simManager.m_randomizer.Int32(100) < NIGHT_TAXI_USAGE_PROBABILITY[(int)wealthLevel]) ||
							(!simManager.m_isNightTime && simManager.m_randomizer.Int32(100) < DAY_TAXI_USAGE_PROBABILITY[(int)wealthLevel])) {
							useTaxi = true; // NON-STOCK CODE
						}
					}
				} else {
					// NON-STOCK CODE START
					if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
						useBike = true;
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						useCar = true;
					}
					// NON-STOCK CODE END

					if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
						randomParking = true;
					}
				}
			}

			byte districtId = Singleton<DistrictManager>.instance.GetDistrict(startPos);
			DistrictPolicies.Services servicePolicies = Singleton<DistrictManager>.instance.m_districts.m_buffer[(int)districtId].m_servicePolicies;
			int transportUsageProb = (servicePolicies & DistrictPolicies.Services.FreeTransport) != DistrictPolicies.Services.None ? FREE_TRANSPORT_USAGE_PROBABILITY[(int)wealthLevel] : TRANSPORT_USAGE_PROBABILITY[(int)wealthLevel];

			bool mayUseTransport = false;
			if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) { // STOCK CODE
				mayUseTransport = true;
				if (useTaxi || simManager.m_randomizer.Int32(100) < transportUsageProb) {
					usePublicTransport = true;
					//mayUseTransport = true;
					//Log._Debug($"CustomCitizenAI: citizen {instanceID} can use public transport");
				}
			}

			ExtVehicleType extVehicleType = ExtVehicleType.None;
			NetInfo.LaneType laneType = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;

			if (!usePublicTransport && !useCar && !useBike && !useTaxi && mayUseTransport) {
				usePublicTransport = true;
			}

			if (usePublicTransport) {
				// cims using public transport do not use pocket cars
				useCar = false;

				laneType |= NetInfo.LaneType.PublicTransport;
				extVehicleType |= ExtVehicleType.PublicTransport;
			}

			if (useTaxi) {
				laneType |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				vehicleType |= vehicleInfo.m_vehicleType;
				extVehicleType |= ExtVehicleType.Taxi;
			} else if (useBike || useCar) {
				laneType |= NetInfo.LaneType.Vehicle;
				vehicleType |= vehicleInfo.m_vehicleType;

				if (useBike)
					extVehicleType |= ExtVehicleType.Bicycle;
				else
					extVehicleType |= ExtVehicleType.PassengerCar;
			}

			PathUnit.Position vehiclePosition = default(PathUnit.Position);
			if (useCar) {
				ushort parkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[(int)((UIntPtr)citizenData.m_citizen)].m_parkedVehicle;
				if (parkedVehicle != 0) {
					Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[(int)parkedVehicle].m_position;
					PathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
				}
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;
			PathUnit.Position startPosA;
			PathUnit.Position endPosA;
			if (this.FindPathPosition(instanceID, ref citizenData, startPos, laneType, vehicleType, allowUnderground, out startPosA) &&
				this.FindPathPosition(instanceID, ref citizenData, endPos, laneType, vehicleType, false, out endPosA)) {

				PathUnit.Position position2 = default(PathUnit.Position);
				uint path;
#if DEBUG
				//Log._Debug($"CustomCitizenAI: citizen instance {instanceID}, id {citizenData.m_citizen}. vehicleType={vehicleType} laneType={laneType} extVehicleType={extVehicleType} usePublicTransport={usePublicTransport} useTaxi={useTaxi} useBike={useBike} useCar={useCar} wealthLevel={wealthLevel}");
#endif
				// NON-STOCK CODE END //
				if (Singleton<CustomPathManager>.instance.CreatePath(false, (ExtVehicleType)extVehicleType, null, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, position2, endPosA, position2, vehiclePosition, laneType, vehicleType, 20000f, false, false, false, false, randomParking)) {
					if (citizenData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(citizenData.m_path);
					}
					citizenData.m_path = path;
					citizenData.m_flags |= CitizenInstance.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}
	}
}

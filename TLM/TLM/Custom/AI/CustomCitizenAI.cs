using ColossalFramework;
using ColossalFramework.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomCitizenAI : CitizenAI {
		public static readonly int[] FREE_TRANSPORT_USAGE_PROBABILITY = { 80, 50, 5 };
		public static readonly int[] TRANSPORT_USAGE_PROBABILITY = { 40, 25, 10 };

		// CitizenAI
		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
			NetInfo.LaneType laneType = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
			bool randomParking = false;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None && Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						SimulationManager instance = Singleton<SimulationManager>.instance;
						if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
							laneType |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
							vehicleType |= vehicleInfo.m_vehicleType;
						}
					}
				} else {
					laneType |= NetInfo.LaneType.Vehicle;
					vehicleType |= vehicleInfo.m_vehicleType;
					if (citizenData.m_targetBuilding != 0 && Singleton<BuildingManager>.instance.m_buildings.m_buffer[(int)citizenData.m_targetBuilding].Info.m_class.m_service > ItemClass.Service.Office) {
						randomParking = true;
					}
				}
			}
			PathUnit.Position vehiclePosition = default(PathUnit.Position);
			ushort parkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[(int)((UIntPtr)citizenData.m_citizen)].m_parkedVehicle;
			if (parkedVehicle != 0) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[(int)parkedVehicle].m_position;
				PathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition);
			}
			bool allowUnderground = (citizenData.m_flags & (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) != CitizenInstance.Flags.None;
			PathUnit.Position startPosA;
			PathUnit.Position endPosA;
			if (this.FindPathPosition(instanceID, ref citizenData, startPos, laneType, vehicleType, allowUnderground, out startPosA) &&
				this.FindPathPosition(instanceID, ref citizenData, endPos, laneType, vehicleType, false, out endPosA)) {

				// NON-STOCK CODE START //
				Citizen.Wealth wealthLevel = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].WealthLevel;

				byte districtId = Singleton<DistrictManager>.instance.GetDistrict(startPos);
				DistrictPolicies.Services servicePolicies = Singleton<DistrictManager>.instance.m_districts.m_buffer[(int)districtId].m_servicePolicies;
				int transportUsageProb = (servicePolicies & DistrictPolicies.Services.FreeTransport) != DistrictPolicies.Services.None ? FREE_TRANSPORT_USAGE_PROBABILITY[(int)wealthLevel] : TRANSPORT_USAGE_PROBABILITY[(int)wealthLevel];

				//bool mayUseTransport = false;
				if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) { // STOCK CODE
					if (vehicleInfo == null || (instanceID % 100)+1 <= transportUsageProb) {
						laneType |= NetInfo.LaneType.PublicTransport; // STOCK CODE
						//mayUseTransport = true;
						//Log._Debug($"CustomCitizenAI: citizen {instanceID} can use public transport");
					}
				}
				// NON-STOCK CODE END //
				PathUnit.Position position2 = default(PathUnit.Position);
				uint path;
				// NON-STOCK CODE START //
				ExtVehicleType extVehicleType = ExtVehicleType.PassengerCar;
				if (vehicleInfo != null && vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle)
					extVehicleType = ExtVehicleType.Bicycle;
				//Log._Debug($"CustomCitizenAI: citizen instance {instanceID}, id {citizenData.m_citizen}. {vehicleType} {extVehicleType} mayUseTransport={mayUseTransport} wealthLevel={wealthLevel}");
                bool res = Singleton<CustomPathManager>.instance.CreatePath((ExtVehicleType)extVehicleType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, position2, endPosA, position2, vehiclePosition, laneType, vehicleType, 20000f, false, false, false, false, randomParking);
				// NON-STOCK CODE END //
				if (res) {
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

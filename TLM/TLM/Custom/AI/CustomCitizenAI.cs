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
	public class CustomCitizenAI : CitizenAI {
		internal enum TransportMode {
			None,
			Car,
			PublicTransport,
			Taxi
		}

		public static readonly int[] FREE_TRANSPORT_USAGE_PROBABILITY = { 90, 80, 50 };
		public static readonly int[] TRANSPORT_USAGE_PROBABILITY = { 50, 40, 30 };
		public static readonly int[] DAY_TAXI_USAGE_PROBABILITY = { 5, 10, 50 };
		public static readonly int[] NIGHT_TAXI_USAGE_PROBABILITY = { 10, 40, 70 };

		internal static TransportMode[] currentTransportMode = new TransportMode[CitizenManager.MAX_INSTANCE_COUNT];

		internal void OnLevelUnloading() {
			for (int i = 0; i < currentTransportMode.Length; ++i)
				currentTransportMode[i] = TransportMode.None;
		}

		// CitizenAI
		public bool CustomStartPathFind(ushort instanceID, ref CitizenInstance citizenData, Vector3 startPos, Vector3 endPos, VehicleInfo vehicleInfo) {
			CitizenManager citMan = Singleton<CitizenManager>.instance;
			if (citMan.m_citizens.m_buffer[citizenData.m_citizen].CurrentLocation == Citizen.Location.Home)
				currentTransportMode[instanceID] = TransportMode.None; // reset currently used transport mode at home

			SimulationManager simManager = Singleton<SimulationManager>.instance;
			Citizen.Wealth wealthLevel = citMan.m_citizens.m_buffer[citizenData.m_citizen].WealthLevel;
			bool couldUseTaxi = false; // could cim use a taxi if it was not forbidden because of randomization?
			//bool couldUseCar = false;
			bool couldUseBike = false;
			bool wouldAffordTaxiVoluntarily = false;

			bool randomParking = false;
			if (vehicleInfo != null) {
				if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi) {
					if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTaxi) == CitizenInstance.Flags.None &&
						Singleton<DistrictManager>.instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u) {
						couldUseTaxi = true;

						if (currentTransportMode[instanceID] == TransportMode.Taxi || (currentTransportMode[instanceID] == TransportMode.None &&
							((simManager.m_isNightTime && simManager.m_randomizer.Int32(100) < NIGHT_TAXI_USAGE_PROBABILITY[(int)wealthLevel]) ||
							(!simManager.m_isNightTime && simManager.m_randomizer.Int32(100) < DAY_TAXI_USAGE_PROBABILITY[(int)wealthLevel])))) {
							wouldAffordTaxiVoluntarily = true; // NON-STOCK CODE
						}
					}
				} else {
					// NON-STOCK CODE START
					if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Bicycle) {
						couldUseBike = true;
					} else if (vehicleInfo.m_vehicleType == VehicleInfo.VehicleType.Car) {
						//couldUseCar = true;
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

			bool useTaxi = false;
			bool useBike = false;
			bool useCar = false;
			bool usePublicTransport = false;

			if ((citizenData.m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) { // STOCK CODE
				if (currentTransportMode[instanceID] == TransportMode.PublicTransport || useTaxi || (currentTransportMode[instanceID] == TransportMode.None && simManager.m_randomizer.Int32(100) < transportUsageProb)) {
					usePublicTransport = true;
				}
			}

			ushort parkedVehicle = Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenData.m_citizen].m_parkedVehicle;
			bool couldUseParkedCar = false; // cims are not allowed to use pocket cars (unless we have no choice)
			PathUnit.Position vehiclePosition = default(PathUnit.Position);
			if (parkedVehicle != 0) {
				Vector3 position = Singleton<VehicleManager>.instance.m_parkedVehicles.m_buffer[(int)parkedVehicle].m_position;
				if (PathManager.FindPathPosition(position, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, VehicleInfo.VehicleType.Car, false, false, 32f, out vehiclePosition)) {
					couldUseParkedCar = true;
				}
			}

			if (couldUseBike) {
				// bikes may be transported
				useBike = true;
			}

			if (!usePublicTransport) {
				if (couldUseParkedCar && currentTransportMode[instanceID] == TransportMode.Car) {
					useCar = true;
				} else if ((wouldAffordTaxiVoluntarily && currentTransportMode[instanceID] == TransportMode.Taxi) || couldUseTaxi) {
					useTaxi = true;
				} else {
					// fallback
					useCar = true;
				}
			}

			ExtVehicleType extVehicleType = ExtVehicleType.None;
			NetInfo.LaneType laneType = NetInfo.LaneType.Pedestrian;
			VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;

			if (usePublicTransport) {
				currentTransportMode[instanceID] = TransportMode.PublicTransport;
				laneType |= NetInfo.LaneType.PublicTransport;
				extVehicleType |= ExtVehicleType.PublicTransport;
			}

			if (useBike) {
				laneType |= NetInfo.LaneType.Vehicle;
				if (vehicleInfo != null)
					vehicleType |= vehicleInfo.m_vehicleType;
				extVehicleType |= ExtVehicleType.Bicycle;
			}

			if (useTaxi) {
				currentTransportMode[instanceID] = TransportMode.Taxi;
				laneType |= (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
				if (vehicleInfo != null)
					vehicleType |= vehicleInfo.m_vehicleType;
				extVehicleType |= ExtVehicleType.Taxi;
			}
			
			if (useCar) {
				currentTransportMode[instanceID] = TransportMode.Car;
				laneType |= NetInfo.LaneType.Vehicle;
				if (vehicleInfo != null)
					vehicleType |= vehicleInfo.m_vehicleType;
				extVehicleType |= ExtVehicleType.PassengerCar;
			}

			//Log._Debug($"Citizen {instanceID}: usePublicTransport={usePublicTransport} useCar={useCar} useTaxi={useTaxi} useBike={useBike} vehicleInfo.vehicleType={vehicleInfo?.m_vehicleType} laneType={laneType} vehicleType={vehicleType} extVehicleType={extVehicleType}");

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

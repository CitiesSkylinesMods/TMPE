#define PATHRECALCx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Geometry;
using TrafficManager.Manager;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomTaxiAI : CarAI {
		public static ushort GetPassengerInstance(ushort vehicleID, ref Vehicle data) {
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint curUnitId = data.m_citizenUnits;
			int numIterations = 0;
			while (curUnitId != 0u) {
				uint nextUnit = instance.m_units.m_buffer[curUnitId].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizenId = instance.m_units.m_buffer[curUnitId].GetCitizen(i);
					if (citizenId != 0u) {
						ushort citizenInstanceId = instance.m_citizens.m_buffer[citizenId].m_instance;
						if (citizenInstanceId != 0) {
							return citizenInstanceId;
						}
					}
				}
				curUnitId = nextUnit;
				if (++numIterations > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return 0;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
#if DEBUG
			//Log._Debug($"CustomTaxiAI.CustomStartPathFind called for vehicle {vehicleID}");
#endif

#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif

			CitizenManager instance = Singleton<CitizenManager>.instance;
			ushort passengerInstanceId = CustomTaxiAI.GetPassengerInstance(vehicleID, ref vehicleData);
			if (passengerInstanceId == 0 || (instance.m_instances.m_buffer[(int)passengerInstanceId].m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}
			VehicleInfo info = this.m_info;
			CitizenInfo info2 = instance.m_instances.m_buffer[(int)passengerInstanceId].Info;
			NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Pedestrian | NetInfo.LaneType.TransportVehicle;
			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float startSqrDistA;
			float startSqrDistB;
			PathUnit.Position endPosA;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out startSqrDistA, out startSqrDistB) &&
				info2.m_citizenAI.FindPathPosition(passengerInstanceId, ref instance.m_instances.m_buffer[(int)passengerInstanceId], endPos, laneTypes, vehicleType, undergroundTarget, out endPosA)) {
				if ((instance.m_instances.m_buffer[(int)passengerInstanceId].m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					laneTypes |= NetInfo.LaneType.PublicTransport;

					uint citizenId = instance.m_instances.m_buffer[passengerInstanceId].m_citizen;
					if (citizenId != 0u && (instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
						laneTypes |= NetInfo.LaneType.EvacuationTransport;
					}
				}
				if (!startBothWays || startSqrDistA < 10f) {
					startPosB = default(PathUnit.Position);
				}
				PathUnit.Position endPosB = default(PathUnit.Position);
				SimulationManager instance2 = Singleton<SimulationManager>.instance;
				uint path;
				if (CustomPathManager._instance.CreatePath(
#if PATHRECALC
					recalcRequested,
#endif
					ExtVehicleType.Taxi, vehicleID, 0, out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleType, 20000f)) {
#if USEPATHWAITCOUNTER
					VehicleState state = VehicleStateManager.Instance._GetVehicleState(vehicleID);
					state.PathWaitCounter = 0;
#endif

					if (vehicleData.m_path != 0u) {
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
					}
					vehicleData.m_path = path;
					vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
					return true;
				}
			}
			return false;
		}

	}
}

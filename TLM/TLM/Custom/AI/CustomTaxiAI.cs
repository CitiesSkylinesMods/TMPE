#define PATHRECALCx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomTaxiAI : CarAI {
		public static ushort GetPassengerInstance(ushort vehicleID, ref Vehicle data) {
			CitizenManager instance = Singleton<CitizenManager>.instance;
			uint num = data.m_citizenUnits;
			int num2 = 0;
			while (num != 0u) {
				uint nextUnit = instance.m_units.m_buffer[(int)((UIntPtr)num)].m_nextUnit;
				for (int i = 0; i < 5; i++) {
					uint citizen = instance.m_units.m_buffer[(int)((UIntPtr)num)].GetCitizen(i);
					if (citizen != 0u) {
						ushort instance2 = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
						if (instance2 != 0) {
							return instance2;
						}
					}
				}
				num = nextUnit;
				if (++num2 > 524288) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return 0;
		}

		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays, bool undergroundTarget) {
			VehicleStateManager.Instance()._GetVehicleState(vehicleID).VehicleType = ExtVehicleType.Taxi;

#if PATHRECALC
			VehicleState state = VehicleStateManager._GetVehicleState(vehicleID);
			bool recalcRequested = state.PathRecalculationRequested;
			state.PathRecalculationRequested = false;
#endif

			CitizenManager instance = Singleton<CitizenManager>.instance;
			ushort passengerInstance = CustomTaxiAI.GetPassengerInstance(vehicleID, ref vehicleData);
			if (passengerInstance == 0 || (instance.m_instances.m_buffer[(int)passengerInstance].m_flags & CitizenInstance.Flags.Character) != CitizenInstance.Flags.None) {
				return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays, undergroundTarget);
			}
			VehicleInfo info = this.m_info;
			CitizenInfo info2 = instance.m_instances.m_buffer[(int)passengerInstance].Info;
			NetInfo.LaneType laneType = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Pedestrian | NetInfo.LaneType.TransportVehicle;
			VehicleInfo.VehicleType vehicleType = this.m_info.m_vehicleType;
			bool allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != 0;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			if (CustomPathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle, info.m_vehicleType, allowUnderground, false, 32f, out startPosA, out startPosB, out num, out num2) &&
				info2.m_citizenAI.FindPathPosition(passengerInstance, ref instance.m_instances.m_buffer[(int)passengerInstance], endPos, laneType, vehicleType, undergroundTarget, out endPosA)) {
				if ((instance.m_instances.m_buffer[(int)passengerInstance].m_flags & CitizenInstance.Flags.CannotUseTransport) == CitizenInstance.Flags.None) {
					laneType |= NetInfo.LaneType.PublicTransport;
				}
				if (!startBothWays || num < 10f) {
					startPosB = default(PathUnit.Position);
				}
				PathUnit.Position endPosB = default(PathUnit.Position);
				SimulationManager instance2 = Singleton<SimulationManager>.instance;
				uint path;
				if (Singleton<CustomPathManager>.instance.CreatePath(
#if PATHRECALC
					recalcRequested,
#endif
					ExtVehicleType.Taxi, out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneType, vehicleType, 20000f)) {
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

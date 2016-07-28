using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	class CustomShipAI : ShipAI {
		public bool CustomStartPathFind(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays) {
			/// NON-STOCK CODE START ///
			ExtVehicleType vehicleType = VehicleStateManager.Instance()._GetVehicleState(vehicleID).VehicleType;
			if (vehicleType == ExtVehicleType.None) {
#if DEBUG
				Log._Debug($"CustomShipAI.CustomStartPathFind: Vehicle {vehicleID} does not have a valid vehicle type!");
#endif
				vehicleType = ExtVehicleType.Ship;
			} else if (vehicleType == ExtVehicleType.CargoShip)
				vehicleType = ExtVehicleType.CargoVehicle;
			/// NON-STOCK CODE END ///

			VehicleInfo info = this.m_info;
			PathUnit.Position startPosA;
			PathUnit.Position startPosB;
			float num;
			float num2;
			PathUnit.Position endPosA;
			PathUnit.Position endPosB;
			float num3;
			float num4;
			if (PathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, false, 64f, out startPosA, out startPosB, out num, out num2) && PathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, info.m_vehicleType, false, false, 64f, out endPosA, out endPosB, out num3, out num4)) {
				if (!startBothWays || num < 10f) {
					startPosB = default(PathUnit.Position);
				}
				if (!endBothWays || num3 < 10f) {
					endPosB = default(PathUnit.Position);
				}
				uint path;
				bool res = Singleton<CustomPathManager>.instance.CreatePath((ExtVehicleType)vehicleType, out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, info.m_vehicleType, 20000f);
				if (res) {
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

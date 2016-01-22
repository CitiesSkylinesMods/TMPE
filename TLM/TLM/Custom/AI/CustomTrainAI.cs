using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Custom.AI {
	public class CustomTrainAI : TrainAI {
		public void TrafficManagerSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
			try {
				if ((vehicleData.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None) {
					byte pathFindFlags = Singleton<PathManager>.instance.m_pathUnits.m_buffer[(int)((UIntPtr)vehicleData.m_path)].m_pathFindFlags;
					if ((pathFindFlags & 4) != 0) {
						this.PathFindReady(vehicleId, ref vehicleData);
					} else if ((pathFindFlags & 8) != 0 || vehicleData.m_path == 0u) {
						vehicleData.m_flags &= ~Vehicle.Flags.WaitingPath;
						Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
						vehicleData.m_path = 0u;
						vehicleData.Unspawn(vehicleId);
						return;
					}
				} else if ((vehicleData.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None) {
					this.TrySpawn(vehicleId, ref vehicleData);
				}

				/// NON-STOCK CODE START ///
				try {
					CustomCarAI.HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], false, false, 5);
				} catch (Exception e) {
					Log.Error("TrainAI TrafficManagerSimulationStep Error: " + e.ToString());
				}
				/// NON-STOCK CODE END ///

				bool flag = (vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None;
				ushort num;
				if (flag) {
					num = vehicleData.GetLastVehicle(vehicleId);
				} else {
					num = vehicleId;
				}
				VehicleManager instance = Singleton<VehicleManager>.instance;
				VehicleInfo info = instance.m_vehicles.m_buffer[(int)num].Info;
				info.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, 0);
				if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
					return;
				}
				bool flag2 = (vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None;
				if (flag2 != flag) {
					flag = flag2;
					if (flag) {
						num = vehicleData.GetLastVehicle(vehicleId);
					} else {
						num = vehicleId;
					}
					info = instance.m_vehicles.m_buffer[(int)num].Info;
					info.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, 0);
					if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
						return;
					}
					flag2 = ((vehicleData.m_flags & Vehicle.Flags.Reversed) != Vehicle.Flags.None);
					if (flag2 != flag) {
						Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
						return;
					}
				}
				if (flag) {
					num = instance.m_vehicles.m_buffer[(int)num].m_leadingVehicle;
					int num2 = 0;
					while (num != 0) {
						info = instance.m_vehicles.m_buffer[(int)num].Info;
						info.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, 0);
						if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
							return;
						}
						num = instance.m_vehicles.m_buffer[(int)num].m_leadingVehicle;
						if (++num2 > 16384) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				} else {
					num = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
					int num3 = 0;
					while (num != 0) {
						info = instance.m_vehicles.m_buffer[(int)num].Info;
						info.m_vehicleAI.SimulationStep(num, ref instance.m_vehicles.m_buffer[(int)num], vehicleId, ref vehicleData, 0);
						if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
							return;
						}
						num = instance.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
						if (++num3 > 16384) {
							CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
							break;
						}
					}
				}
				if ((vehicleData.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == Vehicle.Flags.None || vehicleData.m_blockCounter == 255) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				}
			} catch (Exception ex) {
				Log.Error("Error in TrainAI.SimulationStep: " + ex.ToString());
			}
		}
	}
}

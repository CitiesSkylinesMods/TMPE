using System;
using ColossalFramework;
using UnityEngine;
using TrafficManager.Custom.Manager;

namespace TrafficManager.Custom.AI
{
    class CustomPassengerCarAI : CarAI
    {
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos)
        {
			try {
				if ((data.m_flags & Vehicle.Flags.Congestion) != Vehicle.Flags.None && LoadingExtension.Instance.DespawnEnabled) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} else {
					base.SimulationStep(vehicleId, ref data, physicsLodRefPos);
				}
			} catch (Exception ex) {
				Log.Error("Error in CustomPassengerCarAI.SimulationStep: " + ex.ToString());
			}
        }

        public bool StartPathFind2(ushort vehicleId, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            var info = m_info;
            var driverInstance = GetDriverInstance2(vehicleId, ref vehicleData);
            if (driverInstance == 0)
            {
                return false;
            }
            var instance = Singleton<CitizenManager>.instance;
            var info2 = instance.m_instances.m_buffer[driverInstance].Info;
            var laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Pedestrian;
            var vehicleType = m_info.m_vehicleType;
            var allowUnderground = (vehicleData.m_flags & Vehicle.Flags.Underground) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            PathUnit.Position endPosA;
            const bool requireConnect = false;
            const float maxDistance = 32f;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, info.m_vehicleType, allowUnderground, requireConnect, maxDistance, out startPosA, out startPosB, out num, out num2) && info2.m_citizenAI.FindPathPosition(driverInstance, ref instance.m_instances.m_buffer[driverInstance], endPos, laneTypes, vehicleType, false, out endPosA))
            {
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                PathUnit.Position endPosB = default(PathUnit.Position);
                SimulationManager instance2 = Singleton<SimulationManager>.instance;
                uint path;
                if (Singleton<CustomPathManager>.instance.CreatePath(out path, ref instance2.m_randomizer, instance2.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleType, 20000f, false, false, false, false))
                {
                    if (vehicleData.m_path != 0u)
                    {
                        Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                    }
                    vehicleData.m_path = path;
                    vehicleData.m_flags |= Vehicle.Flags.WaitingPath;
                    return true;
                }
            }
            return false;
        }

        public static ushort GetDriverInstance2(ushort vehicleId, ref Vehicle data)
        {
            CitizenManager instance = Singleton<CitizenManager>.instance;
            uint num = data.m_citizenUnits;
            int num2 = 0;
            while (num != 0u)
            {
                uint nextUnit = instance.m_units.m_buffer[(int)((UIntPtr)num)].m_nextUnit;
                for (int i = 0; i < 5; i++)
                {
                    uint citizen = instance.m_units.m_buffer[(int)((UIntPtr)num)].GetCitizen(i);
                    if (citizen != 0u)
                    {
                        ushort instance2 = instance.m_citizens.m_buffer[(int)((UIntPtr)citizen)].m_instance;
                        if (instance2 != 0)
                        {
                            return instance2;
                        }
                    }
                }
                num = nextUnit;
                if (++num2 > 524288)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            return 0;
        }
    }
}

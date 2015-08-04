using System;
using ColossalFramework;
using UnityEngine;

namespace TrafficManager.CustomAI
{
    class CustomCargoTruckAI : CarAI
    {
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos)
        {
            if ((data.m_flags & Vehicle.Flags.Congestion) != Vehicle.Flags.None && LoadingExtension.Instance.DespawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
            else
            {
                if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None && (data.m_waitCounter += 1) > 20)
                {
                    RemoveOffers(vehicleId, ref data);
                    data.m_flags &= ~Vehicle.Flags.WaitingTarget;
                    data.m_flags |= Vehicle.Flags.GoingBack;
                    data.m_waitCounter = 0;
                    if (!StartPathFind(vehicleId, ref data))
                    {
                        data.Unspawn(vehicleId);
                    }
                }
                BaseSimulationStep(vehicleId, ref data, physicsLodRefPos);
            }
        }

        // TODO: inherit CarAI

        private static void RemoveOffers(ushort vehicleId, ref Vehicle data)
        {
            if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None)
            {
                var offer = default(TransferManager.TransferOffer);
                offer.Vehicle = vehicleId;
                if ((data.m_flags & Vehicle.Flags.TransferToSource) != Vehicle.Flags.None)
                {
                    Singleton<TransferManager>.instance.RemoveIncomingOffer((TransferManager.TransferReason)data.m_transferType, offer);
                }
                else if ((data.m_flags & Vehicle.Flags.TransferToTarget) != Vehicle.Flags.None)
                {
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer((TransferManager.TransferReason)data.m_transferType, offer);
                }
            }
        }

        private void BaseSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos)
        {
            if ((data.m_flags & Vehicle.Flags.WaitingPath) != Vehicle.Flags.None)
            {
                PathManager instance = Singleton<PathManager>.instance;
                byte pathFindFlags = instance.m_pathUnits.m_buffer[(int)((UIntPtr)data.m_path)].m_pathFindFlags;
                if ((pathFindFlags & 4) != 0)
                {
                    data.m_pathPositionIndex = 255;
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    data.m_flags &= ~Vehicle.Flags.Arriving;
                    PathfindSuccess(vehicleId, ref data);
                    TrySpawn(vehicleId, ref data);
                }
                else if ((pathFindFlags & 8) != 0)
                {
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    Singleton<PathManager>.instance.ReleasePath(data.m_path);
                    data.m_path = 0u;
                    PathfindFailure(vehicleId, ref data);
                    return;
                }
            }
            else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None)
            {
                TrySpawn(vehicleId, ref data);
            }
            Vector3 lastFramePosition = data.GetLastFramePosition();
            int lodPhysics;
            if (Vector3.SqrMagnitude(physicsLodRefPos - lastFramePosition) >= 1210000f)
            {
                lodPhysics = 2;
            }
            else if (
                Vector3.SqrMagnitude(Singleton<SimulationManager>.instance.m_simulationView.m_position -
                                     lastFramePosition) >= 250000f)
            {
                lodPhysics = 1;
            }
            else
            {
                lodPhysics = 0;
            }
            SimulationStep(vehicleId, ref data, vehicleId, ref data, lodPhysics);
            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0)
            {
                VehicleManager instance2 = Singleton<VehicleManager>.instance;
                ushort num = data.m_trailingVehicle;
                int num2 = 0;
                while (num != 0)
                {
                    ushort trailingVehicle = instance2.m_vehicles.m_buffer[num].m_trailingVehicle;
                    VehicleInfo info = instance2.m_vehicles.m_buffer[num].Info;
                    info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[num], vehicleId,
                        ref data, lodPhysics);
                    num = trailingVehicle;
                    if (++num2 > 16384)
                    {
                        CODebugBase<LogChannel>.Error(LogChannel.Core,
                            "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }
            int num3 = (m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
            if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
                Vehicle.Flags.None && data.m_cargoParent == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
            else if (data.m_blockCounter >= num3 && LoadingExtension.Instance.DespawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
            }
        }

        public bool StartPathFind2(ushort vehicleId, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != Vehicle.Flags.None)
            {
                return StartPathFind(vehicleId, ref vehicleData, startPos, endPos, startBothWays, endBothWays);
            }
            bool allowUnderground = (vehicleData.m_flags & (Vehicle.Flags.Underground | Vehicle.Flags.Transition)) != Vehicle.Flags.None;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            bool requireConnect = true;
            float maxDistance = 32f;
            bool flag = PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, allowUnderground, requireConnect, maxDistance, out startPosA, out startPosB, out num, out num2);
            PathUnit.Position position;
            PathUnit.Position position2;
            float num3;
            float num4;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, allowUnderground, requireConnect, maxDistance, out position, out position2, out num3, out num4))
            {
                if (!flag || num3 < num)
                {
                    startPosA = position;
                    startPosB = position2;
                    num = num3;
/*
                    num2 = num4;
*/
                }
                flag = true;
            }
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num5;
            float num6;
            bool flag2 = PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, !allowUnderground, requireConnect, maxDistance, out endPosA, out endPosB, out num5, out num6);
            PathUnit.Position position3;
            PathUnit.Position position4;
            float num7;
            float num8;
            if (PathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, !allowUnderground, requireConnect, maxDistance, out position3, out position4, out num7, out num8))
            {
                if (!flag2 || num7 < num5)
                {
                    endPosA = position3;
                    endPosB = position4;
                    num5 = num7;
/*
                    num6 = num8;
*/
                }
                flag2 = true;
            }
            if (flag && flag2)
            {
                CustomPathManager instance = Singleton<CustomPathManager>.instance;
                if (!startBothWays || num < 10f)
                {
                    startPosB = default(PathUnit.Position);
                }
                if (!endBothWays || num5 < 10f)
                {
                    endPosB = default(PathUnit.Position);
                }
                NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.CargoVehicle;
                VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship;
                uint path;
                if (instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleTypes, 20000f, IsHeavyVehicle(), IgnoreBlocked(vehicleId, ref vehicleData), false, false, ItemClass.Service.Industrial))
                {
                    if (vehicleData.m_path != 0u)
                    {
                        instance.ReleasePath(vehicleData.m_path);
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

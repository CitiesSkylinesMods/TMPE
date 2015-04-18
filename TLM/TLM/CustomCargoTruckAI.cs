using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace TrafficManager
{
    class CustomCargoTruckAI : CarAI
    {
        protected void SimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos)
        {
            if ((data.m_flags & Vehicle.Flags.Congestion) != Vehicle.Flags.None && LoadingExtension.Instance.despawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            }
            else
            {
                if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None && (data.m_waitCounter += 1) > 20)
                {
                    RemoveOffers(vehicleID, ref data);
                    data.m_flags &= ~Vehicle.Flags.WaitingTarget;
                    data.m_flags |= Vehicle.Flags.GoingBack;
                    data.m_waitCounter = 0;
                    if (!this.StartPathFind(vehicleID, ref data))
                    {
                        data.Unspawn(vehicleID);
                    }
                }
                BaseSimulationStep(vehicleID, ref data, physicsLodRefPos);
            }
        }

        // TODO: inherit CarAI

        private void RemoveOffers(ushort vehicleID, ref Vehicle data)
        {
            if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None)
            {
                TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
                offer.Vehicle = vehicleID;
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

        protected void BaseSimulationStep(ushort vehicleID, ref Vehicle data, Vector3 physicsLodRefPos)
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
                    this.PathfindSuccess(vehicleID, ref data);
                    this.TrySpawn(vehicleID, ref data);
                }
                else if ((pathFindFlags & 8) != 0)
                {
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    Singleton<PathManager>.instance.ReleasePath(data.m_path);
                    data.m_path = 0u;
                    this.PathfindFailure(vehicleID, ref data);
                    return;
                }
            }
            else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != Vehicle.Flags.None)
            {
                this.TrySpawn(vehicleID, ref data);
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
            this.SimulationStep(vehicleID, ref data, vehicleID, ref data, lodPhysics);
            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0)
            {
                VehicleManager instance2 = Singleton<VehicleManager>.instance;
                ushort num = data.m_trailingVehicle;
                int num2 = 0;
                while (num != 0)
                {
                    ushort trailingVehicle = instance2.m_vehicles.m_buffer[(int)num].m_trailingVehicle;
                    VehicleInfo info = instance2.m_vehicles.m_buffer[(int)num].Info;
                    info.m_vehicleAI.SimulationStep(num, ref instance2.m_vehicles.m_buffer[(int)num], vehicleID,
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
            int num3 = (this.m_info.m_class.m_service > ItemClass.Service.Office) ? 150 : 100;
            if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath | Vehicle.Flags.WaitingSpace)) ==
                Vehicle.Flags.None && data.m_cargoParent == 0)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            }
            else if ((int)data.m_blockCounter >= num3 && LoadingExtension.Instance.despawnEnabled)
            {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleID);
            }
        }

        public bool StartPathFind2(ushort vehicleID, ref Vehicle vehicleData, Vector3 startPos, Vector3 endPos, bool startBothWays, bool endBothWays)
        {
            if ((vehicleData.m_flags & (Vehicle.Flags.TransferToSource | Vehicle.Flags.GoingBack)) != Vehicle.Flags.None)
            {
                return base.StartPathFind(vehicleID, ref vehicleData, startPos, endPos, startBothWays, endBothWays);
            }
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            bool flag = PathManager.FindPathPosition(startPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, 32f, out startPosA, out startPosB, out num, out num2);
            PathUnit.Position position;
            PathUnit.Position position2;
            float num3;
            float num4;
            if (PathManager.FindPathPosition(startPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, 32f, out position, out position2, out num3, out num4))
            {
                if (!flag || num3 < num)
                {
                    startPosA = position;
                    startPosB = position2;
                    num = num3;
                    num2 = num4;
                }
                flag = true;
            }
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num5;
            float num6;
            bool flag2 = PathManager.FindPathPosition(endPos, ItemClass.Service.Road, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, 32f, out endPosA, out endPosB, out num5, out num6);
            PathUnit.Position position3;
            PathUnit.Position position4;
            float num7;
            float num8;
            if (PathManager.FindPathPosition(endPos, ItemClass.Service.PublicTransport, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship, 32f, out position3, out position4, out num7, out num8))
            {
                if (!flag2 || num7 < num5)
                {
                    endPosA = position3;
                    endPosB = position4;
                    num5 = num7;
                    num6 = num8;
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
                NetInfo.LaneType laneTypes = NetInfo.LaneType.Vehicle | NetInfo.LaneType.Cargo;
                VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Ship;
                uint path;
                if (instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, laneTypes, vehicleTypes, 20000f, this.IsHeavyVehicle(), this.IgnoreBlocked(vehicleID, ref vehicleData), false, false, ItemClass.Service.Industrial))
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

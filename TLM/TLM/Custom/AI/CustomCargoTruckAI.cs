using System;
using ColossalFramework;
using UnityEngine;

namespace TrafficManager.Custom.AI
{
    class CustomCargoTruckAI : CarAI
    {
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle data, Vector3 physicsLodRefPos)
        {
			try {
				if ((data.m_flags & Vehicle.Flags.Congestion) != Vehicle.Flags.None && LoadingExtension.Instance.DespawnEnabled) {
					Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
				} else {
					if ((data.m_flags & Vehicle.Flags.WaitingTarget) != Vehicle.Flags.None && (data.m_waitCounter += 1) > 20) {
						RemoveOffers(vehicleId, ref data);
						data.m_flags &= ~Vehicle.Flags.WaitingTarget;
						data.m_flags |= Vehicle.Flags.GoingBack;
						data.m_waitCounter = 0;
						if (!StartPathFind(vehicleId, ref data)) {
							data.Unspawn(vehicleId);
						}
					}

					if (Options.simAccuracy <= 1) {
						try {
							CustomCarAI.HandleVehicle(vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleId], true, true);
						} catch (Exception e) {
							Log.Error("CargoTruckAI CustomSimulationStep Error: " + e.ToString());
						}
					}

					BaseSimulationStep(vehicleId, ref data, physicsLodRefPos);
				}
			} catch (Exception ex) {
				Log.Error("Error in CargoTruckAI.SimulationStep: " + ex.ToString());
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
    }
}

namespace TrafficManager.Patch._VehicleAI._AircraftAI {
    using System;
    using System.Reflection;
    using API.Manager;
    using ColossalFramework;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using UnityEngine;
    using Util;
    using Util.Extensions;

    [UsedImplicitly]
    [HarmonyPatch]
    public class SimulationStepPatch {
        private delegate void SimulationStepTargetDelegate(ushort vehicleID,
                                                           ref Vehicle data,
                                                           Vector3 physicsLodRefPos);


        [UsedImplicitly]
        public static MethodBase TargetMethod() =>
            TranspilerUtil.DeclaredMethod<SimulationStepTargetDelegate>(
                typeof(AircraftAI),
                "SimulationStep");

        [UsedImplicitly]
        public static bool Prefix(AircraftAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle data,
                                  Vector3 physicsLodRefPos) {
            if ((data.m_flags & Vehicle.Flags.WaitingPath) != 0) {
                PathManager pathManager = PathManager.instance;
                byte pathFindFlags = pathManager.m_pathUnits.m_buffer[data.m_path].m_pathFindFlags;
                if ((pathFindFlags & 4) != 0) {
                    data.m_pathPositionIndex = byte.MaxValue;
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    __instance.TrySpawn(vehicleID, ref data);
                } else if ((pathFindFlags & 8) != 0) {
                    data.m_flags &= ~Vehicle.Flags.WaitingPath;
                    pathManager.ReleasePath(data.m_path);
                    data.m_path = 0u;
                    data.Unspawn(vehicleID);
                    return false;
                }
            } else if ((data.m_flags & Vehicle.Flags.WaitingSpace) != 0) {
                __instance.TrySpawn(vehicleID, ref data);
            }
            // NON-STOCK CODE START
            IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
            extVehicleMan.UpdateVehiclePosition(vehicleID, ref data);

            if (Options.advancedAI && (data.m_flags & Vehicle.Flags.Spawned) != 0)
            {
                extVehicleMan.LogTraffic(vehicleID, ref data);
            }
            // NON-STOCK CODE END
            __instance.SimulationStep(vehicleID, ref data, vehicleID, ref data, 0);

            if (data.m_leadingVehicle == 0 && data.m_trailingVehicle != 0) {
                VehicleManager vehicleManager = VehicleManager.instance;
                uint maxVehicleCount = vehicleManager.m_vehicles.m_size;
                ushort trailingVehicleId = data.m_trailingVehicle;
                int counter = 0;
                while (trailingVehicleId != 0) {
                    ref Vehicle vehicle = ref trailingVehicleId.ToVehicle();
                    ushort trailingVehicle = vehicle.m_trailingVehicle;
                    vehicle.Info.m_vehicleAI.SimulationStep(
                        trailingVehicleId,
                        ref vehicleManager.m_vehicles.m_buffer[trailingVehicleId],
                        vehicleID,
                        ref data,
                        0);
                    trailingVehicleId = trailingVehicle;
                    if (++counter > maxVehicleCount) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            "Invalid list detected!\n" + Environment.StackTrace);
                        break;
                    }
                }
            }

            if ((data.m_flags & (Vehicle.Flags.Spawned | Vehicle.Flags.WaitingPath |
                                 Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo)) == 0
                || (data.m_blockCounter == byte.MaxValue && VehicleBehaviorManager.Instance.MayDespawn(ref data))) {
                VehicleManager.instance.ReleaseVehicle(vehicleID);
            }

            return false;
        }
    }
}
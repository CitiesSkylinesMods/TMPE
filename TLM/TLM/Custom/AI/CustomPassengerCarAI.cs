namespace TrafficManager.Custom.AI {
    using System;
    using System.Runtime.CompilerServices;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using State;
    using UnityEngine;

    // TODO move Parking AI features from here to a distinct manager
    [TargetType(typeof(PassengerCarAI))]
    public class CustomPassengerCarAI : CarAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
            if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 &&
                VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                return;
            }

            base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
        }

        [RedirectMethod]
        [UsedImplicitly]
        public string CustomGetLocalizedStatus(ushort vehicleId, ref Vehicle data, out InstanceID target) {
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            ushort driverInstanceId = GetDriverInstance(vehicleId, ref data);
            ushort targetBuildingId = 0;
            bool targetIsNode = false;

            if (driverInstanceId != 0) {
                if ((data.m_flags & Vehicle.Flags.Parking) != 0) {
                    uint citizen = citizenManager.m_instances.m_buffer[driverInstanceId].m_citizen;
                    if (citizen != 0u
                        && citizenManager.m_citizens.m_buffer[citizen].m_parkedVehicle != 0)
                    {
                        target = InstanceID.Empty;
                        return Locale.Get("VEHICLE_STATUS_PARKING");
                    }
                }

                targetBuildingId = citizenManager.m_instances.m_buffer[driverInstanceId].m_targetBuilding;
                targetIsNode = (citizenManager.m_instances.m_buffer[driverInstanceId].m_flags
                                & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None;
            }

            if (targetBuildingId == 0) {
                target = InstanceID.Empty;
                return Locale.Get("VEHICLE_STATUS_CONFUSED");
            }

            string ret;
            bool leavingCity = (Singleton<BuildingManager>.instance.m_buildings.m_buffer[targetBuildingId].m_flags
                               & Building.Flags.IncomingOutgoing) != Building.Flags.None;
            if (leavingCity) {
                target = InstanceID.Empty;
                ret = Locale.Get("VEHICLE_STATUS_LEAVING");
            } else {
                target = InstanceID.Empty;
                if (targetIsNode) {
                    target.NetNode = targetBuildingId;
                } else {
                    target.Building = targetBuildingId;
                }

                ret = Locale.Get("VEHICLE_STATUS_GOINGTO");
            }

            // NON-STOCK CODE START
            if (Options.parkingAI) {
                ret = AdvancedParkingManager.Instance.EnrichLocalizedCarStatus(
                    ret,
                    ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId]);
            }

            // NON-STOCK CODE END
            return ret;
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort vehicleId,
                                        ref Vehicle vehicleData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        bool startBothWays,
                                        bool endBothWays,
                                        bool undergroundTarget) {
            ushort driverInstanceId = GetDriverInstance(vehicleId, ref vehicleData);
            return driverInstanceId != 0
                   && Constants.ManagerFactory.VehicleBehaviorManager.StartPassengerCarPathFind(
                       vehicleId,
                       ref vehicleData,
                       m_info,
                       driverInstanceId,
                       ref Singleton<CitizenManager>.instance.m_instances.m_buffer[driverInstanceId],
                       ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId],
                       startPos,
                       endPos,
                       startBothWays,
                       endBothWays,
                       undergroundTarget,
                       IsHeavyVehicle(),
                       CombustionEngine(),
                       IgnoreBlocked(vehicleId, ref vehicleData));
        }

        [RedirectMethod]
        [UsedImplicitly]
        public void CustomUpdateParkedVehicle(ushort parkedId, ref VehicleParked data) {
            uint ownerCitizenId = data.m_ownerCitizen;
            ushort homeId = 0;

            if (ownerCitizenId != 0u) {
                homeId = Singleton<CitizenManager>.instance.m_citizens.m_buffer[ownerCitizenId].m_homeBuilding;
            }

            // NON-STOCK CODE START
            if (!AdvancedParkingManager.Instance.TryMoveParkedVehicle(
                    parkedId,
                    ref data,
                    data.m_position,
                    GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                    homeId)) {
                Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedId);
            }

            // NON-STOCK CODE END
        }

        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomParkVehicle(ushort vehicleId,
                                      ref Vehicle vehicleData,
                                      PathUnit.Position pathPos,
                                      uint nextPath,
                                      int nextPositionIndex,
                                      out byte segmentOffset) {
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;

            // TODO remove this:
            uint driverCitizenId = 0u;
            ushort driverCitizenInstanceId = 0;
            ushort targetBuildingId = 0; // NON-STOCK CODE
            uint curCitizenUnitId = vehicleData.m_citizenUnits;
            int numIterations = 0;

            while (curCitizenUnitId != 0u && driverCitizenId == 0u) {
                uint nextUnit = citizenManager.m_units.m_buffer[curCitizenUnitId].m_nextUnit;
                for (int i = 0; i < 5; i++) {
                    uint citizenId = citizenManager.m_units.m_buffer[curCitizenUnitId].GetCitizen(i);
                    if (citizenId == 0u) {
                        continue;
                    }

                    driverCitizenInstanceId = citizenManager.m_citizens.m_buffer[citizenId].m_instance;
                    if (driverCitizenInstanceId == 0) {
                        continue;
                    }

                    driverCitizenId = citizenManager.m_instances.m_buffer[driverCitizenInstanceId].m_citizen;

                    // NON-STOCK CODE START
                    targetBuildingId = citizenManager.m_instances.m_buffer[driverCitizenInstanceId].m_targetBuilding;

                    // NON-STOCK CODE END
                    break;
                }

                curCitizenUnitId = nextUnit;
                if (++numIterations > CitizenManager.MAX_UNIT_COUNT) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                  $"Invalid list detected!\n{Environment.StackTrace}");
                    break;
                }
            }

#if BENCHMARK
            using (var bm = new Benchmark(null, "ExtParkVehicle")) {
#endif
            return Constants.ManagerFactory.VehicleBehaviorManager.ParkPassengerCar(
                vehicleId,
                ref vehicleData,
                vehicleData.Info,
                driverCitizenId,
                ref citizenManager.m_citizens.m_buffer[driverCitizenId],
                driverCitizenInstanceId,
                ref citizenManager.m_instances.m_buffer[driverCitizenInstanceId],
                ref ExtCitizenInstanceManager.Instance.ExtInstances[driverCitizenInstanceId],
                targetBuildingId,
                pathPos,
                nextPath,
                nextPositionIndex,
                out segmentOffset);
#if BENCHMARK
            }
#endif
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        private ushort GetDriverInstance(ushort vehicleId, ref Vehicle data) {
            Log._DebugOnlyError("GetDriverInstance is not overridden!");
            return 0;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static bool FindParkingSpaceRoadSide(ushort ignoreParked,
                                                    ushort requireSegment,
                                                    Vector3 refPos,
                                                    float width,
                                                    float length,
                                                    out Vector3 parkPos,
                                                    out Quaternion parkRot,
                                                    out float parkOffset) {
            Log._DebugOnlyError("FindParkingSpaceRoadSide is not overridden!");
            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = 0f;
            return false;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static bool FindParkingSpace(bool isElectric,
                                            ushort homeId,
                                            Vector3 refPos,
                                            Vector3 searchDir,
                                            ushort segment,
                                            float width,
                                            float length,
                                            out Vector3 parkPos,
                                            out Quaternion parkRot,
                                            out float parkOffset) {
            Log._DebugOnlyError("FindParkingSpace is not overridden!");
            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = 0f;
            return false;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static bool FindParkingSpaceProp(bool isElectric,
                                                ushort ignoreParked,
                                                PropInfo info,
                                                Vector3 position,
                                                float angle,
                                                bool fixedHeight,
                                                Vector3 refPos,
                                                float width,
                                                float length,
                                                ref float maxDistance,
                                                ref Vector3 parkPos,
                                                ref Quaternion parkRot) {
            Log._DebugOnlyError("FindParkingSpaceProp is not overridden!");
            return false;
        }

        [RedirectReverse]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [UsedImplicitly]
        public static bool CheckOverlap(ushort ignoreParked,
                                        ref Bezier3 bezier,
                                        float offset,
                                        float length,
                                        out float minPos,
                                        out float maxPos) {
            Log._DebugOnlyError("CheckOverlap is not overridden!");
            minPos = 0;
            maxPos = 0;
            return false;
        }
    }
}

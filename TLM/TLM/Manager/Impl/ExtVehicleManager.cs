namespace TrafficManager.Manager.Impl {
    using System;
    using API.Manager;
    using API.Traffic.Data;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.Math;
    using CSUtil.Commons;
    using State;
    using State.ConfigData;
    using Traffic.Data;
    using UnityEngine;

    public class ExtVehicleManager : AbstractCustomManager, IExtVehicleManager {
        public static readonly ExtVehicleManager Instance = new ExtVehicleManager();

        public const int STATE_UPDATE_SHIFT = 6;
        public const int JUNCTION_RECHECK_SHIFT = 4;
        public const uint MAX_TIMED_RAND = 100;
        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail;

        /// <summary>
        /// Known vehicles and their current known positions. Index: vehicle id
        /// </summary>
        public ExtVehicle[] ExtVehicles { get; private set; } = null;

        static ExtVehicleManager() {
            Instance = new ExtVehicleManager();
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Ext. vehicles:");
            for (int i = 0; i < ExtVehicles.Length; ++i) {
                if ((ExtVehicles[i].flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
                    continue;
                }
                Log._Debug($"Vehicle {i}: {ExtVehicles[i]}");
            }
        }

        private ExtVehicleManager() {
            ExtVehicles = new ExtVehicle[Constants.ServiceFactory.VehicleService.MaxVehicleCount];
            for (uint i = 0; i < Constants.ServiceFactory.VehicleService.MaxVehicleCount; ++i) {
                ExtVehicles[i] = new ExtVehicle((ushort)i);
            }
        }

        public void SetJunctionTransitState(ref ExtVehicle extVehicle, VehicleJunctionTransitState transitState) {
            if (transitState != extVehicle.junctionTransitState) {
                extVehicle.junctionTransitState = transitState;
                extVehicle.lastTransitStateUpdate = Now();
            }
        }

        public ushort GetDriverInstanceId(ushort vehicleId, ref Vehicle data) {
            // (stock code from PassengerCarAI.GetDriverInstance)
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            uint citizenUnitId = data.m_citizenUnits;
            int numIter = 0;
            while (citizenUnitId != 0) {
                uint nextCitizenUnitId = citizenManager.m_units.m_buffer[citizenUnitId].m_nextUnit;
                for (int i = 0; i < 5; i++) {
                    uint citizenId = citizenManager.m_units.m_buffer[citizenUnitId].GetCitizen(i);
                    if (citizenId != 0) {
                        ushort citizenInstanceId = citizenManager.m_citizens.m_buffer[citizenId].m_instance;
                        if (citizenInstanceId != 0) {
                            return citizenInstanceId;
                        }
                    }
                }
                citizenUnitId = nextCitizenUnitId;
                if (++numIter > CitizenManager.MAX_UNIT_COUNT) {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
            return 0;
        }

        public void LogTraffic(ushort vehicleId, ref Vehicle vehicle) {
            LogTraffic(vehicleId, ref vehicle, ref ExtVehicles[vehicleId]);
        }

        protected void LogTraffic(ushort vehicleId, ref Vehicle vehicle, ref ExtVehicle extVehicle) {
            if (extVehicle.currentSegmentId == 0) {
                return;
            }
#if MEASUREDENSITY
			ushort length = (ushort)state.totalLength;
			if (length == 0) {
				return;
			}
#endif

            if (Options.advancedAI) {
                TrafficMeasurementManager.Instance.AddTraffic(extVehicle.currentSegmentId, extVehicle.currentLaneIndex
#if MEASUREDENSITY
					, length
#endif
                                                              , (ushort)vehicle.GetLastFrameVelocity().magnitude);
            }
        }

        public void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData) {
            OnReleaseVehicle(vehicleId, ref vehicleData);
            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created ||
                (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnCreateVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
                return;
            }

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnCreateVehicle({vehicleId}): calling OnCreate for vehicle {vehicleId}");
#endif

            OnCreate(ref ExtVehicles[vehicleId], ref vehicleData);
        }

        public ExtVehicleType OnStartPathFind(ushort vehicleId, ref Vehicle vehicleData, ExtVehicleType? vehicleType) {
            if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None ||
                (vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnStartPathFind({vehicleId}, {vehicleType}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
                return ExtVehicleType.None;
            }

            ExtVehicleType ret = OnStartPathFind(ref ExtVehicles[vehicleId], ref vehicleData, vehicleType);

            ushort connectedVehicleId = vehicleId;
            while (true) {
                connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;

                if (connectedVehicleId == 0) {
                    break;
                }

#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnStartPathFind({vehicleId}, {vehicleType}): overriding vehicle type for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
                OnStartPathFind(ref ExtVehicles[connectedVehicleId], ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId], vehicleType);
            }

            return ret;
        }

        public void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) != (Vehicle.Flags.Created | Vehicle.Flags.Spawned) ||
                (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnSpawnVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}, path: {vehicleData.m_path}");
#endif
                return;
            }
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnSpawnVehicle({vehicleId}): calling OnSpawn for vehicle {vehicleId}");
#endif

            ushort connectedVehicleId = vehicleId;
            while (connectedVehicleId != 0) {
                OnSpawn(ref ExtVehicles[connectedVehicleId], ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId]);
                connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
            }
        }

        public void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData) {
            ushort connectedVehicleId = vehicleId;
            while (connectedVehicleId != 0) {
                UpdateVehiclePosition(ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId], ref ExtVehicles[connectedVehicleId]);
                connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
            }
        }

        protected void UpdateVehiclePosition(ref Vehicle vehicleData, ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.UpdateVehiclePosition({extVehicle.vehicleId}) called");
#endif

            if (vehicleData.m_path == 0 || (vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0 ||
                (extVehicle.lastPathId == vehicleData.m_path && extVehicle.lastPathPositionIndex == vehicleData.m_pathPositionIndex)
                ) {
                return;
            }

            PathManager pathManager = Singleton<PathManager>.instance;
            IExtSegmentEndManager segmentEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            // update vehicle position for timed traffic lights and priority signs
            int coarsePathPosIndex = vehicleData.m_pathPositionIndex >> 1;
            PathUnit.Position curPathPos = pathManager.m_pathUnits.m_buffer[vehicleData.m_path].GetPosition(coarsePathPosIndex);
            PathUnit.Position nextPathPos = default(PathUnit.Position);
            pathManager.m_pathUnits.m_buffer[vehicleData.m_path].GetNextPosition(coarsePathPosIndex, out nextPathPos);
            bool startNode = IsTransitNodeCurStartNode(ref curPathPos, ref nextPathPos);
            UpdatePosition(ref extVehicle, ref vehicleData, ref segmentEndMan.ExtSegmentEnds[segmentEndMan.GetIndex(curPathPos.m_segment, startNode)], ref curPathPos, ref nextPathPos);
        }

        public void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
            if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None ||
                (vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) == 0) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnDespawnVehicle({vehicleId}): unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
#endif
                return;
            }

            ushort connectedVehicleId = vehicleId;
            while (connectedVehicleId != 0) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnDespawnVehicle({vehicleId}): calling OnDespawn for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
#endif
                OnDespawn(ref ExtVehicles[connectedVehicleId]);
                connectedVehicleId = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[connectedVehicleId].m_trailingVehicle;
            }
        }

        public void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnReleaseVehicle({vehicleId}) called.");
#endif
            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created ||
                (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnReleaseVehicle({vehicleId}): unhandled vehicle! flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
#endif
                return;
            }

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnReleaseVehicle({vehicleId}): calling OnRelease for vehicle {vehicleId}");
#endif

            OnRelease(ref ExtVehicles[vehicleId], ref vehicleData);
        }

        public void Unlink(ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.Unlink({extVehicle.vehicleId}) called: Unlinking vehicle from all segment ends\nstate:{extVehicle}");

            ushort prevSegmentId = extVehicle.currentSegmentId;
            bool prevStartNode = extVehicle.currentStartNode;
#endif
            extVehicle.lastPositionUpdate = Now();

            if (extVehicle.previousVehicleIdOnSegment != 0) {
                ExtVehicles[extVehicle.previousVehicleIdOnSegment].nextVehicleIdOnSegment = extVehicle.nextVehicleIdOnSegment;
            } else if (extVehicle.currentSegmentId != 0) {
                IExtSegmentEndManager segmentEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                int endIndex = segmentEndMan.GetIndex(extVehicle.currentSegmentId, extVehicle.currentStartNode);
                if (segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId == extVehicle.vehicleId) {
                    segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId = extVehicle.nextVehicleIdOnSegment;
                } else {
                    Log.Error($"ExtVehicleManager.Unlink({extVehicle.vehicleId}): Unexpected first vehicle on segment {extVehicle.currentSegmentId}: {segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId}");
                }
            }

            if (extVehicle.nextVehicleIdOnSegment != 0) {
                ExtVehicles[extVehicle.nextVehicleIdOnSegment].previousVehicleIdOnSegment = extVehicle.previousVehicleIdOnSegment;
            }

            extVehicle.nextVehicleIdOnSegment = 0;
            extVehicle.previousVehicleIdOnSegment = 0;

            extVehicle.currentSegmentId = 0;
            extVehicle.currentStartNode = false;
            extVehicle.currentLaneIndex = 0;

            extVehicle.lastPathId = 0;
            extVehicle.lastPathPositionIndex = 0;

#if DEBUG
            if (DebugSwitch.PedestrianPathfinding.Get()) {
                IExtSegmentEndManager segmentEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                Log._Debug($"ExtVehicleManager.Unlink({extVehicle.vehicleId}) finished: Unlinked vehicle from all segment ends\nstate:{extVehicle}\nold segment end vehicle chain: {ExtSegmentEndManager.Instance.GenerateVehicleChainDebugInfo(prevSegmentId, prevStartNode)}");
            }
#endif
        }

        /// <summary>
        /// Links the given vehicle to the given segment end.
        /// </summary>
        /// <param name="extVehicle">vehicle</param>
        /// <param name="end">ext. segment end</param>
        /// <param name="laneIndex">lane index</param>
        private void Link(ref ExtVehicle extVehicle, ref ExtSegmentEnd end, byte laneIndex) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.Link({extVehicle.vehicleId}) called: Linking vehicle to segment end {end}\nstate:{extVehicle}");
#endif
            extVehicle.currentSegmentId = end.segmentId;
            extVehicle.currentStartNode = end.startNode;
            extVehicle.currentLaneIndex = laneIndex;

            ushort oldFirstRegVehicleId = end.firstVehicleId;
            if (oldFirstRegVehicleId != 0) {
                ExtVehicles[oldFirstRegVehicleId].previousVehicleIdOnSegment = extVehicle.vehicleId;
                extVehicle.nextVehicleIdOnSegment = oldFirstRegVehicleId;
            }
            end.firstVehicleId = extVehicle.vehicleId;

#if DEBUG
            if (DebugSwitch.PedestrianPathfinding.Get())
                Log._Debug($"ExtVehicleManager.Link({extVehicle.vehicleId}) finished: Linked vehicle to segment end: {end}\nstate:{extVehicle}\nsegment end vehicle chain: {ExtSegmentEndManager.Instance.GenerateVehicleChainDebugInfo(extVehicle.currentSegmentId, extVehicle.currentStartNode)}");
#endif
        }

        public void OnCreate(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnCreate({extVehicle.vehicleId}) called: {extVehicle}");
#endif

            if ((extVehicle.flags & ExtVehicleFlags.Created) != ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnCreate({extVehicle.vehicleId}): Vehicle is already created.");
#endif
                OnRelease(ref extVehicle, ref vehicleData);
            }

            DetermineVehicleType(ref extVehicle, ref vehicleData);
            extVehicle.recklessDriver = false;
            extVehicle.flags = ExtVehicleFlags.Created;

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnCreate({extVehicle.vehicleId}) finished: {extVehicle}");
#endif
        }

        public API.Traffic.Enums.ExtVehicleType OnStartPathFind(
            ref ExtVehicle extVehicle,
            ref Vehicle vehicleData,
            API.Traffic.Enums.ExtVehicleType? vehicleType) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, {vehicleType}) called: {extVehicle}");
#endif

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, {vehicleType}): Vehicle has not yet been created.");
#endif
                OnCreate(ref extVehicle, ref vehicleData);
            }

            if (vehicleType != null) {
                extVehicle.vehicleType = (API.Traffic.Enums.ExtVehicleType)vehicleType;
            }

            extVehicle.recklessDriver = Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(extVehicle.vehicleId, ref vehicleData);

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, {vehicleType}) finished: {extVehicle}");
#endif

            StepRand(ref extVehicle, true);
            UpdateDynamicLaneSelectionParameters(ref extVehicle);

            return extVehicle.vehicleType;
        }

        public void OnSpawn(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}) called: {extVehicle}");
#endif

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}): Vehicle has not yet been created.");
#endif
                OnCreate(ref extVehicle, ref vehicleData);
            }

            Unlink(ref extVehicle);

            extVehicle.lastPathId = 0;
            extVehicle.lastPathPositionIndex = 0;
            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.recklessDriver = Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(extVehicle.vehicleId, ref vehicleData);
            StepRand(ref extVehicle, true);
            UpdateDynamicLaneSelectionParameters(ref extVehicle);

            try {
                extVehicle.totalLength = vehicleData.CalculateTotalLength(extVehicle.vehicleId);
            } catch (Exception
#if DEBUG
                    e
#endif
                ) {
                extVehicle.totalLength = 0;
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}): Error occurred while calculating total length: {e}\nstate: {extVehicle}");
#endif
                return;
            }

            extVehicle.flags |= ExtVehicleFlags.Spawned;

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}) finished: {extVehicle}");
#endif
        }

        public void UpdatePosition(ref ExtVehicle extVehicle, ref Vehicle vehicleData, ref ExtSegmentEnd segEnd, ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}) called: {extVehicle}");
#endif

            if ((extVehicle.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): Vehicle is not yet spawned.");
#endif
                OnSpawn(ref extVehicle, ref vehicleData);
            }

            if (extVehicle.nextSegmentId != nextPos.m_segment || extVehicle.nextLaneIndex != nextPos.m_lane) {
                extVehicle.nextSegmentId = nextPos.m_segment;
                extVehicle.nextLaneIndex = nextPos.m_lane;
            }

            bool startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
            //ISegmentEnd end = Constants.ManagerFactory.SegmentEndManager.GetSegmentEnd(curPos.m_segment, startNode);

            if (extVehicle.currentSegmentId != segEnd.segmentId || extVehicle.currentStartNode != segEnd.startNode || extVehicle.currentLaneIndex != curPos.m_lane) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): Current segment end changed. seg. {extVehicle.currentSegmentId}, start {extVehicle.currentStartNode}, lane {extVehicle.currentLaneIndex} -> seg. {segEnd.segmentId}, start {segEnd.startNode}, lane {curPos.m_lane}");
#endif

                if (extVehicle.currentSegmentId != 0) {
#if DEBUG
                    if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                        Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): Unlinking from current segment end");
#endif

                    Unlink(ref extVehicle);
                }

                extVehicle.lastPathId = vehicleData.m_path;
                extVehicle.lastPathPositionIndex = vehicleData.m_pathPositionIndex;

                extVehicle.waitTime = 0;

#if DEBUGVSTATE
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): Linking vehicle to segment end {segEnd.segmentId} @ {segEnd.startNode} ({segEnd.nodeId}). Current position: Seg. {curPos.m_segment}, lane {curPos.m_lane}, offset {curPos.m_offset} / Next position: Seg. {nextPos.m_segment}, lane {nextPos.m_lane}, offset {nextPos.m_offset}");
#endif
                if (segEnd.segmentId != 0) {
                    Link(ref extVehicle, ref segEnd, curPos.m_lane);
                }
                SetJunctionTransitState(ref extVehicle, VehicleJunctionTransitState.Approach);
            }
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}) finshed: {extVehicle}");
#endif
        }

        public void OnDespawn(ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId} called: {extVehicle}");
#endif
            if ((extVehicle.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId}): Vehicle is not spawned.");
#endif
                return;
            }

            Constants.ManagerFactory.ExtCitizenInstanceManager.ResetInstance(GetDriverInstanceId(extVehicle.vehicleId, ref Singleton<VehicleManager>.instance.m_vehicles.m_buffer[extVehicle.vehicleId]));

            Unlink(ref extVehicle);

            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.recklessDriver = false;
            extVehicle.nextSegmentId = 0;
            extVehicle.nextLaneIndex = 0;
            extVehicle.totalLength = 0;
            extVehicle.flags &= ExtVehicleFlags.Created;

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId}) finished: {extVehicle}");
#endif
        }

        public void OnRelease(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}) called: {extVehicle}");
#endif

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}): Vehicle is not created.");
#endif
                return;
            }

            if ((extVehicle.flags & ExtVehicleFlags.Spawned) != ExtVehicleFlags.None) {
#if DEBUG
                if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                    Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}): Vehicle is spawned.");
#endif
                OnDespawn(ref extVehicle);
            } else {
                Unlink(ref extVehicle);
            }

            extVehicle.lastTransitStateUpdate = 0;
            extVehicle.lastPositionUpdate = 0;
            extVehicle.waitTime = 0;
            extVehicle.flags = ExtVehicleFlags.None;
            extVehicle.vehicleType = API.Traffic.Enums.ExtVehicleType.None;
            extVehicle.heavyVehicle = false;
            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.junctionTransitState = VehicleJunctionTransitState.None;
            extVehicle.recklessDriver = false;

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}) finished: {extVehicle}");
#endif
        }

        public bool IsJunctionTransitStateNew(ref ExtVehicle extVehicle) {
            uint frame = Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
            return (extVehicle.lastTransitStateUpdate >> STATE_UPDATE_SHIFT) >= (frame >> STATE_UPDATE_SHIFT);
        }

        public uint GetStaticVehicleRand(ushort vehicleId) {
            return vehicleId % 100u;
        }

        public uint GetTimedVehicleRand(ushort vehicleId) {
            return (uint)((vehicleId % 2) * 50u + (ExtVehicles[vehicleId].timedRand >> 1));
        }

        public void StepRand(ref ExtVehicle extVehicle, bool force) {
            Randomizer rand = Constants.ServiceFactory.SimulationService.Randomizer;
            if (force || (rand.UInt32(GlobalConfig.Instance.Gameplay.VehicleTimedRandModulo) == 0)) {
                extVehicle.timedRand = Options.individualDrivingStyle ? (byte)rand.UInt32(100) : (byte)50;
            }
        }

        public void UpdateDynamicLaneSelectionParameters(ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"VehicleState.UpdateDynamicLaneSelectionParameters({extVehicle.vehicleId}) called.");
#endif

            if (!Options.IsDynamicLaneSelectionActive()) {
                extVehicle.dlsReady = false;
                return;
            }

            if (extVehicle.dlsReady) {
                return;
            }

            float egoism = (float)extVehicle.timedRand / 100f;
            float altruism = 1f - egoism;
            DynamicLaneSelection dls = GlobalConfig.Instance.DynamicLaneSelection;

            if (Options.individualDrivingStyle) {
                extVehicle.maxReservedSpace = extVehicle.recklessDriver
                                                  ? Mathf.Lerp(dls.MinMaxRecklessReservedSpace, dls.MaxMaxRecklessReservedSpace, altruism)
                                                  : Mathf.Lerp(dls.MinMaxReservedSpace, dls.MaxMaxReservedSpace, altruism);
                extVehicle.laneSpeedRandInterval = Mathf.Lerp(dls.MinLaneSpeedRandInterval, dls.MaxLaneSpeedRandInterval, egoism);
                extVehicle.maxOptLaneChanges = (int)Math.Round(Mathf.Lerp(dls.MinMaxOptLaneChanges, dls.MaxMaxOptLaneChanges + 1, egoism));
                extVehicle.maxUnsafeSpeedDiff = Mathf.Lerp(dls.MinMaxUnsafeSpeedDiff, dls.MaxMaxOptLaneChanges, egoism);
                extVehicle.minSafeSpeedImprovement = Mathf.Lerp(dls.MinMinSafeSpeedImprovement, dls.MaxMinSafeSpeedImprovement, altruism);
                extVehicle.minSafeTrafficImprovement = Mathf.Lerp(dls.MinMinSafeTrafficImprovement, dls.MaxMinSafeTrafficImprovement, altruism);
            } else {
                extVehicle.maxReservedSpace = extVehicle.recklessDriver ? dls.MaxRecklessReservedSpace : dls.MaxReservedSpace;
                extVehicle.laneSpeedRandInterval = dls.LaneSpeedRandInterval;
                extVehicle.maxOptLaneChanges = dls.MaxOptLaneChanges;
                extVehicle.maxUnsafeSpeedDiff = dls.MaxUnsafeSpeedDiff;
                extVehicle.minSafeSpeedImprovement = dls.MinSafeSpeedImprovement;
                extVehicle.minSafeTrafficImprovement = dls.MinSafeTrafficImprovement;
            }
            extVehicle.dlsReady = true;
        }

        private static ushort GetTransitNodeId(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
            bool startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
            ushort transitNodeId1 = 0;
            Constants.ServiceFactory.NetService.ProcessSegment(curPos.m_segment, delegate (ushort segmentId, ref NetSegment segment) {
                transitNodeId1 = startNode ? segment.m_startNode : segment.m_endNode;
                return true;
            });

            ushort transitNodeId2 = 0;
            Constants.ServiceFactory.NetService.ProcessSegment(nextPos.m_segment, delegate (ushort segmentId, ref NetSegment segment) {
                transitNodeId2 = startNode ? segment.m_startNode : segment.m_endNode;
                return true;
            });

            if (transitNodeId1 != transitNodeId2) {
                return 0;
            }
            return transitNodeId1;
        }

        private static bool IsTransitNodeCurStartNode(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
            // note: does not check if curPos and nextPos are successive path positions
            bool startNode;
            if (curPos.m_offset == 0) {
                startNode = true;
            } else if (curPos.m_offset == 255) {
                startNode = false;
            } else if (nextPos.m_offset == 0) {
                startNode = true;
            } else {
                startNode = false;
            }
            return startNode;
        }

        private static uint Now() {
            return Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
        }

        private void DetermineVehicleType(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
            VehicleAI ai = vehicleData.Info.m_vehicleAI;

            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                extVehicle.vehicleType = API.Traffic.Enums.ExtVehicleType.Emergency;
            } else {
                ExtVehicleType? type = DetermineVehicleTypeFromAIType(extVehicle.vehicleId, ai, false);
                if (type != null) {
                    extVehicle.vehicleType = (API.Traffic.Enums.ExtVehicleType)type;
                } else {
                    extVehicle.vehicleType = API.Traffic.Enums.ExtVehicleType.None;
                }
            }

            if (extVehicle.vehicleType == API.Traffic.Enums.ExtVehicleType.CargoTruck) {
                extVehicle.heavyVehicle = ((CargoTruckAI)ai).m_isHeavyVehicle;
            } else {
                extVehicle.heavyVehicle = false;
            }

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get())
                Log._Debug($"ExtVehicleManager.DetermineVehicleType({extVehicle.vehicleId}): vehicleType={extVehicle.vehicleType}, heavyVehicle={extVehicle.heavyVehicle}. Info={vehicleData.Info?.name}");
#endif
        }

        private ExtVehicleType? DetermineVehicleTypeFromAIType(ushort vehicleId, VehicleAI ai, bool emergencyOnDuty) {
            if (emergencyOnDuty)
                return ExtVehicleType.Emergency;

            switch (ai.m_info.m_vehicleType) {
                case VehicleInfo.VehicleType.Bicycle:
                    return ExtVehicleType.Bicycle;
                case VehicleInfo.VehicleType.Car:
                    if (ai is PassengerCarAI)
                        return ExtVehicleType.PassengerCar;
                    if (ai is AmbulanceAI || ai is FireTruckAI || ai is PoliceCarAI || ai is HearseAI || ai is GarbageTruckAI || ai is MaintenanceTruckAI || ai is SnowTruckAI || ai is WaterTruckAI || ai is DisasterResponseVehicleAI || ai is ParkMaintenanceVehicleAI || ai is PostVanAI) {
                        return ExtVehicleType.Service;
                    }
                    if (ai is CarTrailerAI)
                        return ExtVehicleType.None;
                    if (ai is BusAI)
                        return ExtVehicleType.Bus;
                    if (ai is TaxiAI)
                        return ExtVehicleType.Taxi;
                    if (ai is CargoTruckAI)
                        return ExtVehicleType.CargoTruck;
                    break;
                case VehicleInfo.VehicleType.Metro:
                case VehicleInfo.VehicleType.Train:
                case VehicleInfo.VehicleType.Monorail:
                    if (ai is CargoTrainAI)
                        return ExtVehicleType.CargoTrain;
                    return ExtVehicleType.PassengerTrain;
                case VehicleInfo.VehicleType.Tram:
                    return ExtVehicleType.Tram;
                case VehicleInfo.VehicleType.Ship:
                    if (ai is PassengerShipAI)
                        return ExtVehicleType.PassengerShip;
                    //if (ai is CargoShipAI)
                    return ExtVehicleType.CargoShip;
                //break;
                case VehicleInfo.VehicleType.Plane:
                    if (ai is PassengerPlaneAI)
                        return ExtVehicleType.PassengerPlane;
                    if (ai is CargoPlaneAI)
                        return ExtVehicleType.CargoPlane;
                    break;
                case VehicleInfo.VehicleType.Helicopter:
                    //if (ai is PassengerPlaneAI)
                    return ExtVehicleType.Helicopter;
                //break;
                case VehicleInfo.VehicleType.Ferry:
                    return ExtVehicleType.Ferry;
                case VehicleInfo.VehicleType.Blimp:
                    return ExtVehicleType.Blimp;
                case VehicleInfo.VehicleType.CableCar:
                    return ExtVehicleType.CableCar;
            }
#if DEBUGVSTATE
            Log._Debug($"ExtVehicleManager.DetermineVehicleType({vehicleId}): Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
#endif
            return null;
        }

        public void InitAllVehicles() {
            Log._Debug("ExtVehicleManager: InitAllVehicles()");
            VehicleManager vehicleManager = Singleton<VehicleManager>.instance;

            for (uint vehicleId = 0; vehicleId < Constants.ServiceFactory.VehicleService.MaxVehicleCount; ++vehicleId) {
                Services.VehicleService.ProcessVehicle((ushort)vehicleId, delegate (ushort vId, ref Vehicle vehicle) {
                    if ((vehicle.m_flags & Vehicle.Flags.Created) == 0) {
                        return true;
                    }

                    OnCreateVehicle(vId, ref vehicle);

                    if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
                        OnStartPathFind(vId, ref vehicle, ExtVehicleType.Emergency);
                    }

                    if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
                        return true;
                    }

                    OnSpawnVehicle(vId, ref vehicle);

                    return true;
                });
            }
        }

        public ushort GetFrontVehicleId(ushort vehicleId, ref Vehicle vehicleData) {
            bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
            ushort frontVehicleId = vehicleId;
            if (reversed) {
                frontVehicleId = vehicleData.GetLastVehicle(vehicleId);
            } else {
                frontVehicleId = vehicleData.GetFirstVehicle(vehicleId);
            }

            return frontVehicleId;
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            for (int i = 0; i < ExtVehicles.Length; ++i) {
                Services.VehicleService.ProcessVehicle((ushort)i, (ushort vehId, ref Vehicle veh) => {
                    OnRelease(ref ExtVehicles[i], ref veh);
                    return true;
                });
            }
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            InitAllVehicles();
        }
    }
}
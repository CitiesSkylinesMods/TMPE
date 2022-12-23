namespace TrafficManager.Manager.Impl {
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Patch._CitizenAI._ResidentAI.Connection;
    using TrafficManager.Patch._CitizenAI._TouristAI.Connection;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using UnityEngine;
    using TrafficManager.Util.Extensions;

    public class ExtVehicleManager
        : AbstractCustomManager,
          IExtVehicleManager
    {
        static ExtVehicleManager() {
            Instance = new ExtVehicleManager();
        }

        private ExtVehicleManager() {
            var maxVehicleCount = VehicleManager.instance.m_vehicles.m_buffer.Length;
            ExtVehicles = new ExtVehicle[maxVehicleCount];
            for (uint i = 0; i < maxVehicleCount; ++i) {
                ExtVehicles[i] = new ExtVehicle((ushort)i);
            }

            _touristElectricCarProbability = GameConnectionManager.Instance.TouristAIConnection.GetElectricCarProbability;
            _residentElectricCarProbability = GameConnectionManager.Instance.ResidentAIConnection.GetElectricCarProbability;
        }

        public static readonly ExtVehicleManager Instance = new ExtVehicleManager();

        private readonly GetElectricCarProbabilityDelegate _touristElectricCarProbability;
        private readonly GetElectricCarProbabilityResidentDelegate _residentElectricCarProbability;
        private const int STATE_UPDATE_SHIFT = 6;
        public const int JUNCTION_RECHECK_SHIFT = 4;

        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro |
            VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus;

        /// <summary>
        /// Known vehicles and their current known positions. Index: vehicle id
        /// </summary>
        public ExtVehicle[] ExtVehicles { get; }

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

        public void SetJunctionTransitState(ref ExtVehicle extVehicle,
                                            VehicleJunctionTransitState transitState) {
            if (transitState != extVehicle.junctionTransitState) {
                extVehicle.junctionTransitState = transitState;
                extVehicle.lastTransitStateUpdate = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            }
        }

        /// <summary>
        /// Gets the driver from a vehicle.
        /// See PassengerCarAI.GetDriverInstance - almost stock code.
        /// </summary>
        /// <param name="vehicleId">Id of the vehicle.</param>
        /// <param name="data">Vehicle data of the vehicle.</param>
        /// <returns>CitizenInstanceId of the driver.</returns>
        public ushort GetDriverInstanceId(ushort vehicleId, ref Vehicle data) {
            CitizenManager citizenManager = Singleton<CitizenManager>.instance;
            CitizenUnit[] citizenUnitsBuf = citizenManager.m_units.m_buffer;
            Citizen[] citizensBuf = citizenManager.m_citizens.m_buffer;
            uint citizenUnitId = data.m_citizenUnits;
            uint maxUnitCount = citizenManager.m_units.m_size;
            int numIter = 0;

            while (citizenUnitId != 0) {
                ref CitizenUnit citizenUnit = ref citizenUnitsBuf[citizenUnitId];
                for (int i = 0; i < 5; i++) {
                    uint citizenId = citizenUnit.GetCitizen(i);

                    if (citizenId != 0) {
                        ushort citizenInstanceId = citizensBuf[citizenId].m_instance;
                        if (citizenInstanceId != 0) {
                            return citizenInstanceId;
                        }
                    }
                }

                citizenUnitId = citizenUnit.m_nextUnit;
                if (++numIter > maxUnitCount) {
                    CODebugBase<LogChannel>.Error(
                        LogChannel.Core,
                        "Invalid list detected!\n" + Environment.StackTrace);
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

            if (SavedGameOptions.Instance.advancedAI) {
                TrafficMeasurementManager.Instance.AddTraffic(
                    extVehicle.currentSegmentId,
                    extVehicle.currentLaneIndex,
#if MEASUREDENSITY
                    length,
#endif
                    (ushort)vehicle.GetLastFrameVelocity().magnitude);
            }
        }

        public void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData) {
            OnReleaseVehicle(vehicleId, ref vehicleData);

#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) !=
                Vehicle.Flags.Created || (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) ==
                VehicleInfo.VehicleType.None) {

                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnCreateVehicle({vehicleId}): unhandled vehicle! " +
                        $"flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
                }

                return;
            }

            if (logVehicleLinking) {
                Log._Debug(
                    $"ExtVehicleManager.OnCreateVehicle({vehicleId}): calling OnCreate for " +
                    $"vehicle {vehicleId}");
            }

            OnCreate(ref ExtVehicles[vehicleId], ref vehicleData);
        }

        public ExtVehicleType OnStartPathFind(ushort vehicleId,
                                              ref Vehicle vehicleData,
                                              ExtVehicleType? vehicleType) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None ||
                (vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) !=
                Vehicle.Flags.Created)
            {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnStartPathFind({vehicleId}, {vehicleType}): " +
                        $"unhandled vehicle! type: {vehicleData.Info.m_vehicleType}");
                }

                return ExtVehicleType.None;
            }

            ExtVehicleType ret = OnStartPathFind(
                ref ExtVehicles[vehicleId],
                ref vehicleData,
                vehicleType);

            ushort connectedVehicleId = vehicleId;
            while (true) {
                connectedVehicleId = connectedVehicleId.ToVehicle().m_trailingVehicle;

                if (connectedVehicleId == 0) {
                    break;
                }

                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnStartPathFind({vehicleId}, {vehicleType}): " +
                        $"overriding vehicle type for connected vehicle {connectedVehicleId} of " +
                        $"vehicle {vehicleId} (trailing)");
                }

                OnStartPathFind(
                    ref ExtVehicles[connectedVehicleId],
                    ref connectedVehicleId.ToVehicle(),
                    vehicleType);
            }

            if (vehicleType == ExtVehicleType.None)
                Log._DebugOnlyWarning($"Vehicle {vehicleId} does not have a valid vehicle type!");

            return ret;
        }

        public void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) !=
                (Vehicle.Flags.Created | Vehicle.Flags.Spawned) ||
                (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None)
            {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnSpawnVehicle({vehicleId}): unhandled vehicle! " +
                        $"flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}, " +
                        $"path: {vehicleData.m_path}");
                }

                return;
            }

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnSpawnVehicle({vehicleId}): calling OnSpawn for " +
                           $"vehicle {vehicleId}");
            }

            ushort connectedVehicleId = vehicleId;

            while (connectedVehicleId != 0) {
                ref Vehicle connectedVehicle = ref connectedVehicleId.ToVehicle();

                OnSpawn(
                    ref ExtVehicles[connectedVehicleId],
                    ref connectedVehicleId.ToVehicle());

                connectedVehicleId = connectedVehicle.m_trailingVehicle;
            }
        }

        public void UpdateVehiclePosition(ushort vehicleId, ref Vehicle vehicleData) {
            ushort connectedVehicleId = vehicleId;
            while (connectedVehicleId != 0) {
                UpdateVehiclePosition(
                    ref connectedVehicleId.ToVehicle(),
                    ref ExtVehicles[connectedVehicleId]);
                connectedVehicleId = connectedVehicleId.ToVehicle().m_trailingVehicle;
            }
        }

        private void UpdateVehiclePosition(ref Vehicle vehicleData, ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get()) {
                Log._Debug($"ExtVehicleManager.UpdateVehiclePosition({extVehicle.vehicleId}) called");
            }
#endif

            if (vehicleData.m_path == 0 || (vehicleData.m_flags & Vehicle.Flags.WaitingPath) != 0 ||
                (extVehicle.lastPathId == vehicleData.m_path
                 && extVehicle.lastPathPositionIndex == vehicleData.m_pathPositionIndex)) {
                return;
            }

            PathManager pathManager = Singleton<PathManager>.instance;
            IExtSegmentEndManager segmentEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            // update vehicle position for timed traffic lights and priority signs
            int coarsePathPosIndex = vehicleData.m_pathPositionIndex >> 1;
            PathUnit.Position curPathPos = pathManager
                                           .m_pathUnits.m_buffer[vehicleData.m_path]
                                           .GetPosition(coarsePathPosIndex);

            pathManager.m_pathUnits.m_buffer[vehicleData.m_path]
                       .GetNextPosition(coarsePathPosIndex, out PathUnit.Position nextPathPos);

            bool startNode = IsTransitNodeCurStartNode(ref curPathPos, ref nextPathPos);
            UpdatePosition(
                ref extVehicle,
                ref vehicleData,
                ref segmentEndMan.ExtSegmentEnds[
                    segmentEndMan.GetIndex(curPathPos.m_segment, startNode)],
                ref curPathPos,
                ref nextPathPos);
        }

        public void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if ((vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None
                || (vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Spawned)) == 0) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnDespawnVehicle({vehicleId}): unhandled vehicle! " +
                        $"type: {vehicleData.Info.m_vehicleType}");
                }

                return;
            }

            ushort connectedVehicleId = vehicleId;
            while (connectedVehicleId != 0) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnDespawnVehicle({vehicleId}): calling OnDespawn " +
                        $"for connected vehicle {connectedVehicleId} of vehicle {vehicleId} (trailing)");
                }

                OnDespawn(ref ExtVehicles[connectedVehicleId]);

                connectedVehicleId = connectedVehicleId.ToVehicle().m_trailingVehicle;
            }
        }

        public void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnReleaseVehicle({vehicleId}) called.");
            }

            if ((vehicleData.m_flags & (Vehicle.Flags.Created | Vehicle.Flags.Deleted)) != Vehicle.Flags.Created
                || (vehicleData.Info.m_vehicleType & VEHICLE_TYPES) == VehicleInfo.VehicleType.None)
            {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnReleaseVehicle({vehicleId}): unhandled vehicle! " +
                        $"flags: {vehicleData.m_flags}, type: {vehicleData.Info.m_vehicleType}");
                }

                return;
            }

            if (logVehicleLinking) {
                Log._Debug(
                    $"ExtVehicleManager.OnReleaseVehicle({vehicleId}): calling OnRelease for " +
                    $"vehicle {vehicleId}");
            }

            OnRelease(ref ExtVehicles[vehicleId], ref vehicleData);
        }

        public void Unlink(ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get()) {
                Log._Debug(
                    $"ExtVehicleManager.Unlink({extVehicle.vehicleId}) called: Unlinking vehicle " +
                    $"from all segment ends\nstate:{extVehicle}");
            }

            ushort prevSegmentId = extVehicle.currentSegmentId;
            bool prevStartNode = extVehicle.currentStartNode;
#endif
            extVehicle.lastPositionUpdate = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            if (extVehicle.previousVehicleIdOnSegment != 0) {
                ExtVehicles[extVehicle.previousVehicleIdOnSegment].nextVehicleIdOnSegment =
                    extVehicle.nextVehicleIdOnSegment;
            } else if (extVehicle.currentSegmentId != 0) {
                IExtSegmentEndManager segmentEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
                int endIndex = segmentEndMan.GetIndex(
                    extVehicle.currentSegmentId,
                    extVehicle.currentStartNode);
                if (segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId == extVehicle.vehicleId) {
                    segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId =
                        extVehicle.nextVehicleIdOnSegment;
                } else {
                    Log.Error(
                        $"ExtVehicleManager.Unlink({extVehicle.vehicleId}): Unexpected first " +
                        $"vehicle on segment {extVehicle.currentSegmentId}: " +
                        $"{segmentEndMan.ExtSegmentEnds[endIndex].firstVehicleId}");
                }
            }

            if (extVehicle.nextVehicleIdOnSegment != 0) {
                ExtVehicles[extVehicle.nextVehicleIdOnSegment].previousVehicleIdOnSegment =
                    extVehicle.previousVehicleIdOnSegment;
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
                string vehicleChainDebugInfo =
                    ExtSegmentEndManager.Instance.GenerateVehicleChainDebugInfo(
                        prevSegmentId,
                        prevStartNode);
                Log._Debug(
                    $"ExtVehicleManager.Unlink({extVehicle.vehicleId}) finished: Unlinked vehicle " +
                    $"from all segment ends\nstate:{extVehicle}\nold segment end vehicle chain: " +
                    vehicleChainDebugInfo);
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
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get()) {
                Log._Debug(
                    $"ExtVehicleManager.Link({extVehicle.vehicleId}) called: Linking vehicle to " +
                    $"segment end {end}\nstate:{extVehicle}");
            }
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
            if (DebugSwitch.PedestrianPathfinding.Get()) {
                string vehicleChainDebugInfo =
                    ExtSegmentEndManager.Instance.GenerateVehicleChainDebugInfo(
                        extVehicle.currentSegmentId,
                        extVehicle.currentStartNode);
                Log._Debug(
                    $"ExtVehicleManager.Link({extVehicle.vehicleId}) finished: Linked vehicle " +
                    $"to segment end: {end}\nstate:{extVehicle}\nsegment end vehicle chain: " +
                    vehicleChainDebugInfo);
            }
#endif
        }

        public void OnCreate(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif
            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnCreate({extVehicle.vehicleId}) called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Created) != ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnCreate({extVehicle.vehicleId}): Vehicle is already created.");
                }

                OnRelease(ref extVehicle, ref vehicleData);
            }

            DetermineVehicleType(ref extVehicle, ref vehicleData);
            extVehicle.recklessDriver = false;
            extVehicle.flags = ExtVehicleFlags.Created;

            if (logVehicleLinking) {
                Log._Debug(
                    $"ExtVehicleManager.OnCreate({extVehicle.vehicleId}) finished: {extVehicle}");
            }
        }

        public ExtVehicleType OnStartPathFind(ref ExtVehicle extVehicle,
                                              ref Vehicle vehicleData,
                                              ExtVehicleType? vehicleType) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if (logVehicleLinking) {
                Log._Debug(
                    $"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, " +
                    $"{vehicleType}) called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, {vehicleType}): " +
                        "Vehicle has not yet been created.");
                }

                OnCreate(ref extVehicle, ref vehicleData);
            }

            if (vehicleType != null) {
                extVehicle.vehicleType = (ExtVehicleType)vehicleType;
            }

            extVehicle.recklessDriver =
                Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(
                    extVehicle.vehicleId,
                    ref vehicleData);

            if (logVehicleLinking) {
                Log._Debug(
                    $"ExtVehicleManager.OnStartPathFind({extVehicle.vehicleId}, {vehicleType}) " +
                    $"finished: {extVehicle}");
            }

            StepRand(ref extVehicle, true);
            UpdateDynamicLaneSelectionParameters(ref extVehicle);

            return extVehicle.vehicleType;
        }

        public void OnSpawn(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif
            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}) called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}): " +
                        "Vehicle has not yet been created.");
                }

                OnCreate(ref extVehicle, ref vehicleData);
            }

            Unlink(ref extVehicle);

            extVehicle.lastPathId = 0;
            extVehicle.lastPathPositionIndex = 0;
            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.recklessDriver =
                Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(
                    extVehicle.vehicleId,
                    ref vehicleData);

            StepRand(ref extVehicle, true);
            UpdateDynamicLaneSelectionParameters(ref extVehicle);

            try {
                extVehicle.totalLength = vehicleData.CalculateTotalLength(extVehicle.vehicleId);
            } catch (Exception e) {
                extVehicle.totalLength = 0;

                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}): " +
                        $"Error occurred while calculating total length: {e}\nstate: {extVehicle}");
                }

                return;
            }

            extVehicle.flags |= ExtVehicleFlags.Spawned;

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnSpawn({extVehicle.vehicleId}) finished: {extVehicle}");
            }
        }

        public void UpdatePosition(ref ExtVehicle extVehicle,
                                   ref Vehicle vehicleData,
                                   ref ExtSegmentEnd segEnd,
                                   ref PathUnit.Position curPos,
                                   ref PathUnit.Position nextPos) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif
            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}) called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): Vehicle is not yet spawned.");
                }

                OnSpawn(ref extVehicle, ref vehicleData);
            }

            if (extVehicle.nextSegmentId != nextPos.m_segment
                || extVehicle.nextLaneIndex != nextPos.m_lane)
            {
                extVehicle.nextSegmentId = nextPos.m_segment;
                extVehicle.nextLaneIndex = nextPos.m_lane;
            }

            if (extVehicle.currentSegmentId != segEnd.segmentId ||
                extVehicle.currentStartNode != segEnd.startNode ||
                extVehicle.currentLaneIndex != curPos.m_lane) {
                if (logVehicleLinking) {
                    Log._Debug(
                        $"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): " +
                        $"Current segment end changed. seg. {extVehicle.currentSegmentId}, " +
                        $"start {extVehicle.currentStartNode}, lane {extVehicle.currentLaneIndex} -> " +
                        $"seg. {segEnd.segmentId}, start {segEnd.startNode}, lane {curPos.m_lane}");
                }

                if (extVehicle.currentSegmentId != 0) {
                    if (logVehicleLinking) {
                        Log._Debug(
                            $"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}): " +
                            "Unlinking from current segment end");
                    }

                    Unlink(ref extVehicle);
                }

                extVehicle.lastPathId = vehicleData.m_path;
                extVehicle.lastPathPositionIndex = vehicleData.m_pathPositionIndex;

                extVehicle.waitTime = 0;

#if DEBUGVSTATE
                if (logVehicleLinking) {
                    Log._DebugFormat(
                        "ExtVehicleManager.UpdatePosition({0}): Linking vehicle to segment end {1} " +
                        "@ {2} ({3}). Current position: Seg. {4}, lane {5}, offset {6} / " +
                        "Next position: Seg. {7}, lane {8}, offset {9}",
                        extVehicle.vehicleId, segEnd.segmentId, segEnd.startNode, segEnd.nodeId,
                        curPos.m_segment, curPos.m_lane, curPos.m_offset, nextPos.m_segment,
                        nextPos.m_lane, nextPos.m_offset);
                }
#endif
                if (segEnd.segmentId != 0) {
                    Link(ref extVehicle, ref segEnd, curPos.m_lane);
                }

                SetJunctionTransitState(ref extVehicle, VehicleJunctionTransitState.Approach);
            }

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.UpdatePosition({extVehicle.vehicleId}) finished: {extVehicle}");
            }
        }

        public void OnDespawn(ref ExtVehicle extVehicle) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId} called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Spawned) == ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId}): Vehicle is not spawned.");
                }

                return;
            }

            Constants.ManagerFactory.ExtCitizenInstanceManager.ResetInstance(
                GetDriverInstanceId(
                    extVehicle.vehicleId,
                    ref extVehicle.vehicleId.ToVehicle()));

            Unlink(ref extVehicle);

            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.recklessDriver = false;
            extVehicle.nextSegmentId = 0;
            extVehicle.nextLaneIndex = 0;
            extVehicle.totalLength = 0;
            extVehicle.flags &= ExtVehicleFlags.Created;

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnDespawn({extVehicle.vehicleId}) finished: {extVehicle}");
            }
        }

        public void OnRelease(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
#if DEBUG
            bool logVehicleLinking = DebugSwitch.VehicleLinkingToSegmentEnd.Get();
#else
            const bool logVehicleLinking = false;
#endif

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}) called: {extVehicle}");
            }

            if ((extVehicle.flags & ExtVehicleFlags.Created) == ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}): Vehicle is not created.");
                }

                return;
            }

            if ((extVehicle.flags & ExtVehicleFlags.Spawned) != ExtVehicleFlags.None) {
                if (logVehicleLinking) {
                    Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}): Vehicle is spawned.");
                }

                OnDespawn(ref extVehicle);
            } else {
                Unlink(ref extVehicle);
            }

            extVehicle.lastTransitStateUpdate = 0;
            extVehicle.lastPositionUpdate = 0;
            extVehicle.waitTime = 0;
            extVehicle.flags = ExtVehicleFlags.None;
            extVehicle.vehicleType = ExtVehicleType.None;
            extVehicle.heavyVehicle = false;
            extVehicle.lastAltLaneSelSegmentId = 0;
            extVehicle.junctionTransitState = VehicleJunctionTransitState.None;
            extVehicle.recklessDriver = false;

            if (logVehicleLinking) {
                Log._Debug($"ExtVehicleManager.OnRelease({extVehicle.vehicleId}) finished: {extVehicle}");
            }
        }

        public bool IsJunctionTransitStateNew(ref ExtVehicle extVehicle) {
            uint frame = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            return (extVehicle.lastTransitStateUpdate >> STATE_UPDATE_SHIFT) >=
                   (frame >> STATE_UPDATE_SHIFT);
        }

        public uint GetStaticVehicleRand(ushort vehicleId) {
            return vehicleId % 100u;
        }

        public uint GetTimedVehicleRand(ushort vehicleId) {
            return (uint)((vehicleId % 2) * 50u + (ExtVehicles[vehicleId].timedRand >> 1));
        }

        public void StepRand(ref ExtVehicle extVehicle, bool force) {
            Randomizer rand = Singleton<SimulationManager>.instance.m_randomizer;
            if (force
                || (rand.UInt32(GlobalConfig.Instance.Gameplay.VehicleTimedRandModulo) == 0))
            {
                extVehicle.timedRand = SavedGameOptions.Instance.individualDrivingStyle
                                           ? (byte)rand.UInt32(100)
                                           : (byte)50;
            }
        }

        public void UpdateDynamicLaneSelectionParameters(ref ExtVehicle extVehicle) {
#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get()) {
                Log._Debug("VehicleState.UpdateDynamicLaneSelectionParameters" +
                           $"({extVehicle.vehicleId}) called.");
            }
#endif

            if (!SavedGameOptions.Instance.IsDynamicLaneSelectionActive()) {
                extVehicle.dlsReady = false;
                return;
            }

            if (extVehicle.dlsReady) {
                return;
            }

            float egoism = extVehicle.timedRand / 100f;
            float altruism = 1f - egoism;
            DynamicLaneSelection dls = GlobalConfig.Instance.DynamicLaneSelection;

            if (SavedGameOptions.Instance.individualDrivingStyle) {
                extVehicle.maxReservedSpace
                    = extVehicle.recklessDriver
                          ? Mathf.Lerp(
                              dls.MinMaxRecklessReservedSpace,
                              dls.MaxMaxRecklessReservedSpace,
                              altruism)
                          : Mathf.Lerp(dls.MinMaxReservedSpace, dls.MaxMaxReservedSpace, altruism);
                extVehicle.laneSpeedRandInterval = Mathf.Lerp(
                    dls.MinLaneSpeedRandInterval,
                    dls.MaxLaneSpeedRandInterval,
                    egoism);
                extVehicle.maxOptLaneChanges = (int)Math.Round(
                    Mathf.Lerp(dls.MinMaxOptLaneChanges, dls.MaxMaxOptLaneChanges + 1, egoism));
                extVehicle.maxUnsafeSpeedDiff = Mathf.Lerp(
                    dls.MinMaxUnsafeSpeedDiff,
                    dls.MaxMaxUnsafeSpeedDiff,
                    egoism);
                extVehicle.minSafeSpeedImprovement = Mathf.Lerp(
                    dls.MinMinSafeSpeedImprovement.GameUnits,
                    dls.MaxMinSafeSpeedImprovement.GameUnits,
                    altruism);
                extVehicle.minSafeTrafficImprovement = Mathf.Lerp(
                    dls.MinMinSafeTrafficImprovement,
                    dls.MaxMinSafeTrafficImprovement,
                    altruism);
            } else {
                extVehicle.maxReservedSpace = extVehicle.recklessDriver
                                                  ? dls.MaxRecklessReservedSpace
                                                  : dls.MaxReservedSpace;
                extVehicle.laneSpeedRandInterval = dls.LaneSpeedRandInterval;
                extVehicle.maxOptLaneChanges = dls.MaxOptLaneChanges;
                extVehicle.maxUnsafeSpeedDiff = dls.MaxUnsafeSpeedDiff;
                extVehicle.minSafeSpeedImprovement = dls.MinSafeSpeedImprovement;
                extVehicle.minSafeTrafficImprovement = dls.MinSafeTrafficImprovement;
            }

            extVehicle.dlsReady = true;
        }

        // [UsedImplicitly]
        private static ushort GetTransitNodeId(ref PathUnit.Position curPos,
                                               ref PathUnit.Position nextPos) {
            bool startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);

            ref NetSegment curPosSegment = ref curPos.m_segment.ToSegment();
            var transitNodeId1 = startNode
                ? curPosSegment.m_startNode
                : curPosSegment.m_endNode;

            ref NetSegment nextPosSegment = ref nextPos.m_segment.ToSegment();
            var transitNodeId2 = startNode
                ? nextPosSegment.m_startNode
                : nextPosSegment.m_endNode;

            return transitNodeId1 != transitNodeId2
                ? (ushort)0
                : transitNodeId1;
        }

        private static bool IsTransitNodeCurStartNode(ref PathUnit.Position curPos,
                                                      ref PathUnit.Position nextPos) {
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

        private void DetermineVehicleType(ref ExtVehicle extVehicle, ref Vehicle vehicleData) {
            VehicleAI ai = vehicleData.Info.m_vehicleAI;

            if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                extVehicle.vehicleType = ExtVehicleType.Emergency;
            } else {
                ExtVehicleType? type = DetermineVehicleTypeFromAIType(
                    extVehicle.vehicleId,
                    ai,
                    false);
                if (type != null) {
                    extVehicle.vehicleType = (ExtVehicleType)type;
                } else {
                    extVehicle.vehicleType = ExtVehicleType.None;
                }
            }

            if (extVehicle.vehicleType == ExtVehicleType.CargoTruck) {
                extVehicle.heavyVehicle = ((CargoTruckAI)ai).m_isHeavyVehicle;
            } else {
                extVehicle.heavyVehicle = false;
            }

#if DEBUG
            if (DebugSwitch.VehicleLinkingToSegmentEnd.Get()) {
                Log._Debug(
                    $"ExtVehicleManager.DetermineVehicleType({extVehicle.vehicleId}): " +
                    $"vehicleType={extVehicle.vehicleType}, heavyVehicle={extVehicle.heavyVehicle}. " +
                    $"Info={vehicleData.Info?.name}");
            }
#endif
        }

        internal ExtVehicleType? DetermineVehicleTypeFromAIType(
            ushort vehicleId,
            VehicleAI ai,
            bool emergencyOnDuty)
        {
            if (emergencyOnDuty) {
                return ExtVehicleType.Emergency;
            }

            switch (ai.m_info.m_vehicleType) {
                case VehicleInfo.VehicleType.Bicycle: {
                    return ExtVehicleType.Bicycle;
                }

                case VehicleInfo.VehicleType.Car: {
                    switch (ai) {
                        case PassengerCarAI _:
                            return ExtVehicleType.PassengerCar;
                        case AmbulanceAI _:
                        case BankVanAI _:
                        case FireTruckAI _:
                        case PoliceCarAI _:
                        case HearseAI _:
                        case GarbageTruckAI _:
                        case MaintenanceTruckAI _:
                        case SnowTruckAI _:
                        case WaterTruckAI _:
                        case DisasterResponseVehicleAI _:
                        case ParkMaintenanceVehicleAI _:
                        case PostVanAI _:
                            return ExtVehicleType.Service;
                        case CarTrailerAI _:
                            return ExtVehicleType.None;
                        case BusAI _:
                            return ExtVehicleType.Bus;
                        case TaxiAI _:
                            return ExtVehicleType.Taxi;
                        case CargoTruckAI _:
                            return ExtVehicleType.CargoTruck;
                    }

                    break;
                }

                case VehicleInfo.VehicleType.Metro:
                case VehicleInfo.VehicleType.Train:
                case VehicleInfo.VehicleType.Monorail: {
                    return ai is CargoTrainAI
                               ? ExtVehicleType.CargoTrain
                               : ExtVehicleType.PassengerTrain;
                }

                case VehicleInfo.VehicleType.Tram: {
                    return ExtVehicleType.Tram;
                }

                case VehicleInfo.VehicleType.Ship: {
                    return ai is PassengerShipAI
                               ? ExtVehicleType.PassengerShip
                               : ExtVehicleType.CargoShip;
                }

                case VehicleInfo.VehicleType.Plane: {
                    switch (ai) {
                        case PassengerPlaneAI _:
                            return ExtVehicleType.PassengerPlane;
                        case CargoPlaneAI _:
                            return ExtVehicleType.CargoPlane;
                    }

                    break;
                }

                case VehicleInfo.VehicleType.Helicopter: {
                    return ExtVehicleType.Helicopter;
                }

                case VehicleInfo.VehicleType.Ferry: {
                    return ExtVehicleType.Ferry;
                }

                case VehicleInfo.VehicleType.Blimp: {
                    return ExtVehicleType.Blimp;
                }

                case VehicleInfo.VehicleType.CableCar: {
                    return ExtVehicleType.CableCar;
                }

                case VehicleInfo.VehicleType.Trolleybus: {
                    return ExtVehicleType.Trolleybus;
                }
            }

#if DEBUGVSTATE
            Log._Debug(
                $"ExtVehicleManager.DetermineVehicleType({vehicleId}): Could not determine " +
                $"vehicle type from ai type: {ai.GetType()}");
#endif
            return null;
        }

        private void InitAllVehicles() {
            Log._Debug("ExtVehicleManager: InitAllVehicles()");

            var maxVehicleCount = VehicleManager.instance.m_vehicles.m_buffer.Length;

            for (uint vehicleId = 0;
                 vehicleId < maxVehicleCount;
                 ++vehicleId) {

                ushort vId = (ushort)vehicleId;
                ref Vehicle vehicle = ref vId.ToVehicle();

                if (!vehicle.IsCreated()) {
                    continue;
                }

                OnCreateVehicle(vId, ref vehicle);

                if ((vehicle.m_flags & Vehicle.Flags.Emergency2) != 0) {
                    OnStartPathFind(vId, ref vehicle, ExtVehicleType.Emergency);
                }

                if ((vehicle.m_flags & Vehicle.Flags.Spawned) == 0) {
                    continue;
                }

                OnSpawnVehicle(vId, ref vehicle);
            }
        }

        /// <summary>
        /// Converts game VehicleInfo.VehicleType to closest TMPE.API.Traffic.Enums.ExtVehicleType
        /// </summary>
        /// <param name="vehicleType"></param>
        /// <param name="vehicleCategory"></param>
        /// <returns></returns>
        public static ExtVehicleType ConvertToExtVehicleType(VehicleInfo.VehicleType vehicleType, VehicleInfo.VehicleCategory vehicleCategory) {
            ExtVehicleType extVehicleType = ExtVehicleType.None;
            if ((vehicleType & VehicleInfo.VehicleType.Car) != VehicleInfo.VehicleType.None) {
                extVehicleType = vehicleCategory.MapCarVehicleCategoryToExtVehicle();
            } else if ((vehicleType & (VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Metro |
                                VehicleInfo.VehicleType.Monorail)) !=
                VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerTrain;
            } else if ((vehicleType & VehicleInfo.VehicleType.Tram) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Tram;
            } else if ((vehicleType & VehicleInfo.VehicleType.Ship) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerShip;
            } else if ((vehicleType & VehicleInfo.VehicleType.Plane) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.PassengerPlane;
            } else if ((vehicleType & VehicleInfo.VehicleType.Ferry) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Ferry;
            } else if ((vehicleType & VehicleInfo.VehicleType.Blimp) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Blimp;
            } else if ((vehicleType & VehicleInfo.VehicleType.CableCar) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.CableCar;
            } else if ((vehicleType & VehicleInfo.VehicleType.Trolleybus) != VehicleInfo.VehicleType.None) {
                extVehicleType = ExtVehicleType.Trolleybus;
            }

            return extVehicleType;
        }

        /// <summary>
        /// Determine if vehicle has to be swapped with electric
        /// </summary>
        /// <param name="currentVehicleInfo">current parked vehicle Info</param>
        /// <param name="homeId">home id</param>
        /// <returns>true if vehicle swap has to be performed otherwise false</returns>
        internal static bool MustSwapParkedCarWithElectric(VehicleInfo currentVehicleInfo, ushort homeId) {
            if (currentVehicleInfo.m_class.m_subService == ItemClass.SubService.ResidentialLowEco) {
                return false;
            }

            Vector3 position = Singleton<BuildingManager>.instance.m_buildings.m_buffer[homeId].m_position;
            DistrictManager districtManager = Singleton<DistrictManager>.instance;
            byte district = districtManager.GetDistrict(position);
            DistrictPolicies.CityPlanning cityPlanningPolicies = districtManager.m_districts.m_buffer[district].m_cityPlanningPolicies;
            // force electric cars in district
            return (cityPlanningPolicies & DistrictPolicies.CityPlanning.ElectricCars) != 0;
        }

        [UsedImplicitly]
        public ushort GetFrontVehicleId(ushort vehicleId, ref Vehicle vehicleData) {
            bool reversed = (vehicleData.m_flags & Vehicle.Flags.Reversed) != 0;
            return reversed
                       ? vehicleData.GetLastVehicle(vehicleId)
                       : vehicleData.GetFirstVehicle(vehicleId);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < ExtVehicles.Length; ++i) {
                ushort vehicleId = (ushort)i;
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                OnRelease(ref ExtVehicles[i], ref vehicle);
            }
        }

        public override void OnAfterLoadData() {
            base.OnAfterLoadData();
            InitAllVehicles();
        }

        /// <summary>
        /// Checks if citizen should use electric car
        /// </summary>
        /// <param name="citizen">Citizen data</param>
        /// <param name="citizenInstance">CitizenInstance data (spawned instance)</param>
        /// <returns>true if should use electric car otherwise false</returns>
        internal bool ShouldUseElectricCar(ref Citizen citizen, ref CitizenInstance citizenInstance) {
            CitizenInfo citizenInfo = citizenInstance.Info;
            int probability;
            if (citizen.m_flags.IsFlagSet(Citizen.Flags.Tourist)) {
                probability = _touristElectricCarProbability(
                    instance: citizenInfo.m_citizenAI as TouristAI,
                    wealth: citizen.WealthLevel);
            } else {
                probability = _residentElectricCarProbability(
                    instance: citizenInfo.m_citizenAI as ResidentAI,
                    instanceID: citizen.m_instance,
                    citizenData: ref citizenInstance,
                    agePhase: citizenInfo.m_agePhase);
            }

            Randomizer randomizer = new Randomizer(citizenInstance.m_citizen);
            return randomizer.Int32(100u) < probability;
        }
    }
}
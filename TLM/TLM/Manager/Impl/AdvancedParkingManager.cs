namespace TrafficManager.Manager.Impl {
    using ColossalFramework.Globalization;
    using ColossalFramework.Math;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using Patch._VehicleAI._PassengerCarAI.Connection;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Extensions;

    public class AdvancedParkingManager
        : AbstractFeatureManager,
          IAdvancedParkingManager
    {
        private readonly Spiral _spiral;
        private Randomizer _randomizer;

        public static readonly AdvancedParkingManager Instance
            = new AdvancedParkingManager(SingletonLite<Spiral>.instance);

        private FindParkingSpaceDelegate _findParkingSpaceDelegate;
        private FindParkingSpacePropDelegate _findParkingSpacePropDelegate;
        private FindParkingSpaceRoadSideDelegate _findParkingSpaceRoadSideDelegate;

        public AdvancedParkingManager(Spiral spiral) {
            _spiral = spiral ?? throw new ArgumentNullException(nameof(spiral));
            _randomizer = new Randomizer();

            _findParkingSpaceDelegate = GameConnectionManager.Instance.PassengerCarAIConnection.FindParkingSpace;
            _findParkingSpacePropDelegate = GameConnectionManager.Instance.PassengerCarAIConnection.FindParkingSpaceProp;
            _findParkingSpaceRoadSideDelegate = GameConnectionManager.Instance.PassengerCarAIConnection.FindParkingSpaceRoadSide;
        }

        protected override void OnDisableFeatureInternal() {
            CitizenManager citizenManager = CitizenManager.instance;
            CitizenInstance[] instancesBuffer = citizenManager.m_instances.m_buffer;
            ExtCitizenInstance[] extCitizenInstances = ExtCitizenInstanceManager.Instance.ExtInstances;

            for (uint citizenInstanceId = 0; citizenInstanceId < extCitizenInstances.Length; ++citizenInstanceId) {
                ExtPathMode pathMode = extCitizenInstances[citizenInstanceId].pathMode;
                switch (pathMode) {
                    case ExtPathMode.RequiresWalkingPathToParkedCar:
                    case ExtPathMode.CalculatingWalkingPathToParkedCar:
                    case ExtPathMode.WalkingToParkedCar:
                    case ExtPathMode.ApproachingParkedCar: {
                        // citizen requires a path to their parked car: release instance to prevent
                        // it from floating
                        citizenManager.ReleaseCitizenInstance((ushort)citizenInstanceId);
                        break;
                    }

                    case ExtPathMode.RequiresCarPath:
                    case ExtPathMode.RequiresMixedCarPathToTarget:
                    case ExtPathMode.CalculatingCarPathToKnownParkPos:
                    case ExtPathMode.CalculatingCarPathToTarget:
                    case ExtPathMode.DrivingToKnownParkPos:
                    case ExtPathMode.DrivingToTarget: {
                        // citizen instance requires a car but is walking: release instance to
                        // prevent it from floating
                        ref CitizenInstance citizenInstance = ref instancesBuffer[citizenInstanceId];
                        if (citizenInstance.IsCharacter()) {
                            citizenManager.ReleaseCitizenInstance((ushort)citizenInstanceId);
                        }

                        break;
                    }
                }
            }

            ExtCitizenManager.Instance.Reset();
            ExtCitizenInstanceManager.Instance.Reset();
        }

        protected override void OnEnableFeatureInternal() {
        }

        public bool EnterParkedCar(ushort instanceId,
                                   ref CitizenInstance instanceData,
                                   ref Citizen citizen,
                                   ushort parkedVehicleId,
                                   out ushort vehicleId) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;

            Log._DebugIf(
                logParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}, ..., {parkedVehicleId}) called.");
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            VehicleManager vehManager = Singleton<VehicleManager>.instance;
            NetManager netManager = Singleton<NetManager>.instance;
            CitizenManager citManager = Singleton<CitizenManager>.instance;

            ref VehicleParked parkedVehicle = ref parkedVehicleId.ToParkedVehicle();
            VehicleInfo parkedVehicleInfo = parkedVehicle.Info;

            if (!CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                  .GetPosition(0, out PathUnit.Position vehLanePathPos)) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not get first car " +
                          $"path position of citizen instance {instanceId}!");
                vehicleId = 0;
                return false;
            }

            uint vehLaneId = PathManager.GetLaneID(vehLanePathPos);
            Log._DebugIf(
                extendedLogParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}): Determined vehicle " +
                      $"position for citizen instance {instanceId}: seg. {vehLanePathPos.m_segment}, " +
                      $"lane {vehLanePathPos.m_lane}, off {vehLanePathPos.m_offset} (lane id {vehLaneId})");

            vehLaneId.ToLane().GetClosestPosition(
                parkedVehicle.m_position,
                out Vector3 vehLanePos,
                out float vehLaneOff);

            var vehLaneOffset = (byte)Mathf.Clamp(Mathf.RoundToInt(vehLaneOff * 255f), 0, 255);

            // movement vector from parked vehicle position to road position
            // Vector3 forwardVector =
            //    parkedVehPos + Vector3.ClampMagnitude(vehLanePos - parkedVehPos, 5f);

            if (vehManager.CreateVehicle(
                out vehicleId,
                ref Singleton<SimulationManager>.instance.m_randomizer,
                parkedVehicleInfo,
                parkedVehicle.m_position,
                TransferManager.TransferReason.None,
                false,
                false)) {
                // update frame data
                ref Vehicle vehicle = ref vehicleId.ToVehicle();

                Vehicle.Frame frame = vehicle.m_frame0;
                frame.m_rotation = parkedVehicle.m_rotation;

                vehicle.m_frame0 = frame;
                vehicle.m_frame1 = frame;
                vehicle.m_frame2 = frame;
                vehicle.m_frame3 = frame;
                parkedVehicleInfo.m_vehicleAI.FrameDataUpdated(
                    vehicleId,
                    ref vehicle,
                    ref frame);

                // update vehicle target position
                vehicle.m_targetPos0 = new Vector4(
                    vehLanePos.x,
                    vehLanePos.y,
                    vehLanePos.z,
                    2f);

                // update other fields
                vehicle.m_flags =
                    vehicle.m_flags | Vehicle.Flags.Stopped;

                vehicle.m_path = instanceData.m_path;
                vehicle.m_pathPositionIndex = 0;
                vehicle.m_lastPathOffset = vehLaneOffset;
                vehicle.m_transferSize =
                    (ushort)(instanceData.m_citizen & 65535u);

                if (!parkedVehicleInfo.m_vehicleAI.TrySpawn(vehicleId, ref vehicle)) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not " +
                              $"spawn a {parkedVehicleInfo.m_vehicleType} for citizen instance {instanceId}!");
                    return false;
                }

                // change instances
                InstanceID parkedVehInstance = InstanceID.Empty;
                parkedVehInstance.ParkedVehicle = parkedVehicleId;
                InstanceID vehInstance = InstanceID.Empty;
                vehInstance.Vehicle = vehicleId;
                Singleton<InstanceManager>.instance.ChangeInstance(parkedVehInstance, vehInstance);

                // set vehicle id for citizen instance
                instanceData.m_path = 0u;

                citizen.SetParkedVehicle(instanceData.m_citizen, 0);
                citizen.SetVehicle(instanceData.m_citizen, vehicleId, 0u);

                // update citizen instance flags
                instanceData.m_flags &= ~CitizenInstance.Flags.WaitingPath;
                instanceData.m_flags &= ~CitizenInstance.Flags.EnteringVehicle;
                instanceData.m_flags &= ~CitizenInstance.Flags.TryingSpawnVehicle;
                instanceData.m_flags &= ~CitizenInstance.Flags.BoredOfWaiting;
                instanceData.m_waitCounter = 0;

                // despawn citizen instance
                instanceData.Unspawn(instanceId);

                if (extendedLogParkingAi) {
                    Log._Debug(
                        $"CustomHumanAI.EnterParkedCar({instanceId}): Citizen instance " +
                        $"{instanceId} is now entering vehicle {vehicleId}. Set vehicle " +
                        $"target position to {vehLanePos} (segment={vehLanePathPos.m_segment}, " +
                        $"lane={vehLanePathPos.m_lane}, offset={vehLanePathPos.m_offset})");
                }

                return true;
            }

            // failed to find a road position
            Log._DebugIf(
                logParkingAi,
                () => $"CustomHumanAI.EnterParkedCar({instanceId}): Could not " +
                      $"find a road position for citizen instance {instanceId} near " +
                      $"parked vehicle {parkedVehicleId}!");
            return false;
        }

        public ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId,
                                                       ref CitizenInstance citizenInstance,
                                                       ref ExtCitizenInstance extInstance,
                                                       ref ExtCitizen extCitizen,
                                                       ref Citizen citizen,
                                                       ExtPathState mainPathState) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == citizenInstanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenInstance.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == citizenInstance.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == citizenInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            Log._DebugIf(
                extendedLogParkingAi,
                () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                $"{mainPathState}) called.");

            if (mainPathState == ExtPathState.Calculating) {
                // main path is still calculating, do not check return path
                Log._DebugIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): still calculating main path. returning CALCULATING.");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;

            // if (!Constants.ManagerFactory.ExtCitizenInstanceManager.IsValid(citizenInstanceId)) {
            // // no citizen
            //#if DEBUG
            // if (debug)
            //  Log._Debug($"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., {mainPathState}): no citizen found!");
            //#endif
            //  return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            // }

            if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
                // main path failed or non-existing
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): mainPathSate is {mainPathState}.");

                if (mainPathState == ExtPathState.Failed) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, " +
                        $"..., {mainPathState}): Checking if path-finding may be repeated.");

                    return OnCitizenPathFindFailure(
                        citizenInstanceId,
                        ref citizenInstance,
                        ref extInstance,
                        ref citizen,
                        ref extCitizen);
                }

                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                    $"{mainPathState}): Resetting instance and returning FAILED.");

                extCitInstMan.Reset(ref extInstance);
                return ExtSoftPathState.FailedHard;
            }

            // main path state is READY

            // main path calculation succeeded: update return path state and check its state if necessary
            extCitInstMan.UpdateReturnPathState(ref extInstance);

            var success = true;
            switch (extInstance.returnPathState) {
                case ExtPathState.None:
                default: {
                    // no return path calculated: ignore
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): return path state is None. Ignoring and " +
                        "returning main path state.");
                    break;
                }

                case ExtPathState.Calculating: // OK
                {
                    Log._DebugIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): return path state is still calculating.");
                    return ExtSoftPathState.Calculating;
                }

                case ExtPathState.Failed: // OK
                {
                    // no walking path from parking position to target found. flag main path as 'failed'.
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): Return path FAILED.");

                    success = false;
                    break;
                }

                case ExtPathState.Ready: {
                    // handle valid return path
                    Log._DebugIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCitizenPathState({citizenInstanceId}, ..., " +
                        $"{mainPathState}): Path is READY.");
                    break;
                }
            }

            extCitInstMan.ReleaseReturnPath(ref extInstance);

            return success
                       ? OnCitizenPathFindSuccess(
                           citizenInstanceId,
                           ref citizenInstance,
                           ref extInstance,
                           ref extCitizen,
                           ref citizen)
                       : OnCitizenPathFindFailure(
                           citizenInstanceId,
                           ref citizenInstance,
                           ref extInstance,
                           ref citizen,
                           ref extCitizen);
        }

        public ExtSoftPathState UpdateCarPathState(ushort vehicleId,
                                                   ref Vehicle vehicleData,
                                                   ref CitizenInstance driverInstance,
                                                   ref ExtCitizenInstance driverExtInstance,
                                                   ExtPathState mainPathState) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId)
                && (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == driverInstance.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == driverInstance.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == driverInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            Log._DebugIf(
                extendedLogParkingAi,
                () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                      $"{mainPathState}) called.");

            if (mainPathState == ExtPathState.Calculating) {
                // main path is still calculating, do not check return path
                Log._DebugIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): still calculating main path. returning CALCULATING.");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            // if (!driverExtInstance.IsValid()) {
            // // no driver
            // #if DEBUG
            //    if (debug)
            //        Log._Debug($"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., {mainPathState}): no driver found!");
            // #endif
            //    return mainPathState;
            // }

            // ExtCitizenInstance driverExtInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(
            // CustomPassengerCarAI.GetDriverInstance(vehicleId, ref vehicleData));
            if (!driverInstance.IsValid()) {
                // no driver
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                    $"{mainPathState}): no driver found!");

                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            if (Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].vehicleType !=
                ExtVehicleType.PassengerCar) {
                // non-passenger cars are not handled
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                    $"{mainPathState}): not a passenger car!");

                extCitInstMan.Reset(ref driverExtInstance);
                return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
            }

            if (mainPathState == ExtPathState.None || mainPathState == ExtPathState.Failed) {
                // main path failed or non-existing: reset return path
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): mainPathSate is {mainPathState}.");

                if (mainPathState == ExtPathState.Failed) {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                        $"{mainPathState}): Checking if path-finding may be repeated.");

                    extCitInstMan.ReleaseReturnPath(ref driverExtInstance);
                    return OnCarPathFindFailure(vehicleId,
                                                ref vehicleData,
                                                ref driverInstance,
                                                ref driverExtInstance);
                }

                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                          $"{mainPathState}): Resetting instance and returning FAILED.");

                extCitInstMan.Reset(ref driverExtInstance);
                return ExtSoftPathState.FailedHard;
            }

            // main path state is READY

            // main path calculation succeeded: update return path state and check its state
            extCitInstMan.UpdateReturnPathState(ref driverExtInstance);

            switch (driverExtInstance.returnPathState) {
                case ExtPathState.None:
                default: {
                    // no return path calculated: ignore
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                              $"{mainPathState}): return path state is None. " +
                              "Setting pathMode=DrivingToTarget and returning main path state.");

                    driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
                    return ExtCitizenInstance.ConvertPathStateToSoftPathState(mainPathState);
                }

                case ExtPathState.Calculating: {
                    // return path not read yet: wait for it
                    Log._DebugIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                              $"{mainPathState}): return path state is still calculating.");

                    return ExtSoftPathState.Calculating;
                }

                case ExtPathState.Failed: {
                    // no walking path from parking position to target found. flag main path as 'failed'.
                    if (logParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                            $"{mainPathState}): Return path {driverExtInstance.returnPathId} " +
                            "FAILED. Forcing path-finding to fail.");
                    }

                    extCitInstMan.Reset(ref driverExtInstance);
                    return ExtSoftPathState.FailedHard;
                }

                case ExtPathState.Ready: {
                    // handle valid return path
                    extCitInstMan.ReleaseReturnPath(ref driverExtInstance);
                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                            $"{mainPathState}): Path is ready for vehicle {vehicleId}, " +
                            $"citizen instance {driverExtInstance.instanceId}! " +
                            $"CurrentPathMode={driverExtInstance.pathMode}");
                    }

                    byte laneTypes = CustomPathManager
                                     ._instance.m_pathUnits.m_buffer[vehicleData.m_path]
                                     .m_laneTypes;
                    bool usesPublicTransport =
                        (laneTypes & (byte)(NetInfo.LaneType.PublicTransport)) != 0;

                    if (usesPublicTransport &&
                        (driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos
                         || driverExtInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos))
                    {
                        driverExtInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                        driverExtInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
                        driverExtInstance.parkingSpaceLocationId = 0;
                    }

                    switch (driverExtInstance.pathMode) {
                        case ExtPathMode.CalculatingCarPathToAltParkPos: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
                            driverExtInstance.parkingPathStartPosition = null;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Path to an alternative parking position is " +
                                    $"READY for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToTarget: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToTarget;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Car path is READY for vehicle {vehicleId}! " +
                                    $"CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                            driverExtInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.UpdateCarPathState({vehicleId}, ..., " +
                                    $"{mainPathState}): Car path to known parking position is READY " +
                                    $"for vehicle {vehicleId}! CurrentPathMode={driverExtInstance.pathMode}");
                            }

                            break;
                        }
                    }

                    return ExtSoftPathState.Ready;
                }
            }
        }

        public ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(
            ushort instanceId,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            Vector3 physicsLodRefPos,
            ref VehicleParked parkedCar)
        {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            bool logParkingAi = false;
            bool extendedLogParkingAi = false;
#endif

            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) != CitizenInstance.Flags.None) {
                Log._DebugIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.CheckCitizenReachedParkedCar({instanceId}): " +
                        $"citizen instance {instanceId} is waiting for path-finding to complete.");

                return ParkedCarApproachState.None;
            }

            // ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);
            if (extInstance.pathMode != ExtPathMode.ApproachingParkedCar &&
                extInstance.pathMode != ExtPathMode.WalkingToParkedCar) {
                if (extendedLogParkingAi) {
                    Log._Debug(
                        "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                        $"({instanceId}): citizen instance {instanceId} is not reaching " +
                        $"a parked car ({extInstance.pathMode})");
                }

                return ParkedCarApproachState.None;
            }

            if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                return ParkedCarApproachState.None;
            }

            Vector3 lastFramePos = instanceData.GetLastFramePosition();
            Vector3 doorPosition = parkedCar.GetClosestDoorPosition(
                parkedCar.m_position,
                VehicleInfo.DoorType.Enter);

            if (extInstance.pathMode == ExtPathMode.WalkingToParkedCar) {
                // check if path is complete
                if (instanceData.m_pathPositionIndex != 255 &&
                    (instanceData.m_path == 0
                     || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                          .GetPosition(instanceData.m_pathPositionIndex >> 1,
                                                       out _)))
                {
                    extInstance.pathMode = ExtPathMode.ApproachingParkedCar;
                    extInstance.lastDistanceToParkedCar =
                        (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;

                    if (logParkingAi) {
                        Log._Debug(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): citizen instance {instanceId} was walking to " +
                            "parked car and reached final path position. " +
                            $"Switched PathMode to {extInstance.pathMode}.");
                    }
                }
            }

            if (extInstance.pathMode != ExtPathMode.ApproachingParkedCar) {
                return ParkedCarApproachState.None;
            }

            Vector3 doorTargetDir = doorPosition - lastFramePos;
            Vector3 doorWalkVector = doorPosition;
            float doorTargetDirMagnitude = doorTargetDir.magnitude;
            if (doorTargetDirMagnitude > 1f) {
                float speed = Mathf.Max(doorTargetDirMagnitude - 5f, doorTargetDirMagnitude * 0.5f);
                doorWalkVector = lastFramePos + (doorTargetDir * (speed / doorTargetDirMagnitude));
            }

            instanceData.m_targetPos = new Vector4(doorWalkVector.x, doorWalkVector.y, doorWalkVector.z, 0.5f);
            instanceData.m_targetDir = VectorUtils.XZ(doorTargetDir);

            CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, physicsLodRefPos);

            float doorSqrDist = (instanceData.GetLastFramePosition() - doorPosition).sqrMagnitude;

            if (doorSqrDist > GlobalConfig.Instance.ParkingAI.MaxParkedCarInstanceSwitchSqrDistance) {
                // citizen is still too far away from the parked car
                ExtPathMode oldPathMode = extInstance.pathMode;
                if (doorSqrDist > extInstance.lastDistanceToParkedCar + 1024f) {

                    // distance has increased dramatically since the last time
                    if (logParkingAi) {
                        Log._Debug(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): Citizen instance {instanceId} is currently " +
                            "reaching their parked car but distance increased! " +
                            $"dist={doorSqrDist}, LastDistanceToParkedCar" +
                            $"={extInstance.lastDistanceToParkedCar}.");
                    }

#if DEBUG
                    if (DebugSwitch.ParkingAIDistanceIssue.Get()) {
                        Log._Debug(
                            "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                            $"({instanceId}): FORCED PAUSE. Distance increased! " +
                            $"Citizen instance {instanceId}. dist={doorSqrDist}");
                        Singleton<SimulationManager>.instance.SimulationPaused = true;
                    }
#endif

                    CitizenInstance.Frame frameData = instanceData.GetLastFrameData();
                    frameData.m_position = doorPosition;
                    instanceData.SetLastFrameData(frameData);

                    extInstance.pathMode = ExtPathMode.RequiresCarPath;

                    return ParkedCarApproachState.Approached;
                }

                if (doorSqrDist < extInstance.lastDistanceToParkedCar) {
                    extInstance.lastDistanceToParkedCar = doorSqrDist;
                }

                if (extendedLogParkingAi) {
                    Log._Debug(
                        "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                        $"({instanceId}): Citizen instance {instanceId} is currently " +
                        $"reaching their parked car (dist={doorSqrDist}, " +
                        $"LastDistanceToParkedCar={extInstance.lastDistanceToParkedCar}). " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                return ParkedCarApproachState.Approaching;
            }

            extInstance.pathMode = ExtPathMode.RequiresCarPath;
            if (logParkingAi) {
                Log._Debug(
                    "AdvancedParkingManager.CitizenApproachingParkedCarSimulationStep" +
                    $"({instanceId}): Citizen instance {instanceId} reached parking position " +
                    $"(dist={doorSqrDist}). Calculating remaining path now. " +
                    $"CurrentDepartureMode={extInstance.pathMode}");
            }

            return ParkedCarApproachState.Approached;
        }

        protected void CitizenApproachingParkedCarSimulationStep(ushort instanceId,
                                                                 ref CitizenInstance instanceData,
                                                                 Vector3 physicsLodRefPos) {
            if ((instanceData.m_flags & CitizenInstance.Flags.Character) == CitizenInstance.Flags.None) {
                return;
            }

            CitizenInstance.Frame lastFrameData = instanceData.GetLastFrameData();
            int oldGridX = Mathf.Clamp(
                (int)((lastFrameData.m_position.x / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            int oldGridY = Mathf.Clamp(
                (int)((lastFrameData.m_position.z / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            bool lodPhysics = Vector3.SqrMagnitude(physicsLodRefPos - lastFrameData.m_position) >= 62500f;

            CitizenApproachingParkedCarSimulationStep(instanceId, ref instanceData, ref lastFrameData, lodPhysics);

            int newGridX = Mathf.Clamp(
                (int)((lastFrameData.m_position.x / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);
            int newGridY = Mathf.Clamp(
                (int)((lastFrameData.m_position.z / CitizenManager.CITIZENGRID_CELL_SIZE) +
                      (CitizenManager.CITIZENGRID_RESOLUTION / 2f)),
                0,
                CitizenManager.CITIZENGRID_RESOLUTION - 1);

            if ((newGridX != oldGridX || newGridY != oldGridY) &&
                (instanceData.m_flags & CitizenInstance.Flags.Character) !=
                CitizenInstance.Flags.None) {
                Singleton<CitizenManager>.instance.RemoveFromGrid(
                    instanceId,
                    ref instanceData,
                    oldGridX,
                    oldGridY);
                Singleton<CitizenManager>.instance.AddToGrid(
                    instanceId,
                    ref instanceData,
                    newGridX,
                    newGridY);
            }

            if (instanceData.m_flags != CitizenInstance.Flags.None) {
                instanceData.SetFrameData(
                    Singleton<SimulationManager>.instance.m_currentFrameIndex,
                    lastFrameData);
            }
        }

        [UsedImplicitly]
        protected void CitizenApproachingParkedCarSimulationStep(ushort instanceId,
                                                                 ref CitizenInstance instanceData,
                                                                 ref CitizenInstance.Frame frameData,
                                                                 bool lodPhysics) {
            frameData.m_position += frameData.m_velocity * 0.5f;

            Vector3 targetDiff = (Vector3)instanceData.m_targetPos - frameData.m_position;
            Vector3 targetVelDiff = targetDiff - frameData.m_velocity;
            float targetVelDiffMag = targetVelDiff.magnitude;

            targetVelDiff *= 2f / Mathf.Max(targetVelDiffMag, 2f);
            frameData.m_velocity += targetVelDiff;
            frameData.m_velocity -= Mathf.Max(
                                         0f,
                                         Vector3.Dot(
                                             (frameData.m_position + frameData.m_velocity) -
                                             (Vector3)instanceData.m_targetPos,
                                             frameData.m_velocity)) /
                                     Mathf.Max(0.01f, frameData.m_velocity.sqrMagnitude) *
                                     frameData.m_velocity;
            if (frameData.m_velocity.sqrMagnitude > 0.01f) {
                frameData.m_rotation = Quaternion.LookRotation(frameData.m_velocity);
            }
        }

        public bool CitizenApproachingTargetSimulationStep(ushort instanceId,
                                                           ref CitizenInstance instanceData,
                                                           ref ExtCitizenInstance extInstance) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            if ((instanceData.m_flags & CitizenInstance.Flags.WaitingPath) !=
                CitizenInstance.Flags.None) {
                Log._DebugIf(
                    extendedLogParkingAi,
                    () => $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                    $"citizen instance {instanceId} is waiting for path-finding to complete.");
                return false;
            }

            // ExtCitizenInstance extInstance = ExtCitizenInstanceManager.Instance.GetExtInstance(instanceId);
            if (extInstance.pathMode != ExtPathMode.WalkingToTarget &&
                extInstance.pathMode != ExtPathMode.TaxiToTarget) {
                if (extendedLogParkingAi) {
                    Log._Debug(
                        $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                        $"citizen instance {instanceId} is not reaching target ({extInstance.pathMode})");
                }

                return false;
            }

            if ((instanceData.m_flags & CitizenInstance.Flags.Character) ==
                CitizenInstance.Flags.None) {
                return false;
            }

            // check if path is complete
            if (instanceData.m_pathPositionIndex != 255
                && (instanceData.m_path == 0
                    || !CustomPathManager._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                         .GetPosition(
                                             instanceData.m_pathPositionIndex >> 1,
                                             out _))) {
                extCitInstMan.Reset(ref extInstance);
                if (logParkingAi) {
                    Log._Debug(
                        $"AdvancedParkingManager.CitizenApproachingTargetSimulationStep({instanceId}): " +
                        $"Citizen instance {instanceId} reached target. " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Handles a path-finding success for activated Parking AI.
        /// </summary>
        /// <param name="instanceId">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="extInstance">Extended citizen instance data</param>
        /// <param name="extCitizen">Extended citizen data</param>
        /// <param name="citizenData">Citizen data</param>
        /// <returns>soft path state</returns>
        protected ExtSoftPathState OnCitizenPathFindSuccess(ushort instanceId,
                                                            ref CitizenInstance instanceData,
                                                            ref ExtCitizenInstance extInstance,
                                                            ref ExtCitizen extCitizen,
                                                            ref Citizen citizenData) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtBuildingManager extBuildingMan = Constants.ManagerFactory.ExtBuildingManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            bool logParkingAi = false;
            bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path-finding succeeded for citizen instance {instanceId}. " +
                    $"Path: {instanceData.m_path} vehicle={citizenData.m_vehicle}");
            }

            if (citizenData.m_vehicle == 0) {
                // citizen does not already have a vehicle assigned
                if (extInstance.pathMode == ExtPathMode.TaxiToTarget) {
                    Log._DebugIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                          "Citizen uses a taxi. Decreasing public transport demand and " +
                          "returning READY.");

                    // cim uses taxi
                    if (instanceData.m_sourceBuilding != 0) {
                        extBuildingMan.RemovePublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                            GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement,
                            true);
                    }

                    if (instanceData.m_targetBuilding != 0) {
                        extBuildingMan.RemovePublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                            GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement,
                            false);
                    }

                    extCitizen.transportMode |= ExtTransportMode.PublicTransport;
                    return ExtSoftPathState.Ready;
                }

                ushort parkedVehicleId = citizenData.m_parkedVehicle;
                var sqrDistToParkedVehicle = 0f;
                if (parkedVehicleId != 0) {
                    // calculate distance to parked vehicle
                    ref VehicleParked parkedVehicle = ref parkedVehicleId.ToParkedVehicle();
                    Vector3 doorPosition = parkedVehicle.GetClosestDoorPosition(
                        parkedVehicle.m_position,
                        VehicleInfo.DoorType.Enter);
                    sqrDistToParkedVehicle = (instanceData.GetLastFramePosition() - doorPosition)
                        .sqrMagnitude;
                }

                byte laneTypes = CustomPathManager
                                ._instance.m_pathUnits.m_buffer[instanceData.m_path].m_laneTypes;
                uint vehicleTypes = CustomPathManager
                                   ._instance.m_pathUnits.m_buffer[instanceData.m_path]
                                   .m_vehicleTypes;
                bool usesPublicTransport =
                    (laneTypes & (byte)NetInfo.LaneType.PublicTransport) != 0;
                bool usesCar = (laneTypes & (byte)(NetInfo.LaneType.Vehicle
                                                  | NetInfo.LaneType.TransportVehicle)) != 0
                              && (vehicleTypes & (ushort)VehicleInfo.VehicleType.Car) != 0;

                if (usesPublicTransport && usesCar &&
                    (extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos ||
                     extInstance.pathMode == ExtPathMode.CalculatingCarPathToAltParkPos)) {
                     // when using public transport together with a car (assuming a
                     // "source -> walk -> drive -> walk -> use public transport -> walk -> target"
                     // path) discard parking space information since the cim has to park near the
                     // public transport stop (instead of parking in the vicinity of the target building).
                     // TODO we could check if the path looks like "source -> walk -> use public transport -> walk -> drive -> [walk ->] target" (in this case parking space information would still be valid)
                    Log._DebugIf(
                        extendedLogParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        "Citizen uses their car together with public transport. " +
                        "Discarding parking space information and setting path mode to " +
                        "CalculatingCarPathToTarget.");

                    extInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                    extInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
                    extInstance.parkingSpaceLocationId = 0;
                }

                switch (extInstance.pathMode) {
                    case ExtPathMode.None: // citizen starts at source building
                    default: {
                        return OnCitizenPathFindSuccess_Default(
                            instanceId,
                            instanceData,
                            ref citizenData,
                            ref extInstance,
                            ref extCitizen,
                            logParkingAi,
                            usesCar,
                            parkedVehicleId,
                            extBuildingMan,
                            usesPublicTransport);
                    }

                    // citizen has not yet entered their car (but is close to do so) and tries to
                    // reach the target directly
                    case ExtPathMode.CalculatingCarPathToTarget:

                    // citizen has not yet entered their (but is close to do so) car and tries to
                    // reach a parking space in the vicinity of the target
                    case ExtPathMode.CalculatingCarPathToKnownParkPos:

                    // citizen has not yet entered their car (but is close to do so) and tries to
                    // reach an alternative parking space in the vicinity of the target
                    case ExtPathMode.CalculatingCarPathToAltParkPos:
                    {
                        return OnCitizenPathFindSuccess_CarPath(
                            instanceId,
                            ref instanceData,
                            ref extInstance,
                            ref citizenData,
                            ref extCitizen,
                            usesCar,
                            logParkingAi,
                            parkedVehicleId,
                            extCitInstMan,
                            sqrDistToParkedVehicle,
                            extendedLogParkingAi,
                            usesPublicTransport,
                            extBuildingMan);
                    }

                    case ExtPathMode.CalculatingWalkingPathToParkedCar: {
                        return OnCitizenPathFindSuccess_ToParkedCar(
                            instanceId,
                            instanceData,
                            ref extInstance,
                            parkedVehicleId,
                            logParkingAi,
                            extCitInstMan);
                    }

                    case ExtPathMode.CalculatingWalkingPathToTarget: {
                        return OnCitizenPathFindSuccess_ToTarget(
                            instanceId,
                            instanceData,
                            ref extInstance,
                            logParkingAi);
                    }
                }
            }

            // citizen has a vehicle assigned
            Log._DebugOnlyWarningIf(
                logParkingAi,
                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                "Citizen has a vehicle assigned but this method does not handle this " +
                "situation. Forcing path-find to fail.");

            extCitInstMan.Reset(ref extInstance);
            return ExtSoftPathState.FailedHard;
        }

        private static ExtSoftPathState OnCitizenPathFindSuccess_ToTarget(
            ushort instanceId,
            CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            bool logParkingAi) {
            // final walking path to target has been calculated
            extInstance.pathMode = ExtPathMode.WalkingToTarget;

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} is now traveling by foot to their final " +
                    $"target. CurrentDepartureMode={extInstance.pathMode}, " +
                    $"targetPos={instanceData.m_targetPos} " +
                    $"lastFramePos={instanceData.GetLastFramePosition()}");
            }

            return ExtSoftPathState.Ready;
        }

        private static ExtSoftPathState OnCitizenPathFindSuccess_ToParkedCar(
            ushort instanceId,
            CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            ushort parkedVehicleId,
            bool logParkingAi,
            IExtCitizenInstanceManager extCitInstMan) {

            // path to parked vehicle has been calculated...
            if (parkedVehicleId == 0) {
                // ... but the parked vehicle has vanished
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} shall walk to their parked vehicle but it " +
                    "disappeared. Retrying path-find for walking.");

                extCitInstMan.Reset(ref extInstance);
                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            extInstance.pathMode = ExtPathMode.WalkingToParkedCar;
            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} is now on their way to its parked vehicle. " +
                    $"CurrentDepartureMode={extInstance.pathMode}, " +
                    $"targetPos={instanceData.m_targetPos} " +
                    $"lastFramePos={instanceData.GetLastFramePosition()}");
            }

            return ExtSoftPathState.Ready;
        }

        private ExtSoftPathState
            OnCitizenPathFindSuccess_CarPath(ushort instanceId,
                                             ref CitizenInstance instanceData,
                                             ref ExtCitizenInstance extInstance,
                                             ref Citizen citizen,
                                             ref ExtCitizen extCitizen,
                                             bool usesCar,
                                             bool logParkingAi,
                                             ushort parkedVehicleId,
                                             IExtCitizenInstanceManager extCitInstMan,
                                             float sqrDistToParkedVehicle,
                                             bool extendedLogParkingAi,
                                             bool usesPublicTransport,
                                             IExtBuildingManager extBuildingMan)
        {
            if (usesCar) {
                // parked car should be reached now
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path for citizen instance {instanceId} contains passenger car section and " +
                    "citizen should stand in front of their car.");

                if (extInstance.atOutsideConnection) {
                    switch (extInstance.pathMode) {
                        // car path calculated starting at road outside connection: success
                        case ExtPathMode.CalculatingCarPathToAltParkPos: {
                            extInstance.pathMode = ExtPathMode.DrivingToAltParkPos;
                            extInstance.parkingPathStartPosition = null;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    "Path to an alternative parking position is READY! " +
                                    $"CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToTarget: {
                            extInstance.pathMode = ExtPathMode.DrivingToTarget;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    $"Car path is READY! CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }

                        case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                            extInstance.pathMode = ExtPathMode.DrivingToKnownParkPos;
                            if (logParkingAi) {
                                Log._Debug(
                                    $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                    "Car path to known parking position is READY! " +
                                    $"CurrentPathMode={extInstance.pathMode}");
                            }

                            break;
                        }
                    }

                    extInstance.atOutsideConnection = false; // citizen leaves outside connection
                    return ExtSoftPathState.Ready;
                }

                if (parkedVehicleId == 0) {
                    // error! could not find/spawn parked car
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Citizen instance {instanceId} still does not have a parked vehicle! " +
                        "Retrying: Cim should walk to target");

                    extCitInstMan.Reset(ref extInstance);
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    return ExtSoftPathState.FailedSoft;
                }

                if (sqrDistToParkedVehicle >
                    4f * GlobalConfig.Instance.ParkingAI.MaxParkedCarInstanceSwitchSqrDistance) {
                    // error! parked car is too far away
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Citizen instance {instanceId} cannot enter parked vehicle because it is " +
                        $"too far away (sqrDistToParkedVehicle={sqrDistToParkedVehicle})! " +
                        "Retrying: Cim should walk to parked car");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                    return ExtSoftPathState.FailedSoft;
                }

                // path using passenger car has been calculated
                if (EnterParkedCar(
                    instanceId,
                    ref instanceData,
                    ref citizen,
                    parkedVehicleId,
                    out ushort vehicleId))
                {
                    extInstance.pathMode =
                        extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget
                            ? ExtPathMode.DrivingToTarget
                            : ExtPathMode.DrivingToKnownParkPos;

                    extCitizen.transportMode |= ExtTransportMode.Car;

                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen instance {instanceId} has entered their car and is now " +
                            $"traveling by car (vehicleId={vehicleId}). " +
                            $"CurrentDepartureMode={extInstance.pathMode}, " +
                            $"targetPos={instanceData.m_targetPos} " +
                            $"lastFramePos={instanceData.GetLastFramePosition()}");
                    }

                    return ExtSoftPathState.Ignore;
                }

                // error! parked car could not be entered (reached vehicle limit?): try to walk to target
                if (logParkingAi) {
                    Log._Debug(
                        $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Entering parked vehicle {parkedVehicleId} failed for citizen " +
                        $"instance {instanceId}. Trying to walk to target. " +
                        $"CurrentDepartureMode={extInstance.pathMode}");
                }

                extCitInstMan.Reset(ref extInstance);
                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            // citizen does not need a car for the calculated path...
            switch (extInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToTarget: {
                    // ... and the path can be reused for walking
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        "A direct car path was queried that does not contain a car section. " +
                        "Switching path mode to walking.");

                    extCitInstMan.Reset(ref extInstance);

                    if (usesPublicTransport) {
                        // decrease public transport demand
                        if (instanceData.m_sourceBuilding != 0) {
                            extBuildingMan.RemovePublicTransportDemand(
                                ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                                GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement,
                                true);
                        }

                        if (instanceData.m_targetBuilding != 0) {
                            extBuildingMan.RemovePublicTransportDemand(
                                ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                                GlobalConfig.Instance.ParkingAI.PublicTransportDemandUsageDecrement,
                                false);
                        }

                        extCitizen.transportMode |= ExtTransportMode.PublicTransport;
                    }

                    extInstance.pathMode = ExtPathMode.WalkingToTarget;
                    return ExtSoftPathState.Ready;
                }

                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                default: {
                    // ... and a path to a parking spot was calculated: dismiss path and
                    // restart path-finding for walking
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        "A parking space car path was queried but it turned out that no car is " +
                        "needed. Retrying path-finding for walking.");

                    extCitInstMan.Reset(ref extInstance);
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    return ExtSoftPathState.FailedSoft;
                }
            }
        }

        private ExtSoftPathState
            OnCitizenPathFindSuccess_Default(ushort instanceId,
                                             CitizenInstance instanceData,
                                             ref Citizen citizen,
                                             ref ExtCitizenInstance extInstance,
                                             ref ExtCitizen extCitizen,
                                             bool logParkingAi,
                                             bool usesCar,
                                             ushort parkedVehicleId,
                                             IExtBuildingManager extBuildingMan,
                                             bool usesPublicTransport)
        {
            if (extInstance.pathMode != ExtPathMode.None) {
                if (logParkingAi) {
                    Log._DebugOnlyWarning(
                        $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Unexpected path mode {extInstance.pathMode}! {extInstance}");
                }
            }

            ParkingAI parkingAiConf = GlobalConfig.Instance.ParkingAI;
            if (usesCar) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Path for citizen instance {instanceId} contains passenger car " +
                    "section. Ensuring that citizen is allowed to use their car.");

                ushort sourceBuildingId = instanceData.m_sourceBuilding;
                ushort homeId = citizen.m_homeBuilding;

                if (parkedVehicleId == 0) {
                    if (logParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                            $"source building {sourceBuildingId} does not have a parked " +
                            $"vehicle! CurrentPathMode={extInstance.pathMode}");
                    }

                    // determine current position vector
                    Vector3 currentPos;
                    ushort currentBuildingId = citizen.GetBuildingByLocation();
                    if (currentBuildingId != 0) {
                        currentPos = currentBuildingId.ToBuilding().m_position;
                        if (logParkingAi) {
                            Log._Debug(
                                $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $"Taking current position from current building {currentBuildingId} " +
                                $"for citizen {instanceData.m_citizen} (citizen instance {instanceId}): " +
                                $"{currentPos} CurrentPathMode={extInstance.pathMode}");
                        }
                    } else {
                        currentBuildingId = sourceBuildingId;
                        currentPos = instanceData.GetLastFramePosition();
                        if (logParkingAi) {
                            Log._Debug(
                                $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                "Taking current position from last frame position for citizen " +
                                $"{instanceData.m_citizen} (citizen instance {instanceId}): " +
                                $"{currentPos}. Home {homeId} pos: " +
                                $"{homeId.ToBuilding().m_position} " +
                                $"CurrentPathMode={extInstance.pathMode}");
                        }
                    }

                    // if tourist (does not have home) and at outside connection, don't try to spawn parked vehicle
                    // cim might just exit the vehicle to despawn at outside connection
                    // or just spawned at the edge of the map - must walk to closest node to spawn the vehicle to enter city(resident/tourist) or go to different connection(dummy traffic)
                    if (homeId == 0 && ExtCitizenInstanceManager.Instance.IsAtOutsideConnection(instanceId, ref instanceData, ref extInstance, currentPos)) {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                  $">> Skipped spawning parked vehicle for citizen {instanceData.m_citizen} " +
                                  $"(instance {instanceId}) is at/near outside connection, currentPos: {currentPos}");

                        extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                        return ExtSoftPathState.FailedSoft;
                    }

                    // try to spawn parked vehicle in the vicinity of the starting point.
                    VehicleInfo vehicleInfo = null;
                    if (instanceData.Info.m_agePhase > Citizen.AgePhase.Child) {
                        bool useElectric = ExtVehicleManager.Instance.ShouldUseElectricCar(ref citizen, ref instanceData);
                        vehicleInfo =
                            Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                                ref Singleton<SimulationManager>.instance.m_randomizer,
                                ItemClass.Service.Residential,
                                useElectric ? ItemClass.SubService.ResidentialLowEco : ItemClass.SubService.ResidentialLow,
                                ItemClass.Level.Level1);
                    }

                    if (vehicleInfo != null) {
                        if (logParkingAi) {
                            Log._Debug(
                                $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                                $"source building {sourceBuildingId} is using their own passenger car. " +
                                $"CurrentPathMode={extInstance.pathMode}");
                        }

                        // spawn a passenger car near the current position
                        if (TrySpawnParkedPassengerCar(
                                citizenId: instanceData.m_citizen,
                                citizen: ref citizen,
                                homeId: homeId,
                                refPos: currentPos,
                                vehicleInfo: vehicleInfo,
                                parkPos: out Vector3 parkPos,
                                reason: out ParkingError parkReason)) {
                            parkedVehicleId = citizen.m_parkedVehicle;
                            Log._DebugIf(
                                logParkingAi,
                                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $"Parked vehicle for citizen {instanceData.m_citizen} " +
                                $"(instance {instanceId}) is {parkedVehicleId} now (parkPos={parkPos}).");

                            if (currentBuildingId != 0) {
                                extBuildingMan.ModifyParkingSpaceDemand(
                                    ref extBuildingMan.ExtBuildings[currentBuildingId],
                                    parkPos,
                                    parkingAiConf.MinSpawnedCarParkingSpaceDemandDelta,
                                    parkingAiConf.MaxSpawnedCarParkingSpaceDemandDelta);
                            }
                        } else {
                            Log._DebugIf(
                                logParkingAi,
                                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                                $">> Failed to spawn parked vehicle for citizen {instanceData.m_citizen} " +
                                $"(citizen instance {instanceId}). reason={parkReason}. homePos: " +
                                $"{homeId.ToBuilding().m_position}, currentPos: {currentPos}");

                            if (parkReason == ParkingError.NoSpaceFound &&
                                currentBuildingId != 0) {
                                extBuildingMan.AddParkingSpaceDemand(
                                    ref extBuildingMan.ExtBuildings[currentBuildingId],
                                    parkingAiConf.FailedSpawnParkingSpaceDemandIncrement);
                            }
                        }
                    } else {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                            $"Citizen {instanceData.m_citizen} (citizen instance {instanceId}), " +
                            $"source building {sourceBuildingId}, home {homeId} does not own a vehicle.");
                    }
                }

                if (parkedVehicleId != 0) {
                    // citizen has to reach their parked vehicle first
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                        $"Calculating path to reach parked vehicle {parkedVehicleId} for citizen " +
                        $"instance {instanceId}. targetPos={instanceData.m_targetPos} " +
                        $"lastFramePos={instanceData.GetLastFramePosition()}");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                    return ExtSoftPathState.FailedSoft;
                }

                // error! could not find/spawn parked car
                Log._DebugIf(
                    logParkingAi,
                    () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): " +
                    $"Citizen instance {instanceId} still does not have a parked vehicle! " +
                    "Retrying: Cim should walk to target");

                extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                return ExtSoftPathState.FailedSoft;
            }

            // path does not contain a car section: path can be reused for walking
            Log._DebugIf(
                logParkingAi,
                () => $"AdvancedParkingManager.OnCitizenPathFindSuccess({instanceId}): A direct car " +
                "path OR initial path was queried that does not contain a car section. " +
                "Switching path mode to walking.");

            if (usesPublicTransport) {
                // decrease public transport demand
                if (instanceData.m_sourceBuilding != 0) {
                    extBuildingMan.RemovePublicTransportDemand(
                        ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                        parkingAiConf.PublicTransportDemandUsageDecrement,
                        true);
                }

                if (instanceData.m_targetBuilding != 0) {
                    extBuildingMan.RemovePublicTransportDemand(
                        ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                        parkingAiConf.PublicTransportDemandUsageDecrement,
                        false);
                }

                extCitizen.transportMode |= ExtTransportMode.PublicTransport;
            }

            extInstance.pathMode = ExtPathMode.WalkingToTarget;
            return ExtSoftPathState.Ready;
        }

        /// <summary>
        /// Handles a path-finding failure for citizen instances and activated Parking AI.
        /// </summary>
        /// <param name="instanceId">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="extInstance">extended citizen instance information</param>
        /// <param name="citizen">citizen information</param>
        /// <param name="extCitizen">extended citizen information</param>
        /// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
        protected ExtSoftPathState OnCitizenPathFindFailure(ushort instanceId,
                                                            ref CitizenInstance instanceData,
                                                            ref ExtCitizenInstance extInstance,
                                                            ref Citizen citizen,
                                                            ref ExtCitizen extCitizen) {
            IExtCitizenInstanceManager extCitInstMan = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtBuildingManager extBuildingMan = Constants.ManagerFactory.ExtBuildingManager;

#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): Path-finding " +
                    $"failed for citizen instance {extInstance.instanceId}. " +
                    $"CurrentPathMode={extInstance.pathMode}");
            }

            // update public transport demands
            if (extInstance.pathMode == ExtPathMode.None ||
                extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget ||
                extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar ||
                extInstance.pathMode == ExtPathMode.TaxiToTarget) {
                // could not reach target building by walking/driving/public transport: increase
                // public transport demand
                if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                    CitizenInstance.Flags.None) {
                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                            "Increasing public transport demand of target building " +
                            $"{instanceData.m_targetBuilding} and source building " +
                            $"{instanceData.m_sourceBuilding}");
                    }

                    if (instanceData.m_targetBuilding != 0) {
                        extBuildingMan.AddPublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_targetBuilding],
                            GlobalConfig.Instance.ParkingAI.PublicTransportDemandIncrement,
                            false);
                    }

                    if (instanceData.m_sourceBuilding != 0) {
                        extBuildingMan.AddPublicTransportDemand(
                            ref extBuildingMan.ExtBuildings[instanceData.m_sourceBuilding],
                            GlobalConfig.Instance.ParkingAI.PublicTransportDemandIncrement,
                            true);
                    }
                }
            }

            // relocate parked car if abandoned
            if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
                // parked car is unreachable
                ushort parkedVehicleId = citizen.m_parkedVehicle;

                if (parkedVehicleId != 0) {
                    // parked car is present
                    ushort homeId = citizen.m_homeBuilding;

                    // calculate distance between citizen and parked car
                    var movedCar = false;
                    Vector3 citizenPos = instanceData.GetLastFramePosition();

                    ref VehicleParked parkedVehicle = ref parkedVehicleId.ToParkedVehicle();

                    Vector3 oldParkedVehiclePos = parkedVehicle.m_position;
                    var parkedToCitizen = (parkedVehicle.m_position - citizenPos).magnitude;
                    if (parkedToCitizen > GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome) {
                        // parked car is far away from current location
                        // -> relocate parked car and try again
                        movedCar = TryMoveParkedVehicle(
                            parkedVehicleId,
                            ref parkedVehicle,
                            citizenPos,
                            GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome,
                            homeId);
                    }

                    if (movedCar) {
                        // successfully moved the parked car to a closer location
                        // -> retry path-finding
                        extInstance.pathMode = ExtPathMode.RequiresWalkingPathToParkedCar;
                        Vector3 parkedPos = parkedVehicleId.ToParkedVehicle().m_position;

                        if (extendedLogParkingAi) {
                            Log._Debug(
                                $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                                $"Relocated parked car {parkedVehicleId} to a closer location (old pos/distance: " +
                                $"{oldParkedVehiclePos}/{parkedToCitizen}, new pos/distance: " +
                                $"{parkedPos}/{(parkedPos - citizenPos).magnitude}) " +
                                $"for citizen @ {citizenPos}. Retrying path-finding. " +
                                $"CurrentPathMode={extInstance.pathMode}");
                        }

                        return ExtSoftPathState.FailedSoft;
                    }

                    // could not move car
                    // -> despawn parked car, walk to target or use public transport
                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                            $"Releasing unreachable parked vehicle {parkedVehicleId} for citizen " +
                            $"instance {extInstance.instanceId}. CurrentPathMode={extInstance.pathMode}");
                    }

                    Singleton<VehicleManager>.instance.ReleaseParkedVehicle(parkedVehicleId);
                }
            }

            // check if path-finding may be repeated
            var ret = ExtSoftPathState.FailedHard;
            switch (extInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToTarget:
                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.CalculatingWalkingPathToParkedCar: {
                    // try to walk to target
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                        "Path failed but it may be retried to walk to the target.");
                    extInstance.pathMode = ExtPathMode.RequiresWalkingPathToTarget;
                    ret = ExtSoftPathState.FailedSoft;
                    break;
                }

                default: {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                        "Path failed and walking to target is not an option. Resetting ext. instance.");
                    extCitInstMan.Reset(ref extInstance);
                    break;
                }
            }

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCitizenPathFindFailure({instanceId}): " +
                    $"Setting CurrentPathMode for citizen instance {extInstance.instanceId} " +
                    $"to {extInstance.pathMode}, ret={ret}");
            }

            // reset current transport mode for hard failures
            if (ret == ExtSoftPathState.FailedHard) {
                extCitizen.transportMode = ExtTransportMode.None;
            }

            return ret;
        }

        /// <summary>
        /// Handles a path-finding failure for citizen instances and activated Parking AI.
        /// </summary>
        /// <param name="vehicleId">Vehicle id</param>
        /// <param name="vehicleData">Vehicle data</param>
        /// <param name="driverInstanceData">Driver citizen instance data</param>
        /// <param name="driverExtInstance">extended citizen instance information of driver</param>
        /// <returns>if true path-finding may be repeated (path mode has been updated), false otherwise</returns>
        [UsedImplicitly]
        protected ExtSoftPathState OnCarPathFindFailure(ushort vehicleId,
                                                        ref Vehicle vehicleData,
                                                        ref CitizenInstance driverInstanceData,
                                                        ref ExtCitizenInstance driverExtInstance) {
            IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
#if DEBUG
            bool citizenDebug =
                (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId)
                && (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == driverExtInstance.instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == driverInstanceData.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == driverInstanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == driverInstanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path-finding failed " +
                    $"for driver citizen instance {driverExtInstance.instanceId}. " +
                    $"CurrentPathMode={driverExtInstance.pathMode}");
            }

            // update parking demands
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.None:
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    // could not reach target building by driving: increase parking space demand
                    if (extendedLogParkingAi) {
                        Log._Debug(
                            $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): " +
                            "Increasing parking space demand of target building " +
                            $"{driverInstanceData.m_targetBuilding}");
                    }

                    if (driverInstanceData.m_targetBuilding != 0) {
                        IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
                        extBuildingManager.AddParkingSpaceDemand(
                            ref extBuildingManager.ExtBuildings[driverInstanceData.m_targetBuilding],
                            GlobalConfig.Instance.ParkingAI.FailedParkingSpaceDemandIncrement);
                    }

                    break;
                }
            }

            // check if path-finding may be repeated
            var ret = ExtSoftPathState.FailedHard;
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    // try to drive directly to the target if public transport is allowed
                    if ((driverInstanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                        CitizenInstance.Flags.None) {
                        Log._DebugIf(
                            logParkingAi,
                            () => $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): " +
                            "Path failed but it may be retried to drive directly to the target " +
                            "/ using public transport.");

                        driverExtInstance.pathMode = ExtPathMode.RequiresMixedCarPathToTarget;
                        ret = ExtSoftPathState.FailedSoft;
                    }

                    break;
                }

                default: {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Path failed " +
                        "and a direct target is not an option. Resetting driver ext. instance.");
                    extCitizenInstanceManager.Reset(ref driverExtInstance);
                    break;
                }
            }

            if (logParkingAi) {
                Log._Debug(
                    $"AdvancedParkingManager.OnCarPathFindFailure({vehicleId}): Setting " +
                    $"CurrentPathMode for driver citizen instance {driverExtInstance.instanceId} " +
                    $"to {driverExtInstance.pathMode}, ret={ret}");
            }

            return ret;
        }

        public bool TryMoveParkedVehicle(ushort parkedVehicleId,
                                         ref VehicleParked parkedVehicle,
                                         Vector3 refPos,
                                         float maxDistance,
                                         ushort homeId) {
            bool found;
            Vector3 parkPos;
            Quaternion parkRot;

            found = Instance.FindParkingSpaceInVicinity(
                    refPos,
                    Vector3.zero,
                    parkedVehicle.Info,
                    homeId,
                    0,
                    maxDistance,
                    out _,
                    out _,
                    out parkPos,
                    out parkRot,
                    out _);

            if (found) {
                Singleton<VehicleManager>.instance.RemoveFromGrid(parkedVehicleId, ref parkedVehicle);
                parkedVehicle.m_position = parkPos;
                parkedVehicle.m_rotation = parkRot;
                Singleton<VehicleManager>.instance.AddToGrid(parkedVehicleId, ref parkedVehicle);
            }

            return found;
        }

        public bool FindParkingSpaceForCitizen(Vector3 endPos,
                                               VehicleInfo vehicleInfo,
                                               ref CitizenInstance driverInstance,
                                               ref ExtCitizenInstance extDriverInstance,
                                               ushort homeId,
                                               bool goingHome,
                                               ushort vehicleId,
                                               bool allowTourists,
                                               out Vector3 parkPos,
                                               ref PathUnit.Position endPathPos,
                                               out bool calculateEndPos) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId)
                && (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == extDriverInstance.instanceId)
                && (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == driverInstance.m_citizen)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == driverInstance.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == driverInstance.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            calculateEndPos = true;
            parkPos = default;

            if (!allowTourists) {
                // TODO remove this from this method
                uint citizenId = driverInstance.m_citizen;

                if (citizenId == 0 ||
                    (CitizenManager.instance.m_citizens.m_buffer[citizenId].m_flags & Citizen.Flags.Tourist) != Citizen.Flags.None) {
                    return false;
                }
            }

            if (extendedLogParkingAi) {
                Log._Debug(
                    $"Citizen instance {extDriverInstance.instanceId} " +
                    $"(CurrentPathMode={extDriverInstance.pathMode}) can still use their passenger " +
                    "car and is either not a tourist or wants to find an alternative parking spot. " +
                    "Finding a parking space before starting path-finding.");
            }

            // find a free parking space
            bool success = FindParkingSpaceInVicinity(
                endPos,
                Vector3.zero,
                vehicleInfo,
                homeId,
                vehicleId,
                goingHome
                    ? GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToHome
                    : GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                out ExtParkingSpaceLocation knownParkingSpaceLocation,
                out ushort knownParkingSpaceLocationId,
                out parkPos,
                out _,
                out float parkOffset);

            extDriverInstance.parkingSpaceLocation = knownParkingSpaceLocation;
            extDriverInstance.parkingSpaceLocationId = knownParkingSpaceLocationId;

            if (!success) {
                return false;
            }

            if (extendedLogParkingAi) {
                Log._Debug(
                    $"Found a parking spot for citizen instance {extDriverInstance.instanceId} " +
                    $"(CurrentPathMode={extDriverInstance.pathMode}) before starting car path: " +
                    $"{knownParkingSpaceLocation} @ {knownParkingSpaceLocationId}");
            }

            switch (knownParkingSpaceLocation) {
                case ExtParkingSpaceLocation.RoadSide: {
                    // found segment with parking space
                    if (logParkingAi) {
                        Log._Debug(
                            $"Found segment {knownParkingSpaceLocationId} for road-side parking " +
                            $"position for citizen instance {extDriverInstance.instanceId}!");
                    }

                    // determine nearest sidewalk position for parking position at segment
                    if (knownParkingSpaceLocationId.ToSegment()
                        .GetClosestLanePosition(
                            parkPos,
                            NetInfo.LaneType.Pedestrian,
                            VehicleInfo.VehicleType.None,
                            VehicleInfo.VehicleCategory.None,
                            out _,
                            out uint laneId,
                            out int laneIndex,
                            out _)) {
                        endPathPos.m_segment = knownParkingSpaceLocationId;
                        endPathPos.m_lane = (byte)laneIndex;
                        endPathPos.m_offset = (byte)(parkOffset * 255f);
                        calculateEndPos = false;

                        // extDriverInstance.CurrentPathMode = successMode;
                        // ExtCitizenInstance.PathMode.CalculatingKnownCarPath;
                        if (logParkingAi) {
                            Log._Debug(
                                "Found an parking spot sidewalk position for citizen instance " +
                                $"{extDriverInstance.instanceId} @ segment {knownParkingSpaceLocationId}, " +
                                $"laneId {laneId}, laneIndex {laneIndex}, offset={endPathPos.m_offset}! " +
                                $"CurrentPathMode={extDriverInstance.pathMode}");
                        }

                        return true;
                    }

                    if (logParkingAi) {
                        Log._Debug(
                            "Could not find an alternative parking spot sidewalk position for " +
                            $"citizen instance {extDriverInstance.instanceId}! " +
                            $"CurrentPathMode={extDriverInstance.pathMode}");
                    }

                    return false;
                }

                case ExtParkingSpaceLocation.Building: {
                    // found a building with parking space
                    if (Constants.ManagerFactory.ExtPathManager.FindPathPositionWithSpiralLoop(
                        parkPos,
                        knownParkingSpaceLocationId.ToBuilding().m_position,
                        ItemClass.Service.Road,
                        NetInfo.LaneType.Pedestrian,
                        VehicleInfo.VehicleType.None,
                        NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                        VehicleInfo.VehicleType.Car,
                        false,
                        false,
                        GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance,
                        false,
                        true,
                        out endPathPos)) {
                        calculateEndPos = false;
                    }

                    if (logParkingAi) {
                        Log._Debug(
                            $"Navigating citizen instance {extDriverInstance.instanceId} to parking " +
                            $"building {knownParkingSpaceLocationId}, parkPos={parkPos}! segment={endPathPos.m_segment}, " +
                            $"laneIndex={endPathPos.m_lane}, offset={endPathPos.m_offset}. " +
                            $"CurrentPathMode={extDriverInstance.pathMode} " +
                            $"calculateEndPos={calculateEndPos}, endPos={endPos}");
                    }

                    return true;
                }

                default:
                    return false;
            }
        }

        public bool TrySpawnParkedPassengerCar(uint citizenId,
                                               ref Citizen citizen,
                                               ushort homeId,
                                               Vector3 refPos,
                                               VehicleInfo vehicleInfo,
                                               out Vector3 parkPos,
                                               out ParkingError reason) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            // var logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            // const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif
            Log._DebugIf(
                extendedLogParkingAi && homeId != 0,
                () => $"Trying to spawn parked passenger car for citizen {citizenId}, " +
                $"home {homeId} @ {refPos}");

            bool roadParkSuccess = TrySpawnParkedPassengerCarRoadSideInternal(
                citizenId,
                refPos,
                vehicleInfo,
                out Vector3 roadParkPos,
                out ParkingError roadParkReason,
                out ushort roadParkedVehicleId,
                updateCitizenParkedVehicle: false);

            bool buildingParkSuccess = TrySpawnParkedPassengerCarBuildingInternal(
                citizenId,
                ref citizen,
                homeId,
                refPos,
                vehicleInfo,
                out Vector3 buildingParkPos,
                out ParkingError buildingParkReason,
                out ushort buildingParkedVehicleId,
                updateCitizenParkedVehicle: false);

            if (!buildingParkSuccess) {
                if (roadParkSuccess) {
                    roadParkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
                }
                parkPos = roadParkPos;
                reason = roadParkReason;
                return roadParkSuccess;
            }

            if (!roadParkSuccess) {
                buildingParkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
                parkPos = buildingParkPos;
                reason = buildingParkReason;
                return true;
            }

            /*
             * Two parked vehicles available, assign the closer one, release the other
             */
            if ((roadParkPos - refPos).sqrMagnitude < (buildingParkPos - refPos).sqrMagnitude) {
                VehicleManager.instance.ReleaseParkedVehicle(buildingParkedVehicleId);
                roadParkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
                parkPos = roadParkPos;
                reason = roadParkReason;
                return true;
            }

            VehicleManager.instance.ReleaseParkedVehicle(roadParkedVehicleId);
            buildingParkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
            parkPos = buildingParkPos;
            reason = buildingParkReason;
            return true;
        }

        [UsedImplicitly]
        public bool TrySpawnParkedPassengerCarRoadSide(uint citizenId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason) {
            return TrySpawnParkedPassengerCarRoadSideInternal(
                citizenId,
                refPos,
                vehicleInfo,
                out parkPos,
                out reason,
                out _,
                updateCitizenParkedVehicle: true);
        }


        private bool TrySpawnParkedPassengerCarRoadSideInternal(uint citizenId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason,
                                                       out ushort roadParkVehicleId,
                                                       bool updateCitizenParkedVehicle = true) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;

            // bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            // const bool extendedLogParkingAi = false;
#endif

            Log._DebugIf(
                logParkingAi,
                () => $"Trying to spawn parked passenger car at road side for citizen {citizenId} @ {refPos}");

            parkPos = Vector3.zero;
            roadParkVehicleId = 0;

            if (FindParkingSpaceRoadSide(
                0,
                refPos,
                vehicleInfo.m_generatedInfo.m_size.x,
                vehicleInfo.m_generatedInfo.m_size.z,
                GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                out parkPos,
                out Quaternion parkRot,
                out _))
            {
                // position found, spawn a parked vehicle
                if (Singleton<VehicleManager>.instance.CreateParkedVehicle(
                    out ushort parkedVehicleId,
                    ref Singleton<SimulationManager>
                        .instance.m_randomizer,
                    vehicleInfo,
                    parkPos,
                    parkRot,
                    citizenId))
                {
                    roadParkVehicleId = parkedVehicleId;
                    if (updateCitizenParkedVehicle) {
                        parkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
                    }

                    if (logParkingAi) {
                        Log._Debug(
                            "[SUCCESS] Spawned parked passenger car at road side for citizen " +
                            $"{citizenId}: {parkedVehicleId} @ {parkPos}");
                    }

                    reason = ParkingError.None;
                    return true;
                }

                reason = ParkingError.LimitHit;
            } else {
                reason = ParkingError.NoSpaceFound;
            }

            Log._DebugIf(
                logParkingAi,
                () => $"[FAIL] Failed to spawn parked passenger car at road side for citizen {citizenId}");
            return false;
        }

        [UsedImplicitly]
        public bool TrySpawnParkedPassengerCarBuilding(uint citizenId,
                                                       ref Citizen citizen,
                                                       ushort homeId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason) {
            return TrySpawnParkedPassengerCarBuildingInternal(
                citizenId,
                ref citizen,
                homeId,
                refPos,
                vehicleInfo,
                out parkPos,
                out reason,
                out _,
                updateCitizenParkedVehicle: true);
        }

        public bool TrySpawnParkedPassengerCarBuildingInternal(uint citizenId,
                                                       ref Citizen citizen,
                                                       ushort homeId,
                                                       Vector3 refPos,
                                                       VehicleInfo vehicleInfo,
                                                       out Vector3 parkPos,
                                                       out ParkingError reason,
                                                       out ushort buildingParkVehicleId,
                                                       bool updateCitizenParkedVehicle = true) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            Log._DebugIf(
                extendedLogParkingAi && homeId != 0,
                () => "Trying to spawn parked passenger car next to building for citizen " +
                $"{citizenId} @ {refPos}");

            parkPos = Vector3.zero;
            buildingParkVehicleId = 0;

            if (FindParkingSpaceBuilding(
                vehicleInfo,
                homeId,
                0,
                0,
                refPos,
                GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding,
                out parkPos,
                out Quaternion parkRot,
                out _))
            {
                // position found, spawn a parked vehicle
                if (Singleton<VehicleManager>.instance.CreateParkedVehicle(
                    out ushort parkedVehicleId,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    vehicleInfo,
                    parkPos,
                    parkRot,
                    citizenId))
                {
                    buildingParkVehicleId = parkedVehicleId;
                    if (updateCitizenParkedVehicle) {
                        parkedVehicleId.AssignToCitizenAndMakeVisible(citizenId);
                    }

                    if (extendedLogParkingAi && homeId != 0) {
                        Log._Debug(
                            "[SUCCESS] Spawned parked passenger car next to building for citizen " +
                            $"{citizenId}: {parkedVehicleId} @ {parkPos}");
                    }

                    reason = ParkingError.None;
                    return true;
                }

                reason = ParkingError.LimitHit;
            } else {
                reason = ParkingError.NoSpaceFound;
            }

            Log._DebugIf(
                logParkingAi && homeId != 0,
                () => "[FAIL] Failed to spawn parked passenger car next to building " +
                $"for citizen {citizenId}");
            return false;
        }

        /// <summary>
        /// Swaps parked car with electric, returns electric vehicle info on success
        /// </summary>
        /// <param name="logParkingAi">should log</param>
        /// <param name="citizenId">citizen id</param>
        /// <param name="citizen">citizen data</param>
        /// <param name="position">parked vehicle position</param>
        /// <param name="rotation">parked vehicle rotation</param>
        /// <param name="electricVehicleInfo">electric vehicle info</param>
        /// <returns>true only if electric vehicle info exists and parked vehicle was swapped
        /// otherwise false
        /// </returns>
        internal static bool SwapParkedVehicleWithElectric(
                                                    bool logParkingAi,
                                                    uint citizenId,
                                                    ref Citizen citizen,
                                                    Vector3 position,
                                                    Quaternion rotation,
                                                    out VehicleInfo electricVehicleInfo) {
            Randomizer randomizer = new Randomizer(citizenId);
            electricVehicleInfo = Singleton<VehicleManager>.instance.GetRandomVehicleInfo(
                ref randomizer,
                ItemClass.Service.Residential,
                ItemClass.SubService.ResidentialLowEco,
                ItemClass.Level.Level1);
            if (!electricVehicleInfo) {
                if (logParkingAi) {
                    Log._Debug($"SwapParkedVehicleWithElectric({citizenId}): Electric VehicleInfo is null! Could not swap parked vehicle.");
                }
                // no electric cars available
                return false;
            }

            if (!VehicleManager.instance.CreateParkedVehicle(
                    out ushort parkedVehicleId,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    electricVehicleInfo,
                    position,
                    rotation,
                    citizenId)) {
                if (logParkingAi) {
                    Log._Debug($"SwapParkedVehicleWithElectric({citizenId}): Could not create parked vehicle. Out of free instances?");
                }
                return false;
            }

            // releases existing parked car, set new one
            citizen.SetParkedVehicle(citizenId, parkedVehicleId);
            parkedVehicleId.ToParkedVehicle().m_flags &= 65527; // clear Parking flag
            return true;

        }

        public bool FindParkingSpaceInVicinity(Vector3 targetPos,
                                               Vector3 searchDir,
                                               VehicleInfo vehicleInfo,
                                               ushort homeId,
                                               ushort vehicleId,
                                               float maxDist,
                                               out ExtParkingSpaceLocation parkingSpaceLocation,
                                               out ushort parkingSpaceLocationId,
                                               out Vector3 parkPos,
                                               out Quaternion parkRot,
                                               out float parkOffset) {
#if DEBUG
            bool vehDebug = DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleId;
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get() && vehDebug;
#else
            const bool logParkingAi = false;
#endif

            // TODO check isElectric
            Vector3 refPos = targetPos + (searchDir * 16f);

            // TODO depending on simulation accuracy, disable searching for both road-side and building parking spaces
            ushort parkingSpaceSegmentId = FindParkingSpaceAtRoadSide(
                0,
                refPos,
                vehicleInfo.m_generatedInfo.m_size.x,
                vehicleInfo.m_generatedInfo.m_size.z,
                maxDist,
                true,
                out Vector3 roadParkPos,
                out Quaternion roadParkRot,
                out float roadParkOffset);

            ushort parkingBuildingId = FindParkingSpaceBuilding(
                vehicleInfo,
                homeId,
                0,
                0,
                refPos,
                maxDist,
                maxDist,
                true,
                out Vector3 buildingParkPos,
                out Quaternion buildingParkRot,
                out float buildingParkOffset);

            if (parkingSpaceSegmentId != 0) {
                if (parkingBuildingId != 0) {
                    Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

                    // choose nearest parking position, after a bit of randomization
                    if ((roadParkPos - targetPos).sqrMagnitude < (buildingParkPos - targetPos).sqrMagnitude
                        && rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) != 0) {
                        // road parking space is closer

                        Log._DebugIf(
                            logParkingAi,
                            () => "Found an (alternative) road-side parking position for " +
                            $"vehicle {vehicleId} @ segment {parkingSpaceSegmentId} after comparing " +
                            $"distance with a building parking position @ {parkingBuildingId}!");

                        parkPos = roadParkPos;
                        parkRot = roadParkRot;
                        parkOffset = roadParkOffset;
                        parkingSpaceLocation = ExtParkingSpaceLocation.RoadSide;
                        parkingSpaceLocationId = parkingSpaceSegmentId;
                        return true;
                    }

                    // choose building parking space
                    Log._DebugIf(
                        logParkingAi,
                        () => $"Found an alternative building parking position for vehicle {vehicleId} " +
                        $"at building {parkingBuildingId} after comparing distance with a road-side " +
                        $"parking position @ {parkingSpaceSegmentId}!");

                    parkPos = buildingParkPos;
                    parkRot = buildingParkRot;
                    parkOffset = buildingParkOffset;
                    parkingSpaceLocation = ExtParkingSpaceLocation.Building;
                    parkingSpaceLocationId = parkingBuildingId;
                    return true;
                }

                // road-side but no building parking space found
                Log._DebugIf(
                    logParkingAi,
                    () => "Found an alternative road-side parking position for vehicle " +
                    $"{vehicleId} @ segment {parkingSpaceSegmentId}!");

                parkPos = roadParkPos;
                parkRot = roadParkRot;
                parkOffset = roadParkOffset;
                parkingSpaceLocation = ExtParkingSpaceLocation.RoadSide;
                parkingSpaceLocationId = parkingSpaceSegmentId;
                return true;
            }

            if (parkingBuildingId != 0) {
                // building but no road-side parking space found
                Log._DebugIf(
                    logParkingAi,
                    () => $"Found an alternative building parking position for vehicle {vehicleId} " +
                    $"at building {parkingBuildingId}!");

                parkPos = buildingParkPos;
                parkRot = buildingParkRot;
                parkOffset = buildingParkOffset;
                parkingSpaceLocation = ExtParkingSpaceLocation.Building;
                parkingSpaceLocationId = parkingBuildingId;
                return true;
            }

            // driverExtInstance.CurrentPathMode = ExtCitizenInstance.PathMode.AltParkFailed;
            parkingSpaceLocation = ExtParkingSpaceLocation.None;
            parkingSpaceLocationId = 0;
            parkPos = default;
            parkRot = default;
            parkOffset = -1f;
            Log._DebugIf(
                logParkingAi,
                () => $"Could not find a road-side or building parking position for vehicle {vehicleId}!");
            return false;
        }

        protected ushort FindParkingSpaceAtRoadSide(ushort ignoreParked,
                                                    Vector3 refPos,
                                                    float width,
                                                    float length,
                                                    float maxDistance,
                                                    bool randomize,
                                                    out Vector3 parkPos,
                                                    out Quaternion parkRot,
                                                    out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif

            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = 0f;

            var centerI = (int)((refPos.z / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((refPos.x / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));

            int radius = Math.Max(1, (int)(maxDistance / (BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

            NetManager netManager = Singleton<NetManager>.instance;
            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

            ushort foundSegmentId = 0;
            Vector3 myParkPos = parkPos;
            Quaternion myParkRot = parkRot;
            float myParkOffset = parkOffset;

            // Local function used for spiral loop below
            bool LoopHandler(int i, int j) {
                if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 ||
                    j >= BuildingManager.BUILDINGGRID_RESOLUTION) {
                    return true;
                }

                ushort segmentId = netManager.m_segmentGrid[(i * BuildingManager.BUILDINGGRID_RESOLUTION) + j];
                var iterations = 0;

                while (segmentId != 0) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    NetInfo segmentInfo = netSegment.Info;
                    Vector3 segCenter = netSegment.m_bounds.center;

                    // randomize target position to allow for opposite road-side parking
                    ParkingAI parkingAiConf = GlobalConfig.Instance.ParkingAI;
                    segCenter.x += rng.Int32(parkingAiConf.ParkingSpacePositionRand) -
                        (parkingAiConf.ParkingSpacePositionRand / 2u);

                    segCenter.z += rng.Int32(parkingAiConf.ParkingSpacePositionRand) -
                        (parkingAiConf.ParkingSpacePositionRand / 2u);

                    if (netSegment.GetClosestLanePosition(
                        segCenter,
                        NetInfo.LaneType.Parking,
                        VehicleInfo.VehicleType.Car,
                        VehicleInfo.VehicleCategory.PassengerCar,
                        out Vector3 innerParkPos,
                        out uint laneId,
                        out int laneIndex,
                        out _))
                    {
                        NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                        if (!SavedGameOptions.Instance.parkingRestrictionsEnabled ||
                            ParkingRestrictionsManager.Instance.IsParkingAllowed(
                                segmentId,
                                laneInfo.m_finalDirection))
                        {
                            if (!SavedGameOptions.Instance.vehicleRestrictionsEnabled ||
                                (VehicleRestrictionsManager.Instance.GetAllowedVehicleTypes(
                                     segmentId,
                                     segmentInfo,
                                     (uint)laneIndex,
                                     laneInfo,
                                     VehicleRestrictionsMode.Configured)
                                 & ExtVehicleType.PassengerCar) != ExtVehicleType.None)
                            {
                                if (_findParkingSpaceRoadSideDelegate(
                                    ignoreParked: ignoreParked,
                                    requireSegment: segmentId,
                                    refPos: innerParkPos,
                                    width: width,
                                    length: length,
                                    parkPos: out innerParkPos,
                                    parkRot: out Quaternion innerParkRot,
                                    parkOffset: out float innerParkOffset))
                                {
                                    Log._DebugIf(
                                        logParkingAi,
                                        () => "FindParkingSpaceRoadSide: Found a parking space for " +
                                        $"refPos {refPos}, segment center {segCenter} " +
                                        $"@ {innerParkPos}, laneId {laneId}, laneIndex {laneIndex}!");

                                    foundSegmentId = segmentId;
                                    myParkPos = innerParkPos;
                                    myParkRot = innerParkRot;
                                    myParkOffset = innerParkOffset;
                                    if (!randomize || rng.Int32(parkingAiConf
                                                .VicinityParkingSpaceSelectionRand) != 0) {
                                        return false;
                                    }
                                } // if find parking roadside
                            } // if allowed vehicle types
                        } // if parking allowed
                    } // if closest lane position

                    segmentId = netSegment.m_nextGridSegment;

                    if (++iterations >= NetManager.MAX_SEGMENT_COUNT) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                } // while segmentid

                return true;
            }

            var coords = _spiral.GetCoordsRandomDirection(radius, ref _randomizer);
            for (int i = 0; i < radius * radius; i++) {
                if (!LoopHandler((int)(centerI + coords[i].x), (int)(centerJ + coords[i].y))) {
                    break;
                }
            }

            if (foundSegmentId == 0) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"FindParkingSpaceRoadSide: Could not find a parking space for refPos {refPos}!");
                return 0;
            }

            parkPos = myParkPos;
            parkRot = myParkRot;
            parkOffset = myParkOffset;

            return foundSegmentId;
        }

        protected ushort FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                                  ushort homeID,
                                                  ushort ignoreParked,
                                                  ushort segmentId,
                                                  Vector3 refPos,
                                                  float maxBuildingDistance,
                                                  float maxParkingSpaceDistance,
                                                  bool randomize,
                                                  out Vector3 parkPos,
                                                  out Quaternion parkRot,
                                                  out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif

            parkPos = Vector3.zero;
            parkRot = Quaternion.identity;
            parkOffset = -1f;

            var centerI = (int)((refPos.z / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                (BuildingManager.BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((refPos.x / BuildingManager.BUILDINGGRID_CELL_SIZE) +
                                 BuildingManager.BUILDINGGRID_RESOLUTION / 2f);
            int radius = Math.Max(
                1,
                (int)(maxBuildingDistance / (BuildingManager.BUILDINGGRID_CELL_SIZE / 2f)) + 1);

            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer;

            ushort foundBuildingId = 0;
            Vector3 myParkPos = parkPos;
            Quaternion myParkRot = parkRot;
            float myParkOffset = parkOffset;

            // Local function used below in SpiralLoop
            bool LoopHandler(int i, int j) {
                if (i < 0 || i >= BuildingManager.BUILDINGGRID_RESOLUTION || j < 0 ||
                    j >= BuildingManager.BUILDINGGRID_RESOLUTION) {
                    return true;
                }

                ushort buildingId = Singleton<BuildingManager>.instance.m_buildingGrid[
                    (i * BuildingManager.BUILDINGGRID_RESOLUTION) + j];
                ref Building building = ref buildingId.ToBuilding();
                var numIterations = 0;
                ParkingAI parkingAiConf = GlobalConfig.Instance.ParkingAI;

                while (buildingId != 0) {
                    if (FindParkingSpacePropAtBuilding(
                        vehicleInfo,
                        homeID,
                        ignoreParked,
                        buildingId,
                        ref building,
                        segmentId,
                        refPos,
                        ref maxParkingSpaceDistance,
                        randomize,
                        out Vector3 innerParkPos,
                        out Quaternion innerParkRot,
                        out float innerParkOffset))
                    {
                        foundBuildingId = buildingId;
                        myParkPos = innerParkPos;
                        myParkRot = innerParkRot;
                        myParkOffset = innerParkOffset;

                        if (!randomize
                            || rng.Int32(parkingAiConf.VicinityParkingSpaceSelectionRand) != 0)
                        {
                            return false;
                        }
                    } // if find parking prop at building

                    buildingId = building.m_nextGridBuilding;
                    building = ref buildingId.ToBuilding();

                    if (++numIterations >= 49152) {
                        CODebugBase<LogChannel>.Error(
                            LogChannel.Core,
                            $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                } // while building id

                return true;
            }

            var coords = _spiral.GetCoordsRandomDirection(radius, ref _randomizer);
            for (int i = 0; i < radius * radius; i++) {
                if (!LoopHandler((int)(centerI + coords[i].x), (int)(centerJ + coords[i].y))) {
                    break;
                }
            }

            if (foundBuildingId == 0) {
                Log._DebugIf(
                    logParkingAi && homeID != 0,
                    () => $"FindParkingSpaceBuilding: Could not find a parking space for homeID {homeID}!");
                return 0;
            }

            parkPos = myParkPos;
            parkRot = myParkRot;
            parkOffset = myParkOffset;

            return foundBuildingId;
        }

        public bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo,
                                                   ushort homeId,
                                                   ushort ignoreParked,
                                                   ushort buildingId,
                                                   ref Building building,
                                                   ushort segmentId,
                                                   Vector3 refPos,
                                                   ref float maxDistance,
                                                   bool randomize,
                                                   out Vector3 parkPos,
                                                   out Quaternion parkRot,
                                                   out float parkOffset) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif
            // int buildingWidth = building.Width;
            int buildingLength = building.Length;

            // NON-STOCK CODE START
            parkOffset = -1f; // only set if segmentId != 0
            parkPos = default;
            parkRot = default;

            if ((building.m_flags & Building.Flags.Created) == Building.Flags.None) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is not created.");
                return false;
            }

            if ((building.m_problems & Notification.Problem1.TurnedOff).IsNotNone) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is not active.");
                return false;
            }

            if ((building.m_flags & Building.Flags.Collapsed) != Building.Flags.None) {
                Log._DebugIf(
                    logParkingAi,
                    () => $"Refusing to find parking space at building {buildingId}! Building is collapsed.");
                return false;
            }

            Randomizer rng = Singleton<SimulationManager>.instance.m_randomizer; // NON-STOCK CODE

            bool isElectric = vehicleInfo.m_class.m_subService != ItemClass.SubService.ResidentialLow;
            BuildingInfo buildingInfo = building.Info;
            Matrix4x4 transformMatrix = default;
            var transformMatrixCalculated = false;
            var result = false;

            if (buildingInfo.m_class.m_service == ItemClass.Service.Residential &&
                buildingId != homeId && rng.Int32((uint)SavedGameOptions.Instance.getRecklessDriverModulo()) != 0) {
                // NON-STOCK CODE
                return false;
            }
            if (building.m_accessSegment != 0) {
                NetInfo accessSegmentInfo = building.m_accessSegment.ToSegment().Info;
                if (accessSegmentInfo != null && accessSegmentInfo.IsPedestrianZoneOrPublicTransportRoad()) {
                    if (logParkingAi) {
                        Log._Debug($"Building connected to pedestrian zone road. {building.m_accessSegment}, building: {buildingId}, home: {homeId}, maxDist: {maxDistance}");
                    }
                    return false;
                }
            }

            var propMinDistance = 9999f; // NON-STOCK CODE

            if (buildingInfo.m_props != null &&
                (buildingInfo.m_hasParkingSpaces & VehicleInfo.VehicleType.Car) !=
                VehicleInfo.VehicleType.None)
            {
                foreach (BuildingInfo.Prop prop in buildingInfo.m_props) {
                    var randomizer = new Randomizer(buildingId << 6 | prop.m_index);
                    if (randomizer.Int32(100u) >= prop.m_probability ||
                        buildingLength < prop.m_requiredLength) {
                        continue;
                    }

                    PropInfo propInfo = prop.m_finalProp;
                    if (propInfo == null) {
                        continue;
                    }

                    propInfo = propInfo.GetVariation(ref randomizer);
                    if (propInfo.m_parkingSpaces == null || propInfo.m_parkingSpaces.Length == 0) {
                        continue;
                    }

                    if (!transformMatrixCalculated) {
                        transformMatrixCalculated = true;
                        Vector3 pos = Building.CalculateMeshPosition(
                            buildingInfo,
                            building.m_position,
                            building.m_angle,
                            building.Length);
                        Quaternion q = Quaternion.AngleAxis(
                            building.m_angle * Mathf.Rad2Deg,
                            Vector3.down);
                        transformMatrix.SetTRS(pos, q, Vector3.one);
                    }

                    Vector3 position = transformMatrix.MultiplyPoint(prop.m_position);
                    if (_findParkingSpacePropDelegate(
                            isElectric: isElectric,
                            ignoreParked: ignoreParked,
                            info: propInfo,
                            position: position,
                            angle: building.m_angle + prop.m_radAngle,
                            fixedHeight: prop.m_fixedHeight,
                            refPos: refPos,
                            width: vehicleInfo.m_generatedInfo.m_size.x,
                            length: vehicleInfo.m_generatedInfo.m_size.z,
                            maxDistance: ref propMinDistance,
                            parkPos: ref parkPos,
                            parkRot: ref parkRot))
                    {
                        // NON-STOCK CODE
                        result = true;
                        if (randomize
                            && propMinDistance <= maxDistance
                            && rng.Int32(GlobalConfig.Instance.ParkingAI.VicinityParkingSpaceSelectionRand) == 0)
                        {
                            break;
                        }
                    }
                }
            }

            if (result && propMinDistance <= maxDistance) {
                maxDistance = propMinDistance; // NON-STOCK CODE
                if (logParkingAi) {
                    Log._Debug(
                        $"Found parking space prop in range ({maxDistance}) at building {buildingId}.");
                }

                if (segmentId == 0) {
                    return true;
                }

                // check if building is accessible from the given segment
                Log._DebugIf(
                    logParkingAi,
                    () => $"Calculating despawn position of building {buildingId} for segment {segmentId}.");

                building.Info.m_buildingAI.CalculateUnspawnPosition(
                    buildingId,
                    ref building,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    vehicleInfo,
                    out Vector3 unspawnPos,
                    out _);

                // calculate segment offset
                if (segmentId.ToSegment().GetClosestLanePosition(
                        unspawnPos,
                        NetInfo.LaneType.Pedestrian,
                        VehicleInfo.VehicleType.None,
                        VehicleInfo.VehicleCategory.None,
                        out Vector3 lanePos,
                        out uint laneId,
                        out int laneIndex,
                        out float laneOffset))
                {
                    Log._DebugIf(
                        logParkingAi,
                        () => "Succeeded in finding despawn position lane offset for building " +
                        $"{buildingId}, segment {segmentId}, unspawnPos={unspawnPos}! " +
                        $"lanePos={lanePos}, dist={(lanePos - unspawnPos).magnitude}, " +
                        $"laneId={laneId}, laneIndex={laneIndex}, laneOffset={laneOffset}");

                    parkOffset = laneOffset;
                } else {
                    Log._DebugIf(
                        logParkingAi,
                        () => $"Could not find despawn position lane offset for building {buildingId}, " +
                        $"segment {segmentId}, unspawnPos={unspawnPos}!");
                }

                return true;
            }

            if (result && logParkingAi) {
                Log._Debug(
                    $"Could not find parking space prop in range ({maxDistance}) " +
                    $"at building {buildingId}.");
            }

            return false;
        }

        public bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo,
                                                          ushort ignoreParked,
                                                          ushort segmentId,
                                                          Vector3 refPos,
                                                          out Vector3 parkPos,
                                                          out Quaternion parkRot,
                                                          out float parkOffset,
                                                          out uint laneId,
                                                          out int laneIndex) {
#if DEBUG
            bool logParkingAi = DebugSwitch.VehicleParkingAILog.Get();
#else
            const bool logParkingAi = false;
#endif
            float width = vehicleInfo.m_generatedInfo.m_size.x;
            float length = vehicleInfo.m_generatedInfo.m_size.z;

            NetManager netManager = Singleton<NetManager>.instance;

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & NetSegment.Flags.Created) != NetSegment.Flags.None)
            {
                if (netSegment.GetClosestLanePosition(
                    refPos,
                    NetInfo.LaneType.Parking,
                    VehicleInfo.VehicleType.Car,
                    VehicleInfo.VehicleCategory.PassengerCar,
                    out parkPos,
                    out laneId,
                    out laneIndex,
                    out parkOffset))
                {
                    if (!SavedGameOptions.Instance.parkingRestrictionsEnabled ||
                        ParkingRestrictionsManager.Instance.IsParkingAllowed(
                            segmentId,
                            netSegment.Info.m_lanes[laneIndex].m_finalDirection))
                    {
                        if (_findParkingSpaceRoadSideDelegate(
                            ignoreParked: ignoreParked,
                            requireSegment: segmentId,
                            refPos: parkPos,
                            width: width,
                            length: length,
                            parkPos: out parkPos,
                            parkRot: out parkRot,
                            parkOffset: out parkOffset))
                        {
                            if (logParkingAi) {
                                Log._Debug(
                                    "FindParkingSpaceRoadSideForVehiclePos: Found a parking space " +
                                    $"for refPos {refPos} @ {parkPos}, laneId {laneId}, " +
                                    $"laneIndex {laneIndex}!");
                            }

                            return true;
                        }
                    }
                }
            }

            parkPos = default;
            parkRot = default;
            laneId = 0;
            laneIndex = -1;
            parkOffset = -1f;
            return false;
        }

        public bool FindParkingSpaceRoadSide(ushort ignoreParked,
                                             Vector3 refPos,
                                             float width,
                                             float length,
                                             float maxDistance,
                                             out Vector3 parkPos,
                                             out Quaternion parkRot,
                                             out float parkOffset) {
            return FindParkingSpaceAtRoadSide(
                       ignoreParked,
                       refPos,
                       width,
                       length,
                       maxDistance,
                       false,
                       out parkPos,
                       out parkRot,
                       out parkOffset) != 0;
        }

        public bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                             ushort homeId,
                                             ushort ignoreParked,
                                             ushort segmentId,
                                             Vector3 refPos,
                                             float maxBuildingDistance,
                                             float maxParkingSpaceDistance,
                                             out Vector3 parkPos,
                                             out Quaternion parkRot,
                                             out float parkOffset) {
            return FindParkingSpaceBuilding(
                       vehicleInfo,
                       homeId,
                       ignoreParked,
                       segmentId,
                       refPos,
                       maxBuildingDistance,
                       maxParkingSpaceDistance,
                       false,
                       out parkPos,
                       out parkRot,
                       out parkOffset) != 0;
        }

        public bool VanillaFindParkingSpaceWithoutRestrictions(bool isElectric,
                                                                ushort homeId,
                                                                Vector3 refPos,
                                                                Vector3 searchDir,
                                                                ushort segmentId,
                                                                float width,
                                                                float length,
                                                                out Vector3 parkPos,
                                                                out Quaternion parkRot,
                                                                out float parkOffset) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!_findParkingSpaceDelegate(isElectric: isElectric,
                                           homeId: homeId,
                                           refPos: refPos,
                                           searchDir: searchDir,
                                           segment: segmentId,
                                           width: width,
                                           length: length,
                                           parkPos: out parkPos,
                                           parkRot: out parkRot,
                                           parkOffset: out parkOffset)) {
                return false;
            }

            // in vanilla parkOffset is always >= 0 for RoadSideParkingSpace
            if (SavedGameOptions.Instance.parkingRestrictionsEnabled && parkOffset >= 0) {
                if (netSegment.GetClosestLanePosition(
                        refPos,
                        NetInfo.LaneType.Parking,
                        VehicleInfo.VehicleType.Car,
                        VehicleInfo.VehicleCategory.PassengerCar,
                        out _,
                        out _,
                        out int laneIndex,
                        out _)) {
                    NetInfo.Direction direction = netSegment.Info.m_lanes[laneIndex].m_finalDirection;
                    if (!ParkingRestrictionsManager.Instance.IsParkingAllowed(segmentId, direction)) {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool GetBuildingInfoViewColor(ushort buildingId,
                                             ref Building buildingData,
                                             ref ExtBuilding extBuilding,
                                             InfoManager.InfoMode infoMode,
                                             out Color? color) {
            color = null;
            InfoProperties.ModeProperties[] modeProperties
                = Singleton<InfoManager>.instance.m_properties.m_modeProperties;

            switch (infoMode) {
                case InfoManager.InfoMode.Traffic: {
                    // parking space demand info view
                    color = Color.Lerp(
                        modeProperties[(int)infoMode].m_targetColor,
                        modeProperties[(int)infoMode].m_negativeColor,
                        extBuilding.parkingSpaceDemand * 0.01f);
                    return true;
                }

                case InfoManager.InfoMode.Transport when !(buildingData.Info.m_buildingAI is DepotAI): {
                    // public transport demand info view
                    // TODO should not depend on UI class "TrafficManagerTool"
                    color = Color.Lerp(
                        modeProperties[(int)InfoManager.InfoMode.Traffic].m_targetColor,
                        modeProperties[(int)InfoManager.InfoMode.Traffic].m_negativeColor,
                            (TrafficManagerTool.CurrentTransportDemandViewMode ==
                             TransportDemandViewMode.Outgoing
                                 ? extBuilding.outgoingPublicTransportDemand
                             : extBuilding.incomingPublicTransportDemand) * 0.01f);
                    return true;
                }

                default:
                    return false;
            }
        }

        public string EnrichLocalizedCitizenStatus(string ret,
                                                   ref ExtCitizenInstance extInstance,
                                                   ref ExtCitizen extCitizen) {
            switch (extInstance.pathMode) {
                case ExtPathMode.ApproachingParkedCar:
                case ExtPathMode.RequiresCarPath:
                case ExtPathMode.RequiresMixedCarPathToTarget: {
                    ret = Translation.AICitizen.Get("Label:Entering vehicle") + ", " + ret;
                    break;
                }

                case ExtPathMode.RequiresWalkingPathToParkedCar:
                case ExtPathMode.CalculatingWalkingPathToParkedCar:
                case ExtPathMode.WalkingToParkedCar: {
                    ret = Translation.AICitizen.Get("Label:Walking to car") + ", " + ret;
                    break;
                }

                case ExtPathMode.CalculatingWalkingPathToTarget:
                case ExtPathMode.TaxiToTarget:
                case ExtPathMode.WalkingToTarget: {
                    if ((extCitizen.transportMode & ExtTransportMode.PublicTransport) != ExtTransportMode.None) {
                        ret = Translation.AICitizen.Get("Label:Using public transport")
                              + ", " + ret;
                    } else {
                        ret = Translation.AICitizen.Get("Label:Walking") + ", " + ret;
                    }

                    break;
                }

                case ExtPathMode.CalculatingCarPathToTarget:
                case ExtPathMode.CalculatingCarPathToKnownParkPos: {
                    ret = Translation.AICitizen.Get("Label:Thinking of a good parking spot")
                          + ", " + ret;
                    break;
                }
            }

            return ret;
        }

        public string EnrichLocalizedCarStatus(string ret, ref ExtCitizenInstance driverExtInstance) {
            switch (driverExtInstance.pathMode) {
                case ExtPathMode.DrivingToAltParkPos: {
                    if (driverExtInstance.failedParkingAttempts <= 1) {
                        ret = Translation.AICar.Get("Label:Driving to a parking spot")
                              + ", " + ret;
                    } else {
                        ret = Translation.AICar.Get("Label:Driving to another parking spot")
                              + " (#" + driverExtInstance.failedParkingAttempts + "), " + ret;
                    }

                    break;
                }

                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.DrivingToKnownParkPos: {
                    ret = Translation.AICar.Get("Label:Driving to a parking spot") + ", " + ret;
                    break;
                }

                case ExtPathMode.ParkingFailed:
                case ExtPathMode.CalculatingCarPathToAltParkPos: {
                    ret = Translation.AICar.Get("Label:Looking for a parking spot") + ", " + ret;
                    break;
                }

                case ExtPathMode.RequiresWalkingPathToTarget: {
                    ret = Locale.Get("VEHICLE_STATUS_PARKING") + ", " + ret;
                    break;
                }
            }

            return ret;
        }
    }
}

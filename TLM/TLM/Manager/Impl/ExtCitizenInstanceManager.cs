namespace TrafficManager.Manager.Impl {
    using ColossalFramework.Globalization;
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using ColossalFramework.Math;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using UnityEngine;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    public class ExtCitizenInstanceManager
        : AbstractCustomManager,
          ICustomDataManager<List<Configuration.ExtCitizenInstanceData>>,
          IExtCitizenInstanceManager
    {
        private ExtCitizenInstanceManager() {
            uint maxInstanceCount = CitizenManager.instance.m_instances.m_size;
            ExtInstances = new ExtCitizenInstance[maxInstanceCount];
            for (uint i = 0; i < maxInstanceCount; ++i) {
                ExtInstances[i] = new ExtCitizenInstance((ushort)i);
            }
        }

        public static readonly ExtCitizenInstanceManager Instance = new ExtCitizenInstanceManager();

        /// <summary>
        /// Gets all additional data for citizen instance. Index: citizen instance id.
        /// </summary>
        public ExtCitizenInstance[] ExtInstances { get; }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Extended citizen instance data:");

            CitizenInstance[] instanceBuffer = CitizenManager.instance.m_instances.m_buffer;
            for (uint citizenInstanceId = 0; citizenInstanceId < ExtInstances.Length; ++citizenInstanceId) {
                ref CitizenInstance citizenInstance = ref instanceBuffer[citizenInstanceId];
                if (!citizenInstance.IsValid()) {
                    continue;
                }

                Log._Debug($"Citizen instance {citizenInstanceId}: {ExtInstances[citizenInstanceId]}");
            }
        }

        public void OnReleaseInstance(ushort instanceId) {
            Reset(ref ExtInstances[instanceId]);
        }

        public void ResetInstance(ushort instanceId) {
            Reset(ref ExtInstances[instanceId]);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            Reset();
        }

        /// <summary>
        /// Generates the localized status for a tourist, which is displayed on <c>TouristWorldInfoPanel</c>.
        /// </summary>
        ///
        /// <param name="instanceID">Citizen instance id.</param>
        /// <param name="data">Citizen instance data.</param>
        /// <param name="mayAddCustomStatus">Will be <c>true</c> if the status can be customised by callee.</param>
        /// <param name="target">The instance of target building or node.</param>
        ///
        /// <returns>Returns the localised tourist status.</returns>
        public string GetTouristLocalizedStatus(ushort instanceID,
                                                ref CitizenInstance data,
                                                out bool mayAddCustomStatus,
                                                out InstanceID target) {
            mayAddCustomStatus = true;
            target = InstanceID.Empty;

            ushort targetBuildingId = data.m_targetBuilding;

            if (IsSweaptAway(ref data) || targetBuildingId == 0) {
                mayAddCustomStatus = false;
                return Locale.Get("CITIZEN_STATUS_CONFUSED");
            }

            uint citizenId = data.m_citizen;
            ushort vehicleId = 0;

            // if true, it means the targetBuildingId is actually a node, not a building
            bool targetIsNode = (data.m_flags & CitizenInstance.Flags.TargetIsNode) != 0;

            if (citizenId != 0u) {
                ref Citizen citizen = ref CitizenManager.instance.m_citizens.m_buffer[citizenId];
                vehicleId = citizen.m_vehicle;
            }

            if (vehicleId != 0) {
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                VehicleInfo info = vehicle.Info;

                switch (info.m_class.m_service) {

                    case ItemClass.Service.Residential
                        when info.m_vehicleType != VehicleInfo.VehicleType.Bicycle
                             && IsVehicleOwnedByCitizen(ref vehicle, citizenId):

                        if (targetIsNode) {
                            target.NetNode = targetBuildingId;
                            return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
                        }

                        if (IsOutsideConnection(targetBuildingId)) {
                            return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
                        }

                        target.Building = targetBuildingId;
                        return Locale.Get("CITIZEN_STATUS_DRIVINGTO");

                    case ItemClass.Service.PublicTransport:
                    case ItemClass.Service.Disaster:

                        if (targetIsNode) {

                            if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != 0) {
                                return Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
                            }

                            ushort transportLine = targetBuildingId.ToNode().m_transportLine;
                            if (vehicle.m_transportLine != transportLine) {
                                target.NetNode = targetBuildingId;
                                return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
                            }

                            break;
                        }

                        if (IsOutsideConnection(targetBuildingId)) {
                            return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
                        }

                        target.Building = targetBuildingId;
                        return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
                }
            }

            if (targetIsNode) {

                target.NetNode = targetBuildingId;

                if ((data.m_flags & CitizenInstance.Flags.OnTour) != 0) {
                    return Locale.Get("CITIZEN_STATUS_VISITING");
                }

                return Locale.Get("CITIZEN_STATUS_GOINGTO");
            }

            if (IsOutsideConnection(targetBuildingId)) {
                return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
            }

            target.Building = targetBuildingId;

            if (IsHangingAround(ref data)) {
                mayAddCustomStatus = false;
                return Locale.Get("CITIZEN_STATUS_VISITING");
            }

            return Locale.Get("CITIZEN_STATUS_GOINGTO");
        }

        /// <summary>
        /// Generates the localized status for a resident, which is displayed on <c>CitizenWorldInfoPanel</c>.
        /// </summary>
        ///
        /// <param name="instanceID">Citizen instance id.</param>
        /// <param name="data">Citizen instance data.</param>
        /// <param name="mayAddCustomStatus">Will be <c>true</c> if the status can be customised by callee.</param>
        /// <param name="target">The instance of target building or node.</param>
        ///
        /// <returns>Returns the localised resident status.</returns>
        public string GetResidentLocalizedStatus(ushort instanceID,
                                                 ref CitizenInstance data,
                                                 out bool mayAddCustomStatus,
                                                 out InstanceID target) {

            mayAddCustomStatus = true;
            target = InstanceID.Empty;

            ushort targetBuildingId = data.m_targetBuilding;

            if (IsSweaptAway(ref data) || targetBuildingId == 0) {
                mayAddCustomStatus = false;
                return Locale.Get("CITIZEN_STATUS_CONFUSED");
            }

            uint citizenId = data.m_citizen;
            var isStudent = false;
            ushort homeId = 0;
            ushort workId = 0;
            ushort vehicleId = 0;

            // if true, it means the targetBuildingId is actually a node, not a building
            bool targetIsNode = (data.m_flags & CitizenInstance.Flags.TargetIsNode) != 0;

            if (citizenId != 0u) {
                ref Citizen citizen = ref CitizenManager.instance.m_citizens.m_buffer[citizenId];
                homeId = citizen.m_homeBuilding;
                workId = citizen.m_workBuilding;
                vehicleId = citizen.m_vehicle;
                isStudent = (citizen.m_flags & Citizen.Flags.Student) != 0;
            }

            if (vehicleId != 0) {
                ref Vehicle vehicle = ref vehicleId.ToVehicle();
                VehicleInfo info = vehicle.Info;

                switch (info.m_class.m_service) {

                    case ItemClass.Service.Residential
                        when info.m_vehicleType != VehicleInfo.VehicleType.Bicycle &&
                             IsVehicleOwnedByCitizen(ref vehicle, citizenId):

                        if (targetIsNode) {
                            target.NetNode = targetBuildingId;
                            return Locale.Get("CITIZEN_STATUS_DRIVINGTO");
                        }

                        if (IsOutsideConnection(targetBuildingId)) {
                            return Locale.Get("CITIZEN_STATUS_DRIVINGTO_OUTSIDE");
                        }

                        if (targetBuildingId == homeId) {
                            return Locale.Get("CITIZEN_STATUS_DRIVINGTO_HOME");
                        }

                        if (targetBuildingId == workId) {
                            return isStudent
                                ? Locale.Get("CITIZEN_STATUS_DRIVINGTO_SCHOOL")
                                : Locale.Get("CITIZEN_STATUS_DRIVINGTO_WORK");
                        }

                        target.Building = targetBuildingId;
                        return Locale.Get("CITIZEN_STATUS_DRIVINGTO");

                    case ItemClass.Service.PublicTransport:
                    case ItemClass.Service.Disaster:

                        if (targetIsNode) {

                            if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != 0) {
                                return Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
                            }

                            ushort transportLine = targetBuildingId.ToNode().m_transportLine;
                            if (vehicle.m_transportLine != transportLine) {
                                target.NetNode = targetBuildingId;
                                return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
                            }

                            break;
                        }

                        if ((data.m_flags & CitizenInstance.Flags.WaitingTaxi) != CitizenInstance.Flags.None) {
                            return Locale.Get("CITIZEN_STATUS_WAITING_TAXI");
                        }

                        if (IsOutsideConnection(targetBuildingId)) {
                            return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_OUTSIDE");
                        }

                        if (targetBuildingId == homeId) {
                            return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_HOME");
                        }

                        if (targetBuildingId == workId) {
                            return isStudent
                                ? Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_SCHOOL")
                                : Locale.Get("CITIZEN_STATUS_TRAVELLINGTO_WORK");
                        }

                        target.Building = targetBuildingId;
                        return Locale.Get("CITIZEN_STATUS_TRAVELLINGTO");
                }
            }

            if (targetIsNode) {
                target.NetNode = targetBuildingId;

                if ((data.m_flags & CitizenInstance.Flags.OnTour) != 0) {
                    return Locale.Get("CITIZEN_STATUS_VISITING");
                }

                return Locale.Get("CITIZEN_STATUS_GOINGTO");
            }

            if (IsOutsideConnection(targetBuildingId)) {
                return Locale.Get("CITIZEN_STATUS_GOINGTO_OUTSIDE");
            }

            if (targetBuildingId == homeId) {
                if (IsHangingAround(ref data)) {
                    mayAddCustomStatus = false;
                    return Locale.Get("CITIZEN_STATUS_AT_HOME");
                }

                return Locale.Get("CITIZEN_STATUS_GOINGTO_HOME");
            }

            if (targetBuildingId == workId) {
                if (IsHangingAround(ref data)) {
                    mayAddCustomStatus = false;
                    return isStudent
                        ? Locale.Get("CITIZEN_STATUS_AT_SCHOOL")
                        : Locale.Get("CITIZEN_STATUS_AT_WORK");
                }

                return isStudent
                    ? Locale.Get("CITIZEN_STATUS_GOINGTO_SCHOOL")
                    : Locale.Get("CITIZEN_STATUS_GOINGTO_WORK");
            }

            target.Building = targetBuildingId;

            if (IsHangingAround(ref data)) {
                mayAddCustomStatus = false;
                return Locale.Get("CITIZEN_STATUS_VISITING");
            }

            return Locale.Get("CITIZEN_STATUS_GOINGTO");
        }

        public bool StartPathFind(
            ushort instanceID,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            ref ExtCitizen extCitizen,
            Vector3 startPos,
            Vector3 endPos,
            VehicleInfo vehicleInfo,
            bool enableTransport,
            bool ignoreCost) {

            try {
                return InternalStartPathFind(
                    instanceID,
                    ref instanceData,
                    ref extInstance,
                    ref extCitizen,
                    startPos,
                    endPos,
                    vehicleInfo,
                    enableTransport,
                    ignoreCost);
            }
            catch (Exception ex) {
                // make sure we have copy of exception in TMPE.log
                Log.Info(ex.ToString());

                // run the code again, this time with full debug logging
                try {
                    InternalStartPathFind(
                        instanceID,
                        ref instanceData,
                        ref extInstance,
                        ref extCitizen,
                        startPos,
                        endPos,
                        vehicleInfo,
                        enableTransport,
                        ignoreCost,
                        true);
                }
                catch {
                    // ignore
                }

                throw;
            }
        }

        internal bool InternalStartPathFind(
            ushort instanceID,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            ref ExtCitizen extCitizen,
            Vector3 startPos,
            Vector3 endPos,
            VehicleInfo vehicleInfo,
            bool enableTransport,
            bool ignoreCost,
            bool adhocDebugLog = false) {
#if DEBUG
            bool citizenDebug
                = (DebugSettings.CitizenInstanceId == 0
                   || DebugSettings.CitizenInstanceId == instanceID)
                  && (DebugSettings.CitizenId == 0
                      || DebugSettings.CitizenId == instanceData.m_citizen)
                  && (DebugSettings.SourceBuildingId == 0
                      || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                  && (DebugSettings.TargetBuildingId == 0
                      || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;

#else
            bool logParkingAi = adhocDebugLog;
            bool extendedLogParkingAi = adhocDebugLog;
#endif

            if (logParkingAi) {
                Log.Info(
                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): called for citizen " +
                    $"{instanceData.m_citizen}, startPos={startPos}, endPos={endPos}, " +
                    $"sourceBuilding={instanceData.m_sourceBuilding}, " +
                    $"targetBuilding={instanceData.m_targetBuilding}, " +
                    $"pathMode={extInstance.pathMode}, enableTransport={enableTransport}, " +
                    $"ignoreCost={ignoreCost}");
            }

            // NON-STOCK CODE START
            CitizenManager citizenManager = CitizenManager.instance;
            ref Citizen citizen = ref citizenManager.m_citizens.m_buffer[instanceData.m_citizen];
            ushort parkedVehicleId = citizen.m_parkedVehicle;
            ushort homeId = citizen.m_homeBuilding;
            CarUsagePolicy carUsageMode = CarUsagePolicy.Allowed;
            var startsAtOutsideConnection = false;
            ParkingAI parkingAiConf = GlobalConfig.Instance.ParkingAI;

            if (SavedGameOptions.Instance.parkingAI) {
                switch (extInstance.pathMode) {
                    case ExtPathMode.RequiresWalkingPathToParkedCar:
                    case ExtPathMode.CalculatingWalkingPathToParkedCar:
                    case ExtPathMode.WalkingToParkedCar:
                    case ExtPathMode.ApproachingParkedCar: {
                            if (parkedVehicleId == 0) {
                                // Parked vehicle not present but citizen wants to reach it
                                // -> Reset path mode
                                if (logParkingAi) {
                                    Log.Info(
                                        $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen " +
                                        $"has CurrentPathMode={extInstance.pathMode} but no parked " +
                                        "vehicle present. Change to 'None'.");
                                }

                                Reset(ref extInstance);
                            } else {
                                // Parked vehicle is present and citizen wants to reach it
                                // -> Prohibit car usage
                                if (extendedLogParkingAi) {
                                    Log.Info(
                                        $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen " +
                                        $"has CurrentPathMode={extInstance.pathMode}.  Change to " +
                                        "'CalculatingWalkingPathToParkedCar'.");
                                }

                                extInstance.pathMode =
                                    ExtPathMode.CalculatingWalkingPathToParkedCar;
                                carUsageMode = CarUsagePolicy.Forbidden;
                            }

                            break;
                        }

                    case ExtPathMode.RequiresWalkingPathToTarget:
                    case ExtPathMode.CalculatingWalkingPathToTarget:
                    case ExtPathMode.WalkingToTarget: {
                            // Citizen walks to target
                            // -> Reset path mode
                            if (extendedLogParkingAi) {
                                Log.Info(
                                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen " +
                                    $"has CurrentPathMode={extInstance.pathMode}. Change to " +
                                    "'CalculatingWalkingPathToTarget'.");
                            }

                            extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
                            carUsageMode = CarUsagePolicy.Forbidden;
                            break;
                        }

                    case ExtPathMode.RequiresCarPath:
                    case ExtPathMode.RequiresMixedCarPathToTarget:
                    case ExtPathMode.DrivingToTarget:
                    case ExtPathMode.DrivingToKnownParkPos:
                    case ExtPathMode.DrivingToAltParkPos:
                    case ExtPathMode.CalculatingCarPathToAltParkPos:
                    case ExtPathMode.CalculatingCarPathToKnownParkPos:
                    case ExtPathMode.CalculatingCarPathToTarget: {
                            if (parkedVehicleId == 0) {
                                // Citizen wants to drive to target but parked vehicle is not present
                                // -> Reset path mode
                                if (logParkingAi) {
                                    Log.Info(
                                        $"CustomCitizenAI.ExtStartPathFind({instanceID}): " +
                                        $"Citizen has CurrentPathMode={extInstance.pathMode} but " +
                                        "no parked vehicle present. Change to 'None'.");
                                }

                                Reset(ref extInstance);
                            } else {
                                // Citizen wants to drive to target and parked vehicle is present
                                // -> Force parked car usage
                                if (extendedLogParkingAi) {
                                    Log.Info(
                                        $"CustomCitizenAI.ExtStartPathFind({instanceID}): " +
                                        $"Citizen has CurrentPathMode={extInstance.pathMode}.  " +
                                        "Change to 'RequiresCarPath'.");
                                }

                                extInstance.pathMode = ExtPathMode.RequiresCarPath;
                                carUsageMode = CarUsagePolicy.ForcedParked;
                                startPos = parkedVehicleId.ToParkedVehicle().m_position; // force to start from the parked car
                            }

                            break;
                        }

                    default: {
                            if (logParkingAi) {
                                Log.Info(
                                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen has " +
                                    $"CurrentPathMode={extInstance.pathMode}. Change to 'None'.");
                            }

                            Reset(ref extInstance);
                            break;
                        }
                }

                startsAtOutsideConnection =
                    Constants.ManagerFactory.ExtCitizenInstanceManager.IsAtOutsideConnection(
                        instanceID,
                        ref instanceData,
                        ref extInstance,
                        startPos);

                if (extInstance.pathMode == ExtPathMode.None) {
                    bool isOnTour = (instanceData.m_flags & CitizenInstance.Flags.OnTour) !=
                                    CitizenInstance.Flags.None;

                    if (isOnTour || ignoreCost) {
                        // Citizen is on a walking tour or is a mascot
                        // -> Prohibit car usage
                        if (logParkingAi) {
                            Log.Info(
                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen ignores " +
                                $"cost ({ignoreCost}) or is on a walking tour ({isOnTour}): Setting " +
                                "path mode to 'CalculatingWalkingPathToTarget'");
                        }

                        carUsageMode = CarUsagePolicy.Forbidden;
                        extInstance.pathMode = ExtPathMode.CalculatingWalkingPathToTarget;
                    } else {
                        // Citizen is not on a walking tour and is not a mascot
                        // -> Check if citizen is located at an outside connection and make them
                        // obey Parking AI restrictions
                        if (instanceData.m_sourceBuilding != 0) {
                            ItemClass.Service sourceBuildingService = instanceData
                                .m_sourceBuilding
                                .ToBuilding()
                                .Info
                                .m_class
                                .m_service;

                            if (startsAtOutsideConnection) {
                                if (sourceBuildingService == ItemClass.Service.Road) {
                                    if (vehicleInfo != null) {
                                        // Citizen is located at a road outside connection and can spawn a car
                                        // -> Force car usage
                                        if (logParkingAi) {
                                            Log.Info(
                                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): " +
                                                "Citizen is located at a road outside connection: " +
                                                "Setting path mode to 'RequiresCarPath' and carUsageMode " +
                                                $"to 'ForcedPocket', pos: {instanceData.GetLastFramePosition()}");
                                        }

                                        extInstance.pathMode = ExtPathMode.RequiresCarPath;
                                        carUsageMode = CarUsagePolicy.ForcedPocket;
                                    } else {
                                        // Citizen is located at a non-road outside connection and
                                        // cannot spawn a car
                                        // -> Path-finding fails
                                        if (logParkingAi) {
                                            Log.Info(
                                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): " +
                                                "Citizen is located at a road outside connection but " +
                                                "does not have a car template: ABORTING PATH-FINDING");
                                        }

                                        Reset(ref extInstance);
                                        return false;
                                    }
                                } else {
                                    // Citizen is located at a non-road outside connection
                                    // -> Prohibit car usage
                                    if (logParkingAi) {
                                        Log.Info(
                                            $"CustomCitizenAI.ExtStartPathFind({instanceID}): " +
                                            "Citizen is located at a non-road outside connection: " +
                                            $"Setting path mode to 'CalculatingWalkingPathToTarget', pos: {instanceData.GetLastFramePosition()}");
                                    }

                                    extInstance.pathMode =
                                        ExtPathMode.CalculatingWalkingPathToTarget;
                                    carUsageMode = CarUsagePolicy.Forbidden;
                                }
                            } // starts at outside connection
                        } // sourceBuilding != 0
                    } // not isOnTour && not ignoreCost
                } // if (extInstance.pathMode == ExtPathMode.None)

                if ((carUsageMode == CarUsagePolicy.Allowed ||
                     carUsageMode == CarUsagePolicy.ForcedParked) && parkedVehicleId != 0) {
                    // Reuse parked vehicle info
                    ref VehicleParked parkedVehicle = ref parkedVehicleId.ToParkedVehicle();
                    vehicleInfo = parkedVehicle.Info;

                    if (homeId != 0 && vehicleInfo &&
                        !citizen.m_flags.IsFlagSet(Citizen.Flags.Tourist) &&
                        ExtVehicleManager.MustSwapParkedCarWithElectric(vehicleInfo, homeId)) {
                        if (logParkingAi) {
                            Log.Info($"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen {instanceData.m_citizen}. Swapping currently parked vehicle ({parkedVehicleId}) with electric");
                        }

                        if (AdvancedParkingManager.SwapParkedVehicleWithElectric(
                                logParkingAi: logParkingAi,
                                citizenId: instanceData.m_citizen,
                                citizen: ref citizen,
                                position: parkedVehicle.m_position,
                                rotation: parkedVehicle.m_rotation,
                                electricVehicleInfo: out VehicleInfo electricVehicleInfo)) {
                            vehicleInfo = electricVehicleInfo;
                            parkedVehicleId = citizen.m_parkedVehicle;
                            parkedVehicle = ref parkedVehicleId.ToParkedVehicle();
                        }
                    }

                    // Check if the citizen should return their car back home
                    if (extInstance.pathMode == ExtPathMode.None && // initiating a new path
                        homeId != 0 && // home building present
                        instanceData.m_targetBuilding == homeId // current target is home
                        ) {
                        // citizen travels back home
                        // -> check if their car should be returned
                        if ((extCitizen.lastTransportMode & ExtTransportMode.Car) !=
                            ExtTransportMode.None) {
                            // citizen travelled by car
                            // -> return car back home
                            extInstance.pathMode =
                                ExtPathMode.CalculatingWalkingPathToParkedCar;
                            carUsageMode = CarUsagePolicy.Forbidden;

                            if (extendedLogParkingAi) {
                                Log.Info(
                                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen used " +
                                    "their car before and is not at home. Forcing to walk to parked car.");
                            }
                        } else {
                            // citizen traveled by other means of transport
                            // -> check distance between home and parked car. if too far away:
                            // force to take the car back home
                            float distHomeToParked = (parkedVehicle.m_position - homeId.ToBuilding().m_position).magnitude;

                            if (distHomeToParked > parkingAiConf.MaxParkedCarDistanceToHome) {
                                // force to take car back home
                                extInstance.pathMode =
                                    ExtPathMode.CalculatingWalkingPathToParkedCar;
                                carUsageMode = CarUsagePolicy.Forbidden;

                                if (extendedLogParkingAi) {
                                    Log.Info(
                                        $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen " +
                                        "wants to go home and parked car is too far away " +
                                        $"({distHomeToParked}). Forcing walking to parked car.");
                                }
                            }
                        }
                    }
                }

                //--------------------------------------------------------------
                // The following holds:
                // - pathMode is now either CalculatingWalkingPathToParkedCar,
                //     CalculatingWalkingPathToTarget, RequiresCarPath or None.
                // - if pathMode is CalculatingWalkingPathToParkedCar or RequiresCarPath: parked
                //     car is present and citizen is not on a walking tour
                // - carUsageMode is valid
                // - if pathMode is RequiresCarPath: carUsageMode is either ForcedParked or
                //     ForcedPocket
                //--------------------------------------------------------------

                // modify path-finding constraints (vehicleInfo, endPos) if citizen is forced to walk
                if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar ||
                    extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToTarget) {
                    // vehicle must not be used since we need a walking path to either
                    // 1. a parked car or
                    // 2. the target building
                    if (extInstance.pathMode == ExtPathMode.CalculatingWalkingPathToParkedCar) {
                        // walk to parked car
                        // -> end position is parked car
                        endPos = parkedVehicleId.ToParkedVehicle().m_position;
                        if (extendedLogParkingAi) {
                            Log.Info(
                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen shall " +
                                $"go to parked vehicle @ {endPos}");
                        }
                    }
                }
            } // if SavedGameOptions.Instance.ParkingAi

            if (extendedLogParkingAi) {
                Log.Info(
                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen is allowed to " +
                    $"drive their car? {carUsageMode}");
            }

            // NON-STOCK CODE END
            //------------------------------------------------------------------
            // semi-stock code: determine path-finding parameters (laneTypes, vehicleTypes,
            // extVehicleType, etc.)
            //------------------------------------------------------------------
            NetInfo.LaneType laneTypes = NetInfo.LaneType.Pedestrian;
            VehicleInfo.VehicleType vehicleTypes = VehicleInfo.VehicleType.None;
            VehicleInfo.VehicleCategory vehicleCategory = VehicleInfo.VehicleCategory.None;

            bool randomParking = false;
            bool combustionEngine = false;
            ExtVehicleType extVehicleType = ExtVehicleType.None;

            if (vehicleInfo != null)
            {
                if (vehicleInfo.m_class.m_subService == ItemClass.SubService.PublicTransportTaxi)
                {
                    if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTaxi) ==
                        CitizenInstance.Flags.None
                        && Singleton<DistrictManager>
                           .instance.m_districts.m_buffer[0].m_productionData.m_finalTaxiCapacity != 0u)
                    {
                        SimulationManager instance = Singleton<SimulationManager>.instance;

                        if (instance.m_isNightTime || instance.m_randomizer.Int32(2u) == 0) {
                            laneTypes |= NetInfo.LaneType.Vehicle |
                                         NetInfo.LaneType.TransportVehicle;
                            vehicleTypes |= vehicleInfo.m_vehicleType;
                            vehicleCategory |= vehicleInfo.vehicleCategory;
                            extVehicleType = ExtVehicleType.Taxi; // NON-STOCK CODE
                            // NON-STOCK CODE START
                            if (SavedGameOptions.Instance.parkingAI) {
                                extInstance.pathMode = ExtPathMode.TaxiToTarget;
                            }

                            // NON-STOCK CODE END
                        }
                    }
                } else {
                    switch (vehicleInfo.m_vehicleType) {
                        // NON-STOCK CODE START
                        case VehicleInfo.VehicleType.Car: {
                            if (carUsageMode != CarUsagePolicy.Forbidden) {
                                extVehicleType = ExtVehicleType.PassengerCar;
                                laneTypes |= NetInfo.LaneType.Vehicle;
                                vehicleTypes |= vehicleInfo.m_vehicleType;
                                vehicleCategory |= vehicleInfo.vehicleCategory;
                                combustionEngine =
                                    vehicleInfo.m_class.m_subService ==
                                    ItemClass.SubService.ResidentialLow;
                            }

                            break;
                        }

                        case VehicleInfo.VehicleType.Bicycle:
                            extVehicleType = ExtVehicleType.Bicycle;
                            laneTypes |= NetInfo.LaneType.Vehicle;
                            vehicleTypes |= vehicleInfo.m_vehicleType;
                            vehicleCategory |= vehicleInfo.vehicleCategory;
                            break;
                    }
                }

                // NON-STOCK CODE END
            }

            // NON-STOCK CODE START
            ExtPathType extPathType = ExtPathType.None;
            PathUnit.Position endPosA = default;
            bool calculateEndPos = true;
            bool allowRandomParking = true;
            ref Building targetBuilding = ref instanceData.m_targetBuilding.ToBuilding();

            if (SavedGameOptions.Instance.parkingAI) {
                // Parking AI
                if (extInstance.pathMode == ExtPathMode.RequiresCarPath) {
                    if (logParkingAi) {
                        Log.Info(
                            $"CustomCitizenAI.ExtStartPathFind({instanceID}): Setting " +
                            $"startPos={startPos} for citizen instance {instanceID}. " +
                            $"CurrentDepartureMode={extInstance.pathMode}");
                    }

                    if (instanceData.m_targetBuilding == 0 ||
                        (targetBuilding.m_flags & Building.Flags.IncomingOutgoing) == Building.Flags.None) {
                        // the citizen is starting their journey and the target is not an outside
                        // connection
                        // -> find a suitable parking space near the target
                        if (logParkingAi) {
                            Log.Info(
                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding parking " +
                                $"space at target for citizen instance {instanceID}. " +
                                $"CurrentDepartureMode={extInstance.pathMode} parkedVehicleId={parkedVehicleId}");
                        }

                        // find a parking space in the vicinity of the target
                        if (AdvancedParkingManager.Instance.FindParkingSpaceForCitizen(
                                endPos,
                                vehicleInfo,
                                ref instanceData,
                                ref extInstance,
                                homeId,
                                instanceData.m_targetBuilding == homeId,
                                0,
                                false,
                                out Vector3 parkPos,
                                ref endPosA,
                                out bool calcEndPos)
                            && CalculateReturnPath(
                                ref extInstance,
                                parkPos,
                                endPos)) {
                            // success
                            extInstance.pathMode = ExtPathMode.CalculatingCarPathToKnownParkPos;

                            // if true, the end path position still needs to be calculated
                            calculateEndPos = calcEndPos;

                            // find a direct path to the calculated parking position
                            allowRandomParking = false;
                            if (logParkingAi) {
                                Log.Info(
                                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Finding " +
                                    $"known parking space for citizen instance {instanceID}, parked " +
                                    $"vehicle {parkedVehicleId} succeeded and return path " +
                                    $"{extInstance.returnPathId} ({extInstance.returnPathState}) " +
                                    $"is calculating. PathMode={extInstance.pathMode}");
                            }

                            // if (!extInstance.CalculateReturnPath(parkPos, endPos)) {
                            //    // TODO retry?
                            //    if (debug)
                            //        Log._Debug(
                            //            $"CustomCitizenAI.CustomStartPathFind: [PFFAIL] Could not
                            // calculate return path for citizen instance {instanceID}, parked vehicle
                            // {parkedVehicleId}. Calling OnPathFindFailed.");
                            //    CustomHumanAI.OnPathFindFailure(extInstance);
                            //    return false;
                            // }
                        }
                    }

                    if (extInstance.pathMode == ExtPathMode.RequiresCarPath) {
                        // no known parking space found (pathMode has not been updated in the block above)
                        // -> calculate direct path to target
                        if (logParkingAi) {
                            Log.Info(
                                $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance " +
                                $"{instanceID} is still at CurrentPathMode={extInstance.pathMode} " +
                                "(no parking space found?). Setting it to CalculatingCarPath. " +
                                $"parkedVehicleId={parkedVehicleId}");
                        }

                        extInstance.pathMode = ExtPathMode.CalculatingCarPathToTarget;
                    }
                }

                // determine path type from path mode
                extPathType = extInstance.GetPathType();
                extInstance.atOutsideConnection = startsAtOutsideConnection;

                //------------------------------------------
                // the following holds:
                // - pathMode is now either CalculatingWalkingPathToParkedCar,
                // CalculatingWalkingPathToTarget, CalculatingCarPathToTarget,
                // CalculatingCarPathToKnownParkPos or None.
                //------------------------------------------
            } // end if SavedGameOptions.Instance.ParkingAi

            /*
             * enable random parking if exact parking space was not calculated yet
             */
            if (extVehicleType == ExtVehicleType.PassengerCar ||
                extVehicleType == ExtVehicleType.Bicycle) {

                if (allowRandomParking
                    && instanceData.m_targetBuilding != 0
                    && targetBuilding.Info
                    && (
                        targetBuilding.Info.m_class.m_service > ItemClass.Service.Office
                        || (instanceData.m_flags & CitizenInstance.Flags.TargetIsNode) != 0
                       )
                    )
                {

                    randomParking = true;
                }
            }

            // NON-STOCK CODE END
            //---------------------------------------------------
            // determine the path position of the parked vehicle
            //---------------------------------------------------
            PathUnit.Position parkedVehiclePathPos = default;

            if (parkedVehicleId != 0 && extVehicleType == ExtVehicleType.PassengerCar) {
                Vector3 position = parkedVehicleId.ToParkedVehicle().m_position;
                Constants.ManagerFactory.ExtPathManager.FindPathPositionWithSpiralLoop(
                    position,
                    ItemClass.Service.Road,
                    NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle,
                    VehicleInfo.VehicleType.Car,
                    VehicleInfo.VehicleCategory.PassengerCar,
                    NetInfo.LaneType.Pedestrian,
                    VehicleInfo.VehicleType.None,
                    false,
                    false,
                    parkingAiConf.MaxBuildingToPedestrianLaneDistance,
                    false,
                    false,
                    out parkedVehiclePathPos);
            }

            bool allowUnderground =
                (instanceData.m_flags &
                 (CitizenInstance.Flags.Underground | CitizenInstance.Flags.Transition)) !=
                CitizenInstance.Flags.None;

            if (logParkingAi) {
                Log.Info($"CustomCitizenAI.ExtStartPathFind({instanceID}): Requesting path-finding " +
                           $"for citizen instance {instanceID}, citizen {instanceData.m_citizen}, " +
                           $"extVehicleType={extVehicleType}, extPathType={extPathType}, startPos={startPos}, " +
                           $"endPos={endPos}, sourceBuilding={instanceData.m_sourceBuilding}, " +
                           $"targetBuilding={instanceData.m_targetBuilding} pathMode={extInstance.pathMode}");
            }

            //-------------------------------------
            // determine start & end path positions
            //
            // NON-STOCK CODE: with Parking AI enabled, the end position must be a pedestrian position
            bool foundEndPos = !calculateEndPos || FindPathPosition(
                                   instanceID,
                                   ref instanceData,
                                   endPos,
                                   SavedGameOptions.Instance.parkingAI &&
                                   (instanceData.m_targetBuilding == 0 ||
                                    (targetBuilding.m_flags &
                                     Building.Flags.IncomingOutgoing) == Building.Flags.None)
                                       ? NetInfo.LaneType.Pedestrian
                                       : laneTypes,
                                   vehicleTypes,
                                   false,
                                   out endPosA);
            bool foundStartPos = false;
            PathUnit.Position startPosA;

            if (SavedGameOptions.Instance.parkingAI &&
                (extInstance.pathMode == ExtPathMode.CalculatingCarPathToTarget ||
                 extInstance.pathMode == ExtPathMode.CalculatingCarPathToKnownParkPos)) {
                // citizen will enter their car now
                // -> find a road start position
                foundStartPos = PathManager.FindPathPosition(
                    startPos,
                    ItemClass.Service.Road,
                    laneTypes & ~NetInfo.LaneType.Pedestrian,
                    vehicleTypes,
                    VehicleInfo.VehicleCategory.All,
                    allowUnderground,
                    false,
                    parkingAiConf.MaxBuildingToPedestrianLaneDistance,
                    false,
                    false,
                    out startPosA);
            } else {
                foundStartPos = FindPathPosition(
                    instanceID,
                    ref instanceData,
                    startPos,
                    laneTypes,
                    vehicleTypes,
                    allowUnderground,
                    out startPosA);
            }

            //---------------------
            // start path-finding
            //---------------------
            if (foundStartPos && // TODO probably fails if vehicle is parked too far away from road
                foundEndPos // NON-STOCK CODE
                ) {

                if (enableTransport) {
                    // public transport usage is allowed for this path
                    if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                        CitizenInstance.Flags.None)
                    {
                        // citizen may use public transport
                        laneTypes |= NetInfo.LaneType.PublicTransport;

                        if ((citizen.m_flags & Citizen.Flags.Evacuating) != Citizen.Flags.None) {
                            laneTypes |= NetInfo.LaneType.EvacuationTransport;
                        }
                    } else if (SavedGameOptions.Instance.parkingAI) { // TODO check for incoming connection
                        // citizen tried to use public transport but waiting time was too long
                        // -> add public transport demand for source building
                        if (instanceData.m_sourceBuilding != 0) {
                            if (logParkingAi) {
                                Log.Info(
                                    $"CustomCitizenAI.ExtStartPathFind({instanceID}): Citizen instance " +
                                    $"{instanceID} cannot uses public transport from building " +
                                    $"{instanceData.m_sourceBuilding} to {instanceData.m_targetBuilding}. " +
                                    "Incrementing public transport demand.");
                            }

                            IExtBuildingManager extBuildingManager = Constants.ManagerFactory.ExtBuildingManager;
                            extBuildingManager.AddPublicTransportDemand(
                                ref extBuildingManager.ExtBuildings[instanceData.m_sourceBuilding],
                                parkingAiConf.PublicTransportDemandWaitingIncrement,
                                true);
                        }
                    }
                }

                PathUnit.Position dummyPathPos = default;

                // NON-STOCK CODE START
                PathCreationArgs args;
                args.extPathType = extPathType;
                args.extVehicleType = extVehicleType;
                args.vehicleId = 0;
                args.spawned = (instanceData.m_flags & CitizenInstance.Flags.Character) !=
                               CitizenInstance.Flags.None;
                args.buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex;
                args.startPosA = startPosA;
                args.startPosB = dummyPathPos;
                args.endPosA = endPosA;
                args.endPosB = dummyPathPos;
                args.vehiclePosition = parkedVehiclePathPos;
                args.laneTypes = laneTypes;
                args.vehicleTypes = vehicleTypes;
                args.vehicleCategories = vehicleCategory;
                args.maxLength = 20000f;
                args.isHeavyVehicle = false;
                args.hasCombustionEngine = combustionEngine;
                args.ignoreBlocked = false;
                args.ignoreFlooded = false;
                args.ignoreCosts = ignoreCost;
                args.randomParking = randomParking;
                args.stablePath = false;
                args.skipQueue = false;

                if ((instanceData.m_flags & CitizenInstance.Flags.OnTour) != 0) {
                    args.stablePath = true;
                    args.maxLength = 160000f;
                    // args.laneTypes &= ~(NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle);
                } else {
                    args.stablePath = false;
                    args.maxLength = 20000f;
                }

                bool res = CustomPathManager._instance.CustomCreatePath(
                    out uint path,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args);

                // NON-STOCK CODE END
                if (res) {
                    if (logParkingAi) {
                        Log.InfoFormat(
                            "CustomCitizenAI.ExtStartPathFind({0}): Path-finding starts for citizen " +
                            "instance {1}, path={2}, extVehicleType={3}, extPathType={4}, " +
                            "startPosA.segment={5}, startPosA.lane={6}, laneType={7}, vehicleType={8}, " +
                            "endPosA.segment={9}, endPosA.lane={10}, vehiclePos.m_segment={11}, " +
                            "vehiclePos.m_lane={12}, vehiclePos.m_offset={13}",
                            instanceID,
                            instanceID,
                            path,
                            extVehicleType,
                            extPathType,
                            startPosA.m_segment,
                            startPosA.m_lane,
                            laneTypes,
                            vehicleTypes,
                            endPosA.m_segment,
                            endPosA.m_lane,
                            parkedVehiclePathPos.m_segment,
                            parkedVehiclePathPos.m_lane,
                            parkedVehiclePathPos.m_offset);
                    }

                    if (instanceData.m_path != 0u) {
                        Singleton<PathManager>.instance.ReleasePath(instanceData.m_path);
                    }

                    instanceData.m_path = path;
                    instanceData.m_flags |= CitizenInstance.Flags.WaitingPath;
                    return true;
                }
            }

            if (logParkingAi && SavedGameOptions.Instance.parkingAI) {
                Log.InfoFormat(
                    "CustomCitizenAI.ExtStartPathFind({0}): CustomCitizenAI.CustomStartPathFind: " +
                    "[PFFAIL] failed for citizen instance {1} (CurrentPathMode={2}). " +
                    "startPosA.segment={3}, startPosA.lane={4}, startPosA.offset={5}, " +
                    "endPosA.segment={6}, endPosA.lane={7}, endPosA.offset={8}, " +
                    "foundStartPos={9}, foundEndPos={10}",
                    instanceID,
                    instanceID,
                    extInstance.pathMode,
                    startPosA.m_segment,
                    startPosA.m_lane,
                    startPosA.m_offset,
                    endPosA.m_segment,
                    endPosA.m_lane,
                    endPosA.m_offset,
                    foundStartPos,
                    foundEndPos);
#if DEBUG
                Reset(ref extInstance);
#endif
            }

            return false;
        }

        public bool FindPathPosition(ushort instanceID,
                                     ref CitizenInstance instanceData,
                                     Vector3 pos,
                                     NetInfo.LaneType laneTypes,
                                     VehicleInfo.VehicleType vehicleTypes,
                                     bool allowUnderground,
                                     out PathUnit.Position position) {
            position = default;
            float minDist = 1E+10f;

            if (PathManager.FindPathPosition(
                    pos,
                    ItemClass.Service.Road,
                    laneTypes,
                    vehicleTypes,
                    VehicleInfo.VehicleCategory.All,
                    allowUnderground,
                    false,
                    SavedGameOptions.Instance.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    false,
                    true,
                    out PathUnit.Position posA,
                    out PathUnit.Position posB,
                    out float distA,
                    out float distB) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if (PathManager.FindPathPosition(
                    pos,
                    ItemClass.Service.Beautification,
                    laneTypes,
                    vehicleTypes,
                    VehicleInfo.VehicleCategory.All,
                    allowUnderground,
                    false,
                    SavedGameOptions.Instance.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    false,
                    true,
                    out posA,
                    out posB,
                    out distA,
                    out distB) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            if ((instanceData.m_flags & CitizenInstance.Flags.CannotUseTransport) ==
                CitizenInstance.Flags.None && PathManager.FindPathPosition(
                    pos,
                    ItemClass.Service.PublicTransport,
                    laneTypes,
                    vehicleTypes,
                    VehicleInfo.VehicleCategory.All,
                    allowUnderground,
                    false,
                    SavedGameOptions.Instance.parkingAI
                        ? GlobalConfig.Instance.ParkingAI.MaxBuildingToPedestrianLaneDistance
                        : 32f,
                    false,
                    true,
                    out posA,
                    out posB,
                    out distA,
                    out distB) && distA < minDist) {
                minDist = distA;
                position = posA;
            }

            return position.m_segment != 0;
        }

        /// <summary>
        /// Releases the return path
        /// </summary>
        public void ReleaseReturnPath(ref ExtCitizenInstance extInstance) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0
                                || DebugSettings.CitizenId == CitizenManager.instance.m_instances.m_buffer[extInstance.instanceId].m_citizen;

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            // bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
#endif

            if (extInstance.returnPathId != 0) {
                if (logParkingAi) {
                    Log._Debug(
                        $"Releasing return path {extInstance.returnPathId} of citizen instance " +
                        $"{extInstance.instanceId}. ReturnPathState={extInstance.returnPathState}");
                }

                Singleton<PathManager>.instance.ReleasePath(extInstance.returnPathId);
                extInstance.returnPathId = 0;
            }

            extInstance.returnPathState = ExtPathState.None;
        }

        public void UpdateReturnPathState(ref ExtCitizenInstance extInstance) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0
                                || DebugSettings.CitizenId == CitizenManager.instance.m_instances.m_buffer[extInstance.instanceId].m_citizen;

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;

            if (extendedLogParkingAi) {
                Log._Debug(
                    $"ExtCitizenInstance.UpdateReturnPathState() called for citizen instance " +
                    $"{extInstance.instanceId}");
            }
#else
            const bool logParkingAi = false;
            const bool extendedLogParkingAi = false;
#endif

            if (extInstance.returnPathId == 0 ||
                extInstance.returnPathState != ExtPathState.Calculating) {
                return;
            }

            byte returnPathFlags = CustomPathManager
                                   ._instance.m_pathUnits.m_buffer[extInstance.returnPathId]
                                   .m_pathFindFlags;

            if ((returnPathFlags & PathUnit.FLAG_READY) != 0) {
                extInstance.returnPathState = ExtPathState.Ready;
                if (extendedLogParkingAi) {
                    Log._Debug(
                        $"CustomHumanAI.CustomSimulationStep: Return path {extInstance.returnPathId} " +
                        $"SUCCEEDED. Flags={returnPathFlags}. " +
                        $"Setting ReturnPathState={extInstance.returnPathState}");
                }
            } else if ((returnPathFlags & PathUnit.FLAG_FAILED) != 0) {
                extInstance.returnPathState = ExtPathState.Failed;
                if (logParkingAi) {
                    Log._Debug(
                        $"CustomHumanAI.CustomSimulationStep: Return path {extInstance.returnPathId} " +
                        $"FAILED. Flags={returnPathFlags}. " +
                        $"Setting ReturnPathState={extInstance.returnPathState}");
                }
            }
        }

        public bool CalculateReturnPath(ref ExtCitizenInstance extInstance,
                                        Vector3 parkPos,
                                        Vector3 targetPos) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0
                                || DebugSettings.CitizenId == CitizenManager.instance.m_instances.m_buffer[extInstance.instanceId].m_citizen;
            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            // bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;
#else
            const bool logParkingAi = false;
#endif
            ReleaseReturnPath(ref extInstance);

            PathUnit.Position targetPathPos = default;
            bool foundParkPathPos = ExtPathManager.Instance.FindCitizenPathPosition(
                parkPos,
                NetInfo.LaneType.Pedestrian,
                VehicleInfo.VehicleType.None,
                NetInfo.LaneType.None,
                VehicleInfo.VehicleType.None,
                false,
                false,
                out PathUnit.Position parkPathPos);

            bool foundTargetPathPos = foundParkPathPos
                                      && ExtPathManager.Instance.FindCitizenPathPosition(
                                          targetPos,
                                          NetInfo.LaneType.Pedestrian,
                                          VehicleInfo.VehicleType.None,
                                          NetInfo.LaneType.None,
                                          VehicleInfo.VehicleType.None,
                                          false,
                                          false,
                                          out targetPathPos);

            if (foundParkPathPos && foundTargetPathPos) {
                PathUnit.Position dummyPathPos = default;
                var args = new PathCreationArgs {
                    extPathType = ExtPathType.WalkingOnly,
                    extVehicleType = ExtVehicleType.None,
                    vehicleId = 0,
                    spawned = true,
                    buildIndex = Singleton<SimulationManager>.instance.m_currentBuildIndex,
                    startPosA = parkPathPos,
                    startPosB = dummyPathPos,
                    endPosA = targetPathPos,
                    endPosB = dummyPathPos,
                    vehiclePosition = dummyPathPos,
                    laneTypes = NetInfo.LaneType.Pedestrian | NetInfo.LaneType.PublicTransport,
                    vehicleTypes = VehicleInfo.VehicleType.None,
                    maxLength = 20000f,
                    isHeavyVehicle = false,
                    hasCombustionEngine = false,
                    ignoreBlocked = false,
                    ignoreFlooded = false,
                    ignoreCosts = false,
                    randomParking = false,
                    stablePath = false,
                    skipQueue = false,
                };

                if (CustomPathManager._instance.CustomCreatePath(
                    out uint pathId,
                    ref Singleton<SimulationManager>.instance.m_randomizer,
                    args)) {
                    if (logParkingAi) {
                        Log._DebugFormat(
                            "ExtCitizenInstance.CalculateReturnPath: Path-finding starts for return " +
                            "path of citizen instance {0}, path={1}, parkPathPos.segment={2}, " +
                            "parkPathPos.lane={3}, targetPathPos.segment={4}, targetPathPos.lane={5}",
                            extInstance.instanceId,
                            pathId,
                            parkPathPos.m_segment,
                            parkPathPos.m_lane,
                            targetPathPos.m_segment,
                            targetPathPos.m_lane);
                    }

                    extInstance.returnPathId = pathId;
                    extInstance.returnPathState = ExtPathState.Calculating;
                    return true;
                }
            }

            if (logParkingAi) {
                Log._Debug("ExtCitizenInstance.CalculateReturnPath: Could not find path " +
                           "position(s) for either the parking position or target position of " +
                           $"citizen instance {extInstance.instanceId}.");
            }

            return false;
        }

        public bool LoadData(List<Configuration.ExtCitizenInstanceData> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} extended citizen instances");

            foreach (Configuration.ExtCitizenInstanceData item in data) {
                try {
                    uint instanceId = item.instanceId;
                    ExtInstances[instanceId].pathMode = (ExtPathMode)item.pathMode;
                    ExtInstances[instanceId].failedParkingAttempts = item.failedParkingAttempts;
                    ExtInstances[instanceId].parkingSpaceLocationId = item.parkingSpaceLocationId;
                    ExtInstances[instanceId].parkingSpaceLocation =
                        (ExtParkingSpaceLocation)item.parkingSpaceLocation;

                    if (item.parkingPathStartPositionSegment != 0) {
                        var pos = new PathUnit.Position {
                            m_segment = item.parkingPathStartPositionSegment,
                            m_lane = item.parkingPathStartPositionLane,
                            m_offset = item.parkingPathStartPositionOffset,
                        };
                        ExtInstances[instanceId].parkingPathStartPosition = pos;
                    } else {
                        ExtInstances[instanceId].parkingPathStartPosition = null;
                    }

                    ExtInstances[instanceId].returnPathId = item.returnPathId;
                    ExtInstances[instanceId].returnPathState = (ExtPathState)item.returnPathState;
                    ExtInstances[instanceId].lastDistanceToParkedCar = item.lastDistanceToParkedCar;
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"Error loading ext. citizen instance: {e}");
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.ExtCitizenInstanceData> SaveData(ref bool success) {
            var ret = new List<Configuration.ExtCitizenInstanceData>();

            CitizenManager citizenManager = CitizenManager.instance;
            CitizenInstance[] instancesBuffer = citizenManager.m_instances.m_buffer;
            uint maxCitizenInstanceCount = citizenManager.m_instances.m_size;

            for (uint instanceId = 0; instanceId < maxCitizenInstanceCount; ++instanceId) {
                try
                {
                    ref CitizenInstance citizenInstance = ref instancesBuffer[instanceId];

                    if (!citizenInstance.IsCreated()) {
                        continue;
                    }

                    if (ExtInstances[instanceId].pathMode == ExtPathMode.None &&
                        ExtInstances[instanceId].returnPathId == 0) {
                        continue;
                    }

                    var item = new Configuration.ExtCitizenInstanceData(instanceId) {
                        pathMode = (int)ExtInstances[instanceId].pathMode,
                        failedParkingAttempts = ExtInstances[instanceId].failedParkingAttempts,
                        parkingSpaceLocationId = ExtInstances[instanceId].parkingSpaceLocationId,
                        parkingSpaceLocation = (int)ExtInstances[instanceId].parkingSpaceLocation,
                    };

                    if (ExtInstances[instanceId].parkingPathStartPosition != null) {
                        var pos = (PathUnit.Position)ExtInstances[instanceId].parkingPathStartPosition;
                        item.parkingPathStartPositionSegment = pos.m_segment;
                        item.parkingPathStartPositionLane = pos.m_lane;
                        item.parkingPathStartPositionOffset = pos.m_offset;
                    } else {
                        item.parkingPathStartPositionSegment = 0;
                        item.parkingPathStartPositionLane = 0;
                        item.parkingPathStartPositionOffset = 0;
                    }

                    item.returnPathId = ExtInstances[instanceId].returnPathId;
                    item.returnPathState = (int)ExtInstances[instanceId].returnPathState;
                    item.lastDistanceToParkedCar = ExtInstances[instanceId].lastDistanceToParkedCar;
                    ret.Add(item);
                }
                catch (Exception ex) {
                    Log.Error($"Exception occurred while saving ext. citizen instances @ {instanceId}: {ex}");
                    success = false;
                }
            }

            return ret;
        }

        public bool IsAtOutsideConnection(ushort instanceId,
                                          ref CitizenInstance instanceData,
                                          ref ExtCitizenInstance extInstance,
                                          Vector3 startPos) {
#if DEBUG
            bool citizenDebug =
                (DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == instanceData.m_citizen)
                && (DebugSettings.CitizenInstanceId == 0 || DebugSettings.CitizenInstanceId == instanceId)
                && (DebugSettings.SourceBuildingId == 0 || DebugSettings.SourceBuildingId == instanceData.m_sourceBuilding)
                && (DebugSettings.TargetBuildingId == 0 || DebugSettings.TargetBuildingId == instanceData.m_targetBuilding);

            bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;

            if (logParkingAi) {
                Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({extInstance.instanceId}): " +
                           $"called. Path: {instanceData.m_path} sourceBuilding={instanceData.m_sourceBuilding} targetBuilding={instanceData.m_targetBuilding}");
            }
#else
            const bool logParkingAi = false;
#endif

            ParkingAI parkingAiConf = GlobalConfig.Instance.ParkingAI;
            float sqrMaxDistToPedestrianLane = parkingAiConf.MaxBuildingToPedestrianLaneDistance *
                                               parkingAiConf.MaxBuildingToPedestrianLaneDistance;

            ref Building sourceBuilding = ref instanceData.m_sourceBuilding.ToBuilding();
            bool ret = sourceBuilding.m_flags.IsFlagSet(Building.Flags.IncomingOutgoing) &&
                       (startPos - sourceBuilding.m_position).sqrMagnitude <= sqrMaxDistToPedestrianLane;

            if (!ret) {
                ref Building targetBuilding = ref instanceData.m_targetBuilding.ToBuilding();
                ret = targetBuilding.m_flags.IsFlagSet(Building.Flags.IncomingOutgoing) &&
                      (startPos - targetBuilding.m_position).sqrMagnitude <= sqrMaxDistToPedestrianLane;
            }

            if (logParkingAi) {
                Log._Debug($"ExtCitizenInstanceManager.IsAtOutsideConnection({instanceId}): ret={ret}");
            }

            return ret;
        }

        public void Reset(ref ExtCitizenInstance extInstance) {
            // Flags = ExtFlags.None;
            extInstance.pathMode = ExtPathMode.None;
            extInstance.failedParkingAttempts = 0;
            extInstance.parkingSpaceLocation = ExtParkingSpaceLocation.None;
            extInstance.parkingSpaceLocationId = 0;
            extInstance.lastDistanceToParkedCar = float.MaxValue;
            extInstance.atOutsideConnection = false;

            // extInstance.ParkedVehiclePosition = default(Vector3);
            ReleaseReturnPath(ref extInstance);
        }

        internal void Reset() {
            for (var i = 0; i < ExtInstances.Length; ++i) {
                Reset(ref ExtInstances[i]);
            }
        }

        /// <summary>
        /// Check if a building id refers to an outside connection.
        /// </summary>
        ///
        /// <param name="buildingId">The id of the building to check.</param>
        ///
        /// <returns>Returns <c>true</c> if it's an outside connection, otherwise <c>false</c>.</returns>
        internal bool IsOutsideConnection(ushort buildingId) {
            ref Building building = ref buildingId.ToBuilding();
            return (building.m_flags & Building.Flags.IncomingOutgoing) != 0;
        }

        /// <summary>
        /// Check if a vehicle is owned by a certain citizen.
        /// </summary>
        ///
        /// <param name="vehicle">The vehicle.</param>
        /// <param name="citizenId">The citizen.</param>
        ///
        /// <returns>Returns <c>true</c> if the vehicle is owned by the citizen, otherwise <c>false</c>.</returns>
        internal bool IsVehicleOwnedByCitizen(ref Vehicle vehicle, uint citizenId) {
            InstanceID id = InstanceID.Empty;
            id.Building = vehicle.m_sourceBuilding;
            return id.Citizen == citizenId;
        }

        /// <summary>
        /// Check if a citizen is caught in a flood, tsunami or tornado.
        /// </summary>
        ///
        /// <param name="citizen">The citizen to inspect.</param>
        ///
        /// <returns>Returns <c>true</c> if having a bad day, otherwise <c>false</c>.</returns>
        internal static bool IsSweaptAway(ref CitizenInstance citizen) =>
            (citizen.m_flags & (CitizenInstance.Flags.Blown | CitizenInstance.Flags.Floating)) != 0;

        /// <summary>
        /// Check if a citizen is loitering at their current location.
        /// </summary>
        ///
        /// <param name="citizen">The citizen to inspect.</param>
        ///
        /// <returns>Returns <c>true</c> if hanging around, otherwise <c>false</c>.</returns>
        internal static bool IsHangingAround(ref CitizenInstance citizen) =>
            citizen.m_path == 0u && (citizen.m_flags & CitizenInstance.Flags.HangAround) != 0;
    }
}
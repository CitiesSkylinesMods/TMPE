namespace TrafficManager.Patch._VehicleAI {
    using System;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Math;
    using Connection;
    using CSUtil.Commons;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using UnityEngine;
    using Util;

    [HarmonyPatch]
    public class UpdatePathTargetPositionsPatch {
        private static InvalidPathDelegate InvalidPath;
        private static ParkVehicleDelegate ParkVehicle;
        private static NeedChangeVehicleTypeDelegate NeedChangeVehicleType;
        private static CalculateSegmentPositionDelegate CalculateSegmentPosition;
        private static CalculateSegmentPositionDelegate2 CalculateSegmentPosition2;
        private static ChangeVehicleTypeDelegate ChangeVehicleType;
        private static UpdateNodeTargetPosDelegate UpdateNodeTargetPos;
        private static ArrivingToDestinationDelegate ArrivingToDestination;
        private static LeftHandDriveDelegate LeftHandDrive;
        private static CalculateTargetSpeedDelegate CalculateTargetSpeed;

        private delegate void UpdatePathTargetPositionsDelegate(ushort vehicleID,
                                                               ref Vehicle vehicleData,
                                                               Vector3 refPos,
                                                               ref int index,
                                                               int max,
                                                               float minSqrDistanceA,
                                                               float minSqrDistanceB);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<UpdatePathTargetPositionsDelegate>(typeof(VehicleAI), "UpdatePathTargetPositions");

        [UsedImplicitly]
        public static void Prepare() {
            VehicleAIConnection vehicleAIConnection = GameConnectionManager.Instance.VehicleAIConnection;
            InvalidPath = vehicleAIConnection.InvalidPath;
            ParkVehicle = vehicleAIConnection.ParkVehicle;
            NeedChangeVehicleType = vehicleAIConnection.NeedChangeVehicleType;
            CalculateSegmentPosition = vehicleAIConnection.CalculateSegmentPosition;
            CalculateSegmentPosition2 = vehicleAIConnection.CalculateSegmentPosition2;
            ChangeVehicleType = vehicleAIConnection.ChangeVehicleType;
            UpdateNodeTargetPos = vehicleAIConnection.UpdateNodeTargetPos;
            ArrivingToDestination = vehicleAIConnection.ArrivingToDestination;
            LeftHandDrive = vehicleAIConnection.LeftHandDrive;
            CalculateTargetSpeed = vehicleAIConnection.CalculateTargetSpeed;
        }

        [UsedImplicitly]
        public static bool Prefix(VehicleAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle vehicleData,
                                  Vector3 refPos,
                                  ref int index,
                                  int max,
                                  float minSqrDistanceA,
                                  float minSqrDistanceB) {
#if DEBUG
            bool logLogic = DebugSwitch.CalculateSegmentPosition.Get()
                           && (GlobalConfig.Instance.Debug.ApiExtVehicleType == API.Traffic.Enums.ExtVehicleType.None
                               || GlobalConfig.Instance.Debug.ApiExtVehicleType == API.Traffic.Enums.ExtVehicleType.RoadVehicle)
                           && (DebugSettings.VehicleId == 0 || DebugSettings.VehicleId == vehicleID);
#else
            var logLogic = false;
#endif
            if (logLogic) {
                Log._Debug(
                    $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}) called.\n" +
                    $"\trefPos={refPos}\n\tindex={index}\n" +
                    $"\tmax={max}\n\tminSqrDistanceA={minSqrDistanceA}\n" +
                    $"\tminSqrDistanceB={minSqrDistanceB}\n\tvehicleData.m_path={vehicleData.m_path}\n" +
                    $"\tvehicleData.m_pathPositionIndex={vehicleData.m_pathPositionIndex}\n" +
                    $"\tvehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}");
            }

            PathManager pathMan = Singleton<PathManager>.instance;
            NetManager netManager = Singleton<NetManager>.instance;

            Vector4 targetPos = vehicleData.m_targetPos0;
            targetPos.w = 1000f;

            float minSqrDistA = minSqrDistanceA;
            uint pathId = vehicleData.m_path;
            byte finePathPosIndex = vehicleData.m_pathPositionIndex;
            byte pathOffset = vehicleData.m_lastPathOffset;

            // initial position
            if (finePathPosIndex == 255) {
                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    $"Initial position. finePathPosIndex={finePathPosIndex}");

                finePathPosIndex = 0;
                if (index <= 0) {
                    vehicleData.m_pathPositionIndex = 0;
                }

                if (!Singleton<PathManager>.instance.m_pathUnits.m_buffer[pathId].CalculatePathPositionOffset(
                        finePathPosIndex >> 1,
                        targetPos,
                        out pathOffset)) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Could not calculate path position offset. ABORT. pathId={pathId}, " +
                        $"finePathPosIndex={finePathPosIndex}, targetPos={targetPos}");
                    GameConnectionManager.Instance.VehicleAIConnection.InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return false;
                }
            }

            // get current path position, check for errors
            if (!pathMan.m_pathUnits.m_buffer[pathId].GetPosition(
                    finePathPosIndex >> 1,
                    out PathUnit.Position currentPosition))
            {
                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    $"Could not get current position. ABORT. pathId={pathId}, " +
                    $"finePathPosIndex={finePathPosIndex}");
                InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return false;
            }

            // get current segment info, check for errors
            NetInfo curSegmentInfo = netManager.m_segments.m_buffer[currentPosition.m_segment].Info;
            if (curSegmentInfo.m_lanes.Length <= currentPosition.m_lane) {
                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    "Invalid lane index. ABORT. " +
                    $"curSegmentInfo.m_lanes.Length={curSegmentInfo.m_lanes.Length}, " +
                    $"currentPosition.m_lane={currentPosition.m_lane}");
                InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                return false;
            }

            // main loop
            uint curLaneId = PathManager.GetLaneID(currentPosition);
            Log._DebugIf(
                logLogic,
                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                $"currentPosition=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, " +
                $"off={currentPosition.m_offset}], targetPos={targetPos}, curLaneId={curLaneId}");

            NetInfo.Lane laneInfo = curSegmentInfo.m_lanes[currentPosition.m_lane];
            bool firstIter = true; // NON-STOCK CODE

            while (true) {
                if ((finePathPosIndex & 1) == 0) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                          "Vehicle is not in transition.");

                    // vehicle is not in transition
                    if (laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Vehicle is not in transition and no cargo lane. " +
                            $"finePathPosIndex={finePathPosIndex}, pathOffset={pathOffset}, " +
                            $"currentPosition.m_offset={currentPosition.m_offset}");

                        int iter = -1;
                        while (pathOffset != currentPosition.m_offset) {
                            // catch up and update target position until we get to the current segment offset
                            ++iter;

                            if (iter != 0) {
                                float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
                                int pathOffsetDelta;
                                if (distDiff < 0f) {
                                    pathOffsetDelta = 4;
                                } else {
                                    pathOffsetDelta = 4 + Mathf.Max(
                                                          0,
                                                          Mathf.CeilToInt(
                                                              distDiff * 256f /
                                                              (netManager
                                                               .m_lanes.m_buffer[curLaneId]
                                                               .m_length + 1f)));
                                }

                                Log._DebugIf(
                                    logLogic,
                                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                    $"Calculated pathOffsetDelta={pathOffsetDelta}. distDiff={distDiff}, " +
                                    $"targetPos={targetPos}, refPos={refPos}");

                                if (pathOffset > currentPosition.m_offset) {
                                    pathOffset = (byte)Mathf.Max(
                                        pathOffset - pathOffsetDelta,
                                        currentPosition.m_offset);
                                    Log._DebugIf(
                                        logLogic,
                                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                        "pathOffset > currentPosition.m_offset: Calculated " +
                                        $"pathOffset={pathOffset}");
                                } else if (pathOffset < currentPosition.m_offset) {
                                    pathOffset = (byte)Mathf.Min(
                                        pathOffset + pathOffsetDelta,
                                        currentPosition.m_offset);
                                    Log._DebugIf(
                                        logLogic,
                                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                        "pathOffset < currentPosition.m_offset: Calculated " +
                                        $"pathOffset={pathOffset}");
                                }
                            }

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Catch-up iteration {iter}.");

                            CalculateSegmentPosition(
                                __instance,
                                vehicleID,
                                ref vehicleData,
                                currentPosition,
                                curLaneId,
                                pathOffset,
                                out Vector3 curSegPos,
                                out Vector3 curSegDir,
                                out float maxSpeed);

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Calculated segment position at iteration {iter}. " +
                                $"IN: pathOffset={pathOffset}, currentPosition=[seg={currentPosition.m_segment}, " +
                                $"lane={currentPosition.m_lane}, off={currentPosition.m_offset}], " +
                                $"curLaneId={curLaneId}, OUT: curSegPos={curSegPos}, " +
                                $"curSegDir={curSegDir}, maxSpeed={maxSpeed}");

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Adapted position for emergency. curSegPos={curSegPos}, maxSpeed={maxSpeed}");

                            targetPos.Set(curSegPos.x, curSegPos.y, curSegPos.z, Mathf.Min(targetPos.w, maxSpeed));
                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Calculated targetPos={targetPos}");

                            float refPosSqrDist = (curSegPos - refPos).sqrMagnitude;
                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Set targetPos={targetPos}, refPosSqrDist={refPosSqrDist}, " +
                                $"curSegPos={curSegPos}, refPos={refPos}");

                            if (refPosSqrDist >= minSqrDistA) {
                                if (index <= 0) {
                                    vehicleData.m_lastPathOffset = pathOffset;
                                }

                                vehicleData.SetTargetPos(index++, targetPos);
                                minSqrDistA = minSqrDistanceB;
                                refPos = targetPos;
                                targetPos.w = 1000f;
                                if (logLogic) {
                                    Log._Debug(
                                        $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                        $"refPosSqrDist >= minSqrDistA: index={index}, " +
                                        $"refPosSqrDist={refPosSqrDist}, minSqrDistA ={minSqrDistA}, " +
                                        $"refPos={refPos}, targetPos={targetPos}");
                                }

                                if (index != max) {
                                    continue;
                                }

                                // maximum target position index reached
                                Log._DebugIf(
                                    logLogic,
                                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                    $"Maximum target position index reached ({max}). ABORT.");
                                return false;
                            }

                            if (logLogic) {
                                Log._Debug(
                                    $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                    $"refPosSqrDist < minSqrDistA: refPosSqrDist={refPosSqrDist}, " +
                                    $"index={index}, minSqrDistA={minSqrDistA}, " +
                                    $"refPos={refPos}, targetPos={targetPos}, curSegPos={curSegPos}");
                            }
                        } // while (pathOffset != currentPosition.m_offset)

                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Catch up complete.");
                    } else {
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Lane is cargo lane. No catch up.");
                    }

                    // set vehicle in transition
                    finePathPosIndex += 1;
                    pathOffset = 0;
                    if (index <= 0) {
                        vehicleData.m_pathPositionIndex = finePathPosIndex;
                        vehicleData.m_lastPathOffset = pathOffset;
                    }

                    if (logLogic) {
                        Log._Debug(
                            $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            $"Vehicle is in transition now. finePathPosIndex={finePathPosIndex}, " +
                            $"pathOffset={pathOffset}, index={index}, " +
                            $"vehicleData.m_pathPositionIndex={vehicleData.m_pathPositionIndex}, " +
                            $"vehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}");
                    }
                } else {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Vehicle is in transition.");
                }

                if ((vehicleData.m_flags2 & Vehicle.Flags2.EndStop) != 0) {
                    if (index <= 0) {
                        targetPos.w = 0f;
                        if (VectorUtils.LengthSqrXZ(vehicleData.GetLastFrameVelocity()) < 0.01f) {
                            vehicleData.m_flags2 &= ~Vehicle.Flags2.EndStop;
                        }
                    } else {
                        targetPos.w = 1f;
                    }

                    while (index < max) {
                        vehicleData.SetTargetPos(index++, targetPos);
                    }

                    return false;
                }

                // vehicle is in transition now
                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    "Vehicle is in transition now.");

                /*
                 * coarse path position format: 0..11 (always equals 'fine path position' / 2
                 * == 'fine path position' >> 1)
                 * fine path position format: 0..23
                 */

                // find next path unit (or abort if at path end)
                int nextCoarsePathPosIndex = (finePathPosIndex >> 1) + 1;
                uint nextPathId = pathId;
                if (nextCoarsePathPosIndex >= pathMan.m_pathUnits.m_buffer[pathId].m_positionCount) {
                    nextCoarsePathPosIndex = 0;
                    nextPathId = pathMan.m_pathUnits.m_buffer[pathId].m_nextPathUnit;
                    if (nextPathId == 0u) {
                        if (index <= 0) {
                            Singleton<PathManager>.instance.ReleasePath(vehicleData.m_path);
                            vehicleData.m_path = 0u;
                        }

                        targetPos.w = 1f;
                        vehicleData.SetTargetPos(index++, targetPos);
                        if (logLogic) {
                            Log._Debug(
                                $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Path end reached. ABORT. nextCoarsePathPosIndex={nextCoarsePathPosIndex}, " +
                                $"finePathPosIndex={finePathPosIndex}, index={index}, " +
                                $"targetPos={targetPos}");
                        }

                        return false;
                    } // if (nextPathId == 0u)
                } // if nextCoarse...

                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    $"Found next path unit. nextCoarsePathPosIndex={nextCoarsePathPosIndex}, " +
                    $"nextPathId={nextPathId}");

                // get next path position, check for errors
                if (!pathMan.m_pathUnits.m_buffer[nextPathId]
                            .GetPosition(nextCoarsePathPosIndex, out PathUnit.Position nextPosition)) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Could not get position for nextCoarsePathPosIndex={nextCoarsePathPosIndex}. " +
                        "ABORT.");
                    InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return false;
                }

                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    $"Got next path position nextPosition=[seg={nextPosition.m_segment}, " +
                    $"lane={nextPosition.m_lane}, off={nextPosition.m_offset}]");

                // check for errors
                NetInfo nextSegmentInfo = netManager.m_segments.m_buffer[nextPosition.m_segment].Info;
                if (nextSegmentInfo.m_lanes.Length <= nextPosition.m_lane) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Invalid lane index. ABORT. " +
                        $"nextSegmentInfo.m_lanes.Length={nextSegmentInfo.m_lanes.Length}, " +
                        $"nextPosition.m_lane={nextPosition.m_lane}");
                    InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return false;
                }

                // find next lane (emergency vehicles / dynamic lane selection)
                int bestLaneIndex = nextPosition.m_lane;
                if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Finding best lane for emergency vehicles. " +
                        $"Before: bestLaneIndex={bestLaneIndex}");

#if ROUTING
                    bestLaneIndex = VehicleBehaviorManager.Instance.FindBestEmergencyLane(
                        vehicleID,
                        ref vehicleData,
                        ref ExtVehicleManager.Instance.ExtVehicles[vehicleID],
                        curLaneId,
                        currentPosition,
                        curSegmentInfo,
                        nextPosition,
                        nextSegmentInfo);
#else // no ROUTING
                    // stock procedure for emergency vehicles on duty
                    bestLaneIndex = FindBestLane(vehicleID, ref vehicleData, nextPosition);
#endif

                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Found best lane for emergency vehicles. " +
                        $"After: bestLaneIndex={bestLaneIndex}");
                } else if (VehicleBehaviorManager.Instance.MayFindBestLane(
                    vehicleID,
                    ref vehicleData,
                    ref ExtVehicleManager.Instance.ExtVehicles[vehicleID]))
                {
                    // NON-STOCK CODE START
                    if (firstIter
                        && __instance.m_info.m_vehicleType == VehicleInfo.VehicleType.Car) {
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Finding best lane for regular vehicles. Before: " +
                            $"bestLaneIndex={bestLaneIndex}");

                        if (!__instance.m_info.m_isLargeVehicle) {
                            if (VehicleBehaviorManager.Instance.MayFindBestLane(
                                vehicleID,
                                ref vehicleData,
                                ref ExtVehicleManager.Instance.ExtVehicles[vehicleID])) {
                                Log._DebugIf(
                                    logLogic,
                                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                    "Using DLS.");

                                uint next2PathId = nextPathId;
                                int next2PathPosIndex = nextCoarsePathPosIndex;
                                NetInfo next2SegmentInfo = null;
                                PathUnit.Position next3PathPos;
                                NetInfo next3SegmentInfo = null;
                                PathUnit.Position next4PathPos;

                                if (PathUnit.GetNextPosition(
                                    ref next2PathId,
                                    ref next2PathPosIndex,
                                    out PathUnit.Position next2PathPos,
                                    out _))
                                {
                                    next2SegmentInfo = netManager.m_segments.m_buffer[next2PathPos.m_segment].Info;

                                    uint next3PathId = next2PathId;
                                    int next3PathPosIndex = next2PathPosIndex;

                                    if (PathUnit.GetNextPosition(
                                        ref next3PathId,
                                        ref next3PathPosIndex,
                                        out next3PathPos,
                                        out _))
                                    {
                                        next3SegmentInfo = netManager.m_segments.m_buffer[next3PathPos.m_segment].Info;

                                        uint next4PathId = next3PathId;
                                        int next4PathPosIndex = next3PathPosIndex;
                                        if (!PathUnit.GetNextPosition(
                                                ref next4PathId,
                                                ref next4PathPosIndex,
                                                out next4PathPos,
                                                out _)) {
                                            next4PathPos = default;
                                        }
                                    } else {
                                        next3PathPos = default;
                                        next4PathPos = default;
                                    }
                                } else {
                                    next2PathPos = default;
                                    next3PathPos = default;
                                    next4PathPos = default;
                                }

                                bestLaneIndex = VehicleBehaviorManager.Instance.FindBestLane(
                                    vehicleID,
                                    ref vehicleData,
                                    ref ExtVehicleManager.Instance.ExtVehicles[vehicleID],
                                    curLaneId,
                                    currentPosition,
                                    curSegmentInfo,
                                    nextPosition,
                                    nextSegmentInfo,
                                    next2PathPos,
                                    next2SegmentInfo,
                                    next3PathPos,
                                    next3SegmentInfo,
                                    next4PathPos);
                            }
                        }

                        // NON-STOCK CODE END
                    }
                }

                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    $"Best lane found. bestLaneIndex={bestLaneIndex} " +
                    $"nextPosition.m_lane={nextPosition.m_lane}");

                // update lane index
                if (bestLaneIndex != nextPosition.m_lane) {
                    nextPosition.m_lane = (byte)bestLaneIndex;
                    pathMan.m_pathUnits.m_buffer[nextPathId].SetPosition(nextCoarsePathPosIndex, nextPosition);

                    // prevent multiple lane changes to the same lane from happening at the same time
                    TrafficMeasurementManager.Instance.AddTraffic(
                        nextPosition.m_segment,
                        nextPosition.m_lane,
                        0); // NON-STOCK CODE
                }

                // check for errors
                uint nextLaneId = PathManager.GetLaneID(nextPosition);
                NetInfo.Lane nextLaneInfo = nextSegmentInfo.m_lanes[nextPosition.m_lane];
                ushort curSegStartNodeId = netManager.m_segments.m_buffer[currentPosition.m_segment].m_startNode;
                ushort curSegEndNodeId = netManager.m_segments.m_buffer[currentPosition.m_segment].m_endNode;
                ushort nextSegStartNodeId = netManager.m_segments.m_buffer[nextPosition.m_segment].m_startNode;
                ushort nextSegEndNodeId = netManager.m_segments.m_buffer[nextPosition.m_segment].m_endNode;

                NetNode.Flags flags1 = netManager.m_nodes.m_buffer[curSegStartNodeId].m_flags
                             | netManager.m_nodes.m_buffer[curSegEndNodeId].m_flags;
                NetNode.Flags flags2 = netManager.m_nodes.m_buffer[nextSegStartNodeId].m_flags
                             | netManager.m_nodes.m_buffer[nextSegEndNodeId].m_flags;

                if (nextSegStartNodeId != curSegStartNodeId
                    && nextSegStartNodeId != curSegEndNodeId
                    && nextSegEndNodeId != curSegStartNodeId
                    && nextSegEndNodeId != curSegEndNodeId
                    && (flags1 & NetNode.Flags.Disabled) == NetNode.Flags.None
                    && (flags2 & NetNode.Flags.Disabled) != NetNode.Flags.None)
                {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"General node/flag fault. ABORT. nextSegStartNodeId={nextSegStartNodeId}, " +
                        $"curSegStartNodeId={curSegStartNodeId}, " +
                        $"nextSegStartNodeId={nextSegStartNodeId}, " +
                        $"curSegEndNodeId={curSegEndNodeId}, " +
                        $"nextSegEndNodeId={nextSegEndNodeId}, " +
                        $"curSegStartNodeId={curSegStartNodeId}, " +
                        $"nextSegEndNodeId={nextSegEndNodeId}, " +
                        $"curSegEndNodeId={curSegEndNodeId}, " +
                        $"flags1={flags1}, flags2={flags2}");
                    InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return false;
                }

                // park vehicle
                if (nextLaneInfo.m_laneType == NetInfo.LaneType.Pedestrian) {
                    if (vehicleID != 0 && (vehicleData.m_flags & Vehicle.Flags.Parking) == 0) {
                        byte inOffset = currentPosition.m_offset;

                        if (ParkVehicle(
                            __instance,
                            vehicleID,
                            ref vehicleData,
                            currentPosition,
                            nextPathId,
                            nextCoarsePathPosIndex << 1,
                            out byte outOffset))
                        {
                            if (outOffset != inOffset) {
                                if (index <= 0) {
                                    vehicleData.m_pathPositionIndex = (byte)(vehicleData.m_pathPositionIndex & -2);
                                    vehicleData.m_lastPathOffset = inOffset;
                                }

                                currentPosition.m_offset = outOffset;
                                pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)pathId)].SetPosition(
                                    finePathPosIndex >> 1,
                                    currentPosition);
                            }

                            vehicleData.m_flags |= Vehicle.Flags.Parking;

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Parking vehicle. FINISH. inOffset={inOffset}, outOffset={outOffset}");
                        } else {
                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                "Parking vehicle failed. ABORT.");
                            InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                        }
                    } else {
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Parking vehicle not allowed. ABORT.");
                    }

                    return false;
                }

                // check for errors
                if ((byte)(nextLaneInfo.m_laneType & (NetInfo.LaneType.Vehicle
                                                      | NetInfo.LaneType.CargoVehicle
                                                      | NetInfo.LaneType.TransportVehicle)) == 0) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Next lane invalid. ABORT. nextLaneInfo.m_laneType={nextLaneInfo.m_laneType}");
                    GameConnectionManager.Instance.VehicleAIConnection.InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                    return false;
                }

                // change vehicle
                if (nextLaneInfo.m_vehicleType != __instance.m_info.m_vehicleType &&
                    NeedChangeVehicleType(
                        __instance,
                        vehicleID,
                        ref vehicleData,
                        nextPosition,
                        nextLaneId,
                        nextLaneInfo.m_vehicleType,
                        ref targetPos))
                {
                    float targetPos0ToRefPosSqrDist = ((Vector3)targetPos - refPos).sqrMagnitude;
                    if (targetPos0ToRefPosSqrDist >= minSqrDistA) {
                        vehicleData.SetTargetPos(index++, targetPos);
                    }

                    if (index <= 0) {
                        while (index < max) {
                            vehicleData.SetTargetPos(index++, targetPos);
                        }

                        if (nextPathId != vehicleData.m_path) {
                            Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
                        }

                        vehicleData.m_pathPositionIndex = (byte)(nextCoarsePathPosIndex << 1);
                        PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out vehicleData.m_lastPathOffset);
                        if (vehicleID != 0
                            && !ChangeVehicleType(__instance, vehicleID, ref vehicleData, nextPosition, nextLaneId))
                        {
                            GameConnectionManager.Instance.VehicleAIConnection.InvalidPath(__instance, vehicleID, ref vehicleData, vehicleID, ref vehicleData);
                        }
                    } else {
                        while (index < max) {
                            vehicleData.SetTargetPos(index++, targetPos);
                        }
                    }

                    if (logLogic) {
                        Log._Debug(
                            $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Need to change vehicle. FINISH. " +
                            $"targetPos0ToRefPosSqrDist={targetPos0ToRefPosSqrDist}, " +
                            $"index={index}");
                    }

                    return false;
                }

                // unset leaving flag
                if (nextPosition.m_segment != currentPosition.m_segment && vehicleID != 0) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Unsetting Leaving flag");
                    vehicleData.m_flags &= ~Vehicle.Flags.Leaving;
                }

                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    "Calculating next segment position");

                // calculate next segment offset
                byte nextSegOffset;
                if ((vehicleData.m_flags & Vehicle.Flags.Flying) != 0) {
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Vehicle is flying");
                    nextSegOffset = (byte)((nextPosition.m_offset < 128) ? 255 : 0);
                } else if (curLaneId != nextLaneId &&
                           laneInfo.m_laneType != NetInfo.LaneType.CargoVehicle) {
                    PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);

                    Bezier3 bezier = default(Bezier3);

                    CalculateSegmentPosition(
                        __instance,
                        vehicleID,
                        ref vehicleData,
                        currentPosition,
                        curLaneId,
                        currentPosition.m_offset,
                        out bezier.a,
                        out Vector3 curSegDir,
                        out float maxSpeed);

                    bool calculateNextNextPos = pathOffset == 0;
                    if (calculateNextNextPos) {
                        if ((vehicleData.m_flags & Vehicle.Flags.Reversed) != 0) {
                            calculateNextNextPos = vehicleData.m_trailingVehicle == 0;
                        } else {
                            calculateNextNextPos = vehicleData.m_leadingVehicle == 0;
                        }
                    }

                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        "Calculated current segment position for regular vehicle. " +
                        $"nextSegOffset={nextSegOffset}, bezier.a={bezier.a}, " +
                        $"curSegDir={curSegDir}, maxSpeed={maxSpeed}, pathOffset={pathOffset}, " +
                        $"calculateNextNextPos={calculateNextNextPos}");

                    Vector3 nextSegDir;
                    float curMaxSpeed;
                    if (calculateNextNextPos) {
                        if (!pathMan.m_pathUnits.m_buffer[nextPathId].GetNextPosition(
                                nextCoarsePathPosIndex,
                                out PathUnit.Position nextNextPosition)) {
                            nextNextPosition = default;
                        }

                        CalculateSegmentPosition2(
                            __instance,
                            vehicleID,
                            ref vehicleData,
                            nextNextPosition,
                            nextPosition,
                            nextLaneId,
                            nextSegOffset,
                            currentPosition,
                            curLaneId,
                            currentPosition.m_offset,
                            index,
                            out bezier.d,
                            out nextSegDir,
                            out curMaxSpeed);

                        if (logLogic) {
                            Log._Debug(
                                $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                "Next next path position needs to be calculated. Used triple-calculation. " +
                                $"IN: nextNextPosition=[seg={nextNextPosition.m_segment}, " +
                                $"lane={nextNextPosition.m_lane}, off={nextNextPosition.m_offset}], " +
                                $"nextPosition=[seg={nextPosition.m_segment}, lane={nextPosition.m_lane}, " +
                                $"off={nextPosition.m_offset}], nextLaneId={nextLaneId}, " +
                                $"nextSegOffset={nextSegOffset}, currentPosition=[" +
                                $"seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, " +
                                $"off={currentPosition.m_offset}], curLaneId={curLaneId}, " +
                                $"index={index}, OUT: bezier.d={bezier.d}, " +
                                $"nextSegDir={nextSegDir}, curMaxSpeed={curMaxSpeed}");
                        }
                    } else {
                        CalculateSegmentPosition(
                            __instance,
                            vehicleID,
                            ref vehicleData,
                            nextPosition,
                            nextLaneId,
                            nextSegOffset,
                            out bezier.d,
                            out nextSegDir,
                            out curMaxSpeed);

                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            "Next next path position needs not to be calculated. Used regular " +
                            $"calculation. IN: nextPosition=[seg={nextPosition.m_segment}, " +
                            $"lane={nextPosition.m_lane}, off={nextPosition.m_offset}], " +
                            $"nextLaneId={nextLaneId}, nextSegOffset={nextSegOffset}, " +
                            $"OUT: bezier.d={bezier.d}, nextSegDir={nextSegDir}, " +
                            $"curMaxSpeed={curMaxSpeed}");
                    }

                    if (curMaxSpeed < 0.01f
                        || (netManager.m_segments.m_buffer[nextPosition.m_segment].m_flags
                            & (NetSegment.Flags.Collapsed
                               | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
                    {
                        if (index <= 0) {
                            vehicleData.m_lastPathOffset = pathOffset;
                        }

                        targetPos = bezier.a;
                        targetPos.w = 0f;
                        while (index < max) {
                            vehicleData.SetTargetPos(index++, targetPos);
                        }

                        if (logLogic) {
                            Log._Debug(
                                $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                "Low max speed or segment flooded/collapsed. FINISH. " +
                                $"curMaxSpeed={curMaxSpeed}, " +
                                $"flags={netManager.m_segments.m_buffer[nextPosition.m_segment].m_flags}, " +
                                $"vehicleData.m_lastPathOffset={vehicleData.m_lastPathOffset}, " +
                                $"targetPos={targetPos}, index={index}");
                        }

                        return false;
                    }

                    if (currentPosition.m_offset == 0) {
                        curSegDir = -curSegDir;
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            $"currentPosition.m_offset == 0: inverted curSegDir={curSegDir}");
                    }

                    if (nextSegOffset < nextPosition.m_offset) {
                        nextSegDir = -nextSegDir;
                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            $"nextSegOffset={nextSegOffset} < " +
                            $"nextPosition.m_offset={nextPosition.m_offset}: inverted " +
                            $"nextSegDir={nextSegDir}");
                    }

                    curSegDir.Normalize();
                    nextSegDir.Normalize();
                    NetSegment.CalculateMiddlePoints(
                        bezier.a,
                        curSegDir,
                        bezier.d,
                        nextSegDir,
                        true,
                        true,
                        out bezier.b,
                        out bezier.c,
                        out float dist);

                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Direction vectors normalied. curSegDir={curSegDir}, nextSegDir={nextSegDir}\n" +
                        $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Calculated bezier middle points. IN: bezier.a={bezier.a}, " +
                        $"curSegDir={curSegDir}, bezier.d={bezier.d}, nextSegDir={nextSegDir}, " +
                        $"true, true, OUT: bezier.b={bezier.b}, bezier.c={bezier.c}, dist={dist}");

                    if (dist > 1f) {
                        ushort nextNodeId;
                        if (nextSegOffset == 0) {
                            nextNodeId = netManager.m_segments.m_buffer[nextPosition.m_segment].m_startNode;
                        } else if (nextSegOffset == 255) {
                            nextNodeId = netManager.m_segments.m_buffer[nextPosition.m_segment].m_endNode;
                        } else {
                            nextNodeId = 0;
                        }

                        float curve = 1.57079637f * (1f + Vector3.Dot(curSegDir, nextSegDir));
                        if (dist > 1f) {
                            curve /= dist;
                        }

                        curMaxSpeed = Mathf.Min(
                            curMaxSpeed,
                            CalculateTargetSpeed(__instance, vehicleID, ref vehicleData, 1000f, curve));

                        Log._DebugIf(
                            logLogic,
                            () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                            $"dist > 1f. nextNodeId={nextNodeId}, curve={curve}, " +
                            $"curMaxSpeed={curMaxSpeed}, pathOffset={pathOffset}");

                        // update node target positions
                        while (pathOffset < 255) {
                            float distDiff = Mathf.Sqrt(minSqrDistA) - Vector3.Distance(targetPos, refPos);
                            int pathOffsetDelta;
                            if (distDiff < 0f) {
                                pathOffsetDelta = 8;
                            } else {
                                pathOffsetDelta = 8 + Mathf.Max(0, Mathf.CeilToInt(distDiff * 256f / (dist + 1f)));
                            }

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                "Preparing to update node target positions (1). " +
                                $"pathOffset={pathOffset}, distDiff={distDiff}, " +
                                $"pathOffsetDelta={pathOffsetDelta}");

                            pathOffset = (byte)Mathf.Min(pathOffset + pathOffsetDelta, 255);
                            Vector3 bezierPos = bezier.Position(pathOffset * 0.003921569f);
                            targetPos.Set(
                                bezierPos.x,
                                bezierPos.y,
                                bezierPos.z,
                                Mathf.Min(targetPos.w, curMaxSpeed));
                            float sqrMagnitude2 = (bezierPos - refPos).sqrMagnitude;

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                "Preparing to update node target positions (2). " +
                                $"pathOffset={pathOffset}, bezierPos={bezierPos}, " +
                                $"targetPos={targetPos}, sqrMagnitude2={sqrMagnitude2}");

                            if (!(sqrMagnitude2 >= minSqrDistA)) {
                                continue;
                            }

                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"sqrMagnitude2={sqrMagnitude2} >= minSqrDistA={minSqrDistA}");

                            if (index <= 0) {
                                vehicleData.m_lastPathOffset = pathOffset;
                                if (logLogic) {
                                    Log._Debug(
                                        $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                        "index <= 0: Setting vehicleData.m_lastPathOffset" +
                                        $"={vehicleData.m_lastPathOffset}");
                                }
                            }

                            if (nextNodeId != 0) {
                                UpdateNodeTargetPos(
                                    __instance,
                                    vehicleID,
                                    ref vehicleData,
                                    nextNodeId,
                                    ref netManager.m_nodes.m_buffer[nextNodeId],
                                    ref targetPos,
                                    index);
                                if (logLogic) {
                                    Log._Debug(
                                        "CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                        $"nextNodeId={nextNodeId} != 0: Updated node target position. " +
                                        $"targetPos={targetPos}, index={index}");
                                }
                            }

                            vehicleData.SetTargetPos(index++, targetPos);
                            minSqrDistA = minSqrDistanceB;
                            refPos = targetPos;
                            targetPos.w = 1000f;
                            Log._DebugIf(
                                logLogic,
                                () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"After updating node target positions. minSqrDistA={minSqrDistA}, " +
                                $"refPos={refPos}");

                            if (index == max) {
                                Log._DebugIf(
                                    logLogic,
                                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                    $"index == max ({max}). " +
                                    "FINISH.");
                                return false;
                            }
                        }
                    }
                } else {
                    PathUnit.CalculatePathPositionOffset(nextLaneId, targetPos, out nextSegOffset);
                    Log._DebugIf(
                        logLogic,
                        () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                        $"Same lane or cargo lane. curLaneId={curLaneId}, nextLaneId={nextLaneId}, " +
                        $"laneInfo.m_laneType={laneInfo.m_laneType}, targetPos={targetPos}, " +
                        $"nextSegOffset={nextSegOffset}");
                }

                // check for arrival
                if (index <= 0) {
                    if ((netManager.m_segments.m_buffer[nextPosition.m_segment].m_flags
                         & NetSegment.Flags.Untouchable) != 0
                        && (netManager.m_segments.m_buffer[currentPosition.m_segment].m_flags
                            & NetSegment.Flags.Untouchable) == NetSegment.Flags.None)
                    {
                        ushort ownerBuildingId = NetSegment.FindOwnerBuilding(nextPosition.m_segment, 363f);

                        if (ownerBuildingId != 0) {
                            BuildingManager buildingMan = Singleton<BuildingManager>.instance;
                            BuildingInfo ownerBuildingInfo = buildingMan.m_buildings.m_buffer[ownerBuildingId].Info;
                            InstanceID itemId = default(InstanceID);
                            itemId.Vehicle = vehicleID;
                            ownerBuildingInfo.m_buildingAI.EnterBuildingSegment(
                                ownerBuildingId,
                                ref buildingMan.m_buildings.m_buffer[ownerBuildingId],
                                nextPosition.m_segment,
                                nextPosition.m_offset,
                                itemId);
                        }
                    }

                    if (nextCoarsePathPosIndex == 0) {
                        Singleton<PathManager>.instance.ReleaseFirstUnit(ref vehicleData.m_path);
                    }

                    if (nextCoarsePathPosIndex >=
                        pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_positionCount - 1 &&
                        pathMan.m_pathUnits.m_buffer[(int)((UIntPtr)nextPathId)].m_nextPathUnit == 0u &&
                        vehicleID != 0)
                    {
                        if (logLogic) {
                            Log._Debug(
                                $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                                $"Arriving at destination. index={index}, " +
                                $"nextCoarsePathPosIndex={nextCoarsePathPosIndex}");
                        }

                        ArrivingToDestination(__instance, vehicleID, ref vehicleData);
                    }
                }

                // prepare next loop iteration: go to next path position
                pathId = nextPathId;
                finePathPosIndex = (byte)(nextCoarsePathPosIndex << 1);
                pathOffset = nextSegOffset;
                if (index <= 0) {
                    vehicleData.m_pathPositionIndex = finePathPosIndex;
                    vehicleData.m_lastPathOffset = pathOffset;
                    vehicleData.m_flags = (vehicleData.m_flags & ~(Vehicle.Flags.OnGravel
                                                                   | Vehicle.Flags.Underground
                                                                   | Vehicle.Flags.Transition))
                                          | nextSegmentInfo.m_setVehicleFlags;

                    if (LeftHandDrive(__instance, nextLaneInfo)) {
                        vehicleData.m_flags |= Vehicle.Flags.LeftHandDrive;
                    } else {
                        vehicleData.m_flags &= Vehicle.Flags.Created | Vehicle.Flags.Deleted |
                                               Vehicle.Flags.Spawned |
                                               Vehicle.Flags.Inverted | Vehicle.Flags.TransferToTarget |
                                               Vehicle.Flags.TransferToSource | Vehicle.Flags.Emergency1 |
                                               Vehicle.Flags.Emergency2 | Vehicle.Flags.WaitingPath |
                                               Vehicle.Flags.Stopped | Vehicle.Flags.Leaving |
                                               Vehicle.Flags.Arriving |
                                               Vehicle.Flags.Reversed | Vehicle.Flags.TakingOff |
                                               Vehicle.Flags.Flying | Vehicle.Flags.Landing |
                                               Vehicle.Flags.WaitingSpace | Vehicle.Flags.WaitingCargo |
                                               Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget |
                                               Vehicle.Flags.Importing | Vehicle.Flags.Exporting |
                                               Vehicle.Flags.Parking | Vehicle.Flags.CustomName |
                                               Vehicle.Flags.OnGravel | Vehicle.Flags.WaitingLoading |
                                               Vehicle.Flags.Congestion | Vehicle.Flags.DummyTraffic |
                                               Vehicle.Flags.Underground | Vehicle.Flags.Transition |
                                               Vehicle.Flags.InsideBuilding;
                    }
                }

                currentPosition = nextPosition;
                curLaneId = nextLaneId;
                laneInfo = nextLaneInfo;
                firstIter = false; // NON-STOCK CODE

                Log._DebugIf(
                    logLogic,
                    () => $"CustomVehicle.CustomUpdatePathTargetPositions({vehicleID}): " +
                    "Prepared next main loop iteration. currentPosition" +
                    $"=[seg={currentPosition.m_segment}, lane={currentPosition.m_lane}, " +
                    $"off={currentPosition.m_offset}], curLaneId={curLaneId}");
            } // end while true

            // Unreachable
        }
    }
}
namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using UnityEngine;

    public interface IVehicleBehaviorManager {
        // TODO define me!
        // TODO documentation

        /// <summary>
        /// Tries to park a passenger car near its current position.
        /// </summary>
        /// <param name="vehicleID">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleInfo">vehicle info</param>
        /// <param name="driverCitizenId">driver citizen id</param>
        /// <param name="driverCitizenInstanceId">driver citizen instance id</param>
        /// <param name="driverExtInstance">driver extended citizen instance</param>
        /// <param name="targetBuildingId">target building id</param>
        /// <param name="pathPos">current path position</param>
        /// <param name="nextPath">next path unit id</param>
        /// <param name="nextPositionIndex">next path position index</param>
        /// <param name="segmentOffset">current segment offset</param>
        /// <returns><code>true</code> if parking (will) succeed, <code>false</code> otherwise</returns>
        bool ParkPassengerCar(ushort vehicleID,
                              ref Vehicle vehicleData,
                              VehicleInfo vehicleInfo,
                              uint driverCitizenId,
                              ref Citizen driverCitizen,
                              ushort driverCitizenInstanceId,
                              ref CitizenInstance driverInstance,
                              ref ExtCitizenInstance driverExtInstance,
                              ushort targetBuildingId,
                              PathUnit.Position pathPos,
                              uint nextPath,
                              int nextPositionIndex,
                              out byte segmentOffset);

        /// <summary>
        /// Starts path-finding for a passenger car.
        /// </summary>
        /// <param name="vehicleID">vehicle id</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleInfo">vehicle info (in stock code, this is passed via
        ///     VehicleAI.m_info. Don't know if this is actually always equal to vehicleData.Info)</param>
        /// <param name="driverInstanceId">driver citizen instance id</param>
        /// <param name="driverInstanceData">driver citizen instance data</param>
        /// <param name="driverExtInstance">driver extended citizen instance</param>
        /// <param name="startPos">start position</param>
        /// <param name="endPos">end position</param>
        /// <param name="startBothWays">allow considering both road sides at start position?</param>
        /// <param name="endBothWays">allow considering both road sides at end position?</param>
        /// <param name="undergroundTarget">is target in undeground?</param>
        /// <param name="isHeavyVehicle">is this a heavy vehicle?</param>
        /// <param name="hasCombustionEngine">does the vehicle have a combustion engine?</param>
        /// <param name="ignoreBlocked">should blocked roads be ignored?</param>
        /// <returns></returns>
        bool StartPassengerCarPathFind(ushort vehicleID,
                                       ref Vehicle vehicleData,
                                       VehicleInfo vehicleInfo,
                                       ushort driverInstanceId,
                                       ref CitizenInstance driverInstanceData,
                                       ref ExtCitizenInstance driverExtInstance,
                                       Vector3 startPos,
                                       Vector3 endPos,
                                       bool startBothWays,
                                       bool endBothWays,
                                       bool undergroundTarget,
                                       bool isHeavyVehicle,
                                       bool hasCombustionEngine,
                                       bool ignoreBlocked);

        /// <summary>
        /// Checks if space reservation at <paramref name="targetPos"/> is allowed. When a custom
        ///     traffic light is active at the transit node
        /// space reservation is only allowed if the light is not red.
        /// </summary>
        /// <param name="transitNodeId">transition node id</param>
        /// <param name="sourcePos">source path position</param>
        /// <param name="targetPos">target path position</param>
        /// <returns></returns>
        bool IsSpaceReservationAllowed(ushort transitNodeId,
                                       PathUnit.Position sourcePos,
                                       PathUnit.Position targetPos);

        /// <summary>
        /// Determines if the given vehicle is driven by a reckless driver.
        /// Note that the result is cached in VehicleState for individual vehicles.
        /// </summary>
        /// <param name="vehicleId"></param>
        /// <param name="vehicleData"></param>
        /// <returns></returns>
        bool IsRecklessDriver(ushort vehicleId, ref Vehicle vehicleData);

        /// <summary>
        /// Identifies the best lane on the next segment.
        /// </summary>
        /// <param name="vehicleId">queried vehicle</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleState">vehicle state</param>
        /// <param name="currentLaneId">current lane id</param>
        /// <param name="currentPathPos">current path position</param>
        /// <param name="currentSegInfo">current segment info</param>
        /// <param name="next1PathPos">1st next path position</param>
        /// <param name="next1SegInfo">1st next segment info</param>
        /// <param name="next2PathPos">2nd next path position</param>
        /// <param name="next3PathPos">3rd next path position</param>
        /// <param name="next4PathPos">4th next path position</param>
        /// <returns>target position lane index</returns>
        int FindBestLane(ushort vehicleId,
                         ref Vehicle vehicleData,
                         ref ExtVehicle vehicleState,
                         uint currentLaneId,
                         PathUnit.Position currentPathPos,
                         NetInfo currentSegInfo,
                         PathUnit.Position next1PathPos,
                         NetInfo next1SegInfo,
                         PathUnit.Position next2PathPos,
                         NetInfo next2SegInfo,
                         PathUnit.Position next3PathPos,
                         NetInfo next3SegInfo,
                         PathUnit.Position next4PathPos);

        /// <summary>
        /// Determines if the given vehicle is allowed to find an alternative lane.
        /// </summary>
        /// <param name="vehicleId">queried vehicle</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="vehicleState">vehicle state</param>
        /// <returns></returns>
        bool MayFindBestLane(ushort vehicleId,
                             ref Vehicle vehicleData,
                             ref ExtVehicle vehicleState);

        /// <summary>
        /// Applies realistic speed multipliers to the given velocity.
        /// </summary>
        /// <param name="speed">vehicle target velocity</param>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="extVehicle">ext. vehicle</param>
        /// <param name="vehicleInfo">vehicle info</param>
        /// <returns>modified target velocity</returns>
        float ApplyRealisticSpeeds(float speed,
                                   ushort vehicleId,
                                   ref ExtVehicle extVehicle,
                                   VehicleInfo vehicleInfo);

        /// <summary>
        /// Calculates the target velocity for the given vehicle.
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="extVehicle">ext. vehicle</param>
        /// <param name="vehicleInfo">vehicle info</param>
        /// <param name="position">current path position</param>
        /// <param name="segment">segment data</param>
        /// <param name="pos">current world position</param>
        /// <param name="maxSpeed">vehicle target velocity</param>
        /// <param name="emergency">specifies if the segment is currently used by emergency vehicles</param>
        /// <returns>modified target velocity</returns>
        float CalcMaxSpeed(ushort vehicleId,
                           ref ExtVehicle extVehicle,
                           VehicleInfo vehicleInfo,
                           PathUnit.Position position,
                           ref NetSegment segment,
                           Vector3 pos,
                           float maxSpeed,
                           bool emergency);
    }
}
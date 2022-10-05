namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using UnityEngine;

    public interface IAdvancedParkingManager : IFeatureManager {
        /// <summary>
        /// Determines the color the given building should be colorized with given the current info view mode.
        /// While the traffic view is active buildings with high parking space demand are colored red and
        /// buildings with low demand are colored green.
        /// </summary>
        /// <param name="buildingId">building id</param>
        /// <param name="buildingData">building data</param>
        /// <param name="infoMode">current info view mode</param>
        /// <param name="color">output color</param>
        /// <returns>true if a custom color should be displayed, false otherwise</returns>
        bool GetBuildingInfoViewColor(ushort buildingId,
                                      ref Building buildingData,
                                      ref ExtBuilding extBuilding,
                                      InfoManager.InfoMode infoMode,
                                      out Color? color);

        /// <summary>
        /// Adds Parking AI related information to the given citizen status text.
        /// </summary>
        /// <param name="ret">status text to enrich</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="extCitizen">extended citizen data</param>
        /// <returns></returns>
        string EnrichLocalizedCitizenStatus(string ret,
                                            ref ExtCitizenInstance extInstance,
                                            ref ExtCitizen extCitizen);

        /// <summary>
        /// Adds Parking AI related information to the given passenger car status text.
        /// </summary>
        /// <param name="ret">status text to enrich</param>
        /// <param name="driverExtInstance">extended citizen instance data</param>
        /// <returns></returns>
        string EnrichLocalizedCarStatus(string ret, ref ExtCitizenInstance driverExtInstance);

        /// <summary>
        /// Makes the given citizen instance enter their parked car.
        /// </summary>
        /// <param name="instanceID">Citizen instance id</param>
        /// <param name="instanceData">Citizen instance data</param>
        /// <param name="citizen">Citizen data</param>
        /// <param name="parkedVehicleId">Parked vehicle id</param>
        /// <param name="vehicleId">Vehicle id</param>
        /// <returns>true if entering the car succeeded, false otherwise</returns>
        bool EnterParkedCar(ushort instanceID,
                            ref CitizenInstance instanceData,
                            ref Citizen citizen,
                            ushort parkedVehicleId,
                            out ushort vehicleId);

        /// <summary>
        /// Merges the current calculation states of the citizen's main path and return path (while walking).
        /// If a definite calculation state can be determined path-find failure/success is handled appropriately.
        /// The returned (soft) path state indicates if further handling must be undertaken by the game.
        /// </summary>
        /// <param name="citizenInstanceId">citizen instance that shall be processed</param>
        /// <param name="citizenInstance">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="extCitizen">extended citizen data</param>
        /// <param name="citizen">citizen data</param>
        /// <param name="mainPathState">current state of the citizen instance's main path</param>
        /// <returns>
        ///		Indication of how (external) game logic should treat this situation:
        ///		<code>Calculating</code>: Paths are still being calculated. Game must await completion.
        ///		<code>Ready</code>: All paths are ready and path-find success must be handled.
        ///		<code>FailedHard</code>: At least one path calculation failed and the failure must be handled.
        ///		<code>FailedSoft</code>: Path-finding must be repeated.
        ///		<code>Ignore</code>: Default citizen behavior must be skipped.
        ///	</returns>
        ExtSoftPathState UpdateCitizenPathState(ushort citizenInstanceId,
                                                ref CitizenInstance citizenInstance,
                                                ref ExtCitizenInstance extInstance,
                                                ref ExtCitizen extCitizen,
                                                ref Citizen citizen,
                                                ExtPathState mainPathState);

        /// <summary>
        /// Merges the current calculation states of the citizen's main path and return path (while driving a passenger car).
        /// If a definite calculation state can be determined path-find failure/success is handled appropriately.
        /// The returned (soft) path state indicates if further handling must be undertaken by the game.
        /// </summary>
        /// <param name="vehicleId">vehicle that shall be processed</param>
        /// <param name="vehicleData">vehicle data</param>
        /// <param name="driverInstance">driver citizen instance</param>
        /// <param name="driverExtInstance">extended citizen instance data of the driving citizen</param>
        /// <param name="mainPathState">current state of the citizen instance's main path</param>
        /// <returns>
        ///		Indication of how (external) game logic should treat this situation:
        ///		<code>Calculating</code>: Paths are still being calculated. Game must await completion.
        ///		<code>Ready</code>: All paths are ready and path-find success must be handled.
        ///		<code>FailedHard</code>: At least one path calculation failed and the failure must be handled.
        ///		<code>FailedSoft</code>: Path-finding must be repeated.
        ///		<code>Ignore</code>: Default citizen behavior must be skipped.
        /// </returns>
        ExtSoftPathState UpdateCarPathState(ushort vehicleId,
                                            ref Vehicle vehicleData,
                                            ref CitizenInstance driverInstance,
                                            ref ExtCitizenInstance driverExtInstance,
                                            ExtPathState mainPathState);

        /// <summary>
        /// Processes a citizen that is approaching their private car.
        /// Internal state information is updated appropriately. The returned approach
        /// state indicates if the approach is finished.
        /// </summary>
        /// <param name="instanceId">citizen instance that shall be processed</param>
        /// <param name="instanceData">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance data</param>
        /// <param name="physicsLodRefPos">simulation accuracy</param>
        /// <param name="parkedCar">parked car data</param>
        /// <returns>
        ///		Approach state indication:
        ///		<code>Approaching</code>: The citizen is currently approaching the parked car.
        ///		<code>Approached</code>: The citizen has approached the car and is ready to enter it.
        ///		<code>Failure</code>: The approach procedure failed (currently not returned).
        /// </returns>
        ParkedCarApproachState CitizenApproachingParkedCarSimulationStep(
            ushort instanceId,
            ref CitizenInstance instanceData,
            ref ExtCitizenInstance extInstance,
            Vector3 physicsLodRefPos,
            ref VehicleParked parkedCar);

        /// <summary>
        /// Processes a citizen that is approaching their target building.
        /// Internal state information is updated appropriately. The returned flag
        /// indicates if the approach is finished.
        /// </summary>
        /// <param name="instanceId">citizen instance that shall be processed</param>
        /// <param name="instanceData">citizen instance data</param>
        /// <param name="extInstance">extended citizen instance</param>
        /// <returns>true if the citizen arrived at the target, false otherwise</returns>
        bool CitizenApproachingTargetSimulationStep(ushort instanceId,
                                                    ref CitizenInstance instanceData,
                                                    ref ExtCitizenInstance extInstance);

        /// <summary>
        /// Finds a free parking space in the vicinity of the given target position <paramref name="endPos"/>
        /// for the given citizen instance <paramref name="extDriverInstance"/>.
        /// </summary>
        /// <param name="endPos">target position</param>
        /// <param name="vehicleInfo">vehicle type that is being used</param>
        /// <param name="driverInstance">cititzen instance that is driving the car</param>
        /// <param name="extDriverInstance">extended cititzen instance that is driving the car</param>
        /// <param name="homeId">Home building of the citizen (may be 0 for tourists/homeless cims)</param>
        /// <param name="goingHome">Specifies if the citizen is going home</param>
        /// <param name="vehicleId">Vehicle that is being used (used for logging)</param>
        /// <param name="allowTourists">If true, method fails if given citizen is a tourist (TODO remove this parameter)</param>
        /// <param name="parkPos">parking position (output)</param>
        /// <param name="endPathPos">sidewalk path position near parking space (output). only valid if <paramref name="calculateEndPos"/> yields false.</param>
        /// <param name="calculateEndPos">if false, a parking space path position could be calculated (TODO negate & rename parameter)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceForCitizen(Vector3 endPos,
                                        VehicleInfo vehicleInfo,
                                        ref CitizenInstance driverInstance,
                                        ref ExtCitizenInstance extDriverInstance,
                                        ushort homeId,
                                        bool goingHome,
                                        ushort vehicleId,
                                        bool allowTourists,
                                        out Vector3 parkPos,
                                        ref PathUnit.Position endPathPos,
                                        out bool calculateEndPos);

        /// <summary>
        /// Tries to relocate the given parked car (<paramref name="parkedVehicleId"/>, <paramref name="parkedVehicle"/>)
        /// within the vicinity of the given reference position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="parkedVehicleId">parked vehicle id</param>
        /// <param name="parkedVehicle">parked vehicle data</param>
        /// <param name="refPos">reference position</param>
        /// <param name="maxDistance">maximum allowed distance between reference position and parking space location</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <returns><code>true</code> if the parked vehicle was relocated, <code>false</code> otherwise</returns>
        bool TryMoveParkedVehicle(ushort parkedVehicleId,
                                  ref VehicleParked parkedVehicle,
                                  Vector3 refPos,
                                  float maxDistance,
                                  ushort homeId);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen ID that requires a parked car</param>
        /// <param name="citizen">Citizen that requires a parked car</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCar(uint citizenId,
                                        ref Citizen citizen,
                                        ushort homeId,
                                        Vector3 refPos,
                                        VehicleInfo vehicleInfo,
                                        out Vector3 parkPos,
                                        out ParkingError reason);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// at a road segment in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen that requires a parked car</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCarRoadSide(uint citizenId,
                                                Vector3 refPos,
                                                VehicleInfo vehicleInfo,
                                                out Vector3 parkPos,
                                                out ParkingError reason);

        /// <summary>
        /// Tries to spawn a parked passenger car for the given citizen <paramref name="citizenId"/>
        /// at a building in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="citizenId">Citizen id that requires a parked car</param>
        /// <param name="citizen">Citizen that requires a parked car</param>
        /// <param name="homeId">Home building id of the citizen (For residential buildings, parked cars may only spawn at the home building)</param>
        /// <param name="refPos">Reference position</param>
        /// <param name="vehicleInfo">Vehicle type to spawn</param>
        /// <param name="parkPos">Parked vehicle position (output)</param>
        /// <param name="reason">Indicates the reason why no car could be spawned when the method returns false</param>
        /// <returns>true if a passenger car could be spawned, false otherwise</returns>
        bool TrySpawnParkedPassengerCarBuilding(uint citizenId,
                                                ref Citizen citizen,
                                                ushort homeId,
                                                Vector3 refPos,
                                                VehicleInfo vehicleInfo,
                                                out Vector3 parkPos,
                                                out ParkingError reason);

        /// <summary>
        /// Tries to find a parking space in the broaded vicinity of the given position <paramref name="targetPos"/>.
        /// </summary>
        /// <param name="targetPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="searchDir">Search direction</param>
        /// <param name="vehicleInfo">Vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="homeId">Home building id of the citizen (citizens are not allowed to park their car on foreign residential premises)</param>
        /// <param name="vehicleId">Vehicle that shall be parked</param>
        /// <param name="maxDist">maximum allowed distance between target position and parking space location</param>
        /// <param name="parkingSpaceLocation">identified parking space location type (only valid if method returns true)</param>
        /// <param name="parkingSpaceLocationId">identified parking space location identifier (only valid if method returns true)</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceInVicinity(Vector3 targetPos,
                                        Vector3 searchDir,
                                        VehicleInfo vehicleInfo,
                                        ushort homeId,
                                        ushort vehicleId,
                                        float maxDist,
                                        out ExtParkingSpaceLocation parkingSpaceLocation,
                                        out ushort parkingSpaceLocationId,
                                        out Vector3 parkPos,
                                        out Quaternion parkRot,
                                        out float parkOffset);

        /// <summary>
        /// Tries to find a parking space for a moving vehicle at a given segment. The search
        /// is restricted to the given segment.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="segmentId">segment to search on</param>
        /// <param name="refPos">current vehicle position</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <param name="laneId">identified parking space lane id (only valid if method returns true)</param>
        /// <param name="laneIndex">identified parking space lane index (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceRoadSideForVehiclePos(VehicleInfo vehicleInfo,
                                                   ushort ignoreParked,
                                                   ushort segmentId,
                                                   Vector3 refPos,
                                                   out Vector3 parkPos,
                                                   out Quaternion parkRot,
                                                   out float parkOffset,
                                                   out uint laneId,
                                                   out int laneIndex);

        /// <summary>
        /// Tries to find a road-side parking space in the vicinity of the given position <paramref name="refPos"/>.
        /// </summary>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="refPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="width">vehicle width</param>
        /// <param name="length">vehicle length</param>
        /// <param name="maxDistance">Maximum allowed distance between the target position and the parking space</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceRoadSide(ushort ignoreParked,
                                      Vector3 refPos,
                                      float width,
                                      float length,
                                      float maxDistance,
                                      out Vector3 parkPos,
                                      out Quaternion parkRot,
                                      out float parkOffset);

        /// <summary>
        /// Tries to find a parking space at a building in the vicinity of the given position
        /// <paramref name="targetPos"/>.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle
        ///     geometry information)</param>
        /// <param name="homeID">Home building id of the citizen (citizens are not allowed to park
        ///     their car on foreign residential premises)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="segmentId">if != 0, the building is forced to be "accessible" from this
        ///     segment (where accessible means "close enough")</param>
        /// <param name="refPos">Target position that is used as a center point for the search
        ///     procedure</param>
        /// <param name="maxBuildingDistance">Maximum allowed distance between the target position
        ///     and the parking building</param>
        /// <param name="maxParkingSpaceDistance">Maximum allowed distance between the target
        ///     position and the parking space</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns
        ///     true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns
        ///     true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns
        ///     true and a segment id was given)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpaceBuilding(VehicleInfo vehicleInfo,
                                      ushort homeID,
                                      ushort ignoreParked,
                                      ushort segmentId,
                                      Vector3 refPos,
                                      float maxBuildingDistance,
                                      float maxParkingSpaceDistance,
                                      out Vector3 parkPos,
                                      out Quaternion parkRot,
                                      out float parkOffset);

        /// <summary>
        /// Tries to find a parking space prop that belongs to the given building <paramref name="buildingID"/>.
        /// </summary>
        /// <param name="vehicleInfo">vehicle that shall be parked (used for gathering vehicle geometry information)</param>
        /// <param name="homeID">Home building id of the citizen (citizens are not allowed to park their car on foreign residential premises)</param>
        /// <param name="ignoreParked">if true, already parked vehicles are ignored</param>
        /// <param name="buildingID">Building that is queried</param>
        /// <param name="building">Building data</param>
        /// <param name="segmentId">if != 0, the building is forced to be "accessible" from this segment (where accessible means "close enough")</param>
        /// <param name="refPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="maxDistance">Maximum allowed distance between the target position and the parking space</param>
        /// <param name="randomize">If true, search is randomized such that not always only the closest parking space is selected.</param>
        /// <param name="parkPos">identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">identified parking space offset (only valid if method returns true and a segment id was given)</param>
        /// <returns>true if a parking space could be found, false otherwise</returns>
        bool FindParkingSpacePropAtBuilding(VehicleInfo vehicleInfo,
                                            ushort homeID,
                                            ushort ignoreParked,
                                            ushort buildingID,
                                            ref Building building,
                                            ushort segmentId,
                                            Vector3 refPos,
                                            ref float maxDistance,
                                            bool randomize,
                                            out Vector3 parkPos,
                                            out Quaternion parkRot,
                                            out float parkOffset);

        /// <summary>
        /// Tries to find parking space using vanilla code, preserving parking restrictions
        /// </summary>
        /// <param name="isElectric">Is electric vehicle</param>
        /// <param name="homeId">Home building id of the citizen (citizens are not allowed to park their car on foreign residential premises)</param>
        /// <param name="refPos">Target position that is used as a center point for the search procedure</param>
        /// <param name="searchDir">Search direction</param>
        /// <param name="segment">If != 0, the building is forced to be "accessible" from this segment (where accessible means "close enough")</param>
        /// <param name="width">Vehicle width</param>
        /// <param name="length">Vehicle length</param>
        /// <param name="parkPos">Identified parking space position (only valid if method returns true)</param>
        /// <param name="parkRot">Identified parking space rotation (only valid if method returns true)</param>
        /// <param name="parkOffset">Identified parking space offset (only valid if method returns true and a segment id was given)</param>
        /// <returns></returns>
        bool VanillaFindParkingSpaceWithoutRestrictions(bool isElectric,
                                                         ushort homeId,
                                                         Vector3 refPos,
                                                         Vector3 searchDir,
                                                         ushort segment,
                                                         float width,
                                                         float length,
                                                         out Vector3 parkPos,
                                                         out Quaternion parkRot,
                                                         out float parkOffset);
    }
}
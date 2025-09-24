namespace TrafficManager.API.Traffic.Data {
    using System.Runtime.InteropServices;
    using TrafficManager.API.Traffic.Enums;

    [StructLayout(LayoutKind.Auto)]
    public struct ExtCitizenInstance {
        public ushort instanceId;

        /// <summary>
        /// Citizen path mode (used for Parking AI)
        /// </summary>
        public ExtPathMode pathMode;

        /// <summary>
        /// Number of times a formerly found parking space is already occupied after reaching its position
        /// </summary>
        public int failedParkingAttempts;

        /// <summary>
        /// Segment id / Building id where a parking space has been found
        /// </summary>
        public ushort parkingSpaceLocationId;

        /// <summary>
        /// Type of object (segment/building) where a parking space has been found
        /// </summary>
        public ExtParkingSpaceLocation parkingSpaceLocation;

        /// <summary>
        /// Path position that is used as a start position when parking fails
        /// </summary>
        public PathUnit.Position? parkingPathStartPosition;

        /// <summary>
        /// Walking path from (alternative) parking spot to target (only used to check if there is
        /// a valid walking path, not actually used at the moment)
        /// </summary>
        public uint returnPathId;

        /// <summary>
        /// State of the return path
        /// </summary>
        public ExtPathState returnPathState;

        /// <summary>
        /// Last known distance to the citizen's parked car
        /// </summary>
        public float lastDistanceToParkedCar;

        /// <summary>
        /// Specifies whether the last path-finding started at an outside connection
        /// </summary>
        public bool atOutsideConnection;

        public ExtCitizenInstance(ushort instanceId) {
            this.instanceId = instanceId;
            pathMode = ExtPathMode.None;
            failedParkingAttempts = 0;
            parkingSpaceLocationId = 0;
            parkingSpaceLocation = ExtParkingSpaceLocation.None;
            parkingPathStartPosition = null;
            returnPathId = 0;
            returnPathState = ExtPathState.None;
            lastDistanceToParkedCar = 0;
            atOutsideConnection = false;
        }

        public override string ToString() {
            return string.Format(
                "[ExtCitizenInstance\n\tinstanceId = {0}\n\tpathMode = {1}\n" +
                "\tfailedParkingAttempts = {2}\n\tparkingSpaceLocationId = {3}\n" +
                "\tparkingSpaceLocation = {4}\n\tparkingPathStartPosition = {5}\n" +
                "\treturnPathId = {6}\n\treturnPathState = {7}\n\tlastDistanceToParkedCar = {8}\n" +
                "\tatOutsideConnection = {9}\nExtCitizenInstance]",
                instanceId,
                pathMode,
                failedParkingAttempts,
                parkingSpaceLocationId,
                parkingSpaceLocation,
                parkingPathStartPosition,
                returnPathId,
                returnPathState,
                lastDistanceToParkedCar,
                atOutsideConnection);
        }

        /// <summary>
        /// Determines the path type through evaluating the current path mode.
        /// </summary>
        /// <returns></returns>
        public ExtPathType GetPathType() {
            switch (pathMode) {
                case ExtPathMode.CalculatingCarPathToAltParkPos:
                case ExtPathMode.CalculatingCarPathToKnownParkPos:
                case ExtPathMode.CalculatingCarPathToTarget:
                case ExtPathMode.DrivingToAltParkPos:
                case ExtPathMode.DrivingToKnownParkPos:
                case ExtPathMode.DrivingToTarget:
                case ExtPathMode.RequiresCarPath:
                case ExtPathMode.RequiresMixedCarPathToTarget:
                case ExtPathMode.ParkingFailed: {
                    return ExtPathType.DrivingOnly;
                }

                case ExtPathMode.CalculatingWalkingPathToParkedCar:
                case ExtPathMode.CalculatingWalkingPathToTarget:
                case ExtPathMode.RequiresWalkingPathToParkedCar:
                case ExtPathMode.RequiresWalkingPathToTarget:
                case ExtPathMode.ApproachingParkedCar:
                case ExtPathMode.WalkingToParkedCar:
                case ExtPathMode.WalkingToTarget: {
                    return ExtPathType.WalkingOnly;
                }

                default: {
                    return ExtPathType.None;
                }
            }
        }

        /// <summary>
        /// Converts an ExtPathState to a ExtSoftPathState.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static ExtSoftPathState ConvertPathStateToSoftPathState(ExtPathState state) {
            return (ExtSoftPathState)((byte)state);
        }
    }
}
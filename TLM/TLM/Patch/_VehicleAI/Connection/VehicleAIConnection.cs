namespace TrafficManager.Patch._VehicleAI.Connection {
    using System;
    using Manager.Connections;

    internal class VehicleAIConnection : IVehicleAIConnection {
        public VehicleAIConnection(CalculateTargetSpeedDelegate calculateTargetSpeedDelegate,
                                   PathfindFailureDelegate pathfindFailureDelegate,
                                   PathfindSuccessDelegate pathfindSuccessDelegate,
                                   InvalidPathDelegate invalidPathDelegate,
                                   ParkVehicleDelegate parkVehicleDelegate,
                                   NeedChangeVehicleTypeDelegate needChangeVehicleTypeDelegate,
                                   CalculateSegmentPositionDelegate calculateSegmentPosition,
                                   CalculateSegmentPositionDelegate2 calculateSegmentPosition2,
                                   ChangeVehicleTypeDelegate changeVehicleTypeDelegate,
                                   UpdateNodeTargetPosDelegate updateNodeTargetPosDelegate,
                                   ArrivingToDestinationDelegate arrivingToDestinationDelegate,
                                   LeftHandDriveDelegate leftHandDriveDelegate) {
            CalculateTargetSpeed = calculateTargetSpeedDelegate ?? throw new ArgumentNullException( nameof(calculateTargetSpeedDelegate));
            PathfindFailure = pathfindFailureDelegate ?? throw new ArgumentNullException(nameof(pathfindFailureDelegate));
            PathfindSuccess = pathfindSuccessDelegate ?? throw new ArgumentNullException(nameof(pathfindSuccessDelegate));
            InvalidPath = invalidPathDelegate ?? throw new ArgumentNullException(nameof(invalidPathDelegate));
            ParkVehicle = parkVehicleDelegate ?? throw new ArgumentNullException(nameof(parkVehicleDelegate));
            NeedChangeVehicleType = needChangeVehicleTypeDelegate ?? throw new ArgumentNullException(nameof(needChangeVehicleTypeDelegate));
            CalculateSegmentPosition = calculateSegmentPosition ?? throw new ArgumentNullException(nameof(calculateSegmentPosition));
            CalculateSegmentPosition2 = calculateSegmentPosition2 ?? throw new ArgumentNullException(nameof(calculateSegmentPosition2));
            ChangeVehicleType = changeVehicleTypeDelegate ?? throw new ArgumentNullException(nameof(changeVehicleTypeDelegate));
            UpdateNodeTargetPos = updateNodeTargetPosDelegate ?? throw new ArgumentNullException(nameof(updateNodeTargetPosDelegate));
            ArrivingToDestination = arrivingToDestinationDelegate ?? throw new ArgumentNullException(nameof(arrivingToDestinationDelegate));
            LeftHandDrive = leftHandDriveDelegate ?? throw new ArgumentNullException(nameof(leftHandDriveDelegate));
        }

        public CalculateTargetSpeedDelegate CalculateTargetSpeed { get; }
        public PathfindFailureDelegate PathfindFailure { get; }
        public PathfindSuccessDelegate PathfindSuccess { get; }
        public InvalidPathDelegate InvalidPath { get; }
        public ParkVehicleDelegate ParkVehicle { get; }
        public NeedChangeVehicleTypeDelegate NeedChangeVehicleType { get; }
        public CalculateSegmentPositionDelegate CalculateSegmentPosition { get; }
        public CalculateSegmentPositionDelegate2 CalculateSegmentPosition2 { get; }
        public ChangeVehicleTypeDelegate ChangeVehicleType { get; }
        public UpdateNodeTargetPosDelegate UpdateNodeTargetPos { get; }
        public ArrivingToDestinationDelegate ArrivingToDestination { get; }
        public LeftHandDriveDelegate LeftHandDrive { get; }
    }
}
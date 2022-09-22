namespace TrafficManager.Patch._VehicleAI.Connection {
    using System;
    using ColossalFramework.Math;
    using UnityEngine;

    public delegate float CalculateTargetSpeedDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle data, float speedLimit, float curve);
    public delegate float CalculateTargetSpeedByNetInfoDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle data, NetInfo info, uint lane, float curve);
    public delegate void PathfindFailureDelegate(CarAI carAI, ushort vehicleID, ref Vehicle data);
    public delegate void PathfindSuccessDelegate(CarAI carAI, ushort vehicleID, ref Vehicle data);
    public delegate void InvalidPathDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, ushort leaderID, ref Vehicle leaderData);
    public delegate bool ParkVehicleDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint nextPath, int nextPositionIndex, out byte segmentOffset);
    public delegate bool NeedChangeVehicleTypeDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID, VehicleInfo.VehicleType laneVehicleType, ref Vector4 target);
    public delegate bool ChangeVehicleTypeDelegate(VehicleAI vehicleAI,ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position pathPos, uint laneID);
    public delegate void CalculateSegmentPositionDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position position, uint laneID, byte offset, out Vector3 pos, out Vector3 dir, out float maxSpeed);
    public delegate void CalculateSegmentPositionDelegate2(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, PathUnit.Position nextPosition, PathUnit.Position position, uint laneID, byte offset, PathUnit.Position prevPos, uint prevLaneID, byte prevOffset, int index, out Vector3 pos, out Vector3 dir, out float maxSpeed);
    public delegate void UpdateNodeTargetPosDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, ushort nodeID, ref NetNode nodeData, ref Vector4 targetPos, int index);
    public delegate void ArrivingToDestinationDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData);
    public delegate bool LeftHandDriveDelegate(VehicleAI vehicleAI, NetInfo.Lane lane);
    public delegate bool NeedStopAtNodeDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle vehicleData, ushort nodeID, ref NetNode nodeData, PathUnit.Position previousPosition, uint prevLane, PathUnit.Position nextPosition, uint nextLane, Bezier3 bezier, out byte stopOffset);

    internal class VehicleAIConnection {
        public VehicleAIConnection(CalculateTargetSpeedDelegate calculateTargetSpeedDelegate,
                                   CalculateTargetSpeedByNetInfoDelegate calculateTargetSpeedByNetInfoDelegate,
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
                                   LeftHandDriveDelegate leftHandDriveDelegate,
                                   NeedStopAtNodeDelegate needStopAtNodeDelegate) {
            CalculateTargetSpeed = calculateTargetSpeedDelegate ?? throw new ArgumentNullException( nameof(calculateTargetSpeedDelegate));
            CalculateTargetSpeedByNetInfo = calculateTargetSpeedByNetInfoDelegate ?? throw new ArgumentNullException( nameof(calculateTargetSpeedByNetInfoDelegate));
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
            NeedStopAtNode = needStopAtNodeDelegate ?? throw new ArgumentNullException(nameof(needStopAtNodeDelegate));
        }

        public CalculateTargetSpeedDelegate CalculateTargetSpeed { get; }
        public CalculateTargetSpeedByNetInfoDelegate CalculateTargetSpeedByNetInfo { get; }
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
        public NeedStopAtNodeDelegate NeedStopAtNode { get; }
    }
}
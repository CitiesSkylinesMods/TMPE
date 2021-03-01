namespace TrafficManager.API.Manager.Connections {
    using UnityEngine;

    public delegate float CalculateTargetSpeedDelegate(VehicleAI vehicleAI, ushort vehicleID, ref Vehicle data, float speedLimit, float curve);
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

    public interface IVehicleAIConnection {
        CalculateTargetSpeedDelegate CalculateTargetSpeed { get; }
        PathfindFailureDelegate PathfindFailure { get; }
        PathfindSuccessDelegate PathfindSuccess { get; }
        InvalidPathDelegate InvalidPath { get; }
        ParkVehicleDelegate ParkVehicle { get; }
        NeedChangeVehicleTypeDelegate NeedChangeVehicleType { get; }
        CalculateSegmentPositionDelegate CalculateSegmentPosition { get; }
        CalculateSegmentPositionDelegate2 CalculateSegmentPosition2 { get; }
        ChangeVehicleTypeDelegate ChangeVehicleType { get; }
        UpdateNodeTargetPosDelegate UpdateNodeTargetPos { get; }
        ArrivingToDestinationDelegate ArrivingToDestination { get; }
        LeftHandDriveDelegate LeftHandDrive { get; }
    }
}
namespace TrafficManager.Manager.Connections {
    using ColossalFramework.Math;
    using UnityEngine;

    public delegate float CalculateTargetSpeedTrainDelegate(TrainAI trainAI, ushort vehicleID, ref Vehicle data, float speedLimit, float curve);
    public delegate void UpdatePathTargetPositionsDelegate(TrainAI trainAI, ushort vehicleID, ref Vehicle vehicleData, Vector3 refPos1, Vector3 refPos2, ushort leaderID, ref Vehicle leaderData, ref int index, int max1, int max2, float minSqrDistanceA, float minSqrDistanceB);

    public delegate int GetNoiseLevelDelegate(TrainAI trainAI);

    public delegate float GetMaxSpeedDelegate(ushort leaderID, ref Vehicle leaderData);

    public delegate float CalculateMaxSpeedDelegate(float targetDistance, float targetSpeed, float maxBraking);

    public delegate bool CheckOverlapDelegate(ushort vehicleID, ref Vehicle vehicleData, Segment3 segment, ushort ignoreVehicle);

    public delegate void ReverseDelegate(ushort leaderID, ref Vehicle leaderData);

    internal interface ITrainAIConnection {
        UpdatePathTargetPositionsDelegate UpdatePathTargetPositions { get; }
        GetNoiseLevelDelegate GetNoiseLevel { get; }
        GetMaxSpeedDelegate GetMaxSpeed { get; }
        CalculateMaxSpeedDelegate CalculateMaxSpeed { get; }
        ReverseDelegate Reverse { get; }
        CalculateTargetSpeedTrainDelegate CalculateTargetSpeed { get; }
        CheckOverlapDelegate CheckOverlap { get; }
    }
}
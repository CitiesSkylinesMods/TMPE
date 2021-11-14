namespace TrafficManager.Patch._VehicleAI._TrainAI.Connection {
    using System;
    using ColossalFramework.Math;
    using UnityEngine;

    public delegate float CalculateTargetSpeedTrainDelegate(TrainAI trainAI,
                                                            ushort vehicleID,
                                                            ref Vehicle data,
                                                            float speedLimit,
                                                            float curve);

    public delegate void UpdatePathTargetPositionsDelegate(TrainAI trainAI,
                                                           ushort vehicleID,
                                                           ref Vehicle vehicleData,
                                                           Vector3 refPos1,
                                                           Vector3 refPos2,
                                                           ushort leaderID,
                                                           ref Vehicle leaderData,
                                                           ref int index,
                                                           int max1,
                                                           int max2,
                                                           float minSqrDistanceA,
                                                           float minSqrDistanceB);
    public delegate int GetNoiseLevelDelegate(TrainAI trainAI);
    public delegate float GetMaxSpeedDelegate(ushort leaderID,
                                              ref Vehicle leaderData);
    public delegate float CalculateMaxSpeedDelegate(float targetDistance,
                                                    float targetSpeed,
                                                    float maxBraking);
    public delegate bool CheckOverlapDelegate(ushort vehicleID,
                                              ref Vehicle vehicleData,
                                              Segment3 segment,
                                              ushort ignoreVehicle);
    public delegate void ReverseDelegate(ushort leaderID,
                                         ref Vehicle leaderData);

    public delegate void ForceTrafficLightsDelegate(TrainAI trainAI,
                                        ushort vehicleID,
                                        ref Vehicle vehicleData,
                                        bool reserveSpace);

    internal class TrainAIConnection {
        internal TrainAIConnection(UpdatePathTargetPositionsDelegate updatePathTargetPositionsDelegate,
                                   GetNoiseLevelDelegate getNoiseLevelDelegate,
                                   GetMaxSpeedDelegate getMaxSpeedDelegate,
                                   CalculateMaxSpeedDelegate calculateMaxSpeedDelegate,
                                   ReverseDelegate reverseDelegate,
                                   CalculateTargetSpeedTrainDelegate calculateTargetSpeedDelegate,
                                   CheckOverlapDelegate checkOverlapDelegate,
                                   ForceTrafficLightsDelegate forceTrafficLightsDelegate) {
            UpdatePathTargetPositions = updatePathTargetPositionsDelegate ?? throw new ArgumentNullException(nameof(updatePathTargetPositionsDelegate));
            GetNoiseLevel = getNoiseLevelDelegate ?? throw new ArgumentNullException(nameof(getNoiseLevelDelegate));
            GetMaxSpeed = getMaxSpeedDelegate ?? throw new ArgumentNullException(nameof(getMaxSpeedDelegate));
            CalculateMaxSpeed = calculateMaxSpeedDelegate ?? throw new ArgumentNullException(nameof(calculateMaxSpeedDelegate));
            Reverse = reverseDelegate ?? throw new ArgumentNullException(nameof(reverseDelegate));
            CalculateTargetSpeed = calculateTargetSpeedDelegate ?? throw new ArgumentNullException( nameof(calculateTargetSpeedDelegate));
            CheckOverlap = checkOverlapDelegate ?? throw new ArgumentNullException(nameof(checkOverlapDelegate));
            ForceTrafficLights = forceTrafficLightsDelegate ?? throw new ArgumentNullException(nameof(forceTrafficLightsDelegate));
        }

        public UpdatePathTargetPositionsDelegate UpdatePathTargetPositions { get; }
        public GetNoiseLevelDelegate GetNoiseLevel { get; }
        public GetMaxSpeedDelegate GetMaxSpeed { get; }
        public CalculateMaxSpeedDelegate CalculateMaxSpeed { get; }
        public ReverseDelegate Reverse { get; }
        public CalculateTargetSpeedTrainDelegate CalculateTargetSpeed { get; }
        public CheckOverlapDelegate CheckOverlap { get; }
        public ForceTrafficLightsDelegate ForceTrafficLights { get; }
    }
}
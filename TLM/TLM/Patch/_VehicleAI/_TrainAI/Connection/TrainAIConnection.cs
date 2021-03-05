namespace TrafficManager.Patch._VehicleAI._TrainAI.Connection {
    using System;
    using API.Manager.Connections;

    internal class TrainAIConnection : ITrainAIConnection {
        internal TrainAIConnection(UpdatePathTargetPositionsDelegate updatePathTargetPositionsDelegate,
                                   GetNoiseLevelDelegate getNoiseLevelDelegate,
                                   GetMaxSpeedDelegate getMaxSpeedDelegate,
                                   CalculateMaxSpeedDelegate calculateMaxSpeedDelegate,
                                   ReverseDelegate reverseDelegate,
                                   CalculateTargetSpeedTrainDelegate calculateTargetSpeedDelegate,
                                   CheckOverlapDelegate checkOverlapDelegate) {
            UpdatePathTargetPositions = updatePathTargetPositionsDelegate ?? throw new ArgumentNullException(nameof(updatePathTargetPositionsDelegate));
            GetNoiseLevel = getNoiseLevelDelegate ?? throw new ArgumentNullException(nameof(getNoiseLevelDelegate));
            GetMaxSpeed = getMaxSpeedDelegate ?? throw new ArgumentNullException(nameof(getMaxSpeedDelegate));
            CalculateMaxSpeed = calculateMaxSpeedDelegate ?? throw new ArgumentNullException(nameof(calculateMaxSpeedDelegate));
            Reverse = reverseDelegate ?? throw new ArgumentNullException(nameof(reverseDelegate));
            CalculateTargetSpeed = calculateTargetSpeedDelegate ?? throw new ArgumentNullException( nameof(calculateTargetSpeedDelegate));
            CheckOverlap = checkOverlapDelegate ?? throw new ArgumentNullException(nameof(checkOverlapDelegate));
        }

        public UpdatePathTargetPositionsDelegate UpdatePathTargetPositions { get; }
        public GetNoiseLevelDelegate GetNoiseLevel { get; }
        public GetMaxSpeedDelegate GetMaxSpeed { get; }
        public CalculateMaxSpeedDelegate CalculateMaxSpeed { get; }
        public ReverseDelegate Reverse { get; }
        public CalculateTargetSpeedTrainDelegate CalculateTargetSpeed { get; }
        public CheckOverlapDelegate CheckOverlap { get; }
    }
}
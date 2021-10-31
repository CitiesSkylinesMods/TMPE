namespace TrafficManager.Patch._VehicleAI._TrainAI.Connection {
    using System;
    using CSUtil.Commons;
    using Util;

    public static class TrainAIHook {
        internal static TrainAIConnection GetConnection() {
            try {
                UpdatePathTargetPositionsDelegate updatePathTargetPositionsDelegate =
                    TranspilerUtil.CreateDelegate<UpdatePathTargetPositionsDelegate>(
                        typeof(TrainAI),
                        "UpdatePathTargetPositions",
                        true);
                GetNoiseLevelDelegate getNoiseLevelDelegate =
                    TranspilerUtil.CreateDelegate<GetNoiseLevelDelegate>(
                        typeof(TrainAI),
                        "GetNoiseLevel",
                        true);
                GetMaxSpeedDelegate getMaxSpeedDelegate =
                    TranspilerUtil.CreateDelegate<GetMaxSpeedDelegate>(
                        typeof(TrainAI),
                        "GetMaxSpeed",
                        false);
                CalculateMaxSpeedDelegate calculateMaxSpeedDelegate =
                    TranspilerUtil.CreateDelegate<CalculateMaxSpeedDelegate>(
                        typeof(TrainAI),
                        "CalculateMaxSpeed",
                        false);
                ReverseDelegate reverseDelegate =
                    TranspilerUtil.CreateDelegate<ReverseDelegate>(
                    typeof(TrainAI),
                    "Reverse",
                    false);
                CalculateTargetSpeedTrainDelegate calculateTargetSpeedDelegate =
                    TranspilerUtil.CreateDelegate<CalculateTargetSpeedTrainDelegate>(
                        typeof(TrainAI),
                        "CalculateTargetSpeed",
                        true);
                CheckOverlapDelegate checkOverlapDelegate =
                    TranspilerUtil.CreateDelegate<CheckOverlapDelegate>(
                        typeof(TrainAI),
                        "CheckOverlap",
                        false);
                ForceTrafficLightsDelegate forceTrafficLightsDelegate =
                    TranspilerUtil.CreateDelegate<ForceTrafficLightsDelegate>(
                        typeof(TrainAI),
                        "ForceTrafficLights",
                        true);

                return new TrainAIConnection(
                    updatePathTargetPositionsDelegate,
                    getNoiseLevelDelegate,
                    getMaxSpeedDelegate,
                    calculateMaxSpeedDelegate,
                    reverseDelegate,
                    calculateTargetSpeedDelegate,
                    checkOverlapDelegate,
                    forceTrafficLightsDelegate);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}
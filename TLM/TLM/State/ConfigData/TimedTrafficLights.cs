namespace TrafficManager.State.ConfigData {
    using TrafficManager.API.Traffic.Enums;

    public class TimedTrafficLights {
        /// <summary>
        /// TTL wait/flow calculation mode
        /// </summary>
        public FlowWaitCalcMode FlowWaitCalcMode = FlowWaitCalcMode.Mean;

        /// <summary>
        /// Default TTL flow-to-wait ratio
        /// </summary>
        public float FlowToWaitRatio = 0.8f;

        /// <summary>
        /// TTL smoothing factor for flowing/waiting vehicles
        /// </summary>
        public float SmoothingFactor = 0.1f;
    }
}
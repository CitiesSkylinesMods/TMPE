namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Enums;

    public interface ITrafficLightManager {
        /// <summary>
        /// returns if the node as any kind of traffic light (vanilla, manual, timed).
        /// </summary>
        public bool HasTrafficLight(ushort nodeId);

        /// <summary>
        /// Manual/timed traffic light cannot be toggled using <see cref="ITrafficLightManager"/>.
        /// Use <see cref="ITrafficLightSimulationManager"/> to do that.
        /// Also certain node types cannot have traffic light.
        /// </summary>
        bool CanToggleTrafficLight(ushort nodeId);

        /// <summary>
        /// if node has no traffic light, vanilla traffic light is set (if possible).
        /// if node has vanilla traffic light, it is removed (if possible).
        /// this method will fail if node has  Manual/timed traffic light.
        /// </summary>
        bool ToggleTrafficLight(ushort nodeId);
    }
}
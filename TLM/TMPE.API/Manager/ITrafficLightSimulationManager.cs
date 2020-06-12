namespace TrafficManager.API.Manager {
    using System.Collections.Generic;
    using TrafficManager.API.TrafficLight.Data;

    public interface ITrafficLightSimulationManager {
        // TODO documentation
        TrafficLightSimulation[] TrafficLightSimulations { get; }

        bool SetUpManualTrafficLight(ushort nodeId);
        bool SetUpTimedTrafficLight(ushort nodeId, IList<ushort> nodeGroup);
        bool HasActiveSimulation(ushort nodeId);
        bool HasActiveTimedSimulation(ushort nodeId);
        bool HasSimulation(ushort nodeId);
        bool HasManualSimulation(ushort nodeId);
        bool HasTimedSimulation(ushort nodeId);
        void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight);
        void SimulationStep();

        /// <summary>
        /// Retrieves the current traffic light state for the given segment end without checking if
        ///     any incoming vehicle/pedestrian traffic is present.
        /// </summary>
        /// <param name="nodeId">junction node id</param>
        /// <param name="fromSegmentId">source segment id</param>
        /// <param name="fromLaneIndex">source lane index</param>
        /// <param name="toSegmentId">target segment id</param>
        /// <param name="segmentData">source segment data</param>
        /// <param name="frame">simulation frame index</param>
        /// <param name="vehicleLightState">traffic light state for vehicles</param>
        /// <param name="pedestrianLightState">traffic light state for pedestrians</param>
        void GetTrafficLightState(
#if DEBUG
            ushort vehicleId,
            ref Vehicle vehicleData,
#endif
            ushort nodeId,
            ushort fromSegmentId,
            byte fromLaneIndex,
            ushort toSegmentId,
            ref NetSegment segmentData,
            uint frame,
            out RoadBaseAI.TrafficLightState vehicleLightState,
            out RoadBaseAI.TrafficLightState pedestrianLightState);

        /// <summary>
        /// Retrieves the current traffic light state for the given segment end and checks if any
        ///     incoming vehicle/pedestrian traffic is present.
        /// </summary>
        /// <param name="nodeId">junction node id</param>
        /// <param name="fromSegmentId">source segment id</param>
        /// <param name="fromLaneIndex">source lane index</param>
        /// <param name="toSegmentId">target segment id</param>
        /// <param name="segmentData">source segment data</param>
        /// <param name="frame">simulation frame index</param>
        /// <param name="vehicleLightState">traffic light state for vehicles</param>
        /// <param name="pedestrianLightState">traffic light state for pedestrians</param>
        /// <param name="vehicles"><code>true</code> if incoming vehicle traffic is detected,
        ///     <code>false otherwise</code>. Note that this only yield correct results for vanilla
        ///     junctions.</param>
        /// <param name="pedestrians"><code>true</code> if incoming pedetrian traffic is detected,
        ///     <code>false otherwise</code>. Note that this only yield correct results for vanilla
        ///     junctions.</param>
        void GetTrafficLightState(
#if DEBUG
            ushort vehicleId,
            ref Vehicle vehicleData,
#endif
            ushort nodeId,
            ushort fromSegmentId,
            byte fromLaneIndex,
            ushort toSegmentId,
            ref NetSegment segmentData,
            uint frame,
            out RoadBaseAI.TrafficLightState vehicleLightState,
            out RoadBaseAI.TrafficLightState pedestrianLightState,
            out bool vehicles,
            out bool pedestrians);

        /// <summary>
        /// Sets the visual traffic light state at the given segment end.
        /// </summary>
        /// <param name="nodeId">junction node id</param>
        /// <param name="segmentData">segment data</param>
        /// <param name="frame">simulation frame index</param>
        /// <param name="vehicleLightState">traffic light state for vehicles</param>
        /// <param name="pedestrianLightState">traffic light state for pedestrians</param>
        /// <param name="vehicles">has incoming vehicle traffic?</param>
        /// <param name="pedestrians">has incoming pedetrian traffic?</param>
        void SetVisualState(ushort nodeId,
                            ref NetSegment segmentData,
                            uint frame,
                            RoadBaseAI.TrafficLightState vehicleLightState,
                            RoadBaseAI.TrafficLightState pedestrianLightState,
                            bool vehicles,
                            bool pedestrians);
    }
}
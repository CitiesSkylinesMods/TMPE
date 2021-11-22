namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;

    public interface IExtNodeManager {
        /// <summary>
        /// Extended node data
        /// </summary>
        ExtNode[] ExtNodes { get; }

        /// <summary>
        /// Adds the segment to the given node
        /// </summary>
        /// <param name="nodeId">node id</param>
        /// <param name="segmentId">segment id</param>
        void AddSegment(ushort nodeId, ushort segmentId);

        /// <summary>
        /// Removes the segment from the given node
        /// </summary>
        /// <param name="nodeId">node id</param>
        /// <param name="segmentId">segment id</param>
        void RemoveSegment(ushort nodeId, ushort segmentId);
    }
}
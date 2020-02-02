namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;

    public interface ITurnOnRedManager {
        TurnOnRedSegments[] TurnOnRedSegments { get; }

        /// <summary>
        /// Retrieves the array index for the given segment end id.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">start node</param>
        /// <returns>array index for the segment end id</returns>
        int GetIndex(ushort segmentId, bool startNode);
    }
}
namespace TrafficManager.API.Manager {
    public interface ILaneConnectionManager {
        // TODO define me!

        /// <summary>
        /// Determines whether u-turn connections exist for the given segment end.
        /// </summary>
        /// <param name="segmentId">segment id</param>
        /// <param name="startNode">at start node?</param>
        /// <returns><code>true</code> if u-turn connections exist, <code>false</code> otherwise</returns>
        bool HasUturnConnections(ushort segmentId, bool startNode);
    }
}
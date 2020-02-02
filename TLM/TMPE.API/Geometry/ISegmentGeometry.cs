namespace TrafficManager.Geometry {
    using JetBrains.Annotations;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Traffic.Enums;

    // Not used
    [UsedImplicitly]
    public interface ISegmentGeometry {
        /// <summary>
        /// Holds the id of the managed segment.
        /// </summary>
        ushort SegmentId { get; }

        /// <summary>
        /// Holds the start node of the managed segment.
        /// </summary>
        ushort StartNodeId { get; }

        /// <summary>
        /// Holds the end node of the managed segment.
        /// </summary>
        ushort EndNodeId { get; }

        /// <summary>
        /// Holds the segment end geometry at the start node
        /// </summary>
        ISegmentEndGeometry StartNodeGeometry { get; }

        /// <summary>
        /// Holds the segment end geometry at the end node
        /// </summary>
        ISegmentEndGeometry EndNodeGeometry { get; }

        /// <summary>
        /// Determines whether the segment is a one-way segment.
        /// </summary>
        bool OneWay { get; }

        /// <summary>
        /// Determines whether the segment is a highway.
        /// </summary>
        bool Highway { get; }

        /// <summary>
        /// Determines whether the segment has a bus lane.
        /// </summary>
        bool BusLane { get; }

        /// <summary>
        /// Determines whether the segment is valid.
        /// </summary>
        bool Valid { get; }

        /// <summary>
        /// Requests recalculation of the managed segment's geometry data.
        /// </summary>
        void Recalculate(GeometryCalculationMode calcMode);

        /// <summary>
        /// Determines the segment end geometry for the given node.
        /// </summary>
        /// <param name="startNode">if <code>true</code> the segment end geometry at the start node is returned, else the segment end geometry at the end node is returned.</param>
        /// <returns>Segment end geometry or <code>null</code> if the segment end is invalid.</returns>
        ISegmentEndGeometry GetEnd(bool startNode);

        /// <summary>
        /// Determines the segment end geometry for the given node.
        /// </summary>
        /// <param name="nodeId">node id</param>
        /// <returns>Segment end geometry or <code>null</code> if the node id or segment end is invalid.</returns>
        ISegmentEndGeometry GetEnd(ushort nodeId);
    }
}
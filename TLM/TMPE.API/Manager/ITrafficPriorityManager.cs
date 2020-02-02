namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;

    public interface ITrafficPriorityManager {
        // TODO define me!

        /// <summary>
        /// Checks if a vehicle (the target vehicle) has to wait for other incoming vehicles at a
        ///     junction with priority signs.
        /// </summary>
        /// <param name="vehicleId">target vehicle</param>
        /// <param name="vehicle">target vehicle data</param>
        /// <param name="curPos">current path position</param>
        /// <param name="curEnd">current segment end</param>
        /// <param name="transitNodeId">transit node</param>
        /// <param name="startNode">true if the transit node is the start node of the current segment</param>
        /// <param name="nextPos">next path position</param>
        /// <param name="transitNode">transit node data</param>
        /// <returns>false if the target vehicle must wait for other vehicles, true otherwise</returns>
        bool HasPriority(ushort vehicleId,
                         ref Vehicle vehicle,
                         ref PathUnit.Position curPos,
                         ref ExtSegmentEnd curEnd,
                         ushort transitNodeId,
                         bool startNode,
                         ref PathUnit.Position nextPos,
                         ref NetNode transitNode);

        PriorityType GetPrioritySign(ushort segmentId, bool startNode);

        bool HasNodePrioritySign(ushort nodeId);
    }
}
namespace TrafficManager.API.Manager {
    using TrafficManager.API.Traffic.Data;
    using UnityEngine;

    public interface IExtBuildingManager {
        // TODO define me!

        /// <summary>
        /// Extended building data
        /// </summary>
        ExtBuilding[] ExtBuildings { get; }

        /// <summary>
        /// Handles a building before a simulation step is performed.
        /// </summary>
        /// <param name="buildingId">building id</param>
        /// <param name="data">building data</param>
        void OnBeforeSimulationStep(ushort buildingId, ref Building data);

        /// <summary>
        /// Resets the given building.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        void Reset(ref ExtBuilding extBuilding);

        /// <summary>
        /// Adds <paramref name="delta"/> units of parking space demand to the given building.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        /// <param name="delta">demand to add</param>
        void AddParkingSpaceDemand(ref ExtBuilding extBuilding, uint delta);

        /// <summary>
        /// Removes <paramref name="delta"/> units of parking space demand from the given building.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        /// <param name="delta">demand to remove</param>
        void RemoveParkingSpaceDemand(ref ExtBuilding extBuilding, uint delta);

        /// <summary>
        /// Adds or removes parking space demand from the given building. Demand is linearly
        ///     interpolated between  <paramref name="minDelta"/> and <paramref name="maxDelta"/>
        ///     according to the distance present between the building and the given parking
        ///     position <paramref name="parkPos"/>.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        /// <param name="parkPos">parking position</param>
        /// <param name="minDelta">minimum demand to add</param>
        /// <param name="minDelta">maximum demand to add</param>
        void ModifyParkingSpaceDemand(ref ExtBuilding extBuilding,
                                      Vector3 parkPos,
                                      int minDelta = -10,
                                      int maxDelta = 10);

        /// <summary>
        /// Adds <paramref name="delta"/> units of public transport demand to the given building.
        ///     Depending on the flag <paramref name="outgoing"/>, either ougoing or incoming demand
        ///     values are updated.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        /// <param name="delta">demand to add</param>
        /// <param name="outgoing">if <code>true</code>, demand is counted as outgoing, otherwise
        ///     demand is counted as incoming</param>
        void AddPublicTransportDemand(ref ExtBuilding extBuilding, uint delta, bool outgoing);

        /// <summary>
        /// Removes <paramref name="delta"/> units of public transport demand from the given building.
        ///     Depending on the flag <paramref name="outgoing"/>, either ougoing or incoming demand
        ///     values are updated.
        /// </summary>
        /// <param name="extBuilding">ext. building</param>
        /// <param name="delta">demand to remove</param>
        /// <param name="outgoing">if <code>true</code>, demand is counted as outgoing, otherwise
        ///     demand is counted as incoming</param>
        void RemovePublicTransportDemand(ref ExtBuilding extBuilding, uint delta, bool outgoing);
    }
}
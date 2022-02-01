namespace TrafficManager.Custom.PathFinding {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;

    /// <summary>
    /// When a bug is fixed in pathfinder, update this class:
    ///
    /// 1. Increment LatestEdition number below
    /// 2. Define all ExtVehicleType that need despawning for _that_ edition
    /// </summary>
    public static class PathfinderUpdates {

        // Edition History:
        // 0 - An old save, unknown pathfinder edition
        // 1 - #1338 Aircraft pathfinding fix

        /// <summary>
        /// Update this each time a despawn-requiring change is
        /// made to <see cref="TrafficManager.Custom.PathFinding"/>
        /// or anything else path-related which merits targeted
        /// vehicle despawning.
        /// </summary>
        [SuppressMessage("Usage", "RAS0002:Readonly field for a non-readonly struct", Justification = "Not performance critical.")]
        private static readonly byte LatestPathfinderEdition = 1;

        /// <summary>
        /// Checks savegame pathfinder edition and, if necessary, despawns
        /// any vehicles that might have invalid paths due to bugs in the
        /// earlier pathfinder edition used when the save was created.
        /// </summary>
        /// <returns>
        /// Returns the <see cref="ExtVehicleType"/> of vehicles that were despawned.
        /// </returns>
        public static ExtVehicleType DespawnVehiclesIfNecessary() {
            var filter = ExtVehicleType.None;

            if (Options.SavegamePathfinderEdition == LatestPathfinderEdition) {
                return filter; // nothing to do, everything is fine
            }

            Log.Info($"Pathfinder update from {Options.SavegamePathfinderEdition} to {LatestPathfinderEdition}.");

            if (Options.SavegamePathfinderEdition < 1) {
                filter |= ExtVehicleType.Plane; // #1338
            }

            // this will also log what gets despawned
            Singleton<SimulationManager>.instance.AddAction(() => {
                UtilityManager.Instance.DespawnVehicles(filter);
            });

            // this will be stored in savegame
            Options.SavegamePathfinderEdition = LatestPathfinderEdition;

            return filter;
        }
    }
}

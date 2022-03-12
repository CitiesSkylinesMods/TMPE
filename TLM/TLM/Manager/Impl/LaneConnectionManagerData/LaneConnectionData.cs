namespace TrafficManager.Manager.Impl.LaneConnectionManagerData {
    internal struct LaneConnectionData {
        public uint LaneId;

        /// <summary>
        /// for every connection, both forward and backward connection pairs are created.
        /// for bi-directional connection both forward and backward are enabled.
        /// for uni-directional connection only forward connection is enabled.
        /// if there is no connection either way, then there must be no LaneConnectionData entry.
        /// </summary>
        public bool Enabled;

        // TODO [issue 354]: add track/car connection type.

        public LaneConnectionData(uint laneId, bool enabled) {
            LaneId = laneId;
            Enabled = enabled;
        }

        public override string ToString() => $"LaneConnectionData({LaneId} ,{Enabled})";
    }
}

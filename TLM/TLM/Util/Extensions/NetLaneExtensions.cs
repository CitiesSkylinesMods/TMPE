namespace TrafficManager.Util.Extensions {
    public static class NetLaneExtensions {
        /// <summary>
        /// Checks if the netLane is Created, but not Deleted.
        /// </summary>
        /// <param name="netLane">netLane</param>
        /// <returns>True if the netLane is valid, otherwise false.</returns>
        public static bool IsValid(this ref NetLane netLane) =>
            ((NetLane.Flags)netLane.m_flags).CheckFlags(
                required: NetLane.Flags.Created,
                forbidden: NetLane.Flags.Deleted);
    }
}

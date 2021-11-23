namespace TrafficManager.Util.Extensions {
    public static class CitizenExtensions {
        /// <summary>
        /// Checks if the citizen is Created, but not Deleted.
        /// </summary>
        /// <param name="citizen">citizen</param>
        /// <returns>True if the citizen is valid, otherwise false.</returns>
        public static bool IsValid(this ref Citizen citizen) =>
            citizen.m_flags.IsFlagSet(Citizen.Flags.Created);
    }
}

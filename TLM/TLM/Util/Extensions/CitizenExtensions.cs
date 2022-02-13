namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    
    public static class CitizenExtensions {
        private static Citizen[] _citizenBuffer = Singleton<CitizenManager>.instance.m_citizens.m_buffer;

        internal static ref Citizen ToCitizen(this uint citizenId) => ref _citizenBuffer[citizenId];

        /// <summary>
        /// Checks if the citizen is Created, but not Deleted.
        /// </summary>
        /// <param name="citizen">citizen</param>
        /// <returns>True if the citizen is valid, otherwise false.</returns>
        public static bool IsValid(this ref Citizen citizen) =>
            citizen.m_flags.IsFlagSet(Citizen.Flags.Created);
    }
}

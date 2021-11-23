namespace TrafficManager.Util.Extensions {
    public static class CitizenInstanceExtensions {
        /// <summary>
        /// Checks if the citizenInstance is Created, but not Deleted.
        /// </summary>
        /// <param name="citizenInstance">citizenInstance</param>
        /// <returns>True if the citizenInstance is valid, otherwise false.</returns>
        public static bool IsValid(this ref CitizenInstance citizenInstance) =>
            citizenInstance.m_flags.CheckFlags(
                required: CitizenInstance.Flags.Created,
                forbidden: CitizenInstance.Flags.Deleted);
    }
}

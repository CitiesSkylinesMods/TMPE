namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    public static class CitizenInstanceExtensions {
        private static CitizenInstance[] _citizenInstanceBuffer = Singleton<CitizenManager>.instance.m_instances.m_buffer;

        internal static ref CitizenInstance ToCitizenInstance(this ushort citizenInstance) => ref _citizenInstanceBuffer[citizenInstance];
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

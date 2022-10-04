namespace TrafficManager.Util.Extensions {
    using ColossalFramework;
    public static class CitizenInstanceExtensions {
        public static bool IsCharacter(this ref CitizenInstance citizenInstance) =>
            citizenInstance.m_flags.IsFlagSet(CitizenInstance.Flags.Character);

        public static bool IsCreated(this ref CitizenInstance citizenInstance) =>
            citizenInstance.m_flags.IsFlagSet(CitizenInstance.Flags.Created);

        public static bool IsWaitingPath(this ref CitizenInstance citizenInstance) =>
            citizenInstance.m_flags.IsFlagSet(CitizenInstance.Flags.WaitingPath);

        public static bool TargetIsNode(this ref CitizenInstance citizenInstance) =>
            citizenInstance.m_flags.IsFlagSet(CitizenInstance.Flags.TargetIsNode);

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

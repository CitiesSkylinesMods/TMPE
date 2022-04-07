namespace TrafficManager.API.Traffic.Enums {
    public enum OverlayCulling : byte {
        None = 0,

        /// <summary>
        /// Overlay display range is based on camera position.
        /// </summary>
        /// <remarks>Standard for TM:PE toolbar.</remarks>
        Camera = 1 << 0,

        /// <summary>
        /// Overlay display range based on mouse pointer position,
        /// but constrained to the camera viewport.
        /// </summary>
        /// <remarks>Useful for bulldozer tool, etc.</remarks>
        Mouse = 1 << 1,
    }
}

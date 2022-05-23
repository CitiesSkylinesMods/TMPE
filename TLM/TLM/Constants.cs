namespace TrafficManager {
    using TrafficManager.API.Manager;
    using TrafficManager.API.Notifier;
    using TrafficManager.API.UI;
    using TrafficManager.U;
    using TrafficManager.UI.Textures;

    public static class Constants {
        /// <summary>
        /// Used where a 0..1f value has to be scaled to byte or a byte to 0..1f
        /// </summary>
        public const float BYTE_TO_FLOAT_SCALE = 1f / 255f;

        /// <summary>
        /// Screen pixel size for overlay signs, such as one-per-segment speed limits.
        /// </summary>
        public static float OverlaySignVisibleSize => 100.0f * UIScaler.UIScale;

        /// <summary>
        /// World size for clickable signs used in overlays. Larger than readonly signs.
        /// This is used as offset in grids of signs such as lane speed limit signs.
        /// </summary>
        public const float OVERLAY_INTERACTIVE_SIGN_SIZE = 6.0f;

        /// <summary>
        /// World size for readonly signs used in overlays.
        /// This is used as offset in grids of signs such as lane speed limit signs.
        /// </summary>
        public const float OVERLAY_READONLY_SIGN_SIZE = 3.8f;

        /// <summary>The maximum amount of segments a node can hold.</summary>
        public const int MAX_SEGMENTS_OF_NODE = 8;

        public static float ByteToFloat(byte b) {
            return b * BYTE_TO_FLOAT_SCALE;
        }

        public static IManagerFactory ManagerFactory => Manager.Impl.ManagerFactory.Instance;

        public static IUIFactory UIFactory => UI.UIFactory.Instance;

        public static INotifier Notifier => TrafficManager.Notifier.Instance;

    }
}

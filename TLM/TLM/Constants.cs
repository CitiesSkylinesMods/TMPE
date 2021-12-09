namespace TrafficManager {
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using UnityEngine;
    using TrafficManager.API.Notifier;
    using TrafficManager.U;

    public static class Constants {
        /// <summary>
        /// Used where a 0..1f value has to be scaled to byte or a byte to 0..1f
        /// </summary>
        public const float BYTE_TO_FLOAT_SCALE = 1f / 255f;

        /// <summary>
        /// Conversion rate from km/h to game speed (also exists in TrafficManager.API.Constants)
        /// </summary>
        public const float SPEED_TO_KMPH = 50.0f; // 1.0f equals 50 km/h

        /// <summary>
        /// Conversion rate from MPH to game speed (also exists in TrafficManager.API.Constants)
        /// </summary>
        [UsedImplicitly]
        public const float SPEED_TO_MPH = 31.06f; // 50 km/h converted to mph

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

        public static INotifier Notifier => TrafficManager.Notifier.Instance;
    }
}

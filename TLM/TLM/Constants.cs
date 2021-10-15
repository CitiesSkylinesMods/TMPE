namespace TrafficManager {
    using GenericGameBridge.Factory;
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using UnityEngine;
    using TrafficManager.API.Notifier;

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
        public const float SPEED_TO_MPH = 32.06f; // 50 km/h converted to mph

        /// <summary>
        /// The maximum amount of segments a node can hold.
        /// </summary>
        public const int MAX_SEGMENTS_OF_NODE = 8;

        public static float ByteToFloat(byte b) {
            return b * BYTE_TO_FLOAT_SCALE;
        }

        public static IServiceFactory ServiceFactory {
            get {
#if UNITTEST
                return TestGameBridge.Factory.ServiceFactory.Instance;
#else
                return CitiesGameBridge.Factory.ServiceFactory.Instance;
#endif
            }
        }

        public static IManagerFactory ManagerFactory => Manager.Impl.ManagerFactory.Instance;

        public static INotifier Notifier => TrafficManager.Notifier.Instance;
    }
}
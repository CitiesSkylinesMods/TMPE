namespace TrafficManager {
    using GenericGameBridge.Factory;
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using UnityEngine;

    public static class Constants {
        /// <summary>
        /// Used where a 0..1f value has to be scaled to byte or a byte to 0..1f
        /// </summary>
        public const float BYTE_TO_FLOAT_SCALE = 1f / 255f;

        /// <summary>
        /// Array of boolean to allow creating foreach loops
        /// </summary>
        public static readonly bool[] ALL_BOOL = { false, true };

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

        /// <summary>Size for clickable signs used in overlays. Larger than readonly signs.</summary>
        public const float OVERLAY_INTERACTIVE_SIGN_SIZE = 6.0f;

        /// <summary>Size for readonly signs used in overlays.</summary>
        public const float OVERLAY_READONLY_SIGN_SIZE = 3.8f;
    }
}
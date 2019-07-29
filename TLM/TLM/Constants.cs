namespace TrafficManager {
    using API.Manager;
    using GenericGameBridge.Factory;

    public static class Constants {
        /// <summary>
        /// A very small value for float comparisons to zero
        /// </summary>
        public const float VERY_SMALL_FLOAT = 1e-12f;

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
    }
}
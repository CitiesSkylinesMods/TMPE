namespace TrafficManager.Util {
    using UnityEngine;

    public class InGameUtil {

        public static InGameUtil Instance { get; private set; }

        /// <summary>
        /// Use only in game, make sure that Instantiate() was called .
        /// </summary>
        public readonly Camera CachedMainCamera;

        public readonly Transform CachedCameraTransform;

        private InGameUtil() {
            CachedMainCamera = Camera.main;
            CachedCameraTransform = CachedMainCamera.transform;
        }

        /// <summary>
        /// Call after loading savegame to cache main camera reference
        /// </summary>
        public static void Instantiate() {
            Instance = new InGameUtil();
        }
    }
}
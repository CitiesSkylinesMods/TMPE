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

        /// <summary>Check whether the position is below the ground level.</summary>
        /// <param name="position">Point in the world.</param>
        /// <returns>True if the position is below the ground level</returns>
        public static bool CheckIsUnderground(Vector3 position) {
            float maxY = position.y;
            float sampledHeight = TerrainManager.instance.SampleDetailHeightSmooth(position);
            return sampledHeight > maxY;
        }
    }
}
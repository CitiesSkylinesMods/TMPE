namespace TrafficManager.UI.New {
    using UnityEngine;

    /// <summary>
    /// Assists with screen scaling issues. Given sizes for 1920x1080 and then scaling
    /// accordingly to the current game resolution up or down.
    /// </summary>
    public class ScreenScaling {
        private readonly float verticalRatio_;

        public ScreenScaling() {
            verticalRatio_ = Screen.height / 1080f;
        }

        /// <summary>
        /// Given a vector tuned for good position or size in 1080p, convert it to a scaled vector
        /// in the current resolution.
        /// </summary>
        /// <param name="for1080P">vector in 1080p</param>
        /// <returns>vector scaled to current screen height</returns>
        public Vector2 Scale(Vector2 for1080P) {
            return for1080P * verticalRatio_;
        }

        /// <summary>
        /// Given a float tuned for good position or size in 1080p, convert it to a scaled float
        /// in the current resolution.
        /// </summary>
        /// <param name="for1080P">float in 1080p</param>
        /// <returns>float scaled to current screen height</returns>
        public float Scale(float for1080P) {
            return for1080P * verticalRatio_;
        }
    }
}
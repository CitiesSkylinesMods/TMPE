namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    /// <summary>
    /// Code of UIScaler from ModTools by Kian Zarrin
    /// https://github.com/kianzarrin/Skylines-ModTools/blob/master/Debugger/UI/UIScaler.cs
    /// </summary>
    public static class UIScaler {
        public static bool TryGetScreenResolution(out Vector2 resolution) {
            UIView uIView = UIView.GetAView();
            if (uIView) {
                resolution = uIView.GetScreenResolution();
                return true;
            }

            resolution = default;
            return false;
        }

        private static float BaseResolutionX {
            get {
                if (TryGetScreenResolution(out Vector2 resolution)) {
                    // 1920f if aspect ratio is 16:9;
                    return resolution.x;
                }

                return 1080f * AspectRatio;
            }
        }

        private static float BaseResolutionY {
            get {
                if (TryGetScreenResolution(out Vector2 resolution)) {
                    // always 1080f. But we keep this code for the sake of future proofing
                    return resolution.y;
                }

                return 1080f;
            }
        }

        public static float AspectRatio => Screen.width / (float)Screen.height;

        /// <summary>Shortcut to reach global main config containing GuiScale.</summary>
        private static State.ConfigData.Main Config => GlobalConfig.Instance.Main;

        public static float MaxWidth {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolutionX : Screen.width;
                return ret / UIScale;
            }
        }

        public static float MaxHeight {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolutionY : Screen.height;
                return ret / UIScale;
            }
        }

        public static float UIAspectScale {
            get {
                var horizontalScale = Screen.width / MaxWidth;
                var verticalScale = Screen.height / MaxHeight;
                return Mathf.Min(horizontalScale, verticalScale);
            }
        }

        public static float UIScale => GlobalConfig.Instance.Main.GuiScale * 0.01f;

        public static Matrix4x4 ScaleMatrix => Matrix4x4.Scale(Vector3.one * UIAspectScale);

        public static Vector2 MousePosition {
            get {
                var mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                return mouse / UIScaler.UIAspectScale;
            }
        }

        /// <summary>
        /// Given a position on screen (unit: pixels) convert to GUI position (always 1920x1080).
        /// </summary>
        /// <param name="screenPos">Pixel position.</param>
        /// <returns>GUI space position.</returns>
        internal static Vector2 ScreenPointToGuiPoint(Vector2 screenPos) {
            // TODO: Optimize, this is frequently called
            return new(
                x: screenPos.x * BaseResolutionX / Screen.width,
                y: screenPos.y * BaseResolutionY / Screen.height);
        }
    }
}
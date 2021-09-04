namespace TrafficManager.U {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using UnityEngine;

    /// <summary>
    /// Code of UIScaler from ModTools by Kian Zarrin
    /// https://github.com/kianzarrin/Skylines-ModTools/blob/master/Debugger/UI/UIScaler.cs
    /// </summary>
    public static class UIScaler {
        private static float BaseResolutionX => UIView.GetAView().GetScreenResolution().x;

        private static float BaseResolutionY => UIView.GetAView().GetScreenResolution().y;

        public static float AspectRatio => Screen.width / (float)Screen.height;

        /// <summary>Shortcut to reach global main config containing GuiScale.</summary>
        private static State.ConfigData.Main Config => GlobalConfig.Instance.Main;

        /// <summary>
        /// Maximum projected width of GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
        public static float MaxWidth {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolutionX : Screen.width;
                return ret / (Config.GuiScalePercent * 0.01f);
            }
        }

        /// <summary>
        /// Maximum projected height of GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
        public static float MaxHeight {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolutionY : Screen.height;
                return ret / (Config.GuiScalePercent * 0.01f);
            }
        }

        public static float UIAspectScale {
            get {
                var horizontalScale = Screen.width / MaxWidth;
                var verticalScale = Screen.height / MaxHeight;
                return Mathf.Min(horizontalScale, verticalScale);
            }
        }

        public static float UIScale => GlobalConfig.Instance.Main.GuiScalePercent * 0.01f;

        public static Matrix4x4 ScaleMatrix => Matrix4x4.Scale(Vector3.one * UIAspectScale);

        /// <summary>
        /// Mouse position in GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
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
            return new(
                x: screenPos.x * BaseResolutionX / Screen.width,
                y: screenPos.y * BaseResolutionY / Screen.height);
        }
    }
}
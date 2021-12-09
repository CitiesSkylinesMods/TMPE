namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Code of UIScaler from ModTools by Kian Zarrin
    /// https://github.com/kianzarrin/Skylines-ModTools/blob/master/Debugger/UI/UIScaler.cs
    /// </summary>
    public static class UIScaler {
        private static State.ConfigData.Main Config => GlobalConfig.Instance.Main;

        internal static Vector2 BaseResolution { get; private set; }

        // internal static float AspectRatio => Screen.width / (float)Screen.height;

        /// <summary>Shortcut to reach global main config containing GuiScale.</summary>

        /// <summary>
        /// Maximum projected width of GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
        internal static float MaxWidth {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolution.x : Screen.width;
                return ret / UIScale;
            }
        }

        /// <summary>
        /// Maximum projected height of GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
        internal static float MaxHeight {
            get {
                float ret = Config.GuiScaleToResolution ? BaseResolution.y : Screen.height;
                return ret / UIScale;
            }
        }

        private static float UIAspectScale() {
            var horizontalScale = Screen.width / MaxWidth;
            var verticalScale = Screen.height / MaxHeight;
            return Mathf.Min(horizontalScale, verticalScale);
        }

        internal static float UIScale => Config.GuiScale * 0.01f;

        internal static Matrix4x4 ScaleMatrix => Matrix4x4.Scale(Vector3.one * UIAspectScale());

        /// <summary>
        /// Mouse position in GUI space when GUI.matrix = ScaleMatrix
        /// </summary>
        internal static Vector2 MousePosition {
            get {
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                return mouse * UIScale / UIAspectScale();
            }
        }

        /// <summary>
        /// Given a position on screen (unit: pixels) convert to GUI position (always 1920x1080).
        /// </summary>
        /// <param name="screenPos">Pixel position.</param>
        /// <returns>GUI space position.</returns>
        internal static Vector2 ScreenPointToGuiPoint(Vector2 screenPos) {
            return new(
                x: screenPos.x * Screen.width / BaseResolution.x,
                y: screenPos.y * Screen.height / BaseResolution.y);
        }

        internal static void Reset() {
            try {
                BaseResolution = UIView.GetAView().GetScreenResolution();
            } catch (Exception ex) {
                ex.LogException();
            }
        }
    }
}
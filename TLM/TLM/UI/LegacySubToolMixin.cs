namespace TrafficManager.UI {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.U;
    using UnityEngine;

    [Obsolete("Used in legacy GUI for migration to new subtool base clas")]
    public class LegacySubToolMixin {
        [Obsolete("Used in legacy GUI")]
        internal static GUILayoutOption[] LegacyEmptyOptionsArray = new GUILayoutOption[0];

        [Obsolete("Used in legacy GUI")]
        protected void LegacyDragWindow(ref Rect window) {
            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            window.x = Mathf.Clamp(window.x, 0, UIScaler.MaxWidth - window.width);
            window.y = Mathf.Clamp(window.y, 0, UIScaler.MaxHeight - window.height);

            bool primaryMouseDown = Input.GetMouseButton(0);
            if (primaryMouseDown) {
                GUI.DragWindow();
            }
        }

        [Obsolete("Used in legacy GUI")]
        private Texture2D LegacyWindowTexture {
            get {
                if (legacyWindowTexture_ == null) {
                    legacyWindowTexture_ = TextureUtil.AdjustAlpha(
                        Textures.MainMenu.WindowBackground,
                        TrafficManagerTool.GetWindowAlpha());
                }

                return legacyWindowTexture_;
            }
        }

        [Obsolete("Used in legacy GUI")]
        private Texture2D legacyWindowTexture_;

        [Obsolete("Used in legacy GUI")]
        private GUIStyle legacyWindowStyle_;

        [Obsolete("Used in legacy GUI")]
        internal GUIStyle LegacyWindowStyle =>
            // ReSharper disable once ConvertToNullCoalescingCompoundAssignment
            legacyWindowStyle_ ??
            (legacyWindowStyle_ = new GUIStyle {
                    normal = {
                        background = LegacyWindowTexture,
                        textColor = Color.white,
                    },
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 20,
                    border = {
                        left = 4,
                        top = 41,
                        right = 4,
                        bottom = 8,
                    },
                    overflow = {
                        bottom = 0,
                        top = 0,
                        right = 12,
                        left = 12,
                    },
                    contentOffset = new Vector2(0, -44),
                    padding = {
                        top = 55,
                    },
                });
    }
}
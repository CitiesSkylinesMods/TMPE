namespace TrafficManager.U {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.Util;
    using UnityEngine;

    public static class ButtonTexture {
        private const string MENU_BUTTON_BACKGROUND = "Bg";
        private const string MENU_BUTTON_FOREGROUND = "Fg";

        private const string MENU_BUTTON_BASE = "Base";
        private const string MENU_BUTTON_HOVERED = "Hovered";
        private const string MENU_BUTTON_MOUSEDOWN = "MouseDown";

        private const string MENU_BUTTON_DEFAULT = "Default";
        private const string MENU_BUTTON_ACTIVE = "Active";

        /// <summary>
        /// Construct texture id for button background, based on prefix, button state and whether
        /// the button is active.
        /// </summary>
        /// <param name="prefix">Prefix, for example button name.</param>
        /// <param name="state">Button state (base, hovered, mousedown).</param>
        /// <param name="active">Whether the button is active.</param>
        /// <returns>Texture id.</returns>
        internal static string GetBackgroundTextureId(
            string prefix,
            ButtonMouseState state,
            bool active)
        {
            string ret = prefix + MENU_BUTTON_BACKGROUND;

            switch (state) {
                case ButtonMouseState.Base: {
                    ret += MENU_BUTTON_BASE;
                    break;
                }

                case ButtonMouseState.Hovered: {
                    ret += MENU_BUTTON_HOVERED;
                    break;
                }

                case ButtonMouseState.MouseDown: {
                    ret += MENU_BUTTON_MOUSEDOWN;
                    break;
                }
            }

            ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
            return ret;
        }

        /// <summary>
        /// Construct texture id for button foreground, based on prefix, what the button does,
        /// and whether it is active.
        /// </summary>
        /// <param name="prefix">Prefix, for example button name.</param>
        /// <param name="function">Button function, becomes part of the name.</param>
        /// <param name="active">Whether button is active.</param>
        /// <returns>Texture id.</returns>
        internal static string GetForegroundTextureId(
            string prefix,
            string function,
            bool active)
        {
            string ret = prefix + MENU_BUTTON_FOREGROUND + function;
            ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
            return ret;
        }

        /// <summary>Part of button start sequence: Creating atlas of textures for that button.</summary>
        /// <param name="buttonName">Name of the button to set up the atlas.</param>
        /// <param name="functionNames">List of function names this button will be performing.
        ///     One string usually.</param>
        /// <param name="canActivate">Whether the button can be in active state.</param>
        /// <param name="atlasTexture">Source texture to take pixels from.</param>
        /// <returns>A new atlas with each sprite having a name from U.ButtonTexture.</returns>
        public static UITextureAtlas CreateAtlas(string buttonName,
                                                 string[] functionNames,
                                                 bool canActivate,
                                                 Texture2D atlasTexture) {
            int textureCount =
                (Enum.GetValues(typeof(ButtonMouseState)).Length * (canActivate ? 2 : 1))
                + (functionNames.Length * 2);
            string[] textureIds = new string[textureCount];
            int i = 0;

            foreach (ButtonMouseState mouseState in EnumUtil.GetValues<ButtonMouseState>()) {
                if (canActivate) {
                    textureIds[i++] = GetBackgroundTextureId(buttonName, mouseState, true);
                }

                textureIds[i++] = GetBackgroundTextureId(buttonName, mouseState, false);
            }

            foreach (string function in functionNames) {
                textureIds[i++] = GetForegroundTextureId(buttonName, function, false);
            }

            foreach (string function in functionNames) {
                textureIds[i++] = GetForegroundTextureId(buttonName, function, true);
            }

            // Now that the names for background/foreground are defined, create an atlas from
            // source texture which is expected to have button images in the appropriate order.
            return TextureUtil.GenerateLinearAtlas(
                "TMPE_" + buttonName + "Atlas",
                atlasTexture,
                textureIds.Length,
                textureIds);
        }
    } // end class
}
namespace TrafficManager.U.Button {
    using System.Collections.Generic;
    using ColossalFramework.UI;

    /// <summary>
    /// Struct defines button atlas keys for button states.
    /// Atlas is loaded when the button is created.
    /// Some fields can be set to true to indicate that sprites exist and should be loaded and used.
    /// Skin setup example. In button constructor write:
    /// this.Skin = new ButtonSkin {
    ///                                Prefix = "MainMenuButton",
    ///                                BackgroundHovered = true,
    ///                                BackgroundActive = true,
    ///                                ForegroundHovered = true,
    ///                                ForegroundActive = true,
    ///                            };
    /// this.atlas = this.Skin.CreateAtlas("MainMenu", 50, 50, 256);
    ///
    /// </summary>
    public class ButtonSkin {
        /// <summary>Foreground sprites are loaded with this prefix.</summary>
        public string Prefix;

        /// <summary>Background sprites are loaded with prefix taken from this field, allows
        /// multiple buttons sharing same background sprites within the same atlas.</summary>
        public string BackgroundPrefix = string.Empty;

        public bool BackgroundDisabled = false;
        public bool BackgroundHovered = false;
        public bool BackgroundActive = false;

        public bool ForegroundDisabled = false;
        public bool ForegroundHovered = false;
        public bool ForegroundActive = false;

        public List<string> CreateAtlasKeysList() {
            // Two normal textures (bg and fg) are always assumed to exist.
            var names = new List<string> { $"{Prefix}-bg-normal", $"{Prefix}-fg-normal" };

            if (BackgroundDisabled) {
                names.Add($"{Prefix}-bg-disabled");
            }
            if (BackgroundHovered) {
                names.Add($"{Prefix}-bg-hovered");
            }
            if (BackgroundActive) {
                names.Add($"{Prefix}-bg-active");
            }
            if (ForegroundDisabled) {
                names.Add($"{Prefix}-fg-disabled");
            }
            if (ForegroundHovered) {
                names.Add($"{Prefix}-fg-hovered");
            }
            if (ForegroundActive) {
                names.Add($"{Prefix}-fg-active");
            }

            return names;
        }

        /// <summary>Following the settings in the Skin fields, load sprites into an UI atlas.
        /// Longer list of atlas keys can be loaded into one atlas.</summary>
        /// <param name="loadingPath">Path inside Resources. directory (dot separated).</param>
        /// <param name="spriteWidth">When loading assume this width.</param>
        /// <param name="spriteHeight">When loading assume this height.</param>
        /// <param name="hintAtlasTextureSize">Square atlas of this size is created.</param>
        /// <param name="atlasKeysList">List of atlas keys to load under the loadingPath. Created by
        /// calling CreateAtlasKeysList() on a ButtonSkin.</param>
        /// <returns>New UI atlas.</returns>
        public UITextureAtlas CreateAtlas(string loadingPath,
                                          int spriteWidth,
                                          int spriteHeight,
                                          int hintAtlasTextureSize,
                                          List<string> atlasKeysList) {
            return TextureUtil.CreateAtlas(
                $"TMPE_U_{Prefix}_Atlas",
                loadingPath,
                atlasKeysList.ToArray(),
                spriteWidth,
                spriteHeight,
                hintAtlasTextureSize);
        }

        /// <summary>
        /// Construct texture id for button background, based on prefix, button state and whether
        /// the button is active.
        /// </summary>
        /// <returns>Atlas sprite id.</returns>
        internal string GetBackgroundTextureId(ControlEnabledState enabledState,
                                               ControlHoveredState hoveredState,
                                               ControlActiveState activeState) {
            string chosenPrefix = string.IsNullOrEmpty(BackgroundPrefix)
                                      ? Prefix
                                      : BackgroundPrefix;
            string ret = chosenPrefix + "-bg";

            if (enabledState == ControlEnabledState.Disabled) {
                return BackgroundDisabled ? ret + "-disabled" : ret + "-normal";
            }

            if (activeState == ControlActiveState.Active) {
                return BackgroundActive ? ret + "-active" : ret + "-normal";
            }

            if (hoveredState == ControlHoveredState.Hovered) {
                return BackgroundHovered ? ret + "-hovered" : ret + "-normal";
            }

            return ret + "-normal";
        }

        /// <summary>
        /// Construct texture id for button foreground, based on prefix, what the button does,
        /// and whether it is active.
        /// </summary>
        /// <returns>Atlas sprite id.</returns>
        internal string GetForegroundTextureId(ControlEnabledState enabledState,
                                               ControlHoveredState hoveredState,
                                               ControlActiveState activeState)
        {
            string ret = Prefix + "-fg";

            if (enabledState == ControlEnabledState.Disabled) {
                return BackgroundDisabled ? ret + "-disabled" : ret + "-normal";
            }

            if (activeState == ControlActiveState.Active) {
                return BackgroundActive ? ret + "-active" : ret + "-normal";
            }

            if (hoveredState == ControlHoveredState.Hovered) {
                return BackgroundHovered ? ret + "-hovered" : ret + "-normal";
            }

            return ret + "-normal";
        }
    } // end class
}

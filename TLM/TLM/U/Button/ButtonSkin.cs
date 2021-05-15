namespace TrafficManager.U {
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using TrafficManager.Util;

    /// <summary>
    /// Struct defines button atlas keys for button states.
    /// Atlas is loaded when the button is created.
    /// Some fields can be set to true to indicate that sprites exist and should be loaded and used.
    /// Skin setup example.
    /// -------------------------------------------------------------------------------
    /// In button constructor write:
    /// this.Skin = new ButtonSkin {
    ///     Prefix = "MainMenuButton",
    ///     BackgroundPrefix = "MainMenuButton", // this also loads the *-bg-normal
    ///     BackgroundHovered = true,
    ///     BackgroundActive = true,
    ///     ForegroundHovered = true,
    ///     ForegroundActive = true,
    /// };
    /// this.atlas = this.Skin.CreateAtlas("MainMenu", 50, 50, 256, Skin.CreateAtlasKeyset());
    /// -------------------------------------------------------------------------------
    /// Background is always loaded if "BackgroundPrefix" is not empty.
    /// Foreground Normal is always loaded.
    /// Rest of the skin sprites you can control using the boolean variables provided.
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

        /// <summary>
        /// Allows loading foreground-normal sprite. Set this to false to only load backgrounds.
        /// </summary>
        public bool ForegroundNormal = true;

        public bool ForegroundDisabled = false;
        public bool ForegroundHovered = false;
        public bool ForegroundActive = false;

        /// <summary>
        /// Create Button Skin for a given button name, which can be hovered, active,
        /// but not disabled.
        /// </summary>
        /// <param name="prefix">Prefix of the filenames to use for this button.</param>
        /// <returns>Button skin object.</returns>
        public static ButtonSkin CreateDefault(string prefix, string backgroundPrefix) {
            return new ButtonSkin {
                Prefix = prefix,
                BackgroundPrefix = backgroundPrefix,

                BackgroundHovered = true,
                BackgroundActive = true,
                BackgroundDisabled = false,

                ForegroundNormal = true,
                ForegroundActive = true,
            };
        }

        /// <summary>Create background only button skin.</summary>
        /// <param name="buttonName">Prefix.</param>
        /// <returns>New skin.</returns>
        public static ButtonSkin CreateDefaultNoForeground(string buttonName) {
            return new ButtonSkin {
                Prefix = buttonName,
                BackgroundPrefix = buttonName, // filename prefix

                BackgroundHovered = true,
                BackgroundActive = true,
                BackgroundDisabled = false,

                ForegroundNormal = false,
                ForegroundActive = false,
            };
        }

        /// <summary>Create foreground-only button skin.</summary>
        /// <param name="buttonName">Prefix.</param>
        /// <returns>New skin.</returns>
        public static ButtonSkin CreateDefaultNoBackground(string buttonName) {
            return new ButtonSkin {
                Prefix = buttonName,
                BackgroundPrefix = string.Empty, // no background

                BackgroundHovered = false,
                BackgroundActive = false,
                BackgroundDisabled = false,

                ForegroundNormal = true,
                ForegroundActive = true,
            };
        }

        /// <summary>
        /// Create set of atlas spritedefs all of the same size.
        /// SpriteDef sets can be merged together and fed into sprite atlas creation call.
        /// </summary>
        /// <param name="atlasBuilder">Will later load sprites and form a texture atlas.</param>
        /// <param name="spriteSize">The size to assume for all sprites.</param>
        public void UpdateAtlasBuilder(AtlasBuilder atlasBuilder, IntVector2 spriteSize) {
            // Two normal textures (bg and fg) are always assumed to exist.
            List<string> names = new List<string>();
            bool haveBackgroundPrefix = !string.IsNullOrEmpty(BackgroundPrefix);
            if (haveBackgroundPrefix) {
                names.Add($"{BackgroundPrefix}-bg-normal");
            }

            if (ForegroundNormal && !string.IsNullOrEmpty(Prefix)) {
                names.Add($"{Prefix}-fg-normal");
            }
            if (BackgroundDisabled && haveBackgroundPrefix) {
                names.Add($"{BackgroundPrefix}-bg-disabled");
            }
            if (BackgroundHovered && haveBackgroundPrefix) {
                names.Add($"{BackgroundPrefix}-bg-hovered");
            }
            if (BackgroundActive && haveBackgroundPrefix) {
                names.Add($"{BackgroundPrefix}-bg-active");
            }
            if (ForegroundDisabled && !string.IsNullOrEmpty(Prefix)) {
                names.Add($"{Prefix}-fg-disabled");
            }
            if (ForegroundHovered && !string.IsNullOrEmpty(Prefix)) {
                names.Add($"{Prefix}-fg-hovered");
            }
            if (ForegroundActive && !string.IsNullOrEmpty(Prefix)) {
                names.Add($"{Prefix}-fg-active");
            }

            // Convert string hashset into spritedefs hashset
            foreach (string n in names) {
                atlasBuilder.Add(new U.AtlasSpriteDef(name: n, size: spriteSize));
            }
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
                return ForegroundDisabled ? ret + "-disabled" : ret + "-normal";
            }

            if (activeState == ControlActiveState.Active) {
                return ForegroundActive ? ret + "-active" : ret + "-normal";
            }

            if (hoveredState == ControlHoveredState.Hovered) {
                return ForegroundHovered ? ret + "-hovered" : ret + "-normal";
            }

            return ret + "-normal";
        }
    } // end class
}

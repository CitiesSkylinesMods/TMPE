namespace TrafficManager.U {
    using System.Collections.Generic;
    using TrafficManager.Util;

    /// <summary>
    /// Struct defines button atlas keys for button states.
    /// Atlas is loaded when the button is created.
    /// Some fields can be set to true to indicate that sprites exist and should be loaded and used.
    /// Skin setup example.
    /// -------------------------------------------------------------------------------
    /// In button constructor write:
    /// this.Skin = ButtonSkin.CreateSimple(
    ///     foregroundPrefix: "MainMenuButton",
    ///     backgroundPrefix: "MainMenuButton")
    /// You can chain more modifier calls to allow hovering, active and disabled textures:
    ///     .CanHover()
    ///     .CanActivate()
    ///     .CanDisable().
    /// this.atlas = this.Skin.CreateAtlas("MainMenu", 50, 50, 256, Skin.CreateAtlasKeyset());
    /// -------------------------------------------------------------------------------
    /// Background is always loaded if "BackgroundPrefix" is not empty.
    /// Foreground Normal is always loaded.
    /// Rest of the skin sprites you can control using the boolean variables provided.
    /// </summary>
    public class ButtonSkin {
        private struct BackgroundSprite {
            /// <summary>Background sprites are loaded with prefix taken from this field, allows
            /// multiple buttons sharing same background sprites within the same atlas.</summary>
            public string Prefix;

            public bool Disabled;
            public bool Hovered;
            public bool Active;
        }

        private BackgroundSprite background_;

        private struct ForegroundSprite {
            /// <summary>Foreground sprites are loaded with this prefix.</summary>
            public string Prefix;

            /// <summary>
            /// Allows loading foreground-normal sprite. Set this to false to only load backgrounds.
            /// </summary>
            public bool Normal;

            public bool Disabled;
            public bool Hovered;
            public bool Active;
        }

        private ForegroundSprite foreground_;

        public string ForegroundPrefix {
            set => foreground_.Prefix = value;
        }

        /// <summary>
        /// Create a simple button skin which has no background and one foreground (normal).
        /// If the prefix begins with / you can write absolute resource path instead, and the atlas
        /// loading path will be ignored. Example: "/MainMenu.Tool.RoundButton".
        /// </summary>
        /// <param name="foregroundPrefix">Texture prefix in the directory specified when loading the atlas.</param>
        /// <param name="backgroundPrefix">Sprite prefix for backgrounds.</param>
        /// <returns>New skin for a hoverable toggle button.</returns>
        public static ButtonSkin CreateSimple(string foregroundPrefix,
                                              string backgroundPrefix) {
            return new() {
                background_ = new BackgroundSprite {
                    Prefix = backgroundPrefix,
                },
                foreground_ = new ForegroundSprite {
                    Prefix = foregroundPrefix,
                    Normal = true,
                },
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
            bool haveBackgroundPrefix = !string.IsNullOrEmpty(background_.Prefix);

            if (haveBackgroundPrefix) {
                names.Add($"{background_.Prefix}-bg-normal");
            }

            if (foreground_.Normal && !string.IsNullOrEmpty(foreground_.Prefix)) {
                names.Add($"{foreground_.Prefix}-fg-normal");
            }

            if (background_.Disabled && haveBackgroundPrefix) {
                names.Add($"{background_.Prefix}-bg-disabled");
            }

            if (background_.Hovered && haveBackgroundPrefix) {
                names.Add($"{background_.Prefix}-bg-hovered");
            }

            if (background_.Active && haveBackgroundPrefix) {
                names.Add($"{background_.Prefix}-bg-active");
            }

            if (foreground_.Disabled && !string.IsNullOrEmpty(foreground_.Prefix)) {
                names.Add($"{foreground_.Prefix}-fg-disabled");
            }

            if (foreground_.Hovered && !string.IsNullOrEmpty(foreground_.Prefix)) {
                names.Add($"{foreground_.Prefix}-fg-hovered");
            }

            if (foreground_.Active && !string.IsNullOrEmpty(foreground_.Prefix)) {
                names.Add($"{foreground_.Prefix}-fg-active");
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
            string chosenPrefix = string.IsNullOrEmpty(background_.Prefix)
                                      ? foreground_.Prefix
                                      : background_.Prefix;
            string ret = chosenPrefix + "-bg";

            if (enabledState == ControlEnabledState.Disabled) {
                return background_.Disabled ? ret + "-disabled" : ret + "-normal";
            }

            // Hovered foreground before active, we want hover icon to be used even if active
            if (hoveredState == ControlHoveredState.Hovered) {
                return background_.Hovered ? ret + "-hovered" : ret + "-normal";
            }

            if (activeState == ControlActiveState.Active) {
                return background_.Active ? ret + "-active" : ret + "-normal";
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
                                               ControlActiveState activeState) {
            string ret = foreground_.Prefix + "-fg";

            if (enabledState == ControlEnabledState.Disabled) {
                return foreground_.Disabled ? ret + "-disabled" : ret + "-normal";
            }

            if (activeState == ControlActiveState.Active) {
                return foreground_.Active ? ret + "-active" : ret + "-normal";
            }

            // Hovered foreground after active, we don't want hover icon to cover active icon
            if (hoveredState == ControlHoveredState.Hovered) {
                return foreground_.Hovered ? ret + "-hovered" : ret + "-normal";
            }

            return ret + "-normal";
        }

        /// <summary>
        /// Call this to allow Active state and using background and foreground sprites.
        /// </summary>
        public ButtonSkin CanActivate(bool foreground = true, bool background = true) {
            background_.Active = background;
            foreground_.Active = foreground;
            return this;
        }

        /// <summary>
        /// Call this to allow Hovered (mouse hover) state and using background and foreground
        /// sprites.
        /// </summary>
        public ButtonSkin CanHover(bool foreground = true, bool background = true) {
            background_.Hovered = background;
            foreground_.Hovered = foreground;
            return this;
        }

        /// <summary>
        /// Call this to allow Hovered (mouse hover) state and using background and foreground
        /// sprites.
        /// </summary>
        public ButtonSkin CanDisable(bool foreground = true, bool background = true) {
            background_.Disabled = background;
            foreground_.Disabled = foreground;
            return this;
        }

        public ButtonSkin NormalForeground(bool value = true) {
            foreground_.Normal = value;
            return this;
        }
    } // end class
}
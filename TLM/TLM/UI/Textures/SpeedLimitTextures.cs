namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using UnityEngine;

    public static class SpeedLimitTextures {
        public class RoadSignTheme {
            private IntVector2 TextureSize;

            /// <summary>Speed limit signs from 5 to 140 km (from 5 to 90 mph) and zero for no limit.</summary>
            public readonly Dictionary<int, Texture2D> Textures = new();

            /// <summary>This list of required speed signs is used for loading.</summary>
            private List<int> SignValues = new();

            private string PathPrefix;

            // Kmph sign sets include range for MPH, but not all pictures are good to go with Kmph or Mph setting.
            // For example Canadian signs have all values to show MPH, but make no sense because the sign says km/h.

            /// <summary>Whether km/h signs range is supported from 5 to 140 step 5.</summary>
            public readonly bool SupportsKmph;

            /// <summary>Whether MPH signs range is supported from 5 to 90 step 5.</summary>
            public readonly bool SupportsMph;

            public readonly string Name;

            /// <summary>Set to true if an attempt to find and load textures was made.</summary>
            public bool AttemptedToLoad = false;

            public RoadSignTheme(string name,
                                 bool supportsMph,
                                 bool supportsKmph,
                                 IntVector2 size,
                                 string pathPrefix) {
                Log._DebugIf(
                    this.TextureSize.x <= this.TextureSize.y,
                    () => $"Constructing a road sign theme {pathPrefix}: Portrait oriented size not supported");

                this.Name = name;
                this.SupportsMph = supportsMph;
                this.SupportsKmph = supportsKmph;
                this.PathPrefix = pathPrefix;
                this.TextureSize = size;

                if (supportsKmph) {
                    // Assumes that signs from 0 to 140 with step 5 exist, 0 denotes no-limit sign
                    for (var kmphValue = 0; kmphValue <= UPPER_KMPH; kmphValue += LOAD_KMPH_STEP) {
                        this.SignValues.Add(kmphValue);
                    }
                } else if (supportsMph) {
                    for (var mphValue = 0; mphValue <= UPPER_MPH; mphValue += MPH_STEP) {
                        this.SignValues.Add(mphValue);
                    }
                }
            }

            public RoadSignTheme Load() {
                if (this.AttemptedToLoad) {
                    return this;
                }

                this.Textures.Clear();
                this.AttemptedToLoad = true;

                foreach (var speedLimit in this.SignValues) {
                    // Log._Debug($"Loading sign texture {this.PathPrefix}.{speedLimit}.png");
                    var resource = LoadDllResource(
                        resourceName: $"{this.PathPrefix}.{speedLimit}.png",
                        size: this.TextureSize,
                        mip: true);
                    this.Textures.Add(speedLimit, resource ? resource : SpeedLimitTextures.Clear);
                }

                return this;
            }

            public void Unload() {
                foreach (var texture in this.Textures) {
                    UnityEngine.Object.Destroy(texture.Value);
                }

                this.Textures.Clear();
                this.AttemptedToLoad = false;
            }

            /// <summary>
            /// Assumes that signs can be square or vertical rectangle, no horizontal themes.
            /// Aspect ratio value which scales width down to have height fully fit.
            /// </summary>
            public Vector2 GetAspectRatio() {
                return new(this.TextureSize.x / (float)this.TextureSize.y, 1.0f);
            }

            /// <summary>Given the speed, return a texture to render.</summary>
            /// <param name="spd">Speed to display.</param>
            /// <returns>Texture to display.</returns>
            public Texture2D GetTexture(SpeedValue spd) {
                // Round to nearest 5 MPH or nearest 5 km/h
                bool mph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                ushort index = mph
                                   ? spd.ToMphRounded(MPH_STEP).Mph
                                   : spd.ToKmphRounded(KMPH_STEP).Kmph;

                // Trim the index since 140 km/h / 90 MPH is the max sign we have
                ushort upper = mph ? UPPER_MPH : UPPER_KMPH;

                try {
                    // Show unlimited if the speed cannot be represented by the available sign textures
                    if (index == 0 || index > upper) {
                        return this.Textures[0];
                    }

                    // Trim from below to not go below index 5 (5 kmph or 5 mph)
                    ushort trimIndex = Math.Max((ushort)5, index);
                    return this.Textures[trimIndex];
                }
                catch (KeyNotFoundException) {
                    return SpeedLimitTextures.NoOverride;
                }
            }
        }

        // We have texture files for every 5 kmph but speed limits palette allows every 10. This is
        // for more precise MPH display.
        internal const ushort KMPH_STEP = 10;
        internal const ushort UPPER_KMPH = 140;
        private const ushort LOAD_KMPH_STEP = 5;
        internal const ushort UPPER_MPH = 90;
        internal const ushort MPH_STEP = 5;

        /// <summary>Displayed in Override view for Speed Limits tool, when there's no override.</summary>
        public static readonly Texture2D NoOverride;

        /// <summary>Blue textures for road/lane default speed limits. Always loaded.</summary>
        public static readonly RoadSignTheme RoadDefaults;

        public static readonly Texture2D Clear;

        public static readonly Dictionary<string, RoadSignTheme> Themes = new ();

        private static RoadSignTheme activeTheme_ = null;

        /// <summary>Names from <see cref="Themes"/> sorted.</summary>
        public static readonly List<string> ThemeNames;

        public static RoadSignTheme ActiveTheme {
            get {
                if (activeTheme_ == null || !activeTheme_.AttemptedToLoad) {
                    activeTheme_ = LoadCurrentTheme();
                }

                return activeTheme_;
            }
        }

        private const string KMPH_GERMANY_THEME = "Kmph_Germany";
        private const string MPH_UK_THEME = "MPH_UK";
        private const string MPH_US_THEME = "MPH_US";
        private const string KMPH_CANADA_THEME = "Kmph_Canada";
        private const string KMPH_SWEDEN_THEME = "Kmph_Sweden";
        private const string KMPH_BRAZIL_THEME = "Kmph_Brazil";
        private const string KMPH_FRANCE_THEME = "Kmph_France";

        private const string DEFAULT_KMPH_THEME = KMPH_GERMANY_THEME;
        private const string DEFAULT_MPH_THEME = MPH_UK_THEME;

        public static string GetDefaultThemeName(bool mph) {
            return mph ? DEFAULT_MPH_THEME : DEFAULT_KMPH_THEME;
        }

        public static int FindDefaultThemeIndex(bool mph) {
            if (mph) {
                return ThemeNames.FindIndex(x => x == DEFAULT_MPH_THEME);
            }

            return ThemeNames.FindIndex(x => x == DEFAULT_KMPH_THEME);
        }

        private static RoadSignTheme LoadCurrentTheme() {
            Main confMain = GlobalConfig.Instance.Main;
            string selectedThemeName = confMain.RoadSignTheme;

            if (Themes.ContainsKey(selectedThemeName)) {
                return Themes[selectedThemeName].Load();
            }

            bool confMainDisplaySpeedLimitsMph = confMain.DisplaySpeedLimitsMph;
            string defaultTheme = GetDefaultThemeName(confMainDisplaySpeedLimitsMph);

            confMain.RoadSignTheme = defaultTheme;
            Log.Info($"Road Sign theme changed to default ({defaultTheme})");

            return Themes[defaultTheme].Load();
        }

        // TODO: Split loading here into dynamic sections, static enforces everything to stay in this ctor
        static SpeedLimitTextures() {
            RoadDefaults = new RoadSignTheme(
                name: "Defaults",
                supportsKmph: true,
                supportsMph: true,
                size: new IntVector2(200),
                pathPrefix: "SpeedLimits.RoadDefaults");
            RoadDefaults.Load();

            Themes.Add(
                KMPH_GERMANY_THEME,
                new RoadSignTheme(
                    name: KMPH_GERMANY_THEME,
                    supportsKmph: true,
                    supportsMph: false,
                    size: new IntVector2(200),
                    pathPrefix: "SpeedLimits." + KMPH_GERMANY_THEME));
            Themes.Add(
                MPH_UK_THEME,
                new RoadSignTheme(
                    name: MPH_UK_THEME,
                    supportsKmph: false,
                    supportsMph: true,
                    size: new IntVector2(200),
                    pathPrefix: "SpeedLimits." + MPH_UK_THEME));
            Themes.Add(
                MPH_US_THEME,
                new RoadSignTheme(
                    name: MPH_US_THEME,
                    supportsKmph: false,
                    supportsMph: true,
                    size: new IntVector2(200, 250),
                    pathPrefix: "SpeedLimits." + MPH_US_THEME));
            Themes.Add(
                KMPH_CANADA_THEME,
                new RoadSignTheme(
                    name: KMPH_CANADA_THEME,
                    supportsKmph: true,
                    supportsMph: false,
                    size: new IntVector2(200, 250),
                    pathPrefix: "SpeedLimits." + KMPH_CANADA_THEME));
            Themes.Add(
                KMPH_SWEDEN_THEME,
                new RoadSignTheme(
                    name: KMPH_SWEDEN_THEME,
                    supportsKmph: true,
                    supportsMph: false,
                    size: new IntVector2(200, 200),
                    pathPrefix: "SpeedLimits." + KMPH_SWEDEN_THEME));
            Themes.Add(
                KMPH_BRAZIL_THEME,
                new RoadSignTheme(
                    name: KMPH_BRAZIL_THEME,
                    supportsKmph: true,
                    supportsMph: false,
                    size: new IntVector2(200, 200),
                    pathPrefix: "SpeedLimits." + KMPH_BRAZIL_THEME));
            Themes.Add(
                KMPH_FRANCE_THEME,
                new RoadSignTheme(
                    name: KMPH_FRANCE_THEME,
                    supportsKmph: true,
                    supportsMph: false,
                    size: new IntVector2(200, 200),
                    pathPrefix: "SpeedLimits." + KMPH_FRANCE_THEME));

            ThemeNames = Themes.Keys.ToList();
            ThemeNames.Sort();

            NoOverride = LoadDllResource(
                resourceName: "SpeedLimits.NoOverride.png",
                size: new IntVector2(200));

            Clear = LoadDllResource("clear.png", new IntVector2(256));
        }

        public static Vector2 DefaultSpeedlimitsAspectRatio() => Vector2.one;

        /// <summary>Called from Options General tab and attempts to change the theme.</summary>
        /// <param name="newTheme">New string key.</param>
        /// <param name="mphEnabled">Whether config is set to showing MPH</param>
        /// <returns>False if the new theme doesn't support km/h and the settings require km/h.
        /// Or false if the `newTheme` key isn't a valid theme name.</returns>
        public static bool OnThemeChanged(string newTheme, bool mphEnabled) {
            if (!Themes.ContainsKey(newTheme)) {
                var defaultTheme = GetDefaultThemeName(mphEnabled);
                Log.Error($"Theme changing to {newTheme} but it isn't known to texture manager, so instead we change to {defaultTheme}");
                newTheme = defaultTheme;
            }

            bool canChange = mphEnabled
                                 ? Themes[newTheme].SupportsMph
                                 : Themes[newTheme].SupportsKmph;

            if (activeTheme_ == null || activeTheme_.Name != newTheme) {
                activeTheme_?.Unload();
            }

            if (canChange) {
                activeTheme_ = Themes[newTheme];
                activeTheme_.Load();
                return true;
            }

            var unitStr = mphEnabled ? "MPH" : "KM/H";
            Log.Error($"Theme changing to {newTheme} but it doesn't support {unitStr} signs, so no action was taken");

            return false;
        }
    }
}
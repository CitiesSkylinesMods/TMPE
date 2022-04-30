namespace TrafficManager.UI.Textures {
    using static TextureResources;
    using System.Collections.Generic;
    using System.Linq;
    using CSUtil.Commons;
    using TrafficManager.Manager;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.SubTools.SpeedLimits;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Singleton which manages road signs themes dynamically loaded and freed as the user .
    /// The textures are loaded when OnLevelLoaded event is fired from the <see cref="Lifecycle.TMPELifecycle"/>.
    /// </summary>
    public class RoadSignThemeManager : AbstractCustomManager {
        /// <summary>
        /// Reports the success of theme change. ForceMph/ForceKmph require the caller to also
        /// change display units because the currently selected display units are not supported.
        /// </summary>
        public enum ChangeThemeResult {
            Success,
            ForceMph,
            ForceKmph,
        }

        // We have texture files for every 5 kmph but speed limits palette allows every 10. This is
        // for more precise MPH display.
        internal const ushort KMPH_STEP = 10;
        internal const ushort UPPER_KMPH = 140;
        internal const ushort LOAD_KMPH_STEP = 5;
        internal const ushort UPPER_MPH = 90;
        internal const ushort MPH_STEP = 5;

        private const string FALLBACK_THEME = "Fallback";

        private const string KMPH_BRAZIL_THEME = "Kmph_Brazil";
        private const string KMPH_CANADA_THEME = "Kmph_Canada";
        private const string KMPH_FRANCE_THEME = "Kmph_France";
        private const string KMPH_GERMANY_THEME = "Kmph_Germany";
        private const string KMPH_INDONESIA_THEME = "Kmph_Indonesia";
        private const string KMPH_JAPAN_THEME = "Kmph_Japan";
        private const string KMPH_SOUTHKOREA_THEME = "Kmph_SouthKorea";
        private const string KMPH_SWEDEN_THEME = "Kmph_Sweden";
        private const string KMPH_CHINA_THEME = "Kmph_China";
        private const string KMPH_CHINA_GENERIC_THEME = "Kmph_China_Generic";
        private const string MPH_UK_THEME = "MPH_UK";
        private const string MPH_US_THEME = "MPH_US";

        private const string DEFAULT_KMPH_THEME = KMPH_GERMANY_THEME;
        private const string DEFAULT_MPH_THEME = MPH_UK_THEME;
        public static RoadSignThemeManager Instance = new();

        /// <summary>Fallback theme to use when a sign is not found in the user-selected theme.</summary>
        public readonly RoadSignTheme FallbackTheme;

        /// <summary>Green textures for road/lane default speed limits. Always loaded.</summary>
        public readonly RoadSignTheme SpeedLimitDefaults;

        /// <summary>Names from <see cref="Themes"/> sorted.</summary>
        internal readonly List<string> ThemeNames = new();

        public readonly Dictionary<string, RoadSignTheme> Themes = new();

        private RoadSignTheme activeTheme_ = null;

        public Texture2D Clear;

        /// <summary>
        /// Displayed in Override view for Speed Limits tool, when there's no override.
        /// This is rather never displayed because there is always either override or default speed
        /// limit value to show, but is still present for "safety".
        /// </summary>
        public Texture2D NoOverride;

        private RoadSignThemeManager() {
            SpeedLimitDefaults = new RoadSignTheme(
                name: "Defaults",
                supportsKmph: true,
                supportsMph: true,
                size: new IntVector2(200),
                pathPrefix: "SignThemes.SpeedLimitDefaults");
            FallbackTheme = new RoadSignTheme(
                name: FALLBACK_THEME,
                supportsKmph: true,
                supportsMph: true,
                size: new IntVector2(200),
                pathPrefix: "SignThemes.Fallback");

            RoadSignTheme NewTheme(string name,
                                   SpeedUnit unit,
                                   int height = 200,
                                   RoadSignTheme parentTheme = null,
                                   bool speedLimitSigns = true) {
                var theme = new RoadSignTheme(
                    name: name,
                    supportsMph: unit == SpeedUnit.Mph,
                    supportsKmph: unit == SpeedUnit.Kmph,
                    size: new IntVector2(200, height),
                    pathPrefix: $"SignThemes.{name}",
                    parentTheme: parentTheme,
                    speedLimitSigns: speedLimitSigns);
                Themes.Add(name, theme);
                return theme;
            }

            NewTheme(name: MPH_UK_THEME, unit: SpeedUnit.Mph);
            NewTheme(name: MPH_US_THEME, unit: SpeedUnit.Mph, height: 250);

            NewTheme(name: KMPH_BRAZIL_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_CANADA_THEME, unit: SpeedUnit.Kmph, height: 250);
            NewTheme(name: KMPH_FRANCE_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_GERMANY_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_INDONESIA_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_JAPAN_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_SOUTHKOREA_THEME, unit: SpeedUnit.Kmph);
            NewTheme(name: KMPH_SWEDEN_THEME, unit: SpeedUnit.Kmph);

            // "Mainland" theme defines road signs for mainland China, with chinese text
            // "Generic" defines more generic signs without text and generic yield sign.
            RoadSignTheme mainlandChina = NewTheme(
                name: KMPH_CHINA_THEME,
                unit: SpeedUnit.Kmph);
            NewTheme(
                name: KMPH_CHINA_GENERIC_THEME,
                unit: SpeedUnit.Kmph,
                parentTheme: mainlandChina,
                speedLimitSigns: false);

            ThemeNames = Themes.Keys.ToList();
            ThemeNames.Sort();
        }

        public static RoadSignTheme ActiveTheme {
            get {
                if (Instance.activeTheme_ == null || !Instance.activeTheme_.AttemptedToLoad) {
                    Instance.activeTheme_ = Instance.LoadCurrentTheme();
                }

                return Instance.activeTheme_;
            }
        }

        public string GetDefaultThemeName(bool mph) {
            return mph ? DEFAULT_MPH_THEME : DEFAULT_KMPH_THEME;
        }

        public int FindDefaultThemeIndex(bool mph) {
            if (mph) {
                return ThemeNames.FindIndex(x => x == DEFAULT_MPH_THEME);
            }

            return ThemeNames.FindIndex(x => x == DEFAULT_KMPH_THEME);
        }

        private RoadSignTheme LoadCurrentTheme() {
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

        public static Vector2 DefaultSpeedlimitsAspectRatio() => Vector2.one;

        /// <summary>Called from Options General tab and attempts to change the theme.</summary>
        /// <param name="newTheme">New string key.</param>
        /// <param name="mphEnabled">Whether config is set to showing MPH</param>
        /// <returns>False if the new theme doesn't support km/h and the settings require km/h.
        /// Or false if the `newTheme` key isn't a valid theme name.</returns>
        public ChangeThemeResult ChangeTheme(string newTheme, bool mphEnabled) {
            if (!Themes.ContainsKey(newTheme)) {
                var defaultTheme = GetDefaultThemeName(mphEnabled);
                Log.Error(
                    $"Theme changing to {newTheme} but it isn't known to texture manager, so instead we change to {defaultTheme}");
                newTheme = defaultTheme;
            }

            if (activeTheme_ != null && activeTheme_.Name != newTheme) {
                activeTheme_.Unload();
                activeTheme_ = Themes[newTheme];
                activeTheme_.Load();

                if (mphEnabled && !activeTheme_.SupportsMph) {
                    // Theme requires KM/H display to be on
                    return ChangeThemeResult.ForceKmph;
                }

                if (!mphEnabled && !activeTheme_.SupportsKmph) {
                    // Theme requires MPH display to be on
                    return ChangeThemeResult.ForceMph;
                }
            }

            return ChangeThemeResult.Success;
        }

        /// <summary>Called by the lifecycle when textures are to be loaded.</summary>
        public override void OnLevelLoading() {
            SpeedLimitDefaults.Load();
            FallbackTheme.Load(whiteTexture: true);

            NoOverride = LoadDllResource(
                resourceName: "SpeedLimits.NoOverride.png",
                size: new IntVector2(200));
            Clear = LoadDllResource(resourceName: "clear.png", size: new IntVector2(256));

            base.OnLevelLoading();
        }

        /// <summary>Called by the lifecycle when textures are to be unloaded.</summary>
        public override void OnLevelUnloading() {
            // Let all themes know its time to unload, even the ones not loaded
            SpeedLimitDefaults.Unload();
            FallbackTheme.Unload();

            foreach (var theme in Themes) {
                theme.Value.Unload();
            }

            UnityEngine.Object.Destroy(Clear);
            UnityEngine.Object.Destroy(NoOverride);

            base.OnLevelUnloading();
        }
    }
}
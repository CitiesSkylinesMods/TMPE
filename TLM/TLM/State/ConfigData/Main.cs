namespace TrafficManager.State.ConfigData {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Xml.Serialization;
    using JetBrains.Annotations;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.SubTools.SpeedLimits;

    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Reviewed.")]
    public class Main {
        /// <summary>Whether floating keybinds panel is visible.</summary>
        public bool KeybindsPanelVisible = true;

        /// <summary>Main menu button position X.</summary>
        public int MainMenuButtonX = 464;

        /// <summary>Main menu button position Y.</summary>
        public int MainMenuButtonY = 10;

        /// <summary>Main menu button is not movable.</summary>
        public bool MainMenuButtonPosLocked = false;

        /// <summary>Main menu position X.</summary>
        public int MainMenuX = MainMenuWindow.DEFAULT_MENU_X;

        /// <summary>Main menu position Y.</summary>
        public int MainMenuY = MainMenuWindow.DEFAULT_MENU_Y;

        /// <summary>Main menu is not movable.</summary>
        public bool MainMenuPosLocked = false;

        /// <summary>Speed Limits tool window position X.</summary>
        public int SpeedLimitsWindowX = 0;

        /// <summary>Speed Limits tool window position Y.</summary>
        public int SpeedLimitsWindowY = 0;

        /// <summary>Put button inside UUI.</summary>
        public bool UseUUI = false;

        /// <summary>Already displayed tutorial messages.</summary>
        public string[] DisplayedTutorialMessages = new string[0];

        /// <summary>Determines if tutorial messages shall show up.</summary>
        public bool EnableTutorial = true;

        /// <summary>Determines if the main menu shall be displayed in a tiny format.</summary>
        [Obsolete("Use GuiScale instead")]
        public bool TinyMainMenu = true;

        /// <summary>User interface transparency, unit: percents, range: 0..90.</summary>
        [Obsolete("Use GuiOpacity instead")]
        public byte GuiTransparency = 75;

        /// <summary>User interface opacity, unit: percents, range: 10..100.</summary>
        public byte GuiOpacity = 75;

        /// <summary>User interface scale for TM:PE. Unit: percents, range: 30..200f.</summary>
        public float GuiScale = 100f;

        /// <summary>
        /// if checked, size remains constant but pixel count changes when resolution changes. Quality drops with lower resolutions.
        /// if unchecked, size changes constant but pixel count remains the same. Maintains same image quality for all resolution.
        /// </summary>
        public bool GuiScaleToResolution = true;

        /// <summary>Overlay transparency, unit: percents, range: 0..90</summary>
        [Obsolete("Use OverlayOpacity instead")]
        public byte OverlayTransparency = 40;

        /// <summary>Overlay icons opacity, unit: percent, range: 10..100.</summary>
        public byte OverlayOpacity = 60;

        /// <summary>
        /// Extended mod compatibility check
        /// </summary>
        public bool ShowCompatibilityCheckErrorMessage = false;

        /// <summary>
        /// Shows warning dialog if any incompatible mods detected
        /// </summary>
        public bool ScanForKnownIncompatibleModsAtStartup = true;

        /// <summary>
        /// Skip disabled mods while running incompatible mod detector
        /// </summary>
        public bool IgnoreDisabledMods = true;

        /// <summary>
        /// Prefer Miles per hour instead of Kmph (affects speed limits display
        /// but internally Kmph are still used).
        /// </summary>
        public bool DisplaySpeedLimitsMph = false;

        public SpeedUnit GetDisplaySpeedUnit() => DisplaySpeedLimitsMph
                                                      ? SpeedUnit.Mph
                                                      : SpeedUnit.Kmph;

        // /// <summary>Selected theme for road signs when MPH is active.</summary>
        // [Obsolete("This now becomes RoadSignTheme")]
        // public MphSignStyle MphRoadSignStyle = MphSignStyle.SquareUS;

        /// <summary>
        /// String key in SpeedLimitsTextures.Themes. Some themes support only MPH, and
        /// some support both Km/h and MPH, changing this might affect
        /// <see cref="DisplaySpeedLimitsMph"/>.
        /// </summary>
        public string RoadSignTheme = string.Empty;

        /// <summary>
        /// If <c>true</c>, <see cref="UI.Helpers.UrlButton"/> links will open in
        /// Steam Overlay if it is available.
        /// </summary>
        public bool OpenUrlsInSteamOverlay = true;

        /// <summary>
        /// Storing version of What's New panel opened last time
        /// </summary>
        [XmlIgnore]
        [CanBeNull]
        public Version LastWhatsNewPanelVersion = null;

        /// <summary>
        /// Property for proper Version serialization
        /// </summary>
        [UsedImplicitly]
        [XmlElement(ElementName = nameof(LastWhatsNewPanelVersion))]
        public string lastWhatsNewPanelVersion {
            get {
                if (this.LastWhatsNewPanelVersion == null)
                    return string.Empty;
                else
                    return this.LastWhatsNewPanelVersion.ToString();
            }
            set {
                if (!string.IsNullOrEmpty(value))
                    this.LastWhatsNewPanelVersion = new Version(value);
            }
        }

        public void AddDisplayedTutorialMessage(string messageKey) {
            HashSet<string> newMessages = DisplayedTutorialMessages != null
                                              ? new HashSet<string>(DisplayedTutorialMessages)
                                              : new HashSet<string>();
            newMessages.Add(messageKey);
            DisplayedTutorialMessages = newMessages.ToArray();
        }
    }
}
namespace TrafficManager.State.ConfigData {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.SubTools.SpeedLimits;

    public class Main {
        /// <summary>Whether floating keybinds panel is visible.</summary>
        public bool KeybindsPanelVisible = true;

        /// <summary>Main menu button position.</summary>
        public int MainMenuButtonX = 464;
        public int MainMenuButtonY = 10;
        public bool MainMenuButtonPosLocked = false;

        /// <summary>
        /// Main menu position
        /// </summary>
        public int MainMenuX = MainMenuWindow.DEFAULT_MENU_X;
        public int MainMenuY = MainMenuWindow.DEFAULT_MENU_Y;
        public bool MainMenuPosLocked = false;

        /// <summary>
        /// Already displayed tutorial messages
        /// </summary>
        public string[] DisplayedTutorialMessages = new string[0];

        /// <summary>
        /// Determines if tutorial messages shall show up
        /// </summary>
        public bool EnableTutorial = true;

        /// <summary>Determines if the main menu shall be displayed in a tiny format.</summary>
        [Obsolete("Do not use. TM:PE now has UI scale slider")]
        public bool TinyMainMenu = true;

        /// <summary>User interface transparency, unit: percents, range: 0..100.</summary>
        [Obsolete("Value is not used anymore, use GuiOpacity instead")]
        public byte GuiTransparency = 75;

        /// <summary>User interface opacity, unit: percents, range: 0..100.</summary>
        public byte GuiOpacity = 75;

        /// <summary>User interface scale for TM:PE. Unit: percents, range: 30..200f.</summary>
        public float GuiScale = 100f;

        /// <summary>
        /// Overlay transparency
        /// </summary>
        public byte OverlayTransparency = 40;

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

        /// <summary>
        /// Selected theme for road signs when MPH is active.
        /// </summary>
        public MphSignStyle MphRoadSignStyle = MphSignStyle.SquareUS;

        public void AddDisplayedTutorialMessage(string messageKey) {
            HashSet<string> newMessages = DisplayedTutorialMessages != null
                                              ? new HashSet<string>(DisplayedTutorialMessages)
                                              : new HashSet<string>();
            newMessages.Add(messageKey);
            DisplayedTutorialMessages = newMessages.ToArray();
        }
    }
}
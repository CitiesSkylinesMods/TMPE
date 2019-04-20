using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.UI.MainMenu;

namespace TrafficManager.State.ConfigData {
	public class Main {
		/// <summary>
		/// Main menu button position
		/// </summary>
		public int MainMenuButtonX = 464;
		public int MainMenuButtonY = 10;
		public bool MainMenuButtonPosLocked = false;

		/// <summary>
		/// Main menu position
		/// </summary>
		public int MainMenuX = MainMenuPanel.DEFAULT_MENU_X;
		public int MainMenuY = MainMenuPanel.DEFAULT_MENU_Y;
		public bool MainMenuPosLocked = false;

		/// <summary>
		/// Already displayed tutorial messages
		/// </summary>
		public string[] DisplayedTutorialMessages = new string[0];

		/// <summary>
		/// Determines if tutorial messages shall show up
		/// </summary>
		public bool EnableTutorial = true;

		/// <summary>
		/// Determines if the main menu shall be displayed in a tiny format
		/// </summary>
		public bool TinyMainMenu = true;

		/// <summary>
		/// User interface transparency
		/// </summary>
		public byte GuiTransparency = 30;

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

		public void AddDisplayedTutorialMessage(string messageKey) {
			HashSet<string> newMessages = DisplayedTutorialMessages != null ? new HashSet<string>(DisplayedTutorialMessages) : new HashSet<string>();
			newMessages.Add(messageKey);
			DisplayedTutorialMessages = newMessages.ToArray();
		}
	}
}

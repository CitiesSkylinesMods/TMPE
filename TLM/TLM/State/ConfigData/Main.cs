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

		public void AddDisplayedTutorialMessage(string messageKey) {
			HashSet<string> newMessages = DisplayedTutorialMessages != null ? new HashSet<string>(DisplayedTutorialMessages) : new HashSet<string>();
			newMessages.Add(messageKey);
			DisplayedTutorialMessages = newMessages.ToArray();
		}
	}
}

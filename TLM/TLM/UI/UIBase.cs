using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.UI.MainMenu;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI {
	public class UIBase : UICustomControl {

		public UIMainMenuButton MainMenuButton { get; private set; }
		public MainMenuPanel MainMenu { get; private set; }
#if DEBUG
		public DebugMenuPanel DebugMenu { get; private set; }
#endif
		private bool _uiShown = false;

		public UIBase() {
			Log._Debug("##### Initializing UIBase.");

			// Get the UIView object. This seems to be the top-level object for most
			// of the UI.
			var uiView = UIView.GetAView();

			// Add a new button to the view.
			MainMenuButton = (UIMainMenuButton)uiView.AddUIComponent(typeof(UIMainMenuButton));

			// add the menu
			MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
#if DEBUG
			DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif
		}

		~UIBase() {
			UnityEngine.Object.Destroy(MainMenuButton);
		}

		public bool IsVisible() {
			return _uiShown;
		}

		public void ToggleMainMenu() {
			if (IsVisible())
				Close();
			else
				Show();
		}

		internal void RebuildMenu() {
			Close();
			if (MainMenu != null) {
				UnityEngine.Object.Destroy(MainMenu);
#if DEBUG
				UnityEngine.Object.Destroy(DebugMenu);
#endif
			}
			var uiView = UIView.GetAView();
			MainMenu = (MainMenuPanel)uiView.AddUIComponent(typeof(MainMenuPanel));
#if DEBUG
			DebugMenu = (DebugMenuPanel)uiView.AddUIComponent(typeof(DebugMenuPanel));
#endif
		}

		public void Show() {
			try {
				ToolsModifierControl.mainToolbar.CloseEverything();
			} catch (Exception e) {
				Log.Error("Error on Show(): " + e.ToString());
			}

			foreach (MenuButton button in GetMenu().Buttons) {
				button.UpdateProperties();
			}
			GetMenu().Show();
#if DEBUG
			GetDebugMenu().Show();
#endif
			LoadingExtension.SetToolMode(TrafficManagerMode.Activated);
			_uiShown = true;
			MainMenuButton.UpdateSprites();
		}

		public void Close() {
			var uiView = UIView.GetAView();
			GetMenu().Hide();
#if DEBUG
			GetDebugMenu().Hide();
			DebugMenuPanel.deactivateButtons();
#endif
			LoadingExtension.TrafficManagerTool.SetToolMode(ToolMode.None);
			LoadingExtension.SetToolMode(TrafficManagerMode.None);
			_uiShown = false;
			MainMenuButton.UpdateSprites();
		}

		internal MainMenuPanel GetMenu() {
			return MainMenu;
		}

#if DEBUG
		internal DebugMenuPanel GetDebugMenu() {
			return DebugMenu;
		}
#endif
	}
}

using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI {
#if !TAM
	public class UIBase : UICustomControl {

		private UIMainMenuButton button;
		public static UITrafficManager menu { get; private set; }
		private bool _uiShown = false;

		public UIBase() {
			Log._Debug("##### Initializing UIBase.");

			// Get the UIView object. This seems to be the top-level object for most
			// of the UI.
			var uiView = UIView.GetAView();

			// Add a new button to the view.
			button = (UIMainMenuButton)uiView.AddUIComponent(typeof(UIMainMenuButton));

			// add the menu
			menu = (UITrafficManager)uiView.AddUIComponent(typeof(UITrafficManager));
		}

		~UIBase() {
			UnityEngine.Object.Destroy(button);
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
			if (menu != null) {
				UnityEngine.Object.Destroy(menu);
			}
			var uiView = UIView.GetAView();
			menu = (UITrafficManager)uiView.AddUIComponent(typeof(UITrafficManager));
		}

		public void Show() {
			try {
				ToolsModifierControl.mainToolbar.CloseEverything();
			} catch (Exception e) {
				Log.Error("Error on Show(): " + e.ToString());
			}

			GetMenu().Show();
			LoadingExtension.SetToolMode(TrafficManagerMode.Activated);
			_uiShown = true;
			button.UpdateSprites();
		}

		public void Close() {
			var uiView = UIView.GetAView();
			GetMenu().Hide();

			UITrafficManager.deactivateButtons();
			TrafficManagerTool.SetToolMode(ToolMode.None);
			LoadingExtension.SetToolMode(TrafficManagerMode.None);
			_uiShown = false;
			button.UpdateSprites();
		}

		internal static UITrafficManager GetMenu() {
			return menu;
		}
	}
#endif
}

using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using TrafficManager.TrafficLight;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI {
#if !TAM
	public class UIBase : UICustomControl {
		
		private UIMainMenuButton button;
		private bool _uiShown = false;

		public UIBase() {
			Log._Debug("##### Initializing UIBase.");

			// Get the UIView object. This seems to be the top-level object for most
			// of the UI.
			var uiView = UIView.GetAView();

			// Add a new button to the view.
			button = (UIMainMenuButton)uiView.AddUIComponent(typeof(UIMainMenuButton));
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

		public void Show() {
			if (LoadingExtension.Instance != null) {
				try {
					ToolsModifierControl.mainToolbar.CloseEverything();
				} catch (Exception e) {
					Log.Error("Error on Show(): " + e.ToString());
				}
				GetMenu().Show();
				LoadingExtension.Instance.SetToolMode(TrafficManagerMode.Activated);
				_uiShown = true;
				button.UpdateSprites();
			} else {
				Log._Debug("TM UI Show: LoadingExtension.Instance is null!");
			}
		}

		public void Close() {
			if (LoadingExtension.Instance != null) {
				var uiView = UIView.GetAView();
				var trafficManager = uiView.FindUIComponent("UITrafficManager");
				if (trafficManager != null) {
					Log._Debug("Hiding TM UI");
					Destroy(trafficManager);
					//trafficManager.Hide();
				} else {
					Log._Debug("Hiding TM UI: null!");
				}

				UITrafficManager.deactivateButtons();
				TrafficManagerTool.SetToolMode(ToolMode.None);
				LoadingExtension.Instance.SetToolMode(TrafficManagerMode.None);
				_uiShown = false;
				button.UpdateSprites();
			} else {
				Log._Debug("TM UI Close: LoadingExtension.Instance is null!");
			}
		}

		internal static UITrafficManager GetMenu() {
			var uiView = UIView.GetAView();
			var menu = uiView.FindUIComponent("UITrafficManager");
			if (menu != null) {
				return (UITrafficManager)menu;
			} else {
				uiView.AddUIComponent(typeof(UITrafficManager));
				return (UITrafficManager)uiView.FindUIComponent("UITrafficManager");
			}
		}
	}
#endif
}

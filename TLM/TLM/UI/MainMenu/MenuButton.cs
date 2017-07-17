using ColossalFramework.Math;
using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public abstract class MenuButton : LinearSpriteButton {
		public enum ButtonFunction {
			LaneConnector,
			ClearTraffic,
			DespawnDisabled,
			DespawnEnabled,
			JunctionRestrictions,
			LaneArrows,
			ManualTrafficLights,
			PrioritySigns,
			SpeedLimits,
			TimedTrafficLights,
			ToggleTrafficLights,
			VehicleRestrictions,
			ParkingRestrictions
		}

		public const string MENU_BUTTON = "TMPE_MenuButton";
		public const int BUTTON_SIZE = 30;
		public override void HandleClick(UIMouseEventParameter p) { }

		protected override void OnClick(UIMouseEventParameter p) {
			OnClickInternal(p);
			foreach (MenuButton button in LoadingExtension.BaseUI.MainMenu.Buttons) {
				button.UpdateProperties();
			}
		}

		public abstract void OnClickInternal(UIMouseEventParameter p);
		public abstract ButtonFunction Function { get; }

		public override bool CanActivate() {
			return true;
		}

		public override string ButtonName {
			get {
				return MENU_BUTTON;
			}
		}

		public override string FunctionName {
			get {
				return Function.ToString();
			}
		}

		public override string[] FunctionNames {
			get {
				var functions = Enum.GetValues(typeof(ButtonFunction));
				string[] ret = new string[functions.Length];
				for (int i = 0; i < functions.Length; ++i) {
					ret[i] = functions.GetValue(i).ToString();
				}
				Log._Debug($"FunctionNames: {ret.ArrayToString()}");
				return ret;
			}
		}

		public override Texture2D AtlasTexture {
			get {
				return TextureResources.MainMenuButtonsTexture2D;
			}
		}

		public override int Width {
			get {
				return 30;
			}
		}

		public override int Height {
			get {
				return 30;
			}
		}
	}
}

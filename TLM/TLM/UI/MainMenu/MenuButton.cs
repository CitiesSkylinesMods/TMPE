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
	public abstract class MenuButton : UIButton {
		public enum ButtonMouseState {
			Base,
			Hovered,
			MouseDown
		}

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

		public const string MENU_BUTTON_BACKGROUND = "Bg";
		public const string MENU_BUTTON_FOREGROUND = "Fg";

		public const string MENU_BUTTON_BASE = "Base";
		public const string MENU_BUTTON_HOVERED = "Hovered";
		public const string MENU_BUTTON_MOUSEDOWN = "MouseDown";

		public const string MENU_BUTTON_DEFAULT = "Default";
		public const string MENU_BUTTON_ACTIVE = "Active";

		public const int BUTTON_SIZE = 30;

		protected static string GetButtonBackgroundTextureId(ButtonMouseState state, bool active) {
			string ret = MENU_BUTTON + MENU_BUTTON_BACKGROUND;

			switch (state) {
				case ButtonMouseState.Base:
					ret += MENU_BUTTON_BASE;
					break;
				case ButtonMouseState.Hovered:
					ret += MENU_BUTTON_HOVERED;
					break;
				case ButtonMouseState.MouseDown:
					ret += MENU_BUTTON_MOUSEDOWN;
					break;
			}

			ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
			return ret;
		}

		protected static string GetButtonForegroundTextureId(ButtonFunction function, bool active) {
			string ret = MENU_BUTTON + MENU_BUTTON_FOREGROUND + function.ToString();
			ret += active ? MENU_BUTTON_ACTIVE : MENU_BUTTON_DEFAULT;
			return ret;
		}

		public override void Start() {
			string[] textureIds = new string[Enum.GetValues(typeof(ButtonMouseState)).Length * 2 + Enum.GetValues(typeof(ButtonFunction)).Length * 2];

			int i = 0;
			foreach (ButtonMouseState mouseState in EnumUtil.GetValues<ButtonMouseState>()) {
				textureIds[i++] = GetButtonBackgroundTextureId(mouseState, true);
				textureIds[i++] = GetButtonBackgroundTextureId(mouseState, false);
			}

			foreach (ButtonFunction function in EnumUtil.GetValues<ButtonFunction>()) {
				textureIds[i++] = GetButtonForegroundTextureId(function, false);
			}

			foreach (ButtonFunction function in EnumUtil.GetValues<ButtonFunction>()) {
				textureIds[i++] = GetButtonForegroundTextureId(function, true);
			}

			// Set the atlases for background/foreground
			atlas = TextureUtil.GenerateLinearAtlas("TMPE_MainMenuButtonsAtlas", TextureResources.MainMenuButtonsTexture2D, textureIds.Length, textureIds);
			
			UpdateProperties();

			// Set the button dimensions.
			width = BUTTON_SIZE;
			height = BUTTON_SIZE;

			// Enable button sounds.
			playAudioEvents = true;
		}

		protected override void OnClick(UIMouseEventParameter p) {
			OnClickInternal(p);
			foreach (MenuButton button in LoadingExtension.BaseUI.MainMenu.Buttons) {
				button.UpdateProperties();
			}
		}

		public abstract void OnClickInternal(UIMouseEventParameter p);
		public abstract ButtonFunction Function { get; }
		public abstract bool Active { get; }
		public abstract string Tooltip { get; }
		public abstract bool Visible { get; }

		internal void UpdateProperties() {
			m_BackgroundSprites.m_Normal = m_BackgroundSprites.m_Disabled = m_BackgroundSprites.m_Focused = GetButtonBackgroundTextureId(ButtonMouseState.Base, Active);
			m_BackgroundSprites.m_Hovered = GetButtonBackgroundTextureId(ButtonMouseState.Hovered, Active);
			m_PressedBgSprite = GetButtonBackgroundTextureId(ButtonMouseState.MouseDown, Active);

			m_ForegroundSprites.m_Normal = m_ForegroundSprites.m_Disabled = m_ForegroundSprites.m_Focused = GetButtonForegroundTextureId(Function, Active);
			m_ForegroundSprites.m_Hovered = m_PressedFgSprite = GetButtonForegroundTextureId(Function, true);

			tooltip = Translation.GetString(Tooltip);
			isVisible = Visible;
			this.Invalidate();
		}
	}
}

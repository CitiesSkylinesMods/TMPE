using ColossalFramework.Math;
using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.UI {
	public class UIMainMenuButton : UIButton {
		public const string MAIN_MENU_BUTTON_BG_BASE = "TMPE_MainMenuButtonBgBase";
		public const string MAIN_MENU_BUTTON_BG_HOVERED = "TMPE_MainMenuButtonBgHovered";
		public const string MAIN_MENU_BUTTON_BG_ACTIVE = "TMPE_MainMenuButtonBgActive";
		public const string MAIN_MENU_BUTTON_FG_BASE = "TMPE_MainMenuButtonFgBase";
		public const string MAIN_MENU_BUTTON_FG_HOVERED = "TMPE_MainMenuButtonFgHovered";
		public const string MAIN_MENU_BUTTON_FG_ACTIVE = "TMPE_MainMenuButtonFgActive";

		public override void Start() {
			// Place the button.
			GlobalConfig config = GlobalConfig.Instance;

			Vector3 pos = new Vector3(config.MainMenuButtonX, config.MainMenuButtonY);
			ClampPosToScreen(ref pos);
			absolutePosition = pos;

			// Set the atlas and background/foreground
			atlas = TextureUtil.GenerateLinearAtlas("TMPE_MainMenuButtonAtlas", TrafficLightToolTextureResources.MainMenuButtonTexture2D, 6, new string[] {
				MAIN_MENU_BUTTON_BG_BASE,
				MAIN_MENU_BUTTON_BG_HOVERED,
				MAIN_MENU_BUTTON_BG_ACTIVE,
				MAIN_MENU_BUTTON_FG_BASE,
				MAIN_MENU_BUTTON_FG_HOVERED,
				MAIN_MENU_BUTTON_FG_ACTIVE
			});
			
			UpdateSprites();

			// Set the button dimensions.
			width = 50;
			height = 50;

			// Enable button sounds.
			playAudioEvents = true;

			var dragHandler = new GameObject("TMPE_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			var drag = dragHandler.AddComponent<UIDragHandle>();

			drag.width = width;
			drag.height = height;
		}

		protected override void OnClick(UIMouseEventParameter p) {
			LoadingExtension.BaseUI.ToggleMainMenu();
			UpdateSprites();
		}

		private void ClampPosToScreen(ref Vector3 pos) {
			if (pos.x < 0)
				pos.x = 0;
			if (pos.y < 0)
				pos.y = 0;
			if (pos.x >= Screen.currentResolution.width)
				pos.x = Screen.currentResolution.width - 1;
			if (pos.y >= Screen.currentResolution.height)
				pos.y = Screen.currentResolution.height - 1;
		}

		protected override void OnPositionChanged() {
			GlobalConfig config = GlobalConfig.Instance;

			bool posChanged = (config.MainMenuButtonX != (int)absolutePosition.x || config.MainMenuButtonY != (int)absolutePosition.y);

			if (posChanged) {
				Log._Debug($"Button position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.MainMenuButtonX = (int)absolutePosition.x;
				config.MainMenuButtonY = (int)absolutePosition.y;

				GlobalConfig.WriteConfig();
			}
			base.OnPositionChanged();
		}

		internal void UpdateSprites() {
			if (! LoadingExtension.BaseUI.IsVisible()) {
				normalBgSprite = disabledBgSprite = focusedBgSprite = MAIN_MENU_BUTTON_BG_BASE;
				hoveredBgSprite = MAIN_MENU_BUTTON_BG_HOVERED;
				pressedBgSprite = MAIN_MENU_BUTTON_BG_ACTIVE;

				normalFgSprite = disabledFgSprite = focusedFgSprite = MAIN_MENU_BUTTON_FG_BASE;
				hoveredFgSprite = MAIN_MENU_BUTTON_FG_HOVERED;
				pressedFgSprite = MAIN_MENU_BUTTON_FG_ACTIVE;
			} else {
				normalBgSprite = disabledBgSprite = focusedBgSprite = hoveredBgSprite = MAIN_MENU_BUTTON_BG_ACTIVE;
				pressedBgSprite = MAIN_MENU_BUTTON_BG_HOVERED;

				normalFgSprite = disabledFgSprite = focusedFgSprite = hoveredFgSprite = MAIN_MENU_BUTTON_FG_ACTIVE;
				pressedFgSprite = MAIN_MENU_BUTTON_FG_HOVERED;
			}
		}
	}
}

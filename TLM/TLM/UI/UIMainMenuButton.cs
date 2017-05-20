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

namespace TrafficManager.UI {
	public class UIMainMenuButton : UIButton {
		public const string MAIN_MENU_BUTTON_BG_BASE = "TMPE_MainMenuButtonBgBase";
		public const string MAIN_MENU_BUTTON_BG_HOVERED = "TMPE_MainMenuButtonBgHovered";
		public const string MAIN_MENU_BUTTON_BG_ACTIVE = "TMPE_MainMenuButtonBgActive";
		public const string MAIN_MENU_BUTTON_FG_BASE = "TMPE_MainMenuButtonFgBase";
		public const string MAIN_MENU_BUTTON_FG_HOVERED = "TMPE_MainMenuButtonFgHovered";
		public const string MAIN_MENU_BUTTON_FG_ACTIVE = "TMPE_MainMenuButtonFgActive";

		public UIDragHandle Drag { get; private set; }

		public override void Start() {
			// Place the button.
			GlobalConfig config = GlobalConfig.Instance;
			Vector3 pos = new Vector3(config.MainMenuButtonX, config.MainMenuButtonY);
			VectorUtil.ClampPosToScreen(ref pos);
			absolutePosition = pos;

			// Set the atlas and background/foreground
			atlas = TextureUtil.GenerateLinearAtlas("TMPE_MainMenuButtonAtlas", TextureResources.MainMenuButtonTexture2D, 6, new string[] {
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

			var dragHandler = new GameObject("TMPE_MainButton_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			Drag = dragHandler.AddComponent<UIDragHandle>();

			Drag.width = width;
			Drag.height = height;
			Drag.enabled = !GlobalConfig.Instance.MainMenuButtonPosLocked;
		}

		internal void SetPosLock(bool lck) {
			Drag.enabled = !lck;
		}

		protected override void OnClick(UIMouseEventParameter p) {
			Log._Debug($"Current tool: {ToolManager.instance.m_properties.CurrentTool}");
			LoadingExtension.BaseUI.ToggleMainMenu();
			UpdateSprites();
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
				m_BackgroundSprites.m_Normal = m_BackgroundSprites.m_Disabled = m_BackgroundSprites.m_Focused = MAIN_MENU_BUTTON_BG_BASE;
				m_BackgroundSprites.m_Hovered = MAIN_MENU_BUTTON_BG_HOVERED;
				m_PressedBgSprite = MAIN_MENU_BUTTON_BG_ACTIVE;

				m_ForegroundSprites.m_Normal = m_ForegroundSprites.m_Disabled = m_ForegroundSprites.m_Focused = MAIN_MENU_BUTTON_FG_BASE;
				m_ForegroundSprites.m_Hovered = MAIN_MENU_BUTTON_FG_HOVERED;
				m_PressedFgSprite = MAIN_MENU_BUTTON_FG_ACTIVE;
			} else {
				m_BackgroundSprites.m_Normal = m_BackgroundSprites.m_Disabled = m_BackgroundSprites.m_Focused = m_BackgroundSprites.m_Hovered = MAIN_MENU_BUTTON_BG_ACTIVE;
				m_PressedBgSprite = MAIN_MENU_BUTTON_BG_HOVERED;

				m_ForegroundSprites.m_Normal = m_ForegroundSprites.m_Disabled = m_ForegroundSprites.m_Focused = m_ForegroundSprites.m_Hovered = MAIN_MENU_BUTTON_FG_ACTIVE;
				m_PressedFgSprite = MAIN_MENU_BUTTON_FG_HOVERED;
			}
			this.Invalidate();
		}
	}
}

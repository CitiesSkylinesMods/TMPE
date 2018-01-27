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
	public class UIMainMenuButton : UIButton, IObserver<GlobalConfig> {
		public const string MAIN_MENU_BUTTON_BG_BASE = "TMPE_MainMenuButtonBgBase";
		public const string MAIN_MENU_BUTTON_BG_HOVERED = "TMPE_MainMenuButtonBgHovered";
		public const string MAIN_MENU_BUTTON_BG_ACTIVE = "TMPE_MainMenuButtonBgActive";
		public const string MAIN_MENU_BUTTON_FG_BASE = "TMPE_MainMenuButtonFgBase";
		public const string MAIN_MENU_BUTTON_FG_HOVERED = "TMPE_MainMenuButtonFgHovered";
		public const string MAIN_MENU_BUTTON_FG_ACTIVE = "TMPE_MainMenuButtonFgActive";

		public const int BUTTON_WIDTH = 50;
		public const int BUTTON_HEIGHT = 50;

		public UIDragHandle Drag { get; private set; }

		IDisposable confDisposable;

		public override void Start() {
			// Place the button.
			OnUpdate(GlobalConfig.Instance);

			confDisposable = GlobalConfig.Instance.Subscribe(this);

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
			width = BUTTON_WIDTH;
			height = BUTTON_HEIGHT;

			// Enable button sounds.
			playAudioEvents = true;

			var dragHandler = new GameObject("TMPE_MainButton_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			Drag = dragHandler.AddComponent<UIDragHandle>();

			Drag.width = width;
			Drag.height = height;
			Drag.enabled = !GlobalConfig.Instance.Main.MainMenuButtonPosLocked;
		}

		public override void OnDestroy() {
			if (confDisposable != null) {
				confDisposable.Dispose();
			}
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

			bool posChanged = (config.Main.MainMenuButtonX != (int)absolutePosition.x || config.Main.MainMenuButtonY != (int)absolutePosition.y);

			if (posChanged) {
				Log._Debug($"Button position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.Main.MainMenuButtonX = (int)absolutePosition.x;
				config.Main.MainMenuButtonY = (int)absolutePosition.y;

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

		public void OnUpdate(IObservable<GlobalConfig> observable) {
			GlobalConfig config = (GlobalConfig)observable;
			UpdatePosition(new Vector2(config.Main.MainMenuButtonX, config.Main.MainMenuButtonY));
		}

		public void UpdatePosition(Vector2 pos) {
			Rect rect = new Rect(pos.x, pos.y, BUTTON_WIDTH, BUTTON_HEIGHT);
			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			VectorUtil.ClampRectToScreen(ref rect, resolution);
			Log.Info($"Setting main menu button position to [{pos.x},{pos.y}]");
			absolutePosition = rect.position;
			Invalidate();
		}
	}
}

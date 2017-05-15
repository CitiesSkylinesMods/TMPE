#define QUEUEDSTATSx
#define EXTRAPFx

using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Custom.PathFinding;
using System.Collections.Generic;
using TrafficManager.Manager;
using CSUtil.Commons;

namespace TrafficManager.UI.MainMenu {
	public class MainMenuPanel : UIPanel {
		private static readonly Type[] MENU_BUTTON_TYPES = new Type[] {
			// first row
			typeof(ToggleTrafficLightsButton),
			typeof(ManualTrafficLightsButton),
			typeof(LaneArrowsButton),
			typeof(LaneConnectorButton),
			typeof(DespawnButton),
			typeof(ClearTrafficButton),
			// second row
			typeof(PrioritySignsButton),
			typeof(TimedTrafficLightsButton),
			typeof(JunctionRestrictionsButton),
			typeof(SpeedLimitsButton),
			typeof(VehicleRestrictionsButton),
			typeof(ParkingRestrictionsButton),
		};
		private const int NUM_BUTTONS_PER_ROW = 6;
		private const int NUM_ROWS = 2;

		public const int VSPACING = 5;
		public const int HSPACING = 5;
		public const int TOP_BORDER = 25;
		public const int BUTTON_SIZE = 30;
		public const int MENU_WIDTH = 215;
		public const int MENU_HEIGHT = 95;

		public MenuButton[] Buttons { get; private set; }
		public UILabel VersionLabel { get; private set; }

		public UIDragHandle Drag { get; private set; }

		//private UILabel optionsLabel;

		public override void Start() {
			isVisible = false;

			backgroundSprite = "GenericPanel";
			color = new Color32(64, 64, 64, 240);
			width = MENU_WIDTH;
			height = MENU_HEIGHT;

			VersionLabel = AddUIComponent<VersionLabel>();
			//optionsLabel = AddUIComponent<OptionsLabel>();

			Buttons = new MenuButton[MENU_BUTTON_TYPES.Length];

			int i = 0;
			int y = TOP_BORDER;
			for (int row = 0; row < NUM_ROWS; ++row) {
				int x = HSPACING;
				for (int col = 0; col < NUM_BUTTONS_PER_ROW; ++col) {
					if (i >= Buttons.Length) {
						break;
					}

					MenuButton button = AddUIComponent(MENU_BUTTON_TYPES[i]) as MenuButton;
					button.relativePosition = new Vector3(x, y);
					Buttons[i++] = button;
					x += BUTTON_SIZE + HSPACING;
				}
				y += BUTTON_SIZE + VSPACING;
			}

			GlobalConfig config = GlobalConfig.Instance;
			Vector3 pos = new Vector3(config.MainMenuX, config.MainMenuY);
			VectorUtil.ClampPosToScreen(ref pos);
			absolutePosition = pos;

			var dragHandler = new GameObject("TMPE_Menu_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			Drag = dragHandler.AddComponent<UIDragHandle>();

			Drag.width = width;
			Drag.height = TOP_BORDER;
			Drag.enabled = !GlobalConfig.Instance.MainMenuPosLocked;
		}

		internal void SetPosLock(bool lck) {
			Drag.enabled = !lck;
		}

		protected override void OnPositionChanged() {
			GlobalConfig config = GlobalConfig.Instance;

			bool posChanged = (config.MainMenuX != (int)absolutePosition.x || config.MainMenuY != (int)absolutePosition.y);

			if (posChanged) {
				Log._Debug($"Menu position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.MainMenuX = (int)absolutePosition.x;
				config.MainMenuY = (int)absolutePosition.y;

				GlobalConfig.WriteConfig();
			}
			base.OnPositionChanged();
		}
	}
}

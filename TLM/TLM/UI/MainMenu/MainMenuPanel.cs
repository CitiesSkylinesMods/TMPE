#define QUEUEDSTATSx

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
using TrafficManager.Util;

namespace TrafficManager.UI.MainMenu {
	public class MainMenuPanel : UIPanel, IObserver<GlobalConfig> {
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

		public class SizeProfile {
			public int NUM_BUTTONS_PER_ROW { get; set; }
			public int NUM_ROWS { get; set; }

			public int VSPACING { get; set; }
			public int HSPACING { get; set; }
			public int TOP_BORDER { get; set; }
			public int BUTTON_SIZE { get; set; }

			public int MENU_WIDTH { get; set; }
			public int MENU_HEIGHT { get; set; }
		}

		public static readonly SizeProfile[] SIZE_PROFILES = new SizeProfile[] {
			new SizeProfile() {
				NUM_BUTTONS_PER_ROW = 6,
				NUM_ROWS = 2,

				VSPACING = 5,
				HSPACING = 5,
				TOP_BORDER = 25,
				BUTTON_SIZE = 30,

				MENU_WIDTH = 215,
				MENU_HEIGHT = 95
			},
			new SizeProfile() {
				NUM_BUTTONS_PER_ROW = 6,
				NUM_ROWS = 2,

				VSPACING = 5,
				HSPACING = 5,
				TOP_BORDER = 25,
				BUTTON_SIZE = 50,

				MENU_WIDTH = 335,
				MENU_HEIGHT = 135
			}
		};

		public const int DEFAULT_MENU_X = 85;
		public const int DEFAULT_MENU_Y = 60;

		public MenuButton[] Buttons { get; private set; }
		public UILabel VersionLabel { get; private set; }
		public UILabel StatsLabel { get; private set; }

		public UIDragHandle Drag { get; private set; }

		IDisposable confDisposable;

		private SizeProfile activeProfile = null;
		private bool started = false;

		//private UILabel optionsLabel;

		public override void Start() {
			GlobalConfig conf = GlobalConfig.Instance;
			DetermineProfile(conf);

			OnUpdate(conf);

			confDisposable = conf.Subscribe(this);

			isVisible = false;

			backgroundSprite = "GenericPanel";
			color = new Color32(64, 64, 64, 240);

			VersionLabel = AddUIComponent<VersionLabel>();
			StatsLabel = AddUIComponent<StatsLabel>();

			Buttons = new MenuButton[MENU_BUTTON_TYPES.Length];
			for (int i = 0; i < MENU_BUTTON_TYPES.Length; ++i) {
				Buttons[i] = AddUIComponent(MENU_BUTTON_TYPES[i]) as MenuButton;
			}

			var dragHandler = new GameObject("TMPE_Menu_DragHandler");
			dragHandler.transform.parent = transform;
			dragHandler.transform.localPosition = Vector3.zero;
			Drag = dragHandler.AddComponent<UIDragHandle>();
			Drag.enabled = !GlobalConfig.Instance.Main.MainMenuPosLocked;

			UpdateAllSizes();
			started = true;
		}

		public override void OnDestroy() {
			if (confDisposable != null) {
				confDisposable.Dispose();
			}
		}

		internal void SetPosLock(bool lck) {
			Drag.enabled = !lck;
		}

		protected override void OnPositionChanged() {
			GlobalConfig config = GlobalConfig.Instance;

			bool posChanged = (config.Main.MainMenuX != (int)absolutePosition.x || config.Main.MainMenuY != (int)absolutePosition.y);

			if (posChanged) {
				Log._Debug($"Menu position changed to {absolutePosition.x}|{absolutePosition.y}");

				config.Main.MainMenuX = (int)absolutePosition.x;
				config.Main.MainMenuY = (int)absolutePosition.y;

				GlobalConfig.WriteConfig();
			}
			base.OnPositionChanged();
		}

		public void OnUpdate(IObservable<GlobalConfig> observable) {
			GlobalConfig config = (GlobalConfig)observable;
			UpdatePosition(new Vector2(config.Main.MainMenuX, config.Main.MainMenuY));
			if (started) {
				DetermineProfile(config);
				UpdateAllSizes();
				Invalidate();
			}
		}

		private void DetermineProfile(GlobalConfig conf) {
			int profileIndex = conf.Main.TinyMainMenu ? 0 : 1;
			activeProfile = SIZE_PROFILES[profileIndex];
		}

		public void UpdateAllSizes() {
			UpdateSize();
			UpdateDragSize();
			UpdateButtons();
		}

		private void UpdateSize() {
			width = activeProfile.MENU_WIDTH;
			height = activeProfile.MENU_HEIGHT;
		}

		private void UpdateDragSize() {
			Drag.width = width;
			Drag.height = activeProfile.TOP_BORDER;
		}

		private void UpdateButtons() {
			int i = 0;
			int y = activeProfile.TOP_BORDER;
			for (int row = 0; row < activeProfile.NUM_ROWS; ++row) {
				int x = activeProfile.HSPACING;
				for (int col = 0; col < activeProfile.NUM_BUTTONS_PER_ROW; ++col) {
					if (i >= Buttons.Length) {
						break;
					}

					MenuButton button = Buttons[i];
					button.relativePosition = new Vector3(x, y);
					button.width = activeProfile.BUTTON_SIZE;
					button.height = activeProfile.BUTTON_SIZE;
					button.Invalidate();
					Buttons[i++] = button;
					x += activeProfile.BUTTON_SIZE + activeProfile.HSPACING;
				}
				y += activeProfile.BUTTON_SIZE + activeProfile.VSPACING;
			}
		}

		public void UpdatePosition(Vector2 pos) {
			Rect rect = new Rect(pos.x, pos.y, activeProfile.MENU_WIDTH, activeProfile.MENU_HEIGHT);
			Vector2 resolution = UIView.GetAView().GetScreenResolution();
			VectorUtil.ClampRectToScreen(ref rect, resolution);
			Log.Info($"Setting main menu position to [{pos.x},{pos.y}]");
			absolutePosition = rect.position;
			Invalidate();
		}
	}
}

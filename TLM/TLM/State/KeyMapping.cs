// Based on keymapping module from CS-MoveIt mod
// Thanks to https://github.com/Quboid/CS-MoveIt

using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using CSUtil.Commons;
using TrafficManager.UI;
using UnityEngine;

namespace TrafficManager.State {
	public class OptionsKeymappingMain : OptionsKeymapping {
		private void Awake() {
			TryCreateConfig();
			AddKeymapping(Translation.GetString("Keybind_toggle_TMPE_main_menu"),
			              KeyToggleTMPEMainMenu);

			AddKeymapping(Translation.GetString("Keybind_toggle_traffic_lights_tool"),
			              KeyToggleTrafficLightTool);
			AddKeymapping(Translation.GetString("Keybind_use_lane_arrow_tool"),
			              KeyLaneArrowTool);
			AddKeymapping(Translation.GetString("Keybind_use_lane_connections_tool"),
			              KeyLaneConnectionsTool);
			AddKeymapping(Translation.GetString("Keybind_use_priority_signs_tool"),
			              KeyPrioritySignsTool);
			AddKeymapping(Translation.GetString("Keybind_use_junction_restrictions_tool"),
			              KeyJunctionRestrictionsTool);
			AddKeymapping(Translation.GetString("Keybind_use_speed_limits_tool"),
			              KeySpeedLimitsTool);

			AddKeymapping(Translation.GetString("Keybind_lane_connector_stay_in_lane"),
			              KeyLaneConnectorStayInLane);
		}
	}

	public class OptionsKeymapping : UICustomControl {
		protected static readonly string KeyBindingTemplate = "KeyBindingTemplate";
		private const string KEYBOARD_SHORTCUTS_FILENAME = "TMPE_Keyboard";

		public static SavedInputKey KeyToggleTMPEMainMenu =
			new SavedInputKey("keyToggleTMPEMainMenu",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.T, true, false, true),
			                  true);

		public static SavedInputKey KeyToggleTrafficLightTool =
			new SavedInputKey("keyToggleTrafficLightTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.T, true, true, false),
			                  true);

		public static SavedInputKey KeyLaneArrowTool =
			new SavedInputKey("keyLaneArrowTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.A, true, true, false),
			                  true);

		public static SavedInputKey KeyLaneConnectionsTool =
			new SavedInputKey("keyLaneConnectionsTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.C, true, true, false),
			                  true);

		public static SavedInputKey KeyPrioritySignsTool =
			new SavedInputKey("keyPrioritySignsTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.P, true, true, false),
			                  true);

		public static SavedInputKey KeyJunctionRestrictionsTool =
			new SavedInputKey("keyJunctionRestrictionsTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.J, true, true, false),
			                  true);

		public static SavedInputKey KeySpeedLimitsTool =
			new SavedInputKey("keySpeedLimitsTool",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.S, true, true, false),
			                  true);

		public static SavedInputKey KeyLaneConnectorStayInLane =
			new SavedInputKey("keyLaneConnectorStayInLane",
			                  KEYBOARD_SHORTCUTS_FILENAME,
			                  SavedInputKey.Encode(KeyCode.S, false, true, false),
			                  true);

		private SavedInputKey editingBinding_;
		private string editingBindingCategory_;

		private int count_;

		protected void TryCreateConfig() {
			try {
				// Creating setting file
				if (GameSettings.FindSettingsFileByName(KEYBOARD_SHORTCUTS_FILENAME) == null) {
					GameSettings.AddSettingsFile(new SettingsFile
					                             {fileName = KEYBOARD_SHORTCUTS_FILENAME});
				}
			}
			catch (Exception) {
				Log._Debug("Could not load/create the keyboard shortcuts file.");
			}
		}

		/// <summary>
		/// Creates a row in the current panel with the label and the button which will prompt user to press a new key.
		/// </summary>
		/// <param name="label">Text to display</param>
		/// <param name="savedInputKey">A SavedInputKey from GlobalConfig.KeyboardShortcuts</param>
		protected void AddKeymapping(string label, SavedInputKey savedInputKey) {
			var uiPanel =
				component.AttachUIComponent(UITemplateManager.GetAsGameObject(KeyBindingTemplate)) as
					UIPanel;
			if (count_++ % 2 == 1) uiPanel.backgroundSprite = null;

			// Create a label
			var uILabel = uiPanel.Find<UILabel>("Name");

			// Create a button which displays the shortcut and modifies it on click
			var uIButton = uiPanel.Find<UIButton>("Binding");
			uIButton.eventKeyDown += OnBindingKeyDown;
			uIButton.eventMouseDown += OnBindingMouseDown;

			// Set label text (as provided) and set button text from the SavedInputKey
			uILabel.text = label;
			uIButton.text = savedInputKey.ToLocalizedString("KEYNAME");
			uIButton.objectUserData = savedInputKey;
		}

		protected void OnEnable() {
			LocaleManager.eventLocaleChanged += OnLocaleChanged;
		}

		protected void OnDisable() {
			LocaleManager.eventLocaleChanged -= OnLocaleChanged;
		}

		protected void OnLocaleChanged() {
			RefreshBindableInputs();
		}

		protected bool IsModifierKey(KeyCode code) {
			return code == KeyCode.LeftControl || code == KeyCode.RightControl ||
			       code == KeyCode.LeftShift || code == KeyCode.RightShift || code == KeyCode.LeftAlt ||
			       code == KeyCode.RightAlt;
		}

		protected bool IsControlDown() {
			return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		}

		protected bool IsShiftDown() {
			return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		}

		protected bool IsAltDown() {
			return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		}

		protected bool IsUnbindableMouseButton(UIMouseButton code) {
			return code == UIMouseButton.Left || code == UIMouseButton.Right;
		}

		protected KeyCode ButtonToKeycode(UIMouseButton button) {
			if (button == UIMouseButton.Left) {
				return KeyCode.Mouse0;
			}

			if (button == UIMouseButton.Right) {
				return KeyCode.Mouse1;
			}

			if (button == UIMouseButton.Middle) {
				return KeyCode.Mouse2;
			}

			if (button == UIMouseButton.Special0) {
				return KeyCode.Mouse3;
			}

			if (button == UIMouseButton.Special1) {
				return KeyCode.Mouse4;
			}

			if (button == UIMouseButton.Special2) {
				return KeyCode.Mouse5;
			}

			if (button == UIMouseButton.Special3) {
				return KeyCode.Mouse6;
			}

			return KeyCode.None;
		}

		protected void OnBindingKeyDown(UIComponent comp, UIKeyEventParameter p) {
			// This will only work if the user clicked the modify button
			// otherwise no effect
			if (editingBinding_ != null && !IsModifierKey(p.keycode)) {
				p.Use();
				UIView.PopModal();
				var keycode = p.keycode;
				var inputKey = (p.keycode == KeyCode.Escape)
					               ? editingBinding_.value
					               : SavedInputKey.Encode(keycode, p.control, p.shift, p.alt);
				if (p.keycode == KeyCode.Backspace) {
					inputKey = SavedInputKey.Empty;
				}

				editingBinding_.value = inputKey;
				var uITextComponent = p.source as UITextComponent;
				uITextComponent.text = editingBinding_.ToLocalizedString("KEYNAME");
				editingBinding_ = null;
				editingBindingCategory_ = string.Empty;
			}
		}

		protected void OnBindingMouseDown(UIComponent comp, UIMouseEventParameter p) {
			// This will only work if the user is not in the process of changing the shortcut
			if (editingBinding_ == null) {
				p.Use();
				editingBinding_ = (SavedInputKey) p.source.objectUserData;
				editingBindingCategory_ = p.source.stringUserData;
				var uIButton = p.source as UIButton;
				uIButton.buttonsMask =
					UIMouseButton.Left | UIMouseButton.Right | UIMouseButton.Middle |
					UIMouseButton.Special0 | UIMouseButton.Special1 | UIMouseButton.Special2 |
					UIMouseButton.Special3;
				uIButton.text = "Press any key";
				p.source.Focus();
				UIView.PushModal(p.source);
			} else if (!IsUnbindableMouseButton(p.buttons)) {
				// This will work if the user clicks while the shortcut change is in progress
				p.Use();
				UIView.PopModal();
				var inputKey = SavedInputKey.Encode(ButtonToKeycode(p.buttons),
				                                    IsControlDown(), IsShiftDown(),
				                                    IsAltDown());

				editingBinding_.value = inputKey;
				var uIButton2 = p.source as UIButton;
				uIButton2.text = editingBinding_.ToLocalizedString("KEYNAME");
				uIButton2.buttonsMask = UIMouseButton.Left;
				editingBinding_ = null;
				editingBindingCategory_ = string.Empty;
			}
		}

		protected void RefreshBindableInputs() {
			foreach (var current in component.GetComponentsInChildren<UIComponent>()) {
				var uITextComponent = current.Find<UITextComponent>("Binding");
				if (uITextComponent != null) {
					var savedInputKey = uITextComponent.objectUserData as SavedInputKey;
					if (savedInputKey != null) {
						uITextComponent.text = savedInputKey.ToLocalizedString("KEYNAME");
					}
				}

				var uILabel = current.Find<UILabel>("Name");
				if (uILabel != null) {
					uILabel.text = Locale.Get("KEYMAPPING", uILabel.stringUserData);
				}
			}
		}

		protected InputKey GetDefaultEntry(string entryName) {
			var field =
				typeof(DefaultSettings).GetField(entryName, BindingFlags.Static | BindingFlags.Public);
			if (field == null) {
				return 0;
			}

			var value = field.GetValue(null);
			if (value is InputKey) {
				return (InputKey) value;
			}

			return 0;
		}

		protected void RefreshKeyMapping() {
			foreach (var current in component.GetComponentsInChildren<UIComponent>()) {
				var uITextComponent = current.Find<UITextComponent>("Binding");
				var savedInputKey = (SavedInputKey) uITextComponent.objectUserData;
				if (editingBinding_ != savedInputKey) {
					uITextComponent.text = savedInputKey.ToLocalizedString("KEYNAME");
				}
			}
		}
	}
}
// Based on keymapping module from CS-MoveIt mod
// Thanks to https://github.com/Quboid/CS-MoveIt
namespace TrafficManager.State.Keybinds {
    using System;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    public class KeybindSettingsBase : UICustomControl {
        private static void OnMainMenuShortcutChanged(SavedInputKey savedinputkey) {
            LoadingExtension.BaseUI.MainMenuButton.UpdateTooltip();
            Log.Info("Main menu shortcut changed");
        }

        protected static readonly string KeyBindingTemplate = "KeyBindingTemplate";
        public const string KEYBOARD_SHORTCUTS_FILENAME = "TMPE_Keybinds";

        /// <value>
        /// This input key can not be changed and is not checked, instead it is display only
        /// </value>
        protected static KeybindSetting ToolCancelViewOnly { get; } = new KeybindSetting(
            "Global",
            "Key_ExitSubtool",
            SavedInputKey.Encode(KeyCode.Escape, false, false, false));

        public static KeybindSetting ToggleMainMenu { get; } = new KeybindSetting(
            "Global",
            "Key_ToggleTMPEMainMenu",
            SavedInputKey.Encode(KeyCode.Semicolon, false, true, false));

        public static KeybindSetting ToggleTrafficLightTool { get; } =
            new KeybindSetting("Global", "Key_ToggleTrafficLightTool");

        public static KeybindSetting LaneArrowTool { get; } =
            new KeybindSetting("Global", "Key_LaneArrowTool");

        public static KeybindSetting LaneConnectionsTool { get; } =
            new KeybindSetting("Global", "Key_LaneConnectionsTool");

        public static KeybindSetting PrioritySignsTool { get; } =
            new KeybindSetting("Global", "Key_PrioritySignsTool");

        public static KeybindSetting JunctionRestrictionsTool { get; } =
            new KeybindSetting("Global", "Key_JunctionRestrictionsTool");

        public static KeybindSetting SpeedLimitsTool { get; } =
            new KeybindSetting("Global", "Key_SpeedLimitsTool");

        public static KeybindSetting LaneConnectorStayInLane { get; } = new KeybindSetting(
            "LaneConnector",
            "Key_LaneConnector_StayInLane",
            SavedInputKey.Encode(KeyCode.S, false, true, false));

        public static KeybindSetting LaneConnectorDelete { get; } = new KeybindSetting(
            "LaneConnector",
            "Key_LaneConnector_Delete",
            SavedInputKey.Encode(KeyCode.Delete, false, false, false),
            SavedInputKey.Encode(KeyCode.Backspace, false, false, false));

        private KeybindUI keybindUi_ = new KeybindUI();

        /// <summary>
        /// Counter to produce alternating UI row colors (dark and light).
        /// </summary>
        private int uiRowCount_;

        protected static void TryCreateConfig() {
            try {
                // Creating setting file
                if (GameSettings.FindSettingsFileByName(KEYBOARD_SHORTCUTS_FILENAME) == null) {
                    GameSettings.AddSettingsFile(
                        new SettingsFile {fileName = KEYBOARD_SHORTCUTS_FILENAME});
                }
            }
            catch (Exception) {
                Log._Debug("Could not load/create the keyboard shortcuts file.");
            }
        }

        protected void BeginForm() {
            keybindUi_.BeginForm(component);
        }

        /// <summary>
        /// Creates a row in the current panel with the label and the button
        /// which will prompt user to press a new key.
        /// </summary>
        /// <param name="label">Localized label</param>
        /// <param name="keybind">The setting to edit</param>
        protected void AddKeybindRowUI(string label, KeybindSetting keybind) {
            var settingsRow = keybindUi_.CreateRowPanel();
            if (uiRowCount_++ % 2 == 1) {
                settingsRow.backgroundSprite = null;
            }

            keybindUi_.CreateLabel(settingsRow, label);
            keybindUi_.CreateKeybindButton(settingsRow, keybind, keybind.Key);
        }

        /// <summary>
        /// Add a second key under the first key, using same row background as the
        /// previous key editor.
        /// </summary>
        /// <param name="keybind"></param>
        protected void AddAlternateUiControl(KeybindSetting keybind) {
            var settingsRow = keybindUi_.CreateRowPanel();
            if (uiRowCount_ % 2 == 1) {
                // color the panel but do not increment uiRowCount
                settingsRow.backgroundSprite = null;
            }

            keybindUi_.CreateLabel(settingsRow, string.Empty);
            keybindUi_.CreateKeybindButton(settingsRow, keybind, keybind.AlternateKey);
        }

        /// <summary>
        /// Creates a line of key mapping but does not allow changing it.
        /// Used to improve awareness.
        /// </summary>
        /// <param name="label">Localized label</param>
        /// <param name="keybind">The setting to edit</param>
        protected void ReadOnlyKeybindUI(string label, KeybindSetting keybind) {
            var settingsRow = keybindUi_.CreateRowPanel();
            if (uiRowCount_++ % 2 == 1) {
                settingsRow.backgroundSprite = null;
            }

            keybindUi_.CreateLabel(settingsRow, label);
            keybindUi_.CreateKeybindText(settingsRow, keybind.Key);
        }

        protected void OnEnable() {
            LocaleManager.eventLocaleChanged += OnLocaleChanged;
        }

        protected void OnDisable() {
            LocaleManager.eventLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged() {
            // RefreshBindableInputs();
        }

//        /// <summary>
//        /// Called on locale change, resets keys to the new language
//        /// </summary>
//        private void RefreshBindableInputs() {
//            foreach (var current in component.GetComponentsInChildren<UIComponent>()) {
//                var uITextComponent = current.Find<UITextComponent>("Binding");
//                if (uITextComponent != null) {
//                    var savedInputKey = uITextComponent.objectUserData as SavedInputKey;
//                    if (savedInputKey != null) {
//                        uITextComponent.text = Keybind.Str(savedInputKey);
//                    }
//                }
//
//                var uILabel = current.Find<UILabel>("Name");
//                if (uILabel != null) {
//                    uILabel.text = Locale.Get("KEYMAPPING", uILabel.stringUserData);
//                }
//            }
//        }
    }
}
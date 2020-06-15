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
        protected static readonly string KeyBindingTemplate = "KeyBindingTemplate";
        public const string KEYBOARD_SHORTCUTS_FILENAME = "TMPE_Keybinds";

        // NOTE: Do not change fields to properties, otherwise also fix the conflict
        // detection code in KeybindUI.FindConflict(inTmpe) which expects these below
        // to be fields of type KeybindSetting.

        /// <summary>
        /// This input key can not be changed and is not checked, instead it is display only
        /// </summary>
        // Not editable
        internal static KeybindSetting Esc = new KeybindSetting(
            cat: "Global",
            configFileKey: "Key_ExitSubtool",
            defaultKey1: SavedInputKey.Encode(key: KeyCode.Escape, control: false, shift: false, alt: false));

        // Not editable
        internal static KeybindSetting RightClick = new KeybindSetting(
            cat: "Global",
            configFileKey: "Key_RightClick",
            defaultKey1: SavedInputKey.Encode(key: KeyCode.Mouse1, control: false, shift: false, alt: false));

        public static KeybindSetting ToggleMainMenu = new KeybindSetting(
            cat: "Global",
            configFileKey: "Key_ToggleTMPEMainMenu",
            defaultKey1: SavedInputKey.Encode(key: KeyCode.Semicolon, control: false, shift: true, alt: false));

        public static KeybindSetting ToggleTrafficLightTool =
            new KeybindSetting("Global", "Key_ToggleTrafficLightTool");

        public static KeybindSetting LaneArrowTool =
            new KeybindSetting("Global", "Key_LaneArrowTool");

        public static KeybindSetting LaneConnectionsTool =
            new KeybindSetting("Global", "Key_LaneConnectionsTool");

        public static KeybindSetting PrioritySignsTool =
            new KeybindSetting("Global", "Key_PrioritySignsTool");

        public static KeybindSetting JunctionRestrictionsTool =
            new KeybindSetting("Global", "Key_JunctionRestrictionsTool");

        public static KeybindSetting SpeedLimitsTool =
            new KeybindSetting("Global", "Key_SpeedLimitsTool");

        public static KeybindSetting LaneConnectorStayInLane = new KeybindSetting(
            "LaneConnector",
            "Key_LaneConnector_StayInLane",
            SavedInputKey.Encode(KeyCode.S, control:true, shift:false, alt:false));

        public static KeybindSetting RestoreDefaultsKey = new KeybindSetting(
            "Global",
            "Key_RestoreDefaults",
            SavedInputKey.Encode(KeyCode.Delete, false, false, false),
            SavedInputKey.Encode(KeyCode.Backspace, false, false, false));

        protected KeybindUI keybindUi_ = new KeybindUI();

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

            keybindUi_.CreateLabel(settingsRow, label, 0.6f);
            keybindUi_.CreateKeybindButton(settingsRow, keybind, keybind.Key, 0.3f);
        }

        /// <summary>
        /// Add a second key under the first key, using same row background as the
        /// previous key editor.
        /// </summary>
        /// <param name="keybind"></param>
        /// <param name="editable1">Whether main key binding is editable or readonly</param>
        /// <param name="editable2">Whether alt key binding is editable or readonly</param>
        protected void AddAlternateKeybindUI(
            string title,
            KeybindSetting keybind,
            bool editable1,
            bool editable2) {
            var settingsRow = keybindUi_.CreateRowPanel();
            if (uiRowCount_ % 2 == 1) {
                // color the panel but do not increment uiRowCount
                settingsRow.backgroundSprite = null;
            }

            keybindUi_.CreateLabel(settingsRow, title, 0.45f);
            if (editable1) {
                keybindUi_.CreateKeybindButton(settingsRow, keybind, keybind.Key, 0.2f);
            } else {
                keybindUi_.CreateKeybindText(settingsRow, keybind.Key, 0.25f);
            }

            if (editable2) {
                keybindUi_.CreateKeybindButton(settingsRow, keybind, keybind.AlternateKey, 0.2f);
            } else {
                keybindUi_.CreateKeybindText(settingsRow, keybind.AlternateKey, 0.25f);
            }
        }

        /// <summary>
        /// Creates a line of key mapping but does not allow changing it.
        /// Used to improve awareness.
        /// </summary>
        /// <param name="label">Localized label</param>
        /// <param name="keybind">The setting to edit</param>
        protected void AddReadOnlyKeybind(string label, KeybindSetting keybind) {
            var settingsRow = keybindUi_.CreateRowPanel();
            if (uiRowCount_++ % 2 == 1) {
                settingsRow.backgroundSprite = null;
            }

            keybindUi_.CreateLabel(settingsRow, label, 0.6f);
            keybindUi_.CreateKeybindText(settingsRow, keybind.Key, 0.3f);
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
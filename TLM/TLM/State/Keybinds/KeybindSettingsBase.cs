// Based on keymapping module from CS-MoveIt mod
// Thanks to https://github.com/Quboid/CS-MoveIt
using TrafficManager.UI;

namespace TrafficManager.State.Keybinds {
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using ColossalFramework;
    using ColossalFramework.Globalization;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using UnityEngine;

    public class KeybindSettingsBase : UICustomControl {
        private static void OnMainMenuShortcutChanged(SavedInputKey savedinputkey) {
            LoadingExtension.BaseUI.MainMenuButton.UpdateTooltip();
            Log.Info("Main menu shortcut changed");
        }

        protected static readonly string KeyBindingTemplate = "KeyBindingTemplate";
        public const string KEYBOARD_SHORTCUTS_FILENAME = "TMPE_Keyboard";

        /// <value>
        /// This input key can not be changed and is not checked, instead it is display only
        /// </value>
        protected static KeybindSetting ToolCancelViewOnly { get; } = new KeybindSetting(
            "Global",
            "keyExitSubtool",
            SavedInputKey.Encode(KeyCode.Escape, false, false, false));

        public static KeybindSetting ToggleMainMenu { get; } = new KeybindSetting(
            "Global",
            "keyToggleTMPEMainMenu",
            SavedInputKey.Encode(KeyCode.Semicolon, false, true, false));

        public static KeybindSetting ToggleTrafficLightTool { get; } =
            new KeybindSetting("Global", "keyToggleTrafficLightTool");

        public static KeybindSetting LaneArrowTool { get; } =
            new KeybindSetting("Global", "keyLaneArrowTool");

        public static KeybindSetting LaneConnectionsTool { get; } =
            new KeybindSetting("Global", "keyLaneConnectionsTool");

        public static KeybindSetting PrioritySignsTool { get; } =
            new KeybindSetting("Global", "keyPrioritySignsTool");

        public static KeybindSetting JunctionRestrictionsTool { get; } =
            new KeybindSetting("Global", "keyJunctionRestrictionsTool");

        public static KeybindSetting SpeedLimitsTool { get; } =
            new KeybindSetting("Global", "keySpeedLimitsTool");

        public static KeybindSetting LaneConnectorStayInLane { get; } = new KeybindSetting(
            "LaneConnector",
            "keyLaneConnectorStayInLane",
            SavedInputKey.Encode(KeyCode.S, false, true, false));

        public static KeybindSetting LaneConnectorDelete { get; } = new KeybindSetting(
            "LaneConnector",
            "keyLaneConnectorDelete",
            SavedInputKey.Encode(KeyCode.Delete, false, false, false),
            SavedInputKey.Encode(KeyCode.Backspace, false, false, false));

        private KeybindSetting.Editable? currentlyEditedBinding_;

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
        protected void AddUiControl(string label, KeybindSetting keybind) {
            var uiPanel = component.AttachUIComponent(
                              UITemplateManager.GetAsGameObject(KeyBindingTemplate)) as UIPanel;
            if (uiRowCount_++ % 2 == 1) {
                uiPanel.backgroundSprite = null;
            }

            // Create a label
            var uiLabel = uiPanel.Find<UILabel>("Name");

            // Create a button which displays the shortcut and modifies it on click
            var uiButton = uiPanel.Find<UIButton>("Binding");
            uiButton.eventKeyDown += OnBindingKeyDown;
            uiButton.eventMouseDown += OnBindingMouseDown;
            uiButton.text = Keybind.Str(keybind.Key); // take the first key only

            // Tell the button handler that we're editing the main Key of this keybind
            uiButton.objectUserData
                = new KeybindSetting.Editable {
                                                  Target = keybind,
                                                  TargetKey = keybind.Key
                                              };

            // Set label text (as provided) and set button text from the SavedInputKey
            uiLabel.text = label;
        }

        /// <summary>
        /// Creates a line of key mapping but does not allow changing it.
        /// Used to improve awareness.
        /// </summary>
        /// <param name="label">Localized label</param>
        /// <param name="keybind">The setting to edit</param>
        protected void AddReadOnlyUi(string label, KeybindSetting keybind) {
            var uiPanel = component.AttachUIComponent(
                              UITemplateManager.GetAsGameObject(KeyBindingTemplate)) as UIPanel;
            if (uiRowCount_++ % 2 == 1) {
                uiPanel.backgroundSprite = null;
            }

            // Create a label
            var uiLabel = uiPanel.Find<UILabel>("Name");

            // Create a button which displays the shortcut and modifies it on click
            var uiReadOnlyKey = uiPanel.Find<UIButton>("Binding");
            uiReadOnlyKey.Disable();

            // Set label text (as provided) and set button text from the InputKey
            uiLabel.text = label;
            uiReadOnlyKey.text = keybind.Str();
        }

        protected void OnEnable() {
            LocaleManager.eventLocaleChanged += OnLocaleChanged;
        }

        protected void OnDisable() {
            LocaleManager.eventLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged() {
            RefreshBindableInputs();
        }

        private void OnBindingKeyDown(UIComponent comp, UIKeyEventParameter p) {
            // This will only work if the user clicked the modify button
            // otherwise no effect
            if (currentlyEditedBinding_ == null || Keybind.IsModifierKey(p.keycode)) {
                return;
            }

            p.Use(); // Consume the event
            UIView.PopModal();
            var keycode = p.keycode;
            var inputKey = (p.keycode == KeyCode.Escape)
                               ? currentlyEditedBinding_.Value.TargetKey
                               : SavedInputKey.Encode(keycode, p.control, p.shift, p.alt);

            var editable = (KeybindSetting.Editable)p.source.objectUserData;
            var category = editable.Target.Category;

            if (p.keycode == KeyCode.Backspace) {
                // TODO: Show hint somewhere for Bksp and Esc special handling
                inputKey = SavedInputKey.Empty;
            }

            var maybeConflict = FindConflict(inputKey, category);
            if (maybeConflict != string.Empty) {
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                    "Key Conflict",
                    Translation.GetString("Keybind_conflict") + "\n\n" + maybeConflict,
                    false);
            } else {
                currentlyEditedBinding_.Value.TargetKey.value = inputKey;
            }

            // Update text on the button
            var uITextComponent = p.source as UITextComponent;
            uITextComponent.text = Keybind.Str(currentlyEditedBinding_.Value.TargetKey);
            currentlyEditedBinding_ = null;
        }

        private void OnBindingMouseDown(UIComponent comp, UIMouseEventParameter p) {
            var editable = (KeybindSetting.Editable)p.source.objectUserData;

            // This will only work if the user is not in the process of changing the shortcut
            if (currentlyEditedBinding_ == null) {
                p.Use();
                currentlyEditedBinding_ = editable;

                var uIButton = p.source as UIButton;
                uIButton.buttonsMask =
                    UIMouseButton.Left | UIMouseButton.Right | UIMouseButton.Middle |
                    UIMouseButton.Special0 | UIMouseButton.Special1 | UIMouseButton.Special2 |
                    UIMouseButton.Special3;
                uIButton.text = "Press any key";
                p.source.Focus();
                UIView.PushModal(p.source);
            } else if (!Keybind.IsUnbindableMouseButton(p.buttons)) {
                // This will work if the user clicks while the shortcut change is in progress
                p.Use();
                UIView.PopModal();
                var inputKey = SavedInputKey.Encode(Keybind.ButtonToKeycode(p.buttons),
                                                    Keybind.IsControlDown(),
                                                    Keybind.IsShiftDown(),
                                                    Keybind.IsAltDown());
                var category = editable.Target.Category;
                var maybeConflict = FindConflict(inputKey, category);
                if (maybeConflict != string.Empty) {
                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                        "Key Conflict",
                        Translation.GetString("Keybind_conflict") + "\n\n" + maybeConflict,
                        false);
                } else {
                    currentlyEditedBinding_.Value.TargetKey.value = inputKey;
                }

                var uIButton2 = p.source as UIButton;
                uIButton2.text = Keybind.Str(currentlyEditedBinding_.Value.TargetKey);
                uIButton2.buttonsMask = UIMouseButton.Left;
                currentlyEditedBinding_ = null;
            }
        }

        private void RefreshBindableInputs() {
            foreach (var current in component.GetComponentsInChildren<UIComponent>()) {
                var uITextComponent = current.Find<UITextComponent>("Binding");
                if (uITextComponent != null) {
                    var savedInputKey = uITextComponent.objectUserData as SavedInputKey;
                    if (savedInputKey != null) {
                        uITextComponent.text = Keybind.Str(savedInputKey);
                    }
                }

                var uILabel = current.Find<UILabel>("Name");
                if (uILabel != null) {
                    uILabel.text = Locale.Get("KEYMAPPING", uILabel.stringUserData);
                }
            }
        }

        /// <summary>
        /// For an inputkey, try find where possibly it is already used.
        /// This covers game Settings class, and self (OptionsKeymapping class).
        /// </summary>
        /// <param name="k">Key to search for the conflicts</param>
        /// <param name="sampleCategory">Check the same category keys if possible</param>
        /// <returns>Empty string for no conflict, or the conflicting key name</returns>
        private string FindConflict(InputKey sample, string sampleCategory) {
            if (Keybind.IsEmpty(sample)) {
                // empty key never conflicts
                return string.Empty;
            }

            var inGameSettings = FindConflictInGameSettings(sample);
            if (!string.IsNullOrEmpty(inGameSettings)) {
                return inGameSettings;
            }

            // Saves and null 'self.editingBinding_' to allow rebinding the key to itself.
            var saveEditingBinding = currentlyEditedBinding_.Value.TargetKey.value;
            currentlyEditedBinding_.Value.TargetKey.value = SavedInputKey.Empty;

            // Check in TMPE settings
            var tmpeSettingsType = typeof(KeybindSettingsBase);
            var tmpeFields = tmpeSettingsType.GetFields(BindingFlags.Static | BindingFlags.Public);

            var inTmpe = FindConflictInTmpe(sample, sampleCategory, tmpeFields);
            currentlyEditedBinding_.Value.TargetKey.value = saveEditingBinding;
            return inTmpe;
        }

        private static string FindConflictInGameSettings(InputKey sample) {
            var fieldList = typeof(Settings).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var field in fieldList) {
                var customAttributes = field.GetCustomAttributes(typeof(RebindableKeyAttribute), false) as RebindableKeyAttribute[];
                if (customAttributes != null && customAttributes.Length > 0) {
                    var category = customAttributes[0].category;
                    if (category != string.Empty && category != "Game") {
                        // Ignore other categories: MapEditor, Decoration, ThemeEditor, ScenarioEditor
                        continue;
                    }

                    var str = field.GetValue(null) as string;

                    var savedInputKey = new SavedInputKey(str,
                                                          Settings.gameSettingsFile,
                                                          GetDefaultEntryInGameSettings(str),
                                                          true);
                    if (savedInputKey.value == sample) {
                        return (category == string.Empty ? string.Empty : (category + " -- "))
                               + CamelCaseSplit(field.Name);
                    }
                }
            }

            return string.Empty;
        }

        private static InputKey GetDefaultEntryInGameSettings(string entryName) {
            var field = typeof(DefaultSettings).GetField(entryName, BindingFlags.Static | BindingFlags.Public);
            if (field == null) {
                return 0;
            }

            var obj = field.GetValue(null);
            if (obj is InputKey) {
                return (InputKey)obj;
            }

            return 0;
        }

        /// <summary>
        /// For given key and category check TM:PE settings for the Global category
        /// and the same category if it is not Global. This will allow reusing key in other tool
        /// categories without conflicting.
        /// </summary>
        /// <param name="sample">The key to search for</param>
        /// <param name="sampleCategory">The category Global or some tool name</param>
        /// <param name="fields">Fields of the key settings class</param>
        /// <returns>Empty string if no conflicts otherwise the key name to print an error</returns>
        private static string FindConflictInTmpe(InputKey sample, string sampleCategory, FieldInfo[] fields) {
            foreach (var field in fields) {
                // This will match inputkeys of TMPE key settings
                if (field.FieldType != typeof(KeybindSetting)) {
                    continue;
                }

                var tmpeSetting = field.GetValue(null) as KeybindSetting;

                // Check category, category=Global will check keys in all categories
                // category=<other> will check Global and its own only
                if (sampleCategory != "Global"
                    && sampleCategory != tmpeSetting.Category) {
                    continue;
                }

                if (tmpeSetting.HasKey(sample)) {
                    return "TM:PE, "
                           + Translation.GetString("Keybind_category_" + tmpeSetting.Category)
                           + " -- " + CamelCaseSplit(field.Name);
                }
            }

            return string.Empty;
        }

        private static string CamelCaseSplit(string s) {
            var words = Regex.Matches(s, @"([A-Z][a-z]+)")
                             .Cast<Match>()
                             .Select(m => m.Value);

            return string.Join(" ", words.ToArray());
        }
    }
}
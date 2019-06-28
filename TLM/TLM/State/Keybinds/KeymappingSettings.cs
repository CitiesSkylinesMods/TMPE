// Based on keymapping module from CS-MoveIt mod
// Thanks to https://github.com/Quboid/CS-MoveIt

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using CSUtil.Commons;
using TrafficManager.UI;
using UnityEngine;

namespace TrafficManager.State.Keybinds {
    public class KeymappingSettings : UICustomControl {
        protected static readonly string KeyBindingTemplate = "KeyBindingTemplate";
        private const string KEYBOARD_SHORTCUTS_FILENAME = "TMPE_Keyboard";

        /// <summary>
        /// This input key can not be changed and is not checked, instead it is display only
        /// </summary>
        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyToolCancel_ViewOnly =
            new SavedInputKey("keyExitSubtool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Encode(KeyCode.Escape, false, false, false),
                              false);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyToggleTMPEMainMenu =
            new SavedInputKey("keyToggleTMPEMainMenu",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Encode(KeyCode.Semicolon, false, true, false),
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyToggleTrafficLightTool =
            new SavedInputKey("keyToggleTrafficLightTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyLaneArrowTool =
            new SavedInputKey("keyLaneArrowTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyLaneConnectionsTool =
            new SavedInputKey("keyLaneConnectionsTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyPrioritySignsTool =
            new SavedInputKey("keyPrioritySignsTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeyJunctionRestrictionsTool =
            new SavedInputKey("keyJunctionRestrictionsTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("Global")]
        public static SavedInputKey KeySpeedLimitsTool =
            new SavedInputKey("keySpeedLimitsTool",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Empty,
                              true);

        [TmpeRebindableKey("LaneConnector")]
        public static SavedInputKey KeyLaneConnectorStayInLane =
            new SavedInputKey("keyLaneConnectorStayInLane",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Encode(KeyCode.S, false, true, false),
                              true);

        [TmpeRebindableKey("LaneConnector")]
        public static SavedInputKey KeyLaneConnectorDelete =
            new SavedInputKey("keyLaneConnectorDelete",
                              KEYBOARD_SHORTCUTS_FILENAME,
                              SavedInputKey.Encode(KeyCode.Delete, false, false, false),
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
        protected void AddKeymapping(string label, SavedInputKey savedInputKey, string category) {
            var uiPanel = component.AttachUIComponent(
                              UITemplateManager.GetAsGameObject(KeyBindingTemplate)) as UIPanel;
            if (count_++ % 2 == 1) {
                uiPanel.backgroundSprite = null;
            }

            // Create a label
            var uiLabel = uiPanel.Find<UILabel>("Name");

            // Create a button which displays the shortcut and modifies it on click
            var uiButton = uiPanel.Find<UIButton>("Binding");
            uiButton.eventKeyDown += OnBindingKeyDown;
            uiButton.eventMouseDown += OnBindingMouseDown;
            uiButton.text = savedInputKey.ToLocalizedString("KEYNAME");
            uiButton.objectUserData = savedInputKey;
            uiButton.stringUserData = category;

            // Set label text (as provided) and set button text from the SavedInputKey
            uiLabel.text = label;
        }

        /// <summary>
        /// Creates a line of key mapping but does not allow changing it.
        /// Used to improve awareness.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="inputKey"></param>
        protected void AddReadOnlyKeymapping(string label, SavedInputKey inputKey) {
            var uiPanel = component.AttachUIComponent(
                              UITemplateManager.GetAsGameObject(KeyBindingTemplate)) as UIPanel;
            if (count_++ % 2 == 1) {
                uiPanel.backgroundSprite = null;
            }

            // Create a label
            var uiLabel = uiPanel.Find<UILabel>("Name");

            // Create a button which displays the shortcut and modifies it on click
            var uiReadOnlyKey = uiPanel.Find<UIButton>("Binding");
            uiReadOnlyKey.Disable();

            // Set label text (as provided) and set button text from the InputKey
            uiLabel.text = label;
            uiReadOnlyKey.text = inputKey.ToLocalizedString("KEYNAME");
            uiReadOnlyKey.objectUserData = inputKey;
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
                var category = p.source.stringUserData;

                if (p.keycode == KeyCode.Backspace) {
                    inputKey = SavedInputKey.Empty;
                }
                var maybeConflict = FindConflict(inputKey, category);
                if (maybeConflict != string.Empty) {
                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                        "Key Conflict",
                        Translation.GetString("Keybind_conflict") + "\n\n" + maybeConflict,
                        false);
                } else {
                    editingBinding_.value = inputKey;
                }

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
                var category = p.source.stringUserData;
                var maybeConflict = FindConflict(inputKey, category);
                if (maybeConflict != string.Empty) {
                    UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage(
                        "Key Conflict",
                        Translation.GetString("Keybind_conflict") + "\n\n" + maybeConflict,
                        false);
                } else {
                    editingBinding_.value = inputKey;
                }

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

        protected void RefreshKeyMapping() {
            foreach (var current in component.GetComponentsInChildren<UIComponent>()) {
                var uITextComponent = current.Find<UITextComponent>("Binding");
                var savedInputKey = (SavedInputKey) uITextComponent.objectUserData;
                if (editingBinding_ != savedInputKey) {
                    uITextComponent.text = savedInputKey.ToLocalizedString("KEYNAME");
                }
            }
        }

        /// <summary>
        /// For an inputkey, try find where possibly it is already used.
        /// This covers game Settings class, and self (OptionsKeymapping class).
        /// </summary>
        /// <param name="k">Key to search for the conflicts</param>
        /// <returns></returns>
        private string FindConflict(InputKey sample, string sampleCategory) {
            if (sample == SavedInputKey.Empty
                || sample == SavedInputKey.Encode(KeyCode.None, false, false, false)) {
                // empty key never conflicts
                return string.Empty;
            }

            var inGameSettings = FindConflictInGameSettings(sample);
            if (!string.IsNullOrEmpty(inGameSettings)) {
                return inGameSettings;
            }

            // Saves and null 'self.editingBinding_' to allow rebinding the key to itself.
            var saveEditingBinding = editingBinding_;
            editingBinding_.value = SavedInputKey.Empty;

            // Check in TMPE settings
            var tmpeSettingsType = typeof(KeymappingSettings);
            var tmpeFields = tmpeSettingsType.GetFields(BindingFlags.Static | BindingFlags.Public);

            var inTmpe = FindConflictInTmpe(sample, sampleCategory, tmpeFields);
            editingBinding_ = saveEditingBinding;
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
                if (field.FieldType != typeof(SavedInputKey)) {
                    continue;
                }

                var rebindableKeyAttrs = field.GetCustomAttributes(
                                             typeof(TmpeRebindableKey),
                                             false) as TmpeRebindableKey[];
                if (rebindableKeyAttrs == null || rebindableKeyAttrs.Length <= 0) {
                    continue;
                }

                // Check category, category=Global will check keys in all categories
                // category=<other> will check Global and its own only
                var rebindableKeyCategory = rebindableKeyAttrs[0].Category;
                if (sampleCategory != "Global" && sampleCategory != rebindableKeyCategory) {
                    continue;
                }

                var key = (SavedInputKey) field.GetValue(null);
                if (key.value == sample) {
                    return "TM:PE, "
                           + Translation.GetString("Keybind_category_" + rebindableKeyCategory)
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
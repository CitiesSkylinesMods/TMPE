namespace TrafficManager.State.Keybinds {
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using ColossalFramework;
    using ColossalFramework.UI;
    using UI;
    using UnityEngine;

    /// <summary>
    /// Helper for creating keyboard bindings Settings page.
    /// </summary>
    public class KeybindUI {
        private KeybindSetting.Editable? currentlyEditedBinding_;
        private const float ROW_WIDTH = 744f;
        private const float ROW_HEIGHT = 34f;

        /// <summary>
        /// Scrollable panel for keybinds, first created on Unity Awake call
        /// </summary>
        private UIComponent settingsPanel_;

        public void BeginForm(UIComponent component) {
            settingsPanel_ = CreateScrollablePanel(component);
        }

        /// <summary>
        /// Creates a row for keyboard bindings editor. The row will contain a text
        /// label, a button to edit the key, and X button to delete the key.
        /// </summary>
        /// <param name="root">The component where the UI is attached</param>
        /// <returns>The new scrollable panel</returns>
        public static UIComponent CreateScrollablePanel(UIComponent root) {
            var scrollablePanel = root.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.backgroundSprite = string.Empty;
            scrollablePanel.size = new Vector2(ROW_WIDTH, 0);
            scrollablePanel.relativePosition = Vector3.zero;
            scrollablePanel.clipChildren = true;
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.autoLayoutDirection = LayoutDirection.Vertical;
            scrollablePanel.autoLayout = true;

            return scrollablePanel;
        }

        public UIPanel CreateRowPanel() {
            settingsPanel_.size += new Vector2(0f, ROW_HEIGHT);

            var p = settingsPanel_.AddUIComponent<UIPanel>();
            p.size = new Vector2(ROW_WIDTH, ROW_HEIGHT);
            p.autoLayoutStart = LayoutStart.TopLeft;
            p.autoLayoutDirection = LayoutDirection.Horizontal;
            p.autoLayout = true;

            return p;
        }

        public void CreateLabel(UIPanel parent, string text) {
            var label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.size = new Vector2(ROW_WIDTH * 0.6f, ROW_HEIGHT);
            label.text = text;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Left;
        }

        public void CreateKeybindButton(UIPanel parent, KeybindSetting setting, SavedInputKey editKey) {
            var btn = parent.AddUIComponent<UIButton>();
            btn.size = new Vector2(ROW_WIDTH * 0.3f, ROW_HEIGHT);
            btn.text = Keybind.Str(editKey);
            btn.hoveredTextColor = new Color32(128, 128, 255, 255); // darker blue
            btn.pressedTextColor = new Color32(192, 192, 255, 255); // lighter blue
            btn.normalBgSprite = "ButtonMenu";

            btn.eventKeyDown += OnBindingKeyDown;
            btn.eventMouseDown += OnBindingMouseDown;
            btn.objectUserData
                = new KeybindSetting.Editable { Target = setting, TargetKey = editKey };

            // Add X button
            var btnX = parent.AddUIComponent<UIButton>();
            btnX.autoSize = false;
            btnX.size = new Vector2(ROW_HEIGHT, ROW_HEIGHT);
            btnX.normalBgSprite = "ButtonMenu";
            btnX.text = "X";
            btnX.eventClicked += (component, eventParam) => {
                editKey.value = SavedInputKey.Empty;
                btn.text = Keybind.Str(editKey);
            };
        }

        /// <summary>
        /// Create read-only display of a key binding
        /// </summary>
        /// <param name="parent">The panel to host it</param>
        /// <param name="showKey">The key to display</param>
        public void CreateKeybindText(UIPanel parent, SavedInputKey showKey) {
            var label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.size = new Vector2(ROW_WIDTH * 0.3f, ROW_HEIGHT);
            label.text = Keybind.Str(showKey);
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Center;
            label.textColor = new Color32(128, 128, 128, 255); // grey
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
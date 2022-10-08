namespace TrafficManager.State.Keybinds {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System;
    using TrafficManager.UI;
    using UnityEngine;
    using TrafficManager.UI.Helpers;
    using TrafficManager.U;

    /// <summary>
    /// Helper for creating keyboard bindings Settings page.
    /// </summary>
    public class KeybindUI {
        private const float ROW_WIDTH = 744f - 15f;
        private const float ROW_HEIGHT = 34f;

        private KeybindSetting.Editable? currentlyEditedBinding_;

        /// <summary>
        /// Scrollable panel, first created on Unity Awake call
        /// </summary>
        private UIComponent scrollPanel_;

        /// <summary>
        /// Group panel with text title for adding controls in it
        /// </summary>
        private UIComponent currentGroup_;

        /// <summary>
        /// Creates a row for keyboard bindings editor. The row will contain a text
        /// label, a button to edit the key, and X button to delete the key.
        /// </summary>
        /// <param name="root">The component where the UI is attached</param>
        /// <returns>The new scrollable panel</returns>
        public static UIComponent CreateScrollablePanel(UIComponent root) {
            if (root is UIPanel p) {
                // parent has wrong default layout direction (arranging panel then scrollbar)
                p.autoLayoutDirection = LayoutDirection.Horizontal;
            }
            var scrollablePanel = root.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.backgroundSprite = string.Empty;
            scrollablePanel.size = new Vector2(root.width - 20, 680);
            scrollablePanel.relativePosition = Vector3.zero;

            scrollablePanel.clipChildren = true;
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.autoLayoutDirection = LayoutDirection.Vertical;
            scrollablePanel.autoLayout = true;

            scrollablePanel.scrollWheelDirection = UIOrientation.Vertical;
            scrollablePanel.builtinKeyNavigation = true;

            var verticalScroll = root.AddUIComponent<UIScrollbar>();
            verticalScroll.stepSize = 1;
            verticalScroll.relativePosition = new Vector2(root.width - 10, 0);
            verticalScroll.orientation = UIOrientation.Vertical;
            verticalScroll.size = new Vector2(16, 660);
            verticalScroll.incrementAmount = 25;
            verticalScroll.scrollEasingType = EasingType.BackEaseOut;

            scrollablePanel.verticalScrollbar = verticalScroll;

            var track = verticalScroll.AddUIComponent<UISlicedSprite>();
            track.spriteName = "ScrollbarTrack";
            track.relativePosition = Vector3.zero;
            track.size = new Vector2(14, 660);

            verticalScroll.trackObject = track;

            var thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.spriteName = "ScrollbarThumb";
            thumb.autoSize = true;
            thumb.size = new Vector3(14, 0);
            thumb.relativePosition = Vector3.zero;
            verticalScroll.thumbObject = thumb;

            return scrollablePanel;
        }

        public void BeginForm(UIComponent component) {
            scrollPanel_ = CreateScrollablePanel(component);
        }

        /// <summary>
        /// Create an empty row of ROW_HEIGHT pixels, with left-to-right layout
        /// </summary>
        /// <returns>The row panel</returns>
        public UIPanel CreateRowPanel() {
            var rowPanel = currentGroup_.AddUIComponent<UIPanel>();
            rowPanel.size = new Vector2(ROW_WIDTH, ROW_HEIGHT);
            rowPanel.autoLayoutStart = LayoutStart.TopLeft;
            rowPanel.autoLayoutDirection = LayoutDirection.Horizontal;
            rowPanel.autoLayout = true;

            return rowPanel;
        }

        /// <summary>
        /// Create a box with title
        /// </summary>
        /// <param name="text">Title</param>
        private void BeginGroup(string text) {
            const string K_GROUP_TEMPLATE = "OptionsGroupTemplate";
            var groupPanel = scrollPanel_.AttachUIComponent(
                              UITemplateManager.GetAsGameObject(K_GROUP_TEMPLATE)) as UIPanel;
            groupPanel.autoLayoutStart = LayoutStart.TopLeft;
            groupPanel.autoLayoutDirection = LayoutDirection.Vertical;
            groupPanel.autoLayout = true;

            groupPanel.Find<UILabel>("Label").text = text;

            currentGroup_ = groupPanel.Find("Content");
        }

        /// <summary>
        /// Close the group and expand the scroll panel to include it
        /// </summary>
        private void EndGroup() {
            currentGroup_ = null;
        }

        public UILabel CreateLabel(UIPanel parent, string text, float widthFraction) {
            var label = parent.AddUIComponent<UILabel>();
            label.wordWrap = true;
            label.autoSize = false;
            label.size = new Vector2(ROW_WIDTH * widthFraction, ROW_HEIGHT);
            label.text = text;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Left;
            return label;
        }

        public void CreateKeybindButton(
            UIPanel parent,
            KeybindSetting setting,
            SavedInputKey editKey,
            float widthFraction) {
            var btn = parent.AddUIComponent<UIButton>();
            btn.size = new Vector2(ROW_WIDTH * widthFraction, ROW_HEIGHT);
            btn.text = Keybind.ToLocalizedString(editKey);
            btn.hoveredTextColor = new Color32(128, 128, 255, 255); // darker blue
            btn.pressedTextColor = new Color32(192, 192, 255, 255); // lighter blue
            btn.normalBgSprite = "ButtonMenu";
            btn.atlas = TextureUtil.Ingame;

            btn.eventKeyDown += OnBindingKeyDown;
            btn.eventMouseDown += OnBindingMouseDown;
            btn.objectUserData
                = new KeybindSetting.Editable { Target = setting, TargetKey = editKey };

            AddXButton(parent, editKey, btn, setting);
        }

        /// <summary>
        /// Add X button to the right of another button
        /// </summary>
        /// <param name="parent">The panel to host the new button.</param>
        /// <param name="editKey">The key to be cleared on click.</param>
        /// <param name="alignTo">Align X button to the right of this.</param>
        /// <param name="setting">KeybindSetting to notify that key was cleared.</param>
        private static void AddXButton(UIPanel parent,
                                       SavedInputKey editKey,
                                       UIButton alignTo,
                                       KeybindSetting setting) {
            UIButton btnX = parent.AddUIComponent<UIButton>();
            btnX.autoSize = false;
            btnX.size = new Vector2(ROW_HEIGHT, ROW_HEIGHT);
            btnX.normalBgSprite = "buttonclose";
            btnX.hoveredBgSprite = "buttonclosehover";
            btnX.pressedBgSprite = "buttonclosepressed";
            btnX.eventClicked += (component, eventParam) => {
                editKey.value = SavedInputKey.Empty;
                alignTo.text = Keybind.ToLocalizedString(editKey);
                setting.NotifyKeyChanged();
            };
        }

        /// <summary>
        /// Create read-only display of a key binding
        /// </summary>
        /// <param name="parent">The panel to host it</param>
        /// <param name="showKey">The key to display</param>
        /// <returns>The created UILabel</returns>
        public UILabel CreateKeybindText(UIPanel parent, SavedInputKey showKey, float widthFraction) {
            var label = parent.AddUIComponent<UILabel>();
            label.autoSize = false;
            label.size = new Vector2(ROW_WIDTH * widthFraction, ROW_HEIGHT);
            label.text = Keybind.ToLocalizedString(showKey);
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Center;
            label.textColor = new Color32(128, 128, 128, 255); // grey
            return label;
        }

        /// <summary>
        /// Performs group creation sequence: BeginGroup, add keybinds UI rows, EndGroup
        /// </summary>
        /// <param name="title">Translated title</param>
        /// <param name="code">Function which adds keybind rows</param>
        public void AddGroup(string title, Action code) {
            BeginGroup(title);
            code.Invoke();
            EndGroup();
        }

        private void OnBindingKeyDown(UIComponent comp, UIKeyEventParameter evParam) {
            try {
                // This will only work if the user clicked the modify button
                // otherwise no effect
                if (!currentlyEditedBinding_.HasValue || Keybind.IsModifierKey(evParam.keycode)) {
                    return;
                }

                evParam.Use(); // Consume the event
                var editedBinding = currentlyEditedBinding_; // will be nulled by closing modal
                UIView.PopModal();

                var keybindButton = evParam.source as UIButton;
                var inputKey = SavedInputKey.Encode(evParam.keycode, evParam.control, evParam.shift, evParam.alt);
                var editable = (KeybindSetting.Editable)evParam.source.objectUserData;
                var category = editable.Target.Category;

                if (evParam.keycode != KeyCode.Escape) {
                    // Check the key conflict
                    var maybeConflict = FindConflict(editedBinding.Value, inputKey, category);
                    if (maybeConflict != string.Empty) {
                        var message = Translation.Options.Get("Keybinds.Dialog.Text:Keybind conflict")
                                      + "\n\n" + maybeConflict;
                        Log.Info($"Keybind conflict: {message}");
                        Prompt.Warning("Key Conflict", message);
                    } else {
                        editedBinding.Value.TargetKey.value = inputKey;
                        editedBinding.Value.Target.NotifyKeyChanged();
                    }
                }

                keybindButton.text = Keybind.ToLocalizedString(editedBinding.Value.TargetKey);
                currentlyEditedBinding_ = null;
            } catch (Exception e) {
                Log.Error($"{e}");
            }
        }

        private void OnBindingMouseDown(UIComponent comp, UIMouseEventParameter evParam) {
            var editable = (KeybindSetting.Editable)evParam.source.objectUserData;
            var keybindButton = evParam.source as UIButton;

            // This will only work if the user is not in the process of changing the shortcut
            if (currentlyEditedBinding_ == null) {
                evParam.Use();
                StartKeybindEditMode(editable, keybindButton);
            } else if (!Keybind.IsUnbindableMouseButton(evParam.buttons)) {
                // This will work if the user clicks while the shortcut change is in progress
                evParam.Use();
                var editedBinding = currentlyEditedBinding_; // will be nulled by closing modal
                UIView.PopModal();

                var inputKey = SavedInputKey.Encode(Keybind.ButtonToKeycode(evParam.buttons),
                                                    Keybind.IsControlDown(),
                                                    Keybind.IsShiftDown(),
                                                    Keybind.IsAltDown());
                var category = editable.Target.Category;
                var maybeConflict = FindConflict(editedBinding.Value, inputKey, category);
                if (maybeConflict != string.Empty) {
                    var message = Translation.Options.Get("Keybinds.Dialog.Text:Keybind conflict")
                                  + "\n\n" + maybeConflict;
                    Log.Info($"Keybind conflict: {message}");
                    Prompt.Warning("Key Conflict", message);
                } else {
                    editedBinding.Value.TargetKey.value = inputKey;
                    editedBinding.Value.Target.NotifyKeyChanged();
                }

                keybindButton.buttonsMask = UIMouseButton.Left;
                keybindButton.text = Keybind.ToLocalizedString(editedBinding.Value.TargetKey);
                currentlyEditedBinding_ = null;
            }
        }

        /// <summary>
        /// Set the button text to welcoming message. Push the button as modal blocking
        /// everything else on screen and capturing the input.
        /// </summary>
        /// <param name="editable">The keysetting and inputkey inside it, to edit</param>
        /// <param name="keybindButton">The button to become modal</param>
        private void StartKeybindEditMode(KeybindSetting.Editable editable, UIButton keybindButton) {
            currentlyEditedBinding_ = editable;

            keybindButton.buttonsMask =
                UIMouseButton.Left | UIMouseButton.Right | UIMouseButton.Middle |
                UIMouseButton.Special0 | UIMouseButton.Special1 | UIMouseButton.Special2 |
                UIMouseButton.Special3;
            keybindButton.text = "Press key (or Esc)";
            keybindButton.Focus();
            UIView.PushModal(keybindButton, OnKeybindModalPopped);
        }

        /// <summary>
        /// Called by the UIView when modal was popped without us knowing
        /// </summary>
        /// <param name="component">The button which temporarily was modal</param>
        private void OnKeybindModalPopped(UIComponent component) {
            var keybindButton = component as UIButton;
            if (keybindButton != null && currentlyEditedBinding_ != null) {
                keybindButton.text = Keybind.ToLocalizedString(currentlyEditedBinding_.Value.TargetKey);
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
        private string FindConflict(KeybindSetting.Editable editedKeybind,
                                    InputKey sample,
                                    string sampleCategory) {
            if (Keybind.IsEmpty(sample)) {
                // empty key never conflicts
                return string.Empty;
            }

            var inGameSettings = FindConflictInGameSettings(sample);
            if (!string.IsNullOrEmpty(inGameSettings)) {
                return inGameSettings;
            }

            // Saves and null 'self.editingBinding_' to allow rebinding the key to itself.
            var saveEditingBinding = editedKeybind.TargetKey.value;
            editedKeybind.TargetKey.value = SavedInputKey.Empty;

            // Check in TMPE settings
            var tmpeSettingsType = typeof(KeybindSettingsBase);
            var tmpeFields = tmpeSettingsType.GetFields(BindingFlags.Static | BindingFlags.Public);

            var inTmpe = FindConflictInTmpe(sample, sampleCategory, tmpeFields);
            editedKeybind.TargetKey.value = saveEditingBinding;
            return inTmpe;
        }

        private static string FindConflictInGameSettings(InputKey sample) {
            var fieldList = typeof(Settings).GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var field in fieldList) {
                var customAttributes =
                    field.GetCustomAttributes(typeof(RebindableKeyAttribute), false) as RebindableKeyAttribute[];
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
        /// <param name="testSample">The key to search for</param>
        /// <param name="testSampleCategory">The category Global or some tool name</param>
        /// <param name="fields">Fields of the key settings class</param>
        /// <returns>Empty string if no conflicts otherwise the key name to print an error</returns>
        private static string FindConflictInTmpe(InputKey testSample,
                                                 string testSampleCategory,
                                                 FieldInfo[] fields) {
            foreach (var field in fields) {
                // This will match inputkeys of TMPE key settings
                if (field.FieldType != typeof(KeybindSetting)) {
                    continue;
                }

                var tmpeSetting = field.GetValue(null) as KeybindSetting;

                // Check category
                // settingCategory=Global will check against any other test samples
                // category=<other> will check Global and its own only
                if (tmpeSetting.Category == "Global"
                    || testSampleCategory == tmpeSetting.Category) {
                    if (tmpeSetting.HasKey(testSample)) {
                        return "TM:PE, "
                               + Translation.Options.Get("KeybindCategory." + tmpeSetting.Category)
                               + " -- " + CamelCaseSplit(field.Name);
                    }
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

namespace TrafficManager.State.Keybinds {
    using System.Collections.Generic;
    using ColossalFramework;
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Contains one or two SavedInputKeys, and event handler when the key is changed.
    /// </summary>
    public class KeybindSetting {
        public KeybindSetting(string cat,
                              string configFileKey,
                              InputKey? defaultKey1 = null) {
            Category = cat;
            Key = new SavedInputKey(
                configFileKey,
                KeybindSettingsBase.KEYBOARD_SHORTCUTS_FILENAME,
                defaultKey1 ?? SavedInputKey.Empty,
                true);
        }

        public KeybindSetting(string cat,
                              string configFileKey,
                              InputKey? defaultKey1,
                              InputKey? defaultKey2) {
            Category = cat;
            Key = new SavedInputKey(
                name: configFileKey,
                fileName: KeybindSettingsBase.KEYBOARD_SHORTCUTS_FILENAME,
                def: defaultKey1 ?? SavedInputKey.Empty,
                autoUpdate: true);
            AlternateKey = new SavedInputKey(
                name: configFileKey + "_Alternate",
                fileName: KeybindSettingsBase.KEYBOARD_SHORTCUTS_FILENAME,
                def: defaultKey2 ?? SavedInputKey.Empty,
                autoUpdate: true);
        }

        /// <summary>
        /// Keybind Setting for custom readonly keybind
        /// </summary>
        /// <param name="cat">Category </param>
        /// <param name="keyName">key name in the settings file</param>
        /// <param name="configFile">Config filename</param>
        /// <param name="inputKey">inputKey instance</param>
        public KeybindSetting(string cat,
                              string keyName,
                              string configFile,
                              InputKey inputKey) {
            Category = cat;
            Key = new SavedInputKey(
                keyName,
                configFile,
                inputKey,
                true);
        }

        /// <summary>
        /// Used by the GUI to tell the button event handler which key is being edited
        /// </summary>
        public struct Editable {
            public KeybindSetting Target;
            public SavedInputKey TargetKey;
        }

        /// <summary>
        /// Groups input keys by categories, also helps to know the usage for conflict search.
        /// </summary>
        public string Category;

        /// <summary>
        /// The key itself, bound to a config file value
        /// </summary>
        public SavedInputKey Key { get; }

        /// <summary>
        /// A second key, which can possibly be used or kept null
        /// </summary>
        [CanBeNull]
        public SavedInputKey AlternateKey { get; }

        private OnKeyChangedHandler onKeyChanged_;

        public delegate void OnKeyChangedHandler();

        public void OnKeyChanged(OnKeyChangedHandler onChanged) {
            onKeyChanged_ = onChanged;
        }

        public void NotifyKeyChanged() {
            onKeyChanged_?.Invoke();
        }

        /// <summary>
        /// Produce a keybind tooltip text, or two if alternate key is set. Prefixed if not empty.
        /// </summary>
        /// <param name="prefix">Prefix will be added if any key is not empty</param>
        /// <returns>String tooltip with the key shortcut or two</returns>
        public string ToLocalizedString(string prefix = "") {
            var result = default(string);
            if (!Keybind.IsEmpty(Key)) {
                result += prefix + Keybind.ToLocalizedString(Key);
            }

            if (AlternateKey == null || Keybind.IsEmpty(AlternateKey)) {
                return result;
            }

            if (result.IsNullOrWhiteSpace()) {
                result += prefix;
            } else {
                result += " | ";
            }

            return result + Keybind.ToLocalizedString(AlternateKey);
        }

        /// <summary>
        /// Produce a keybind tooltip text, or two if alternate key is set. Prefixed if not empty.
        /// </summary>
        /// <param name="prefix">Prefix will be added if any key is not empty</param>
        /// <returns>String tooltip with the key shortcut or two</returns>
        public List<string> ToLocalizedStringList() {
            var result = new List<string>(capacity: 2);
            if (!Keybind.IsEmpty(Key)) {
                result.Add(Keybind.ToLocalizedString(Key));;
            }

            if (AlternateKey != null && !Keybind.IsEmpty(AlternateKey)) {
                result.Add(Keybind.ToLocalizedString(AlternateKey));
            }

            return result;
        }

        /// <summary>Method to check if a button is pressed.</summary>
        /// <param name="e"></param>
        /// <returns>true for as long as user holds the key</returns>
        public bool IsPressed(Event e) {
            return Key.IsPressed(e)
                   || (AlternateKey != null && AlternateKey.IsPressed(e));
        }

        /// <summary>Check whether keyDownEvent matches either of the shortcuts.</summary>
        /// <param name="kep">Event coming into eventKeyDown.</param>
        /// <returns>Whether the main key or alternate key is pressed.</returns>
        public bool IsPressed(UIKeyEventParameter kep) {
            EventModifiers modifiers = (kep.alt ? EventModifiers.Alt : 0)
                                       | (kep.control ? EventModifiers.Control : 0)
                                       | (kep.shift ? EventModifiers.Shift : 0);
            return Key.IsPressed(EventType.keyDown, kep.keycode, modifiers)
                   || (AlternateKey != null
                       && AlternateKey.IsPressed(EventType.keyDown, kep.keycode, modifiers));
        }

        private bool prev_value = false;

        /// <summary>
        /// Determines when user first presses the key. the event is consumed first time
        /// this function is called.
        /// </summary>
        /// <param name="e"></param>
        /// <returns>true once when user presses the key.</returns>
        public bool KeyDown(Event e) {
            bool value = Key.IsPressed(e);
            bool ret = value && !prev_value;
            if (ret || !value) {
                prev_value = value;
            }
            return ret;
        }

        /// <summary>
        /// Determines when user releases the key
        /// </summary>
        /// <param name="e"></param>
        /// <returns>true once when user releases the key.</returns>
        public bool KeyUp() {
            return Key.IsKeyUp();
        }

        /// <summary>
        /// Check whether main or alt key are the same as k
        /// </summary>
        /// <param name="k">Find key</param>
        /// <returns>We have the key</returns>
        public bool HasKey(InputKey k) {
            return Key.value == k
                   || (AlternateKey != null && AlternateKey.value == k);
        }
    }
}
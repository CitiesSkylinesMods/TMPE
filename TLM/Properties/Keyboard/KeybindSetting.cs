namespace UXMod.Keyboard {
    using Client;
    using ColossalFramework;
    using JetBrains.Annotations;
    using UnityEngine;

    /// <summary>
    /// Contains one or two SavedInputKeys, and event handler when the key is changed.
    /// </summary>
    public class KeybindSetting {
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

        public KeybindSetting(ClientMod client_mod,
                              string cat,
                              string config_file_key,
                              InputKey? default_key1 = null) {
            Category = cat;
            Key = new SavedInputKey(
                config_file_key,
                client_mod.Config.GetFilename("Keybinds"),
                default_key1 ?? SavedInputKey.Empty,
                true);
        }

        public void OnKeyChanged(OnKeyChangedHandler onChanged) {
            onKeyChanged_ = onChanged;
        }

        public void NotifyKeyChanged() {
            onKeyChanged_?.Invoke();
        }

        public KeybindSetting(ClientMod client_mod,
                              string cat,
                              string configFileKey,
                              InputKey? defaultKey1,
                              InputKey? defaultKey2) {
            Category = cat;
            Key = new SavedInputKey(
                configFileKey,
                client_mod.Config.GetFilename("Keybinds"),
                defaultKey1 ?? SavedInputKey.Empty,
                true);
            AlternateKey = new SavedInputKey(
                configFileKey + "_Alternate",
                client_mod.Config.GetFilename("Keybinds"),
                defaultKey2 ?? SavedInputKey.Empty,
                true);
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

        public bool IsPressed(Event e) {
            return Key.IsPressed(e)
                   || (AlternateKey != null && AlternateKey.IsPressed(e));
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
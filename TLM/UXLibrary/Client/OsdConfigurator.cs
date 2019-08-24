using System.Collections.Generic;
using JetBrains.Annotations;
using UXLibrary.Keyboard;

namespace UXLibrary.Client {
    /// <summary>
    /// Creates a list of objects with commands to be sent to the UX panel. The UX panel code will
    /// update the On-Screen panel with the mode hints and actions. UX panel can be created either
    /// by UX mod or locally if UX mod is not installed, the panel name is unique and it will be
    /// reused.
    /// </summary>
    [UsedImplicitly]
    public class OsdConfigurator {
        /// <summary>
        /// Contains list of lists, where each list is a command with parameters.
        /// ["Title", Text] - will set top line describing the current edit mode (can be long
        ///     enough to wrap multiple lines).
        /// ["Action0", "Text"] - displays an action where no shortcut keys are available for it
        /// ["Action1", InputKey K, "Text"] - displays an action with 1 shortcut key
        /// ["Action2", InputKey K, InputKey K2, "Text"] - displays an action with 2 shortcut keys
        /// </summary>
        private readonly List<List<object>> commands_;

        private UxLibrary uxmod_;

        public OsdConfigurator(UxLibrary uxmod) {
            uxmod_ = uxmod;
            commands_ = new List<List<object>>();
        }

        /// <summary>
        /// Add "set title text" command to the items_
        /// </summary>
        /// <param name="text">The text to show</param>
        /// <returns>Configurator object to continue chaining calls</returns>
        [UsedImplicitly]
        public OsdConfigurator Title(string text) {
            var c = new List<object> {"Title", text};
            commands_.Add(c);
            return this;
        }

        /// <summary>
        /// Adds an action with short text and keybind setting which contains 0, 1 or 2 shortcuts
        /// </summary>
        /// <param name="label">The text</param>
        /// <param name="setting">The setting with keybinds in it</param>
        /// <returns>Configurator object to continue chaining calls</returns>
        [UsedImplicitly]
        public OsdConfigurator Action(string label, KeybindSetting setting) {
            var c = new List<object>();
            if (Keybind.IsEmpty(setting.Key)) {
                c.Add("Action0");
                c.Add(label);
            } else {
                if (setting.AlternateKey == null) {
                    // One keybind available
                    c.Add("Action1");
                    c.Add(Keybind.ToLocalizedString(setting.Key));
                    c.Add(label);
                } else {
                    // One keybind available
                    c.Add("Action2");
                    c.Add(Keybind.ToLocalizedString(setting.Key));
                    c.Add(Keybind.ToLocalizedString(setting.AlternateKey));
                    c.Add(label);
                }
            }
            commands_.Add(c);

            return this;
        }

        /// <summary>
        /// Finalizes building list of OSD commands, and sends the commands to the panel
        /// </summary>
        [UsedImplicitly]
        public void Show() {
            uxmod_.SendUpdateOsdMessage(commands_);
        }

        /// <summary>
        /// Sends clear OSD command
        /// </summary>
        [UsedImplicitly]
        public void Clear() {
            var clear = new List<List<object>>();
            clear.Add(new List<object> {"Clear"});
            uxmod_.SendUpdateOsdMessage(clear);
        }
    }

}
using System;
using ColossalFramework;

namespace UXLibrary.Client {
    /// <summary>
    /// This class ensures that configuration file is created and exists for
    /// loading/saving client-specific values such as keybinds.
    /// </summary>
    public class ConfigFile {
        private readonly UxLibrary uxMod_;

        internal readonly string ConfName;

        public ConfigFile(UxLibrary uxmod, string confName) {
            ConfName = confName;
            uxMod_ = uxmod;
            TryCreateConfig();
        }

        private void TryCreateConfig() {
            if (string.IsNullOrEmpty(uxMod_.OwnerName)) {
                return;
            }

            try {
                // Creating setting file for the keyboard settings
                if (GameSettings.FindSettingsFileByName(ConfName) == null) {
                    GameSettings.AddSettingsFile(new SettingsFile {fileName = ConfName});
                }
            }
            catch (Exception) {
                uxMod_.Log("Could not load/create the keyboard shortcuts file.");
            }
        }
    }
}
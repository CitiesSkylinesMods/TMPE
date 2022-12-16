namespace TrafficManager.State {
    using ICities;
    using System;
    using TrafficManager.UI;
    using TrafficManager.UI.Helpers;
    using TrafficManager.Util;

    public static class GeneralTab_CompatibilityGroup {

        public static CheckboxOption ScanForKnownIncompatibleModsAtStartup =
            new (nameof(Main.ScanForKnownIncompatibleModsAtStartup), Scope.Global) {
                Label = "Checkbox:Scan for known incompatible mods on startup",
                Translator = Translation.ModConflicts.Get,
                Handler = OnScanForKnownIncompatibleModsAtStartupChanged,
            };

        public static CheckboxOption IgnoreDisabledMods =
            new (nameof(Main.IgnoreDisabledMods), Scope.Global) {
                Label = "Checkbox:Ignore disabled mods",
                Indent = true,
                Translator = Translation.ModConflicts.Get,
                Handler = OnIgnoreDisabledModsChanged,
            };

        public static CheckboxOption ShowCompatibilityCheckErrorMessage =
            new(nameof(Main.ShowCompatibilityCheckErrorMessage), Scope.Global) {
                Label = "General.Checkbox:Notify me about TM:PE startup conflicts",
                Handler = OnShowCompatibilityCheckErrorMessageChanged,
            };

        static GeneralTab_CompatibilityGroup() {
            try {
                IgnoreDisabledMods
                    .PropagateTrueTo(ScanForKnownIncompatibleModsAtStartup);
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        private static ConfigData.Main Main => GlobalConfig.Instance.Main;

        internal static void AddUI(UIHelperBase tab) {

            var group = tab.AddGroup(T("General.Group:Compatibility"));

            ScanForKnownIncompatibleModsAtStartup.AddUI(group)
                .Value = Main.ScanForKnownIncompatibleModsAtStartup;
            IgnoreDisabledMods.AddUI(group)
                .Value = Main.IgnoreDisabledMods;
            ShowCompatibilityCheckErrorMessage.AddUI(group)
                .Value = Main.ShowCompatibilityCheckErrorMessage;
        }

        private static string T(string key) => Translation.Options.Get(key);

        private static void OnScanForKnownIncompatibleModsAtStartupChanged(bool value) {
            if (Main.ScanForKnownIncompatibleModsAtStartup == value) return;

            Main.ScanForKnownIncompatibleModsAtStartup = value;
            GlobalConfig.WriteConfig();
        }

        private static void OnIgnoreDisabledModsChanged(bool value) {
            if (Main.IgnoreDisabledMods == value) return;

            Main.IgnoreDisabledMods = value;
            GlobalConfig.WriteConfig();
        }

        private static void OnShowCompatibilityCheckErrorMessageChanged(bool value) {
            if (Main.ShowCompatibilityCheckErrorMessage == value) return;

            Main.ShowCompatibilityCheckErrorMessage = value;
            GlobalConfig.WriteConfig();
        }
    }
}
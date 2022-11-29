namespace TrafficManager.Lifecycle {
    using ColossalFramework.Globalization;
    using ICities;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.UI.WhatsNew;
    using TrafficManager.Util;

    public class TrafficManagerMod : ILoadingExtension, IUserMod {
        public static string ModName => $"TM:PE {WhatsNew.CurrentVersion} {VersionUtil.BRANCH}";

        public string Name => ModName;

        public string Description => "Manage your city's traffic";

        [UsedImplicitly]
        public void OnEnabled() => TMPELifecycle.StartMod();

        [UsedImplicitly]
        public void OnDisabled() => TMPELifecycle.EndMod();

        [UsedImplicitly]
        public void OnSettingsUI(UIHelper helper) {
            // Note: This bugs out if done in OnEnabled(), hence doing it here instead.
            LocaleManager.eventLocaleChanged -= Translation.HandleGameLocaleChange;
            LocaleManager.eventLocaleChanged += Translation.HandleGameLocaleChange;
            SavedGameOptions.Ensure();
            TMPESettings.MakeSettings(helper);
        }

        public void OnCreated(ILoading loading) { }
        public void OnReleased() { }
        public void OnLevelLoaded(LoadMode mode) => TMPELifecycle.Instance.Load();
        public void OnLevelUnloading() => TMPELifecycle.Instance.Unload();
    }
}

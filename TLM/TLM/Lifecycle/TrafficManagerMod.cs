namespace TrafficManager.Lifecycle {
    using ColossalFramework.Globalization;
    using ICities;
    using JetBrains.Annotations;
    using System.Diagnostics.CodeAnalysis;
    using TrafficManager.State;
    using TrafficManager.UI;
    using TrafficManager.Util;

    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1502:Element should not be on a single line", Justification = "Reviewed.")]
    public class TrafficManagerMod : ILoadingExtension, IUserMod {
        public static string ModName => $"TM:PE {VersionUtil.VersionString} {VersionUtil.BRANCH.ToUpper()}";

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
            Options.MakeSettings(helper);
        }

        public void OnCreated(ILoading loading) { }
        public void OnReleased() { }
        public void OnLevelLoaded(LoadMode mode) => TMPELifecycle.Instance.Load();
        public void OnLevelUnloading() => TMPELifecycle.Instance.Unload();
    }
}

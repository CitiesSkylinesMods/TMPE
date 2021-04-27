namespace TrafficManager.Lifecycle {
    using ColossalFramework.Globalization;
    using ICities;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.State;
    using TrafficManager.UI;

    public class TrafficManagerMod : ILoadingExtension, IUserMod {
#if LABS
        public const string BRANCH = "LABS";
#elif DEBUG
        public const string BRANCH = "DEBUG";
#else
        public const string BRANCH = "STABLE";
#endif

        // Use SharedAssemblyInfo.cs to modify TM:PE version
        // External mods (eg. CSUR Toolbox) reference the versioning for compatibility purposes
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        // used for in-game display
        public static string VersionString => ModVersion.ToString(3);

        public static readonly string ModName = "TM:PE " + VersionString + " " + BRANCH;

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

namespace TrafficManager.Util {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Reflection;
    using TrafficManager.Lifecycle;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Much of this stuff will be replaced as part of PR #699.
    /// </summary>
    [SuppressMessage("Performance", "HAA0101:Array allocation for params parameter", Justification = "Not performance critical")]
    [SuppressMessage("Performance", "HAA0601:Value type to reference type conversion causing boxing allocation", Justification = "Not performance critical")]
    [SuppressMessage("Performance", "HAA0302:Display class allocation to capture closure", Justification = "Not performance critical")]
    public static class VersionUtil {
        public const string BRANCH =
#if LABS
            "LABS";
#elif DEBUG
            "DEBUG";
#else
            "STABLE";
#endif

        public const uint EXPECTED_GAME_VERSION_U = 188997904u;

        public static Version ExpectedGameVersion => new Version(1, 13, 1, 1);

        public static string ExpectedGameVersionString => BuildConfig.VersionToString(EXPECTED_GAME_VERSION_U, false);

        public static uint CurrentGameVersionU =>
            (uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION)).GetValue(null);

        public static Version CurrentGameVersion => new Version(
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_A)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_B)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_C)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_BUILD_NUMBER)).GetValue(null));

        // Use SharedAssemblyInfo.cs to modify TM:PE version
        // External mods (eg. CSUR Toolbox) reference the versioning for compatibility purposes
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        // used for in-game display
        public static string VersionString => ModVersion.ToString(3);

        /// <summary>
        /// Logs some info about TMPE build, mono version, etc.
        /// </summary>
        public static void LogEnvironmentDetails() {
            LogBuildDetails();
            LogTmpeGuid();
            LogMonoVersion();
        }

        /// <summary>
        /// TMPE build info and what game ver it expects.
        /// </summary>
        public static void LogBuildDetails() {
            Log.InfoFormat(
                "TM:PE enabled. Version {0}, Build {1} {2} for game version {3}",
                VersionString,
                ModVersion,
                BRANCH,
                BuildConfig.applicationVersion);
        }

        /// <summary>
        /// Log TMPE Guid.
        /// </summary>
        public static void LogTmpeGuid() {
            Log.InfoFormat(
                "Enabled TM:PE has GUID {0}",
                Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId);
        }

        /// <summary>
        /// Log Mono version.
        /// </summary>
        public static void LogMonoVersion() {
            // Log Mono version
            Type monoRt = Type.GetType("Mono.Runtime");
            if (monoRt != null) {
                MethodInfo displayName = monoRt.GetMethod(
                    "GetDisplayName",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (displayName != null) {
                    Log.InfoFormat("Mono version: {0}", displayName.Invoke(null, null));
                }
            }
        }


        /// <summary>
        /// Checks to see if game version is what TMPE expects, and if not warns users.
        ///
        /// This will be replaced as part of #699.
        /// </summary>
        public static void CheckGameVersion() {
            Log._Debug("CurrentGameVersion == ExpectedGameVersion : " + (CurrentGameVersion == ExpectedGameVersion));
            if (CurrentGameVersionU != EXPECTED_GAME_VERSION_U) {
                Log.Info($"Detected game version v{BuildConfig.applicationVersion}. TMPE built for {ExpectedGameVersionString}");

                if (CurrentGameVersion < ExpectedGameVersion) {
                    // game too old
                    string msg = string.Format(
                        "Traffic Manager: President Edition detected that you are running " +
                        "a newer game version ({0}) than TM:PE has been built for ({1}). " +
                        "Please be aware that TM:PE has not been updated for the newest game " +
                        "version yet and thus it is very likely it will not work as expected.",
                        BuildConfig.applicationVersion,
                        ExpectedGameVersionString);
                    Log.Error(msg);
                    Shortcuts.ShowErrorDialog("TM:PE has not been updated yet", msg);
                } else if (CurrentGameVersion > ExpectedGameVersion) {
                    // TMPE too old
                    string msg = string.Format(
                        "Traffic Manager: President Edition has been built for game version {0}. " +
                        "You are running game version {1}. Some features of TM:PE will not " +
                        "work with older game versions. Please let Steam update your game.",
                        ExpectedGameVersionString,
                        BuildConfig.applicationVersion);
                    Log.Error(msg);
                    Shortcuts.ShowErrorDialog("Your game should be updated", msg);
                }
            }
        }
    }
}
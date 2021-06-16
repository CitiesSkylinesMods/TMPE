namespace TrafficManager.Util {
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
#if TEST
            "TEST";
#elif DEBUG
            "DEBUG";
#else
            "STABLE";
#endif

        /// <summary>
        /// VersionB of the game changes with mod breaking game updates .
        /// </summary>
        private const int VERSION_COMPONENTS_COUNT = 2;

        // we could alternatively use BuildConfig.APPLICATION_VERSION because const values are evaluated at compile time.
        // but I have decided not to do this because I don't want this to happen automatically with a rebuild if
        // CS updates. these values should be changed manaually so as to force us to acknowledge that they have changed.
        public const uint EXPECTED_GAME_VERSION_U = 189262096U;

        // see comments for EXPECTED_GAME_VERSION_U.
        public static Version ExpectedGameVersion => new Version(1, 13, 3, 9);

        public static string ExpectedGameVersionString => BuildConfig.VersionToString(EXPECTED_GAME_VERSION_U, false);

        // we cannot use BuildConfig.APPLICATION_VERSION directly (it would not make sense).
        // because BuildConfig.APPLICATION_VERSION is const and constants are resolved at compile-time.
        // this is important when game version changes but TMPE dll is old. at such circumstance
        // if we use BuildConfig.APPLICATION_VERSION then we will not notice that game version has changed.
        // using reflection is a WORKAROUND to force the compiler to get value dynamically at run-time.
        public static uint CurrentGameVersionU =>
            (uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION)).GetValue(null);

        // see comments CurrentGameVersionU.
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
        /// take first n components of the version.
        /// </summary>
        static Version Take(this Version version, int n) {
            return new Version(version.ToString(n));
        }

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
            if (CurrentGameVersionU != EXPECTED_GAME_VERSION_U) {
                Log.Info($"Detected game version v{BuildConfig.applicationVersion}. TMPE built for {ExpectedGameVersionString}");
                Log._Debug($"CurrentGameVersion={CurrentGameVersion} ExpectedGameVersion={ExpectedGameVersion}");
                Version current = CurrentGameVersion.Take(VERSION_COMPONENTS_COUNT);
                Version expected = ExpectedGameVersion.Take(VERSION_COMPONENTS_COUNT);

                if (current < expected) {
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
                } else if (current > expected) {
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
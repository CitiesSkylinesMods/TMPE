namespace TrafficManager.Util {
    using System;
    using System.Reflection;
    using CSUtil.Commons;
    using TrafficManager.Lifecycle;
    using TrafficManager.UI.Helpers;

    /// <summary>
    /// Much of this stuff will be replaced as part of PR #699.
    /// </summary>
    public static class VersionUtil {

#if TEST
        public const string BRANCH = "TEST";
        public const bool IS_DEBUG = false;
        public const bool IS_TEST = true;
        public const bool IS_STABLE = false;
#elif DEBUG
        public const string BRANCH = "DEBUG";
        public const bool IS_DEBUG = true;
        public const bool IS_TEST = false;
        public const bool IS_STABLE = false;
#else
        public const string BRANCH = "STABLE";
        public const bool IS_DEBUG = false;
        public const bool IS_TEST = false;
        public const bool IS_STABLE = true;
#endif

        /// <summary>
        /// VersionB of the game changes with mod breaking game updates .
        /// </summary>
        private const int VERSION_COMPONENTS_COUNT = 2;

        // we could alternatively use BuildConfig.APPLICATION_VERSION because const values are evaluated at compile time.
        // but I have decided not to do this because I don't want this to happen automatically with a rebuild if
        // CS updates. these values should be changed manually so as to force us to acknowledge that they have changed.
        public const uint EXPECTED_GAME_VERSION_U = 218358032u;

        // see comments for EXPECTED_GAME_VERSION_U.
        public static Version ExpectedGameVersion => new Version(1, 20, 1, 1);

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
        /// Returns <c>true</c> if this is a STABLE/RELEASE build, otherwise <c>false</c>.
        /// </summary>
        public static bool IsStableRelease => BRANCH == "STABLE";

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
        /// TMPE build info and the version of the game which the mod was built using
        /// (BuildConfig.applicationVersion is resolved at compile time!)
        /// </summary>
        public static void LogBuildDetails() {
            Log.InfoFormat(
                "{0} - Build {1} {2} compiled under C:SL {3}",
                TrafficManagerMod.ModName,
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

        public static bool CheckGameVersion() {
            Log.Info($"Expected C:SL v{ExpectedGameVersionString} - Actual C:SL v{BuildConfig.applicationVersion}");
            if (CurrentGameVersionU != EXPECTED_GAME_VERSION_U) {
                Log._Debug($"CurrentGameVersion={CurrentGameVersion} ExpectedGameVersion={ExpectedGameVersion}");
                Version current = CurrentGameVersion.Take(VERSION_COMPONENTS_COUNT);
                Version expected = ExpectedGameVersion.Take(VERSION_COMPONENTS_COUNT);

                if (current != expected) {
                    string msg = string.Format(
                        "{0} is designed for Cities: Skylines {1}. However you are using Cities: " +
                        "Skylines {2} - this is likely to cause severe problems or crashes." +
                        "\n\n" +
                        "Please ensure you're using the right version of TM:PE for this version of " +
                        "Cities: Skylines before proceeding, or disable TM:PE until the problem is " +
                        "resolved. If you need help, contact us via Steam Workshop page or Discord chat.",
                        TrafficManagerMod.ModName,
                        ExpectedGameVersionString,
                        BuildConfig.applicationVersion);
                    Log.Error(msg);

                    Prompt.Error(
                        current > expected
                            ? "TM:PE needs updating!"
                            : "Cities: Skylines needs updating!",
                        msg);

                    return false;
                }
            }
            return true;
        }
    }
}
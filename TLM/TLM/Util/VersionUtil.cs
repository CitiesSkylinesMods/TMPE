namespace TrafficManager.Util {
    using CSUtil.Commons;
    using System;
    using System.Reflection;
    using TrafficManager.Lifecycle;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The three main types of TM:PE build.
    /// </summary>
    /// <remarks>Use `VersionUtil.BRANCH` to get release type of current build.</remarks>
    public enum ReleaseType {
        /// <summary>
        /// A TEST build for release to the TEST workshop page.
        /// </summary>
        Test,

        /// <summary>
        /// A DEBUG build during development cycle.
        /// </summary>
        Debug,

        /// <summary>
        /// A STABLE build for release to the STABLE workshop page.
        /// </summary>
        Stable,
    }

    /// <summary>
    /// Much of this stuff will be replaced as part of PR #699.
    /// </summary>
    [SuppressMessage("Performance", "HAA0101:Array allocation for params parameter", Justification = "Not performance critical")]
    [SuppressMessage("Performance", "HAA0601:Value type to reference type conversion causing boxing allocation", Justification = "Not performance critical")]
    [SuppressMessage("Performance", "HAA0302:Display class allocation to capture closure", Justification = "Not performance critical")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Reviewed.")]
    public static class VersionUtil {

        /// <summary>
        /// Specifies the <see cref="ReleaseType"/> of the build.
        /// </summary>
        /// <remarks>Use <see cref="ToUpper(ReleaseType)"/> extension if you want uppercase string.</remarks>
        public const ReleaseType BRANCH =
#if TEST
            ReleaseType.Test;
#elif DEBUG
            ReleaseType.Debug;
#else
            ReleaseType.Stable;
#endif

        /// <summary>
        /// Specifies the <see cref="BuildConfig.APPLICATION_VERSION"/> this TM:PE build is designed for.
        /// </summary>
        /// <remarks>
        /// We manually specify value here to force us to consider the need for updates when game version changes.
        /// </remarks>
        public const uint EXPECTED_GAME_VERSION_U = 189262096U;

        /// <summary>
        /// <see cref="BuildConfig.APPLICATION_VERSION_B"/> changes when there are likely to be severe
        /// breaking changes for mods.
        /// </summary>
        private const int VERSION_COMPONENTS_COUNT = 2;

        /// <summary>
        /// Gets the manually defined <see cref="BuildConfig.APPLICATION_VERSION"/> this TM:PE build is designed for.
        /// </summary>
        /// <remarks>
        /// We manually specify value here to force us to consider the need for updates when game version changes.
        /// </remarks>
        public static Version ExpectedGameVersion => new Version(1, 13, 3, 9);

        /// <summary>
        /// Gets <see cref="EXPECTED_GAME_VERSION_U"/> as a human-friendly string.
        /// </summary>
        public static string ExpectedGameVersionString => BuildConfig.VersionToString(EXPECTED_GAME_VERSION_U, false);

        /// <summary>
        /// Gets the game version at runtime.
        /// </summary>
        /// <remarks>
        /// The game version is a <c>const</c> which is resolved at compile-time and thus won't indicate
        /// actual running version of the game should it subsequently be updated. To workaround this, we
        /// use reflection to get the game version at runtime.
        /// </remarks>
        public static uint CurrentGameVersionU =>
            (uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION)).GetValue(null);

        /// <summary>
        /// Gets the game version at runtime.
        /// </summary>
        /// <remarks>
        /// The game version is a <c>const</c> which is resolved at compile-time and thus won't indicate
        /// actual running version of the game should it subsequently be updated. To workaround this, we
        /// use reflection to get the game version at runtime.
        /// </remarks>
        public static Version CurrentGameVersion => new Version(
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_A)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_B)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_VERSION_C)).GetValue(null),
            (int)(uint)typeof(BuildConfig).GetField(nameof(BuildConfig.APPLICATION_BUILD_NUMBER)).GetValue(null));

        /// <summary>
        /// Gets the compile-time version of this TM:PE build from <c>SharedAssemblyInfo.cs</c>.
        /// </summary>
        /// <remarks>
        /// External mods (eg. CSUR Toolbox) reference this value for compatibility purposes.
        /// </remarks>
        public static Version ModVersion => typeof(TrafficManagerMod).Assembly.GetName().Version;

        /// <summary>
        /// Gets the Guid of the active instance of TM:PE.
        /// </summary>
        public static Guid TmpeGuid => Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;

        /// <summary>
        /// Gets the <see cref="ModVersion"/> as a <c>string</c>; used for in-game display (mod options, toolbar, etc).
        /// </summary>
        public static string VersionString => ModVersion.ToString(3);

        /// <summary>
        /// Returns <c>true</c> if <see cref="BRANCH"/> is <see cref="ReleaseType.Stable"/>, otherwise <c>false</c>.
        /// </summary>
        public static bool IsStableRelease => BRANCH == ReleaseType.Stable;

        /// <summary>
        /// Converts a <see cref="ReleaseType"/> to uppercase string.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>Uppercase string representation of the <paramref name="value"/>.</returns>
        public static string ToUpper(this ReleaseType value) {
            return value.ToString().ToUpper();
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
            Log.InfoFormat("Enabled TM:PE has GUID {0}", TmpeGuid);
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

        /// <summary>
        /// Take the first <paramref name="n"/> components of a <see cref="Version"/> and use that to construct a new <see cref="Version"/>.
        /// </summary>
        /// <param name="version">The source version.</param>
        /// <param name="n">The number of components to take.</param>
        /// <returns>A new version based on the first <paramref name="n"/> components of <paramref name="version"/>.</returns>
        private static Version Take(this Version version, int n) {
            return new Version(version.ToString(n));
        }
    }
}
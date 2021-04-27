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

        // These values from `BuildConfig` class (`APPLICATION_VERSION` constants) in game file `Managed/Assembly-CSharp.dll` (use ILSpy to inspect them)
        public const uint EXPECTED_GAME_VERSION = 188868624U;
        public static Version ExpectedGameVersion => new Version(1, 13, 1, 1);
        public static Version CurrentGameVersion => new Version(
            (int)BuildConfig.APPLICATION_VERSION_A,
            (int)BuildConfig.APPLICATION_VERSION_B,
            (int)BuildConfig.APPLICATION_VERSION_C,
            (int)BuildConfig.APPLICATION_BUILD_NUMBER);

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
            if (BuildConfig.APPLICATION_VERSION != EXPECTED_GAME_VERSION) {
                Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

                int compare = CurrentGameVersion.CompareTo(ExpectedGameVersion);
                if (compare > 1) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition detected that you are running " +
                        "a newer game version ({0}) than TM:PE has been built for ({1}). " +
                        "Please be aware that TM:PE has not been updated for the newest game " +
                        "version yet and thus it is very likely it will not work as expected.",
                        BuildConfig.applicationVersion,
                        BuildConfig.VersionToString(EXPECTED_GAME_VERSION, false));

                    Log.Error(msg);
                    Singleton<SimulationManager>.instance.m_ThreadingWrapper.QueueMainThread(
                            () => {
                                UIView.library
                                      .ShowModal<ExceptionPanel>("ExceptionPanel")
                                      .SetMessage(
                                          "TM:PE has not been updated yet",
                                          msg,
                                          false);
                            });
                } else if (compare < 1) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition has been built for game version {0}. " +
                        "You are running game version {1}. Some features of TM:PE will not " +
                        "work with older game versions. Please let Steam update your game.",
                        BuildConfig.VersionToString(EXPECTED_GAME_VERSION, false),
                        BuildConfig.applicationVersion);

                    Log.Error(msg);
                    Singleton<SimulationManager>
                        .instance.m_ThreadingWrapper.QueueMainThread(
                            () => {
                                UIView.library
                                      .ShowModal<ExceptionPanel>("ExceptionPanel")
                                      .SetMessage(
                                          "Your game should be updated",
                                          msg,
                                          false);
                            });
                }
            }
        }
    }
}
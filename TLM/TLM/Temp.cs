namespace TrafficManager {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Reflection;
    using TrafficManager.Util;

    /// <summary>
    /// This class is a temporary place to put a bunch of stuff until a better place is found for it.
    ///
    /// Much of this stuff will be replaced as part of PR #699.
    /// </summary>
    public class Temp {

        /// <summary>
        /// Logs some info about TMPE build, mono version, etc.
        /// </summary>
        public static void LogEnvironmentDetails() {
            LogBuildDetails();
            LogTmpeGuid();
            LogMonoVersion();
        }

        /// <summary>
        /// Log TMPE build info and what game ver it expects.
        /// </summary>
        public static void LogBuildDetails() {
            Log.InfoFormat(
                "TM:PE enabled. Version {0}, Build {1} {2} for game version {3}.{4}.{5}-f{6}",
                TrafficManagerMod.VersionString,
                Assembly.GetExecutingAssembly().GetName().Version,
                TrafficManagerMod.BRANCH,
                TrafficManagerMod.GAME_VERSION_A,
                TrafficManagerMod.GAME_VERSION_B,
                TrafficManagerMod.GAME_VERSION_C,
                TrafficManagerMod.GAME_VERSION_BUILD);
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
        /// Run compatibility checker.
        /// </summary>
        /// 
        /// <returns>Returns <c>false</c> if issues found, otherwise <c>true</c>.</returns>
        public static bool CheckCompatibility() {
            ModsCompatibilityChecker mcc = new ModsCompatibilityChecker();
            mcc.PerformModCheck();
            return true; // ideally this would return false if there are compatibility issues (#699 will sort that)
        }

        /// <summary>
        /// Checks to see if game version is what TMPE expects, and if not warns users.
        ///
        /// This will be replaced as part of #699.
        /// </summary>
        public static void CheckGameVersion() {
            if (BuildConfig.applicationVersion != BuildConfig.VersionToString(
        TrafficManagerMod.GAME_VERSION,
        false)) {
                string[] majorVersionElms = BuildConfig.applicationVersion.Split('-');
                string[] versionElms = majorVersionElms[0].Split('.');
                uint versionA = Convert.ToUInt32(versionElms[0]);
                uint versionB = Convert.ToUInt32(versionElms[1]);
                uint versionC = Convert.ToUInt32(versionElms[2]);

                Log.Info($"Detected game version v{BuildConfig.applicationVersion}");

                bool isModTooOld = TrafficManagerMod.GAME_VERSION_A < versionA ||
                                   (TrafficManagerMod.GAME_VERSION_A == versionA &&
                                    TrafficManagerMod.GAME_VERSION_B < versionB);
                // || (TrafficManagerMod.GameVersionA == versionA
                // && TrafficManagerMod.GameVersionB == versionB
                // && TrafficManagerMod.GameVersionC < versionC);

                bool isModNewer = TrafficManagerMod.GAME_VERSION_A < versionA ||
                                  (TrafficManagerMod.GAME_VERSION_A == versionA &&
                                   TrafficManagerMod.GAME_VERSION_B > versionB);
                // || (TrafficManagerMod.GameVersionA == versionA
                // && TrafficManagerMod.GameVersionB == versionB
                // && TrafficManagerMod.GameVersionC > versionC);

                if (isModTooOld) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition detected that you are running " +
                        "a newer game version ({0}) than TM:PE has been built for ({1}). " +
                        "Please be aware that TM:PE has not been updated for the newest game " +
                        "version yet and thus it is very likely it will not work as expected.",
                        BuildConfig.applicationVersion,
                        BuildConfig.VersionToString(TrafficManagerMod.GAME_VERSION, false));

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
                } else if (isModNewer) {
                    string msg = string.Format(
                        "Traffic Manager: President Edition has been built for game version {0}. " +
                        "You are running game version {1}. Some features of TM:PE will not " +
                        "work with older game versions. Please let Steam update your game.",
                        BuildConfig.VersionToString(TrafficManagerMod.GAME_VERSION, false),
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

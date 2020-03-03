namespace TrafficManager {
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

    }
}

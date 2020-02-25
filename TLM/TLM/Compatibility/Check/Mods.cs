namespace TrafficManager.Compatibility.Check {
    using ColossalFramework.Plugins;
    using ColossalFramework;
    using CSUtil.Commons;
    using static ColossalFramework.Plugins.PluginManager;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TrafficManager.Compatibility.Enum;
    using TrafficManager.Compatibility.Struct;
    using TrafficManager.State;

    /// <summary>
    /// Scans for known incompatible mods as defined by <see cref="IncompatibleMods.List"/>.
    /// </summary>
    public class Mods {

        // Strings for log entries
        internal const string LOG_ENTRY_FORMAT = "{0} {1} {2} {3}\n";
        internal const string MARKER_ENABLED = "*";
        internal const string MARKER_BLANK = " ";
        internal const string MARKER_TMPE = ">";
        internal const string MARKER_CRITICAL = "C";
        internal const string MARKER_MAJOR = "M";
        internal const string MARKER_MINOR = "m";

        internal const string LOCAL_MOD_STR = "(local)";
        internal const string BUNDLED_MOD_STR = "(bundled)";

        /// <summary>
        /// Scans installed mods (local and workshop) looking for known incompatibilities.
        /// </summary>
        /// 
        /// <param name="results">A dictionary issues found (will be empty if no issues).</param>
        /// <param name="critical">Number of critical incompatibilities.</param>
        /// <param name="major">Number of major incompatibilities.</param>
        /// <param name="minor">Number of minor incompatibilities.</param>
        /// <param name="tmpe">Number of non-obsolete TM:PE mods.</param>
        /// 
        /// <returns>Returns <c>true</c> if incompatible mods detected, otherwise <c>false</c>.</returns>
        public static bool Verify(
            out Dictionary<PluginInfo, ModDescriptor> results,
            out int minor,
            out int major,
            out int critical,
            out int tmpe) {

            results = new Dictionary<PluginInfo, ModDescriptor>();

            // current verification state
            bool verified = true;

            // check minor severity incompatibilities?
            bool scanMinor = GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup;

            // check disabled mods? note: Critical incompatibilities are always processed
            bool scanDisabled = !GlobalConfig.Instance.Main.IgnoreDisabledMods;

            // batch all logging in to a single log message
            // 6000 chars is roughly 120 mods worth of logging
            StringBuilder log = new StringBuilder(6000);

            log.AppendFormat(
                "Compatibility.Check.Mods.Verify() scanMinor={0}, scanDisabled={1}\n\n",
                scanMinor,
                scanDisabled);

            // Variables for log file entries
            string logWorkshopId;
            string logIncompatible;

            // problem counters
            minor = major = critical = tmpe = 0;

            PluginManager manager = Singleton<PluginManager>.instance;

            List<PluginInfo> mods = new List<PluginInfo>(manager.modCount);

            mods.AddRange(manager.GetPluginsInfo()); // normal mods
            mods.AddRange(manager.GetCameraPluginInfos()); // camera scripts

            // iterate plugins
            foreach (PluginInfo mod in mods) {

                try {
                    // Generate descriptor for the mod
                    ModDescriptor descriptor = mod;

                    results.Add(mod, descriptor);

                    // String to log for workshop id
                    logWorkshopId = mod.isBuiltin
                        ? BUNDLED_MOD_STR
                        : descriptor.ModIsLocal
                            ? LOCAL_MOD_STR
                            : descriptor.ModWorkshopId.ToString();

                    switch (descriptor.Incompatibility) {

                        case Severity.Critical:
                            logIncompatible = MARKER_CRITICAL;
                            ++critical;
                            verified = false;
                            break;

                        case Severity.Major:
                            logIncompatible = MARKER_MAJOR;
                            ++major;
                            if (mod.isEnabled || scanDisabled) {
                                verified = false;
                            }
                            break;

                        case Severity.Minor:
                            logIncompatible = MARKER_MINOR;
                            ++minor;
                            if (scanMinor && (mod.isEnabled || scanDisabled)) {
                                verified = false;
                            }
                            break;

                        case Severity.TMPE:
                            logIncompatible = MARKER_TMPE;
                            ++tmpe;
                            if (descriptor.AssemblyGuid != CompatibilityManager.SelfGuid) {
                                verified = false;
                            }
                            break;

                        default:
                        case Severity.None:
                            logIncompatible = MARKER_BLANK;
                            break;
                    }

                    log.AppendFormat(
                        LOG_ENTRY_FORMAT,
                        logIncompatible,
                        mod.isEnabled ? MARKER_ENABLED : MARKER_BLANK,
                        logWorkshopId.PadRight(12),
                        descriptor.ModName);

                } catch (Exception e) {
                    Log.ErrorFormat(
                        "Error scanning {0}:\n{1}",
                        mod.modPath,
                        e.ToString());
                }

            } // foreach

            log.AppendFormat(
                "\n{0} Mod(s): {1} [*] enabled, {2} [C]ritical, {3} [M]ajor, {4} [m]inor, {5} [>] TM:PE\n",
                manager.modCount,
                manager.enabledModCount,
                critical,
                major,
                minor,
                tmpe);

            Log.Info(log.ToString());

            return verified;
        }
    }
}

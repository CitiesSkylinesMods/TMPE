namespace TrafficManager.Compatibility.Check {
    using ColossalFramework.Plugins;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using static ColossalFramework.Plugins.PluginManager;
    using System.Collections.Generic;
    using System.IO;
    using System;
    using TrafficManager.State;
    using System.Text;
    using TrafficManager.Compatibility.Enum;
    using TrafficManager.Compatibility.Struct;
    using System.Reflection;

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

        /// <summary>
        /// Scans installed mods (local and workshop) looking for known incompatibilities.
        /// </summary>
        /// 
        /// <param name="results">A dictionary issues found (will be empty if no issues).</param>
        /// 
        /// <returns>Returns <c>true</c> if incompatible mods detected, otherwise <c>false</c>.</returns>
        public static bool Verify(out Dictionary<PluginInfo, ModDescriptor> results) {

            Log.Info("Compatibility.Check.Mods.Scan()");

            results = new Dictionary<PluginInfo, ModDescriptor>();

            // current verification state
            bool verified = true;

            // check minor severity incompatibilities?
            bool scanMinor = GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup;

            // check disabled mods? note: Critical incompatibilities are always processed
            bool scanDisabled = !GlobalConfig.Instance.Main.IgnoreDisabledMods;

            // batch all logging in to a single log message
            // 6000 chars is roughly 120 mods worth of logging
            StringBuilder sb = new StringBuilder(6000);

            sb.AppendFormat(
                "Scanning (scanMinor={0}, scanDisabled={1})...\n\n",
                scanMinor,
                scanDisabled);

            // Variables for log file entries
            string logWorkshopId;
            string logIncompatible;
            int minor = 0,
                major = 0,
                critical = 0;

            // iterate plugins
            foreach (PluginInfo mod in Singleton<PluginManager>.instance.GetPluginsInfo()) {

                try {
                    // Filter out bundled mods
                    // Note: Need to check camera scripts as one incompatible mod is published as a camera script!
                    if (!mod.isBuiltin) {

                        ModDescriptor descriptor = mod;

                        logWorkshopId = descriptor.ModIsLocal
                            ? LOCAL_MOD_STR
                            : descriptor.ModWorkshopId.ToString();

                        switch (descriptor.Incompatibility) {
                            case Severity.None:
                                logIncompatible = MARKER_BLANK;
                                break;
                            case Severity.Candidate:
                                logIncompatible = MARKER_TMPE;
                                if (descriptor.AssemblyGuid != CompatibilityManager.SelfGuid) {
                                    verified = false;
                                    results.Add(mod, descriptor);
                                }
                                break;
                            case Severity.Minor:
                                logIncompatible = MARKER_MINOR;
                                if (scanMinor && (mod.isEnabled || scanDisabled)) {
                                    verified = false;
                                    ++minor;
                                    results.Add(mod, descriptor);
                                }
                                break;
                            case Severity.Major:
                                logIncompatible = MARKER_MAJOR;
                                if (mod.isEnabled || scanDisabled) {
                                    verified = false;
                                    ++major;
                                    results.Add(mod, descriptor);
                                }
                                break;
                            case Severity.Critical:
                                logIncompatible = MARKER_CRITICAL;
                                verified = false;
                                ++critical;
                                results.Add(mod, descriptor);
                                break;
                            default:
                                logIncompatible = MARKER_BLANK;
                                break;
                        }

                        sb.AppendFormat(
                            LOG_ENTRY_FORMAT,
                            logIncompatible,
                            mod.isEnabled ? MARKER_ENABLED : MARKER_BLANK,
                            logWorkshopId.PadRight(12),
                            descriptor.ModName);
                    }

                } catch (Exception e) {
                    Log.ErrorFormat(
                        "Error scanning mod {0}:\n{1}",
                        mod.modPath,
                        e.ToString());
                }
            }

            sb.AppendFormat(
                "\nScan complete: {0} [C]ritical, {1} [M]ajor, {2} [m]inor; [*] = Enabled",
                critical,
                major,
                minor);

            Log.Info(sb.ToString());

            return verified;
        }



        internal static bool IsTrafficManager(string name) {
            return name.Contains("Traffic Manager")
                || name.Contains("TM:PE")
                || name.Contains("TrafficManager");
        }




    }
}

namespace TrafficManager.Compatibility {
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

    /// <summary>
    /// Scans for known incompatible mods as defined by <see cref="IncompatibleMods.List"/>.
    /// </summary>
    public class ModScanner {

        /// <summary>
        /// Game always uses ulong.MaxValue to depict local mods.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RAS0002:Readonly field for a non-readonly struct", Justification = "Rarely used.")]
        internal static readonly ulong Local = ulong.MaxValue;

        /// <summary>
        /// Scans installed mods (local and workshop) looking for known incompatibilities.
        /// </summary>
        /// 
        /// <param name="critical">A dictionary of critical incompatibilities.</param>
        /// <param name="major">A dictionary of major incompatibilities.</param>
        /// <param name="minor">A dictionary of minor incompatibilities.</param>
        /// 
        /// <returns>Returns <c>true</c> if incompatible mods detected, otherwise <c>false</c>.</returns>
        public static bool Scan(
            out Dictionary<PluginInfo, string> critical,
            out Dictionary<PluginInfo, string> major,
            out Dictionary<PluginInfo, string> minor) {

            Log.Info("ModScanner.Scan()");

            critical = new Dictionary<PluginInfo, string>();
            major = new Dictionary<PluginInfo, string>();
            minor = new Dictionary<PluginInfo, string>();

            // did we detect any incompatible mods yet?
            bool detected = false;

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
            string logEnabled;
            string logWorkshopId;
            string logIncompatible;

            // Common strings
            string entryFormat = "{0} {1} {2} {3}\n";
            string strAsterisk = "*";
            string strSpace = " ";
            string strLocal = "(local)";
            string strCritical = "C";
            string strMajor = "M";
            string strMinor = "m";

            // iterate plugins
            foreach (PluginInfo mod in Singleton<PluginManager>.instance.GetPluginsInfo()) {

                try {
                    // filter out bundled and camera script mods
                    if (!mod.isBuiltin && !mod.isCameraScript) {

                        // basic details
                        string modName = GetModName(mod);
                        ulong workshopId = mod.publishedFileID.AsUInt64;
                        bool isLocal = workshopId == Local;

                        // Values for log file entry
                        logEnabled = mod.isEnabled ? strAsterisk : strSpace;
                        logWorkshopId = isLocal ? strLocal : workshopId.ToString();
                        logIncompatible = strSpace;

                        if (isLocal) {

                            if (IsLocalModIncompatible(modName) && GetModGuid(mod) != CompatibilityManager.SelfGuid) {
                                logIncompatible = strCritical;
                                detected = true;
                                critical.Add(mod, modName);
                            }
                            modName += $" /{Path.GetFileName(mod.modPath)}";

                        } else if (IncompatibleMods.List.TryGetValue(workshopId, out Severity severity)) {

                            switch (severity) {
                                case Severity.Critical:
                                    logIncompatible = strCritical;
                                    detected = true;
                                    critical.Add(mod, modName);
                                    break;
                                case Severity.Major:
                                    logIncompatible = strMajor;
                                    if (mod.isEnabled || scanDisabled) {
                                        detected = true;
                                        major.Add(mod, modName);
                                    }
                                    break;
                                case Severity.Minor:
                                    logIncompatible = strMinor;
                                    if (scanMinor && (mod.isEnabled || scanDisabled)) {
                                        detected = true;
                                        minor.Add(mod, modName);
                                    }
                                    break;
                            }

                        }

                        sb.AppendFormat(
                            entryFormat,
                            logIncompatible,
                            logEnabled,
                            logWorkshopId.PadRight(12),
                            modName);
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
                critical.Count,
                major.Count,
                minor.Count);

            Log.Info(sb.ToString());

            return detected;
        }

        /// <summary>
        /// Identify problematic local mods by name.
        /// </summary>
        /// 
        /// <param name="name">Name of the mod.</param>
        /// 
        /// <returns>Returns <c>true</c> if incompatible, otheriwse <c>false</c>.</returns>
        internal static bool IsLocalModIncompatible(string name) {
            return name.Contains("Traffic Manager") || name.Contains("TM:PE") || name.Contains("Traffic++");
        }

        /// <summary>
        /// Returns <see cref="IUserMod.Name"/> or <see cref="PluginInfo.name"/> (assembly name) for a mod.
        /// </summary>
        /// 
        /// <param name="mod">The mods' <see cref="PluginInfo"/>.</param>
        /// 
        /// <returns>The name of the specified mod.</returns>
        internal static string GetModName(PluginInfo mod) {
            string name;
            try {
                name = ((IUserMod)mod.userModInstance).Name;
            } catch {
                Log.ErrorFormat(
                    "Unable to get userModInstrance.Name for {0}",
                    mod.modPath);
                name = mod.name;
            }
            return name;
        }

        /// <summary>
        /// Returns the <see cref="Guid"/> for a mod.
        /// </summary>
        /// 
        /// <param name="mod">The mods' <see cref="PluginInfo"/>.</param>
        /// 
        /// <returns>The <see cref="Guid"/> of the mod.</returns>
        internal static Guid GetModGuid(PluginInfo mod) {
            Guid id;
            try {
                id = mod.userModInstance.GetType().Assembly.ManifestModule.ModuleVersionId;
            } catch {
                Log.ErrorFormat(
                    "Unable to get Guid for {0}",
                    mod.modPath);
                id = Guid.Empty;
            }
            return id;
        }

    }
}

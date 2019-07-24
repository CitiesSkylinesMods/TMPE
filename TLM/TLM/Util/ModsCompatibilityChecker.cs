namespace TrafficManager.Util {
    using ColossalFramework;
    using ColossalFramework.Plugins;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using ICities;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using State;
    using UI;
    using UnityEngine;
    using static ColossalFramework.Plugins.PluginManager;

    public class ModsCompatibilityChecker {
        // Game always uses ulong.MaxValue to depict local mods
        private const ulong LOCAL_MOD = ulong.MaxValue;

        // Used for LoadIncompatibleModsList()
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        private const string INCOMPATIBLE_MODS_FILE = "incompatible_mods.txt";

        // parsed contents of incompatible_mods.txt
        private readonly Dictionary<ulong, string> knownIncompatibleMods;

        public ModsCompatibilityChecker() {
            knownIncompatibleMods = LoadListOfIncompatibleMods();
        }

        /// <summary>
        /// Initiates scan for incompatible mods. If any found, and the user has enabled the mod checker, it creates and initialises the modal dialog panel.
        /// </summary>
        public void PerformModCheck() {
            try {
                Dictionary<PluginInfo, string> detected = ScanForIncompatibleMods();

                if (detected.Count > 0) {
                    IncompatibleModsPanel panel = UIView.GetAView().AddUIComponent(typeof(IncompatibleModsPanel)) as IncompatibleModsPanel;
                    panel.IncompatibleMods = detected;
                    panel.Initialize();
                    UIView.PushModal(panel);
                    UIView.SetFocus(panel);
                }
            }
            catch (Exception e) {
                Log.Info("Something went wrong while checking incompatible mods - see main game log for details.");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Iterates installed mods looking for known incompatibilities.
        /// </summary>
        ///
        /// <returns>A list of detected incompatible mods.</returns>
        /// 
        /// <exception cref="ArgumentException">Invalid folder path (contains invalid characters, is empty, or contains only white spaces).</exception>
        /// <exception cref="PathTooLongException">Path is too long (longer than the system-defined maximum length).</exception>
        public Dictionary<PluginInfo, string> ScanForIncompatibleMods() {
            Guid selfGuid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;

            // check known incompatible mods? (incompatible_mods.txt)
            bool checkKnown = GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup;

            // only check enabled mods?
            bool filterToEnabled = GlobalConfig.Instance.Main.IgnoreDisabledMods;

            // batch all logging in to a single log message
            string logStr = $"TM:PE Incompatible Mod Checker ({checkKnown},{filterToEnabled}):\n\n";

            // list of installed incompatible mods
            Dictionary<PluginInfo, string> results = new Dictionary<PluginInfo, string>();

            // iterate plugins
            foreach (PluginInfo mod in Singleton<PluginManager>.instance.GetPluginsInfo()) {
                if (!mod.isBuiltin && !mod.isCameraScript) {

                    string strModName = GetModName(mod);
                    ulong workshopID = mod.publishedFileID.AsUInt64;
                    bool isLocal = workshopID == LOCAL_MOD;

                    string strEnabled = mod.isEnabled ? "*" : " ";
                    string strWorkshopId = isLocal ? "(local)" : workshopID.ToString();
                    string strIncompatible = " ";

                    if (knownIncompatibleMods.ContainsKey(workshopID)) {

                        strIncompatible = "!";
                        if (checkKnown && (!filterToEnabled || mod.isEnabled)) {
                            Debug.Log("[TM:PE] Incompatible mod detected: " + strModName);
                            results.Add(mod, strModName);
                        }

                    } else if (strModName.Contains("TM:PE") || strModName.Contains("Traffic Manager")) {

                        if (GetModGuid(mod) != selfGuid) {
                            string strFolder = Path.GetFileName(mod.modPath);
                            strIncompatible = "!";
                            Debug.Log("[TM:PE] Duplicate instance detected: " + strModName + " in " + strFolder);
                            results.Add(mod, strModName + " /" + strFolder);
                        }

                    }

                    logStr += $"{strIncompatible} {strEnabled} {strWorkshopId.PadRight(12)} {strModName}\n";
                }
            }

            Log.Info(logStr);
            Log.Info("Scan complete: " + results.Count.ToString() + " incompatible mod(s) found");

            return results;
        }

        /// <summary>
        /// Gets the name of the specified mod.
        /// 
        /// It will return the <see cref="IUserMod.Name"/> if found, otherwise it will return <see cref="PluginInfo.name"/> (assembly name).
        /// </summary>
        /// 
        /// <param name="plugin">The <see cref="PluginInfo"/> associated with the mod.</param>
        /// 
        /// <returns>The name of the specified plugin.</returns>
        public string GetModName(PluginInfo plugin) {
            return ((IUserMod)plugin.userModInstance).Name;
        }

        /// <summary>
        /// Gets the <see cref="Guid"/> of a mod.
        /// </summary>
        /// 
        /// <param name="plugin">The <see cref="PluginInfo"/> associated with the mod.</param>
        /// 
        /// <returns>The <see cref="Guid"/> of the mod.</returns>
        public Guid GetModGuid(PluginInfo plugin) {
            return plugin.userModInstance.GetType().Assembly.ManifestModule.ModuleVersionId;
        }

        /// <summary>
        /// Loads and parses the <c>incompatible_mods.txt</c> resource, adds other workshop branches of TM:PE as applicable.
        /// </summary>
        /// 
        /// <returns>A dictionary of mod names referenced by Steam Workshop ID.</returns>
        private Dictionary<ulong, string> LoadListOfIncompatibleMods() {
            // list of known incompatible mods
            Dictionary<ulong, string> results = new Dictionary<ulong, string>();

            // load the file
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCES_PREFIX + INCOMPATIBLE_MODS_FILE)) {
                using (StreamReader sr = new StreamReader(st)) {
                    lines = sr.ReadToEnd().Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
                }
            }

            // parse the file
            for (int i = 0; i < lines.Length; i++) {
                if (!string.IsNullOrEmpty(lines[i])) {
                    string[] strings = lines[i].Split(';');
                    if (ulong.TryParse(strings[0], out ulong steamId)) {
                        results.Add(steamId, strings[1]);
                    }
                }
            }

            Log.Info($"{RESOURCES_PREFIX}{INCOMPATIBLE_MODS_FILE} contains {results.Count} entries");

            return results;
        }
    }
}

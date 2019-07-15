using ColossalFramework;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CSUtil.Commons;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TrafficManager.UI;
using UnityEngine;
using static ColossalFramework.Plugins.PluginManager;

namespace TrafficManager.Util
{
    public class ModsCompatibilityChecker
    {
        public const ulong LOCAL_MOD = ulong.MaxValue;

        // Used for LoadIncompatibleModsList()
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        private const string INCOMPATIBLE_MODS_FILE = "incompatible_mods.txt";

        // parsed contents of incompatible_mods.txt
        private readonly Dictionary<ulong, string> knownIncompatibleMods;

        public ModsCompatibilityChecker()
        {
            knownIncompatibleMods = LoadListOfIncompatibleMods();
        }

        /// <summary>
        /// Initiates scan for incompatible mods. If any found, and the user has enabled the mod checker, it creates and initialises the modal dialog panel.
        /// </summary>
        public void PerformModCheck()
        {
            try
            {
                Dictionary<PluginInfo, string> detected = ScanForIncompatibleMods();

                if (detected.Count > 0)
                {
                    IncompatibleModsPanel panel = UIView.GetAView().AddUIComponent(typeof(IncompatibleModsPanel)) as IncompatibleModsPanel;
                    panel.IncompatibleMods = detected;
                    panel.Initialize();
                    UIView.PushModal(panel);
                    UIView.SetFocus(panel);
                }
            }
            catch (Exception e)
            {
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
        public Dictionary<PluginInfo, string> ScanForIncompatibleMods()
        {
            Guid selfGuid = Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId;

            Log.Info($"Scanning for incompatible mods; My GUID = {selfGuid}");

            // list of installed incompatible mods
            Dictionary<PluginInfo, string> results = new Dictionary<PluginInfo, string>();

            // only check enabled mods?
            bool filterToEnabled = State.GlobalConfig.Instance.Main.IgnoreDisabledMods;

            // iterate plugins
            foreach (PluginInfo mod in Singleton<PluginManager>.instance.GetPluginsInfo())
            {
                if (!mod.isBuiltin && !mod.isCameraScript && (!filterToEnabled || mod.isEnabled))
                {
                    string modName = GetModName(mod);
                    ulong workshopID = mod.publishedFileID.AsUInt64;

                    if (knownIncompatibleMods.ContainsKey(workshopID))
                    {
                        // must be online workshop mod
                        Log.Info($"Incompatible with: {workshopID} - {modName}");
                        results.Add(mod, modName);
                    }
                    else if (modName.Contains("TM:PE") || modName.Contains("Traffic Manager"))
                    {
                        // It's a TM:PE build - either local or workshop
                        string workshopIDstr = workshopID == LOCAL_MOD ? "LOCAL" : workshopID.ToString();
                        Guid currentGuid = GetModGuid(mod);

                        if (currentGuid == selfGuid)
                        {
                            Log.Info($"Found myself: '{modName}' (Workshop ID: {workshopIDstr}, GUID: {currentGuid}) in '{mod.modPath}'");
                        }
                        else
                        {
                            Log.Info($"Detected conflicting '{modName}' (Workshop ID: {workshopIDstr}, GUID: {currentGuid}) in '{mod.modPath}'");
                            results.Add(mod, $"{modName} in /{Path.GetFileName(mod.modPath)}");
                        }
                    }
                }
            }

            Log.Info($"Scan complete: {results.Count} incompatible mod(s) found");

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
        public string GetModName(PluginInfo plugin)
        {
            return ((IUserMod)plugin.userModInstance).Name;
        }

        /// <summary>
        /// Gets the <see cref="Guid"/> of a mod.
        /// </summary>
        /// 
        /// <param name="plugin">The <see cref="PluginInfo"/> associated with the mod.</param>
        /// 
        /// <returns>The <see cref="Guid"/> of the mod.</returns>
        public Guid GetModGuid(PluginInfo plugin)
        {
            return plugin.userModInstance.GetType().Assembly.ManifestModule.ModuleVersionId;
        }

        /// <summary>
        /// Loads and parses the <c>incompatible_mods.txt</c> resource, adds other workshop branches of TM:PE as applicable.
        /// </summary>
        /// 
        /// <returns>A dictionary of mod names referenced by Steam Workshop ID.</returns>
        private Dictionary<ulong, string> LoadListOfIncompatibleMods()
        {
            // list of known incompatible mods
            Dictionary<ulong, string> results = new Dictionary<ulong, string>();

            // load the file
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCES_PREFIX + INCOMPATIBLE_MODS_FILE))
            {
                using (StreamReader sr = new StreamReader(st))
                {
                    lines = sr.ReadToEnd().Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
                }
            }

            Log.Info($"{RESOURCES_PREFIX}{INCOMPATIBLE_MODS_FILE} contains {lines.Length} entries");

            // parse the file
            for (int i = 0; i < lines.Length; i++)
            {
                ulong steamId;
                string[] strings = lines[i].Split(';');
                if (ulong.TryParse(strings[0], out steamId))
                {
                    results.Add(steamId, strings[1]);
                }
            }

            return results;
        }
    }
}
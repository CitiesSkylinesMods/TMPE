using ColossalFramework;
using ColossalFramework.PlatformServices; // used in RELEASE builds
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CSUtil.Commons;
using ICities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // used in RELEASE builds
using System.Reflection;
using TrafficManager.UI;
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
        private readonly Dictionary<ulong, string> incompatibleMods;

        public ModsCompatibilityChecker()
        {
            incompatibleMods = LoadListOfIncompatibleMods();
        }

        /// <summary>
        /// Initiates scan for incompatible mods. If any found, and the user has enabled the mod checker, it creates and initialises the modal dialog panel.
        /// </summary>
        public void PerformModCheck()
        {
            Dictionary<PluginInfo, string> detected = ScanForIncompatibleMods();

            if (detected.Count > 0 && State.GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup)
            {
                IncompatibleModsPanel panel = UIView.GetAView().AddUIComponent(typeof(IncompatibleModsPanel)) as IncompatibleModsPanel;
                panel.IncompatibleMods = detected;
                panel.Initialize();
                UIView.PushModal(panel);
                UIView.SetFocus(panel);
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
            Log.Info("Scanning for incompatible mods");

            // list of installed incompatible mods
            Dictionary<PluginInfo, string> results = new Dictionary<PluginInfo, string>();

            // only check enabled mods?
            bool filterToEnabled = State.GlobalConfig.Instance.Main.IgnoreDisabledMods;

#if !DEBUG
            bool offline = IsOffline();
#endif

            // iterate plugins
            foreach (PluginInfo mod in Singleton<PluginManager>.instance.GetPluginsInfo())
            {
                if (!mod.isBuiltin && !mod.isCameraScript && (!filterToEnabled || mod.isEnabled))
                {
                    string modName = GetModName(mod);

                    if (incompatibleMods.ContainsKey(mod.publishedFileID.AsUInt64))
                    {
                        Log.Info($"Incompatible mod: {mod.publishedFileID.AsUInt64} - {modName}");
                        results.Add(mod, modName);
                    }
#if !DEBUG
                    // Workshop TM:PE builds treat local builds as incompatible
                    else if (!offline && mod.publishedFileID.AsUInt64 == LOCAL_MOD && (modName.Contains("TM:PE") || modName.Contains("Traffic Manager")))
                    {
                        Log.Info($"Local TM:PE detected: '{modName}' in '{mod.modPath}'");
                        string folder = mod.modPath.Split(Path.DirectorySeparatorChar).Last();
                        results.Add(mod, $"{modName} in /{folder}");
                    }
#endif
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
            string name = plugin.name;
            IUserMod[] instances = plugin.GetInstances<IUserMod>();
            if (instances.Length > 0)
            {
                name = instances[0].Name;
            }
            return name;
        }

        /// <summary>
        /// Works out if the game is effectively running in offline mode, in which no workshop mod subscriptions will be active.
        /// 
        /// Applicalbe "offline" states include:
        /// 
        /// * Origin plaform service (no support for Steam workshop)
        /// * Steam (or other platform service) not active
        /// * --noWorkshop launch option
        /// 
        /// This is allows LABS and STABLE builds to be used offline without trying to delete themselves.
        /// </summary>
        /// 
        /// <returns>Returns <c>true</c> if game is offline for any reason, otherwise <c>false</c>.</returns>
#if !DEBUG
        private bool IsOffline()
        {
            // TODO: Work out if TGP and QQGame platform services allow workshop
            if (PluginManager.noWorkshop)
            {
                return true;
            }
            else if (PlatformService.platformType == PlatformType.Origin)
            {
                return true;
            }
            else if (!PlatformService.active)
            {
                return true;
            }
            return false;
        }
#endif

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

            Log.Info($"{INCOMPATIBLE_MODS_FILE} contains {lines.Length} entries");

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

            // Treat other workshop-published branches of TM:PE, as applicable, as conflicts
#if LABS
            results.Add(583429740u , "TM:PE STABLE");
#elif DEBUG
            results.Add(1637663252u, "TM:PE LABS");
            results.Add(583429740u, "TM:PE STABLE");
#else
            results.Add(1637663252u, "TM:PE LABS");
#endif

            return results;
        }
    }
}
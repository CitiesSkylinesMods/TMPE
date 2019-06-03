using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CSUtil.Commons;
using ICities;
using TrafficManager.UI;
using UnityEngine;
using static ColossalFramework.Plugins.PluginManager;

namespace TrafficManager.Util {
    public class ModsCompatibilityChecker {
        //TODO include %APPDATA% mods folder
        private const ulong LOCAL_TMPE = 0u;

        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        private const string DEFAULT_INCOMPATIBLE_MODS_FILENAME = "incompatible_mods.txt";
        private readonly ulong[] userModList;
        private readonly Dictionary<ulong, string> incompatibleModList;

        public ModsCompatibilityChecker() {
            incompatibleModList = LoadIncompatibleModList();
            userModList = GetUserModsList();
        }

        public void PerformModCheck() {
            Log.Info("Performing incompatible mods check");
            Dictionary<ulong, string> incompatibleMods = new Dictionary<ulong, string>();
            for (int i = 0; i < userModList.Length; i++) {
                string incompatibleModName;
                if (incompatibleModList.TryGetValue(userModList[i], out incompatibleModName)) {
                    incompatibleMods.Add(userModList[i], incompatibleModName);
                }
            }

#if !DEBUG
            if (HasLocalTMPE())
            {
                incompatibleMods.Add(LOCAL_TMPE, "TM:PE Local Build");
            }
#endif

            if (incompatibleMods.Count > 0) {
                Log.Warning("Incompatible mods detected! Count: " + incompatibleMods.Count);

                if (State.GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup) {
                    IncompatibleModsPanel panel = UIView.GetAView().AddUIComponent(typeof(IncompatibleModsPanel)) as IncompatibleModsPanel;
                    panel.IncompatibleMods = incompatibleMods;
                    panel.Initialize();
                    UIView.PushModal(panel);
                    UIView.SetFocus(panel);
                }
            } else {
                Log.Info("No incompatible mods detected");
            }
        }

        private Dictionary<ulong, string> LoadIncompatibleModList() {
            Dictionary<ulong, string> incompatibleMods = new Dictionary<ulong, string>();
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCES_PREFIX + DEFAULT_INCOMPATIBLE_MODS_FILENAME)) {
                using (StreamReader sr = new StreamReader(st)) {
                    lines = sr.ReadToEnd().Split(new string[] {"\n", "\r\n"}, StringSplitOptions.None);
                }
            }

            for (int i = 0; i < lines.Length; i++) {
                string[] strings = lines[i].Split(';');
                ulong steamId;
                if (ulong.TryParse(strings[0], out steamId)) {
                    incompatibleMods.Add(steamId, strings[1]);
                }
            }

            // Treat any other branches of TM:PE as incompatible (causes issues with saving road customisations).
            // This still won't detect local builds, but will reduce support workload from end users who have
            // multiple TM:PE subscribed.
            // See also: https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues/211 
#if LABS
            incompatibleMods.Add(583429740u , "TM:PE STABLE");
#elif DEBUG // assume local build
            incompatibleMods.Add(1637663252u, "TM:PE LABS");
            incompatibleMods.Add(583429740u , "TM:PE STABLE");
#else // STABLE
            incompatibleMods.Add(1637663252u, "TM:PE LABS");
#endif

            return incompatibleMods;
        }

        private ulong[] GetUserModsList() {
            if (State.GlobalConfig.Instance.Main.IgnoreDisabledMods) {
                return PluginManager.instance.GetPluginsInfo().Where(plugin => plugin.isEnabled).Select(info => info.publishedFileID.AsUInt64).ToArray();
            }
            PublishedFileId[] ids = ContentManagerPanel.subscribedItemsTable.ToArray();
            return ids.Select(id => id.AsUInt64).ToArray();
        }

        private bool HasLocalTMPE()
        {
            try
            {
                foreach (PluginInfo plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                {
                    // How to detect local mod: plugin.publishedFileID.AsUInt64 == ulong.MaxValue
                    if (!plugin.isBuiltin && !plugin.isCameraScript && plugin.publishedFileID.AsUInt64 == ulong.MaxValue && GetModName(plugin).Contains("TM:PE"))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning("ModsCompatibilityChecker.HasLocalTMPE() error - see game log for details");
                Debug.LogException(e);
            }
            return false;
        }

        // returns name of mod as defined in the IUserMod class of that mod
        private string GetModName(PluginInfo plugin)
        {
            string name = plugin.name;
            IUserMod[] instances = plugin.GetInstances<IUserMod>();
            if ((int)instances.Length > 0)
            {
                name = instances[0].Name;
            }
            return name;
        }
    }
}
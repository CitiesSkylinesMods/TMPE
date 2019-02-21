using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ColossalFramework.PlatformServices;
using ColossalFramework.UI;
using CSUtil.Commons;
using UnityEngine;

namespace TrafficManager.Util {
    public class ModsCompatibilityChecker {
        private const string RESOURCES_PREFIX = "TrafficManager.Resources.";
        private static readonly string DEFAULT_INCOMPATIBLE_MODS_FILENAME = "incompatible_mods.txt";
        private static readonly string STEAM_WORKSHOP_URL_PART = "https://steamcommunity.com/sharedfiles/filedetails/?id=";
        private ulong[] userModList;
        private Dictionary<ulong, string> incompatibleModList;

        public ModsCompatibilityChecker() {
            incompatibleModList = LoadIncompatibleModList();
            userModList = GetUserModsList();
        }

        private string BuildUrl(ulong steamId) {
            return STEAM_WORKSHOP_URL_PART + steamId;
        }

        public void PerformModCheck() {
            Log.Info("Performing incompatible mods check");
            Dictionary<ulong, string> incompatibleMods = new Dictionary<ulong, string>();
            for (int i = 0; i < userModList.Length; i++) {
                if (incompatibleModList.TryGetValue(userModList[i], out string incompatibleModName)) {
                    incompatibleMods.Add(userModList[i], incompatibleModName);
                }        
            }

            if (incompatibleMods.Count > 0) {
                StringBuilder builder = new StringBuilder().AppendLine()
                    .AppendLine("Following list of mods is incompatible with current version of Traffic Manager: President Edition")
                    .AppendLine("Please unsubscribe all mods from list below to prevent unexpected errors in game");
                incompatibleMods.ForEach(pair => { builder.Append(pair.Value).AppendLine(BuildUrl(pair.Key)); });
                UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("TM:PE Incompatible mods detected", builder.ToString(), false);
                Log.Info(builder.ToString());
            } else {
                Log.Info("No incompatible mods detected");
            }
        }
        private Dictionary<ulong, string> LoadIncompatibleModList() {
            Dictionary<ulong, string> incompatibleMods = new Dictionary<ulong, string>();
            string[] lines;
            using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(RESOURCES_PREFIX + DEFAULT_INCOMPATIBLE_MODS_FILENAME)) {
                using (StreamReader sr = new StreamReader(st)) {
                    lines = sr.ReadToEnd().Split(new string[] { "\n", "\r\n" }, StringSplitOptions.None);
                }
            }

            for (int i = 0; i < lines.Length; i++) {
                string[] strings = lines[i].Split(';');
                ulong steamId;
                if (ulong.TryParse(strings[0], out steamId)) {
                    
                incompatibleMods.Add(steamId, strings[1]);
                }
            }
            return incompatibleMods;
        }

        private ulong[] GetUserModsList() {
            PublishedFileId[] ids = ContentManagerPanel.subscribedItemsTable.ToArray();
            return ids.Select(id => id.AsUInt64).ToArray();
        }
    }
}
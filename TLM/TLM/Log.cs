using System;
using UnityEngine;

namespace TrafficManager {

    public static class Log {
        const string Prefix = "TrafficLightManager: ";
		private static readonly bool InGameDebug = true; // Environment.OSVersion.Platform != PlatformID.Unix;

        public static void Message(string s) {
#if DEBUG
            try {
                if (InGameDebug) {
                    //DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, Prefix + s.ToString());
                    Debug.Log(Prefix + s);
                }
            } catch (Exception) {
                // cross thread issue?
            }
            //Debug.Log(Prefix + s.ToString());
#endif
        }

        public static void Error(string s) {
            try {
                if (InGameDebug) {
#if DEBUG
					Debug.LogError(Prefix + s);
#else
					DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s);
#endif
				}
            } catch (Exception) {
                // cross thread issue?
            }
            //Debug.LogError(Prefix + s.ToString());
        }

        public static void Warning(string s) {
            try {
                if (InGameDebug) {
#if DEBUG
					Debug.LogWarning(Prefix + s);
#else
					DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s);
#endif
				}
			} catch (Exception) {
                // cross thread issue?
            }
            //Debug.LogWarning(Prefix + s.ToString());
        }
    }

}

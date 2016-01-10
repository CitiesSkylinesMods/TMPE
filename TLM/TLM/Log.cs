using System;
using UnityEngine;

namespace TrafficManager {

    public static class Log {
        const string Prefix = "TrafficLightManager: ";
        private static readonly bool InGameDebug = Environment.OSVersion.Platform != PlatformID.Unix;

        public static void Message(object s) {
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

        public static void Error(object s) {
            try {
                if (InGameDebug) {
                    //DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s.ToString());
                    Debug.LogError(Prefix + s);
                }
            } catch (Exception) {
                // cross thread issue?
            }
            //Debug.LogError(Prefix + s.ToString());
        }

        public static void Warning(object s) {
            try {
                if (InGameDebug) {
                    //DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s.ToString());
                    Debug.LogWarning(Prefix + s);
                }
            } catch (Exception) {
                // cross thread issue?
            }
            //Debug.LogWarning(Prefix + s.ToString());
        }
    }

}

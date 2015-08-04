using System;
using UnityEngine;

namespace TrafficManager
{

    public static class Log
    {
        const string Prefix = "TrafficLightManager: ";
        private static readonly bool InGameDebug = Environment.OSVersion.Platform != PlatformID.Unix;

        public static void Message(object s)
        {
            try
            {
                if (InGameDebug)
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, Prefix + s.ToString());
            }
            catch (Exception)
            {
                // cross thread issue?
            }
            Debug.Log(Prefix + s.ToString());
        }

        public static void Error(object s)
        {
            try
            {
                if (InGameDebug)
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s.ToString());
            }
            catch (Exception)
            {
                // cross thread issue?
            }
            Debug.LogError(Prefix + s.ToString());
        }

        public static void Warning(object s)
        {
            try
            {
                if (InGameDebug)
                    DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s.ToString());
            }
            catch (Exception)
            {
                // cross thread issue?
            }
            Debug.LogWarning(Prefix + s.ToString());
        }


        internal static void Warning(InstanceType instanceType)
        {
            if (InGameDebug)
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix);
            Debug.LogWarning(Prefix);
        }
    }

}

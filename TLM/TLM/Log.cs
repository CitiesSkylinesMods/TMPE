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
            if(InGameDebug)
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, Prefix + s.ToString());
            Debug.Log(Prefix + s.ToString());
        }

        public static void Error(object s)
        {
            if (InGameDebug)
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s.ToString());
            Debug.LogError(Prefix + s.ToString());
        }

        public static void Warning(object s)
        {
            if (InGameDebug)
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s.ToString());
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

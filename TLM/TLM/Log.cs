using System;

namespace TrafficManager
{

    public static class Log
    {
        const string Prefix = "TrafficLightManager: ";

        public static void Message(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, Prefix + s.ToString());
        }

        public static void Error(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, Prefix + s.ToString());
        }

        public static void Warning(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + s.ToString());
        }


        internal static void Warning(InstanceType instanceType)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, Prefix + String.Empty.ToString());
        }
    }

}

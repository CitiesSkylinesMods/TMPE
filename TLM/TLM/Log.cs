namespace TrafficManager
{

    public static class Log
    {
        const string prefix = "TrafficLightManager: ";

        public static void Message(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, prefix + s.ToString());
        }

        public static void Error(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Error, prefix + s.ToString());
        }

        public static void Warning(object s)
        {
            DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Warning, prefix + s.ToString());
        }


        internal static void Warning(InstanceType instanceType)
        {
            throw new System.NotImplementedException();
        }
    }

}

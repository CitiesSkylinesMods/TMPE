namespace TrafficManager.State.MultiplayerAPIIntegration
{
    using CSM.API;
    using CSM.Helpers;
    using CSUtil.Commons;
    using TrafficManager.API;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State.MultiplayerAPIIntegration.Commands;
    using TrafficManager.Util;

    /// <summary>
    /// Allows TMPE to connect to CSM (Cities Skylines Multiplayer),
    /// sending notification to all clients.
    /// </summary>
    internal class CSMConnection : Connection {
        public static IgnoreHelper ignoreHelper;
        /// <summary>
        /// Initializes a new instance of the <see cref="CSMConnection"/> class.
        /// Sets name, and Level loaded event listener.
        /// </summary>
        public CSMConnection()
        {
            this.name = "Traffic Manger: PE";
            ignoreHelper = new IgnoreHelper();
            Notifier.EventLevelLoaded += this.OnLevelLoad;
        }

        private void NotificationListener(OnModifiedEventArgs eventArgs)
        {
#if DEBUG
            bool logCSMNotification = DebugSwitch.CSMNotification.Get();
#else
            const bool logCSMNotification = false;
#endif
            if (ignoreHelper.IsIgnored())
            {
                return;
            }
            SimulationManager.instance.AddAction(() =>
            {
                string base64Data = null;
                TMPEMoveItIntegration moveItIntegration = new TMPEMoveItIntegration();
                object data = moveItIntegration.Copy(eventArgs.InstanceID);
                if (data != null)
                {
                    base64Data = moveItIntegration.Encode64(data);
                }

                this.SentToAll(new TMPENotification
                {
                    Base64RecordObject = base64Data,
                    DataVersion = VersionUtil.ModVersion.ToString(),
                });

                if (logCSMNotification)
                {
                    Log._Debug($"CSMConnection.NotificationListener({eventArgs.InstanceID}): sent data to all. ");
                }
            });
        }

        private void OnLevelLoad()
        {
            SimulationManager.instance.m_ManagersWrapper.threading.QueueSimulationThread(() => {
                Notifier.EventModified += this.NotificationListener;
            });
        }
    }
}

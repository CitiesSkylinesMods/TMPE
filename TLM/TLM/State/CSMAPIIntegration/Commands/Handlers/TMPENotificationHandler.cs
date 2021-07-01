namespace TrafficManager.State.MultiplayerAPIIntegration.Commands {
    using CSM.API.Commands;
    using CSM.Helpers;
    using MoveItIntegration;

    /// <summary>
    ///  A Command handler for Notifications sent by TMPE with CSM over network from external.
    /// </summary>
    public class TMPENotificationHandler : CommandHandler<TMPENotification>
    {
        /// <summary>
        /// Handles the processing of incoming TMPENotification from other players
        /// applying the data received to the local player.
        /// </summary>
        /// <param name="command">The TMPE Notification containing other players changes.</param>
        protected override void Handle(TMPENotification command)
        {
            IgnoreHelper.Instance.StartIgnore();
            TMPEMoveItIntegrationFactory moveItIntegrationFactory = new TMPEMoveItIntegrationFactory();
            MoveItIntegrationBase moveItIntegration = moveItIntegrationFactory.GetInstance();
            System.Version version = new System.Version(command.DataVersion);
            object record = moveItIntegration.Decode64(command.Base64RecordObject, version);
            moveItIntegration.Paste(InstanceID.Empty, record, null);
            IgnoreHelper.Instance.EndIgnore();
        }
    }
}

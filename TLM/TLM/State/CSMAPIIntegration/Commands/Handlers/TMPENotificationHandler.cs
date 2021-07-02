namespace TrafficManager.State.MultiplayerAPIIntegration.Commands {
    using System;
    using System.Diagnostics;
    using CitiesGameBridge.Service;
    using ColossalFramework;
    using CSM.API.Commands;
    using CSM.Helpers;
    using CSUtil.Commons;
    using MoveItIntegration;
    using TrafficManager.API;
    using Util;
    using Util.Record;
    using static TrafficManager.Util.Shortcuts;

    /// <summary>
    ///  A Command handler for Notifications sent by TMPE with CSM over network from external.
    /// </summary>
    public class TMPENotificationHandler : CommandHandler<TMPENotification> {

        readonly TMPEMoveItIntegrationFactory moveItIntegrationFactory = new TMPEMoveItIntegrationFactory();
        /// <summary>
        /// Handles the processing of incoming TMPENotification from other players
        /// applying the data received to the local player.
        /// </summary>
        /// <param name="command">The TMPE Notification containing other players changes.</param>
        protected override void Handle(TMPENotification command) {
            MoveItIntegrationBase moveItIntegration = moveItIntegrationFactory.GetInstance();
            System.Version version = new System.Version(command.DataVersion);
            object record = moveItIntegration.Decode64(command.Base64RecordObject, version);
            EnsureAllLanesAreValid(record);
        }

        private void EnsureAllLanesAreValid(object record) {

            int laneInvalid = 0;
            if (record is TrafficRulesRecord r) {
                foreach (var trafficRecords in r.Records) {
                    if (trafficRecords is SegmentRecord segmentRecord) {
                        foreach (uint laneId in segmentRecord.allLaneIds_) {
                            if (!netService.IsLaneAndItsSegmentValid(laneId)) {
                                laneInvalid++;
                                break;
                            }
                        }
                    }
                }
            }
            if (laneInvalid == 0) {
                PasteRecord(record);
            } else {
                Log.Info("TMPENotificationHandler: Dropped notification - " + record.ToString());
            }
        }

        private void PasteRecord(object record) {
            MoveItIntegrationBase moveItIntegration = moveItIntegrationFactory.GetInstance();
            SimulationManager.instance.AddAction(() => CSMConnection.ignoreHelper.StartIgnore());
            try {
                moveItIntegration.Paste(InstanceID.Empty, record, null);
            }
            catch (Exception e) {
                Log.Warning("TMPENotificationHandler: Could not paste record - " + e.Message);
            }
            
            SimulationManager.instance.AddAction(() => CSMConnection.ignoreHelper.EndIgnore());
        }
    }
}

namespace TrafficManager
{
    using System;
    using TrafficManager.API.Notifier;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    public class Notifier : INotifier
    {
        public event Action EventLevelLoaded;

        // TODO [issue #967]: notify TTL Start/Stop events
        public event Action<OnModifiedEventArgs> EventModified;

        public static Notifier Instance { get; } = new Notifier();

        public void OnLevelLoaded() => EventLevelLoaded?.Invoke();

        private void OnModified(OnModifiedEventArgs args)
        {
            SimulationManager.instance.AddAction(() =>
                EventModified?.Invoke(args));
        }

        public void OnSegmentModified(ushort segmentId, object sender = null, object data = null) {
            // skip if no listeners
            if (EventModified != null) {
                OnModified(
                    new OnModifiedEventArgs {
                        InstanceID = new InstanceID { NetSegment = segmentId },
                        Sender = sender,
                        Data = data,
                    });
            }
        }

        public void OnNodeModified(ushort nodeId, object sender = null, object data = null)
        {
            // skip if no listeners
            if (EventModified != null) {
                OnModified(
                    new OnModifiedEventArgs {
                        InstanceID = new InstanceID { NetNode = nodeId },
                        Sender = sender,
                        Data = data,
                    });
            }
        }

        public void OnSegmentNodesModified(ushort segmentId, object sender = null, object data = null)
        {
            // skip if no listeners
            if (EventModified != null) {
                ref NetSegment netSegment = ref segmentId.ToSegment();
                OnNodeModified(netSegment.m_startNode, sender, data);
                OnNodeModified(netSegment.m_endNode, sender, data);
            }
        }
    }
}

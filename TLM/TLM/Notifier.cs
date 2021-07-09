namespace TrafficManager
{
    using System;
    using TrafficManager.API.Notifier;
    public class Notifier : INotifier
    {
        public event Action EventLevelLoaded;

        // TODO [issue #967]: notify TTL Start/Stop events
        public event Action<OnModifiedEventArgs> EventModified;

        public static Notifier Instance { get; } = new Notifier();

        public void OnLevelLoaded() => EventLevelLoaded?.Invoke();

        public void OnModified(OnModifiedEventArgs args)
        {
            SimulationManager.instance.AddAction(() =>
                EventModified?.Invoke(args));
        }

        public void OnSegmentModified(ushort segmentId, object sender = null, object data = null) {
            OnModified(new OnModifiedEventArgs {
                InstanceID = new InstanceID { NetSegment = segmentId },
                Sender = sender,
                Data = data,
            });
        }

        public void OnNodeModified(ushort nodeId, object sender = null, object data = null)
        {
            OnModified(new OnModifiedEventArgs
            {
                InstanceID = new InstanceID { NetNode = nodeId },
                Sender = sender,
                Data = data,
            });
        }

        public void OnSegmentNodesMofied(ushort segmentId, object sender = null, object data = null)
        {
            var segments = NetManager.instance.m_segments.m_buffer;
            OnNodeModified(segments[segmentId].m_startNode, sender, data);
            OnNodeModified(segments[segmentId].m_endNode, sender, data);
        }
    }
}

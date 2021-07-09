using System;

namespace TrafficManager.API
{
    public class OnModifiedEventArgs
    {
        /// <summary>
        /// node/segment (citizen/vehicle in future) that has been modified.
        /// </summary>
        public InstanceID InstanceID;

        /// <summary>
        /// Reserved: the sender that caused the rule to be modified.
        /// </summary>
        public object Sender;

        /// <summary>
        /// Reserved: The rule that has been modified. object type is decided by sender.
        /// </summary>
        public object Data;
    }

    public class Notifier
    {
        public static Notifier _instance;
        public static Notifier Instance => _instance ??= new Notifier();


        public static Action EventLevelLoaded;

        // TODO [issue #967]: notify TTL Start/Stop events
        public static Action<OnModifiedEventArgs> EventModified;

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

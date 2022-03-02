namespace TrafficManager.Manager.Impl {
    using System;
    using TrafficManager.API;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Util;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    internal class GeometryNotifier : IObserver<GeometryUpdate> {
        void IObserver<GeometryUpdate>.OnUpdate(GeometryUpdate subject) {
            try {
                if (subject.nodeId is ushort nodeId) {
                    Notifier.Instance.OnNodeModified(nodeId, this);
                } else if (subject.segment is ExtSegment segmentExt) {
                    Notifier.Instance.OnSegmentModified(segmentExt.segmentId, this);
                }else  if (subject.replacement.newSegmentEndId is ISegmentEndId newSegmentEndId) {
                    ushort nodeId2 = newSegmentEndId.SegmentId.ToSegment().GetNodeId(newSegmentEndId.StartNode);
                    Notifier.Instance.OnNodeModified(nodeId2, this);
                }
            } catch (Exception ex) {
                Shortcuts.LogException(ex);
            }
        }
    }
}
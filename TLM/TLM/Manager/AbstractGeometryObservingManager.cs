namespace TrafficManager.Manager {
    using System;
    using CSUtil.Commons;
    using Geometry;
    using State;
    using State.ConfigData;
    using Traffic.Data;
    using Util;

    public abstract class AbstractGeometryObservingManager : AbstractCustomManager, IObserver<GeometryUpdate> {
        private IDisposable geoUpdateUnsubscriber = null;

        private object geoLock = new object();

        /// <summary>
        /// Handles an invalid segment
        /// </summary>
        /// <param name="geometry">segment geometry</param>
        protected virtual void HandleInvalidSegment(ref ExtSegment seg) { }

        /// <summary>
        /// Handles a valid segment
        /// </summary>
        /// <param name="geometry">segment geometry</param>
        protected virtual void HandleValidSegment(ref ExtSegment seg) { }

        /// <summary>
        /// Handles an invalid node
        /// </summary>
        /// <param name="geometry">node geometry</param>
        protected virtual void HandleInvalidNode(ushort nodeId, ref NetNode node) { }

        /// <summary>
        /// Handles a valid node
        /// </summary>
        /// <param name="geometry">node geometry</param>
        protected virtual void HandleValidNode(ushort nodeId, ref NetNode node) { }

        /// <summary>
        /// Handles a segment replacement
        /// </summary>
        /// <param name="replacement">segment replacement</param>
        /// <param name="newEndGeo">new segment end geometry</param>
        protected virtual void HandleSegmentEndReplacement(SegmentEndReplacement replacement, ref ExtSegmentEnd segEnd) { }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
        }

        public override void OnLevelLoading() {
            base.OnLevelLoading();
            geoUpdateUnsubscriber = Constants.ManagerFactory.GeometryManager.Subscribe(this);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            if (geoUpdateUnsubscriber != null) {
                geoUpdateUnsubscriber.Dispose();
            }
        }

        public void OnUpdate(GeometryUpdate update) {
            if (update.segment != null) {
                // Handle a segment update
                ExtSegment seg = (ExtSegment)update.segment;
                if (!seg.valid) {
#if DEBUG
                    if (DebugSwitch.GeometryDebug.Get())
                        Log._Debug($"{this.GetType().Name}.HandleInvalidSegment({seg.segmentId})");
#endif
                    HandleInvalidSegment(ref seg);
                } else {
#if DEBUG
                    if (DebugSwitch.GeometryDebug.Get())
                        Log._Debug($"{this.GetType().Name}.HandleValidSegment({seg.segmentId})");
#endif
                    HandleValidSegment(ref seg);
                }
            } else if (update.nodeId != null) {
                // Handle a node update
                ushort nodeId = (ushort)update.nodeId;
                Services.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
                    if ((node.m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) == NetNode.Flags.Created) {
#if DEBUG
                        if (DebugSwitch.GeometryDebug.Get())
                            Log._Debug($"{this.GetType().Name}.HandleValidNode({nodeId})");
#endif
                        HandleValidNode(nodeId, ref node);
                    } else {
#if DEBUG
                        if (DebugSwitch.GeometryDebug.Get())
                            Log._Debug($"{this.GetType().Name}.HandleInvalidNode({nodeId})");
#endif
                        HandleInvalidNode(nodeId, ref node);
                    }
                    return true;
                });
            } else {
                // Handle a segment end replacement
                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

#if DEBUG
                if (DebugSwitch.GeometryDebug.Get())
                    Log._Debug($"{this.GetType().Name}.HandleSegmentReplacement({update.replacement.oldSegmentEndId} -> {update.replacement.newSegmentEndId})");
#endif
                HandleSegmentEndReplacement(update.replacement, ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(update.replacement.newSegmentEndId.SegmentId, update.replacement.newSegmentEndId.StartNode)]);
            }
        }

        ~AbstractGeometryObservingManager() {
            if (geoUpdateUnsubscriber != null) {
                geoUpdateUnsubscriber.Dispose();
            }
        }
    }
}

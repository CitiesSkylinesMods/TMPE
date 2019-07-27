namespace TrafficManager.Manager {
    using System;
    using API.Manager;
    using CSUtil.Commons;
    using Geometry;
    using JetBrains.Annotations;
    using State.ConfigData;
    using Traffic.Data;
    using Util;

    public abstract class AbstractGeometryObservingManager : AbstractCustomManager, IObserver<GeometryUpdate> {
        private IDisposable geoUpdateUnsubscriber;

        [UsedImplicitly]
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
#if DEBUG
            bool logGeometry = DebugSwitch.GeometryDebug.Get();
#else
            const bool logGeometry = false;
#endif
            if (update.segment != null) {
                // Handle a segment update
                ExtSegment seg = (ExtSegment)update.segment;

                if (!seg.valid) {
                    if (logGeometry) {
                        Log._Debug($"{GetType().Name}.HandleInvalidSegment({seg.segmentId})");
                    }

                    HandleInvalidSegment(ref seg);
                } else {
                    if (logGeometry) {
                        Log._Debug($"{GetType().Name}.HandleValidSegment({seg.segmentId})");
                    }

                    HandleValidSegment(ref seg);
                }
            } else if (update.nodeId != null) {
                // Handle a node update
                ushort nodeId = (ushort)update.nodeId;
                Services.NetService.ProcessNode(
                    nodeId,
                    (ushort nId, ref NetNode node) => {
                        if ((node.m_flags &
                             (NetNode.Flags.Created | NetNode.Flags.Deleted)) ==
                            NetNode.Flags.Created) {
                            if (logGeometry) {
                                Log._Debug($"{GetType().Name}.HandleValidNode({nodeId})");
                            }

                            HandleValidNode(nodeId, ref node);
                        } else {
                            if (logGeometry) {
                                Log._Debug($"{GetType().Name}.HandleInvalidNode({nodeId})");
                            }

                            HandleInvalidNode(nodeId, ref node);
                        }

                        return true;
                    });
            } else {
                // Handle a segment end replacement
                IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

                if (logGeometry) {
                    Log._Debug(
                        $"{GetType().Name}.HandleSegmentReplacement({update.replacement.oldSegmentEndId} " +
                        $"-> {update.replacement.newSegmentEndId})");
                }

                int index0 = segEndMan.GetIndex(
                    update.replacement.newSegmentEndId.SegmentId,
                    update.replacement.newSegmentEndId.StartNode);

                HandleSegmentEndReplacement(
                    update.replacement,
                    ref segEndMan.ExtSegmentEnds[index0]);
            }
        }

        ~AbstractGeometryObservingManager() {
            geoUpdateUnsubscriber?.Dispose();
        }
    }
}
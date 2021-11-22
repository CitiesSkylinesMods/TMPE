namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Threading;
    using System;
    using TrafficManager.API.Geometry;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Util;
    using TrafficManager.Geometry;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    public class GeometryManager
        : AbstractCustomManager,
          IGeometryManager
    {
        public static GeometryManager Instance { get; } = new GeometryManager();

        private class GeometryUpdateObservable : GenericObservable<GeometryUpdate> {
        }

        private bool stateUpdated;
        private readonly ulong[] updatedSegmentBuckets;
        private readonly ulong[] updatedNodeBuckets;
        private readonly object updateLock;
        private readonly Queue<SegmentEndReplacement> segmentReplacements;
        private readonly GeometryUpdateObservable geometryUpdateObservable;

        private GeometryManager() {
            stateUpdated = false;
            updatedSegmentBuckets = new ulong[576];
            updatedNodeBuckets = new ulong[512];
            updateLock = new object();
            segmentReplacements = new Queue<SegmentEndReplacement>();
            geometryUpdateObservable = new GeometryUpdateObservable();
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            segmentReplacements.Clear();
            SimulationStep();
        }

        public void OnUpdateSegment(ref ExtSegment seg) {
            MarkAsUpdated(ref seg);
        }

        public void SimulationStep(bool onlyFirstPass = false) {
#if DEBUG
            bool logGeometry = DebugSwitch.GeometryDebug.Get();
#else
            const bool logGeometry = false;
#endif
            if (!stateUpdated) {
                return;
            }

            NetManager netManager = Singleton<NetManager>.instance;
            if (!onlyFirstPass && (netManager.m_segmentsUpdated || netManager.m_nodesUpdated)) {
                // TODO maybe refactor NetManager use (however this could influence performance)
                if (logGeometry) {
                    Log._Debug(
                        $"GeometryManager.SimulationStep(): Skipping! stateUpdated={stateUpdated}, " +
                        $"m_segmentsUpdated={netManager.m_segmentsUpdated}, " +
                        $"m_nodesUpdated={netManager.m_nodesUpdated}");
                }

                return;
            }

            lock(updateLock) {

                bool updatesMissing = onlyFirstPass;

                for (var pass = 0; pass < (onlyFirstPass ? 1 : 2); ++pass) {
                    bool firstPass = pass == 0;

                    int len = updatedSegmentBuckets.Length;
                    for (var i = 0; i < len; i++) {
                        ulong segMask = updatedSegmentBuckets[i];

                        if (segMask == 0uL) {
                            continue;
                        }

                        for (var m = 0; m < 64; m++) {
                            if ((segMask & 1uL << m) == 0uL) {
                                continue;
                            }

                            ushort segmentId = (ushort)(i << 6 | m);
                            ExtSegment seg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId];

                            if (firstPass ^ !seg.valid) {
                                if (!firstPass) {
                                    updatesMissing = true;
                                    if (logGeometry) {
                                        Log.Warning(
                                            "GeometryManager.SimulationStep(): Detected invalid " +
                                            $"segment {segmentId} in second pass");
                                    }
                                }

                                continue;
                            }

                            if (logGeometry) {
                                Log._Debug(
                                    $"GeometryManager.SimulationStep(): Notifying observers about " +
                                    $"segment {segmentId}. Valid? {seg.valid} First pass? {firstPass}");
                            }

                            NotifyObservers(new GeometryUpdate(ref seg));
                            updatedSegmentBuckets[i] &= ~(1uL << m);
                        }
                    }

                    len = updatedNodeBuckets.Length;

                    for (var i = 0; i < len; i++) {
                        ulong nodeMask = updatedNodeBuckets[i];

                        if (nodeMask == 0uL) {
                            continue;
                        }

                        for (var m = 0; m < 64; m++) {
                            if ((nodeMask & 1uL << m) == 0uL) {
                                continue;
                            }

                            ushort nodeId = (ushort)(i << 6 | m);
                            bool valid = nodeId.ToNode().IsValid();

                            if (firstPass ^ !valid) {
                                if (!firstPass) {
                                    updatesMissing = true;

                                    if (logGeometry) {
                                        Log.Warning(
                                            "GeometryManager.SimulationStep(): Detected invalid " +
                                            $"node {nodeId} in second pass");
                                    }
                                }

                                continue;
                            }

                            if (logGeometry) {
                                Log._Debug(
                                    "GeometryManager.SimulationStep(): Notifying observers about " +
                                    $"node {nodeId}. Valid? {valid} First pass? {firstPass}");
                            }

                            NotifyObservers(new GeometryUpdate(nodeId));
                            updatedNodeBuckets[i] &= ~(1uL << m);
                        }
                    }
                }

                if (updatesMissing) {
                    return;
                }

                while (segmentReplacements.Count > 0) {
                    SegmentEndReplacement replacement = segmentReplacements.Dequeue();

                    if (logGeometry) {
                        Log._Debug(
                            "GeometryManager.SimulationStep(): Notifying observers about " +
                            $"segment end replacement {replacement}");
                    }

                    NotifyObservers(new GeometryUpdate(replacement));
                }

                stateUpdated = false;
            }
        }

        public void MarkAllAsUpdated() {
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (uint segmentId = 0; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref NetSegment netSegment = ref ((ushort)segmentId).ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                MarkAsUpdated(
                    ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                    true);
            }
        }

        public void MarkAsUpdated(ref ExtSegment seg, bool updateNodes = true) {
#if DEBUG
            if (DebugSwitch.GeometryDebug.Get()) {
                Log._Debug(
                    $"GeometryManager.MarkAsUpdated(segment {seg.segmentId}): Marking segment as updated");
            }
#endif
            lock(updateLock) {

                updatedSegmentBuckets[seg.segmentId >> 6] |= 1uL << (seg.segmentId & 63);
                stateUpdated = true;

                if (updateNodes) {
                    ref NetSegment netSegment = ref seg.segmentId.ToSegment();

                    MarkAsUpdated(netSegment.m_startNode);
                    MarkAsUpdated(netSegment.m_endNode);
                }

                if (!seg.valid) {
                    SimulationStep(true);
                }
            }
        }

        public void MarkAsUpdated(ushort nodeId, bool updateSegments = false) {
#if DEBUG
            if (DebugSwitch.GeometryDebug.Get()) {
                Log._Debug(
                    $"GeometryManager.MarkAsUpdated(node {nodeId}): Marking node as updated");
            }
#endif
            lock(updateLock) {

                if (nodeId == 0) {
                    return;
                }

                updatedNodeBuckets[nodeId >> 6] |= 1uL << (nodeId & 63);
                stateUpdated = true;

                ref NetNode netNode = ref nodeId.ToNode();

                if (updateSegments) {
                    for (int i = 0; i < 8; ++i) {
                        ushort segmentId = netNode.GetSegment(i);
                        if (segmentId != 0) {
                            MarkAsUpdated(
                                ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[segmentId],
                                false);
                        }
                    }
                }

                if (!netNode.IsValid()) {
                    SimulationStep(true);
                }
            }
        }

        public void OnSegmentEndReplacement(SegmentEndReplacement replacement) {
#if DEBUG
            if (DebugSwitch.GeometryDebug.Get()) {
                Log._Debug(
                    "GeometryManager.OnSegmentEndReplacement(): Detected segment replacement: " +
                    $"{replacement.oldSegmentEndId.SegmentId} -> {replacement.newSegmentEndId.SegmentId}");
            }
#endif
            lock(updateLock) {
                segmentReplacements.Enqueue(replacement);
                stateUpdated = true;
            }
        }

        public IDisposable Subscribe(IObserver<GeometryUpdate> observer) {
#if DEBUG
            if (DebugSwitch.GeometryDebug.Get()) {
                Log._Debug(
                    $"GeometryManager.Subscribe(): Subscribing observer {observer.GetType().Name}");
            }
#endif
            return geometryUpdateObservable.Subscribe(observer);
        }

        private void NotifyObservers(GeometryUpdate geometryUpdate) {
            geometryUpdateObservable.NotifyObservers(geometryUpdate);
        }
    }
}
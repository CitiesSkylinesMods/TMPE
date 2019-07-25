﻿namespace TrafficManager.Manager.Impl {
    using System;
    using API.Manager;
    using CSUtil.Commons;
    using State.ConfigData;
    using Traffic;
    using Traffic.Impl;

    [Obsolete("should be removed when implementing issue #240")]
    public class SegmentEndManager
        : AbstractCustomManager,
          ISegmentEndManager
    {
        public static readonly SegmentEndManager Instance = new SegmentEndManager();

        private ISegmentEnd[] SegmentEnds;

        private SegmentEndManager() {
            // Resharper notice: Either change field to solid type, or change new type to interface array?
            SegmentEnds = new SegmentEnd[2 * NetManager.MAX_SEGMENT_COUNT];
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Segment ends:");

            for (int i = 0; i < SegmentEnds.Length; ++i) {
                if (SegmentEnds[i] == null) {
                    continue;
                }

                Log._Debug($"Segment end {i}: {SegmentEnds[i]}");
            }
        }

        public ISegmentEnd GetSegmentEnd(ISegmentEndId endId) {
            return GetSegmentEnd(endId.SegmentId, endId.StartNode);
        }

        public ISegmentEnd GetSegmentEnd(ushort segmentId, bool startNode) {
            return SegmentEnds[GetIndex(segmentId, startNode)];
        }

        public ISegmentEnd GetOrAddSegmentEnd(ISegmentEndId endId) {
            return GetOrAddSegmentEnd(endId.SegmentId, endId.StartNode);
        }

        public ISegmentEnd GetOrAddSegmentEnd(ushort segmentId, bool startNode) {
            ISegmentEnd end = GetSegmentEnd(segmentId, startNode);
            if (end != null) {
                return end;
            }

            if (!Services.NetService.IsSegmentValid(segmentId)) {
                Log.Warning(
                    $"SegmentEndManager.GetOrAddSegmentEnd({segmentId}, {startNode}): Refusing to " +
                    "add segment end for invalid segment.");
                return null;
            }

            return SegmentEnds[GetIndex(segmentId, startNode)] = new SegmentEnd(segmentId, startNode);
        }

        public void RemoveSegmentEnd(ISegmentEndId endId) {
            RemoveSegmentEnd(endId.SegmentId, endId.StartNode);
        }

        public void RemoveSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif

            if (logPriority) {
                Log._Debug($"SegmentEndManager.RemoveSegmentEnd({segmentId}, {startNode}) called");
            }

            DestroySegmentEnd(GetIndex(segmentId, startNode));
        }

        public void RemoveSegmentEnds(ushort segmentId) {
            RemoveSegmentEnd(segmentId, true);
            RemoveSegmentEnd(segmentId, false);
        }

        public bool UpdateSegmentEnd(ISegmentEndId endId) {
            return UpdateSegmentEnd(endId.SegmentId, endId.StartNode);
        }

        public bool UpdateSegmentEnd(ushort segmentId, bool startNode) {
#if DEBUG
            bool logPriority = DebugSwitch.PriorityRules.Get()
                               && (DebugSettings.SegmentId <= 0
                                   || segmentId == DebugSettings.SegmentId);
#else
            const bool logPriority = false;
#endif

            if (! Services.NetService.IsSegmentValid(segmentId)) {
                if (logPriority) {
                    Log._Debug(
                        $"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): Segment " +
                        $"{segmentId} is invalid. Removing all segment ends.");
                }

                RemoveSegmentEnds(segmentId);
                return false;
            }

            if (TrafficPriorityManager.Instance.HasSegmentPrioritySign(segmentId, startNode)
                || TrafficLightSimulationManager.Instance.HasTimedSimulation(
                    Services.NetService.GetSegmentNodeId(
                        segmentId,
                        startNode))) {
                if (logPriority) {
                    Log._DebugFormat(
                        "SegmentEndManager.UpdateSegmentEnd({0}, {1}): Segment {2} @ {3} has timed " +
                        "light or priority sign. Adding segment end {4} @ {5}",
                        segmentId, startNode, segmentId, startNode, segmentId, startNode);
                }

                ISegmentEnd end = GetOrAddSegmentEnd(segmentId, startNode);
                if (end == null) {
                    Log.Warning($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): " +
                                "Failed to add segment end.");
                    return false;
                }

                if (logPriority) {
                    Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): " +
                               "Added segment end. Updating now.");
                }

                end.Update();
                if (logPriority) {
                    Log._Debug($"SegmentEndManager.UpdateSegmentEnd({segmentId}, {startNode}): " +
                               "Update of segment end finished.");
                }

                return true;
            } else {
                if (logPriority) {
                    Log._DebugFormat(
                        "SegmentEndManager.UpdateSegmentEnd({0}, {1}): Segment {2} @ {3} neither has " +
                        "timed light nor priority sign. Removing segment end {4} @ {5}",
                        segmentId, startNode, segmentId, startNode, segmentId, startNode);
                }

                RemoveSegmentEnd(segmentId, startNode);
                return false;
            }
        }

        private int GetIndex(ushort segmentId, bool startNode) {
            return segmentId + (startNode ? 0 : NetManager.MAX_SEGMENT_COUNT);
        }

        // protected override void HandleInvalidSegment(SegmentGeometry geometry) {
        //    RemoveSegmentEnds(geometry.SegmentId);
        // }
        //
        // protected override void HandleValidSegment(SegmentGeometry geometry) {
        //    RemoveSegmentEnds(geometry.SegmentId);
        // }

        protected void DestroySegmentEnd(int index) {
            // Log._Debug($"SegmentEndManager.DestroySegmentEnd({index}) called");
            SegmentEnds[index] = null;
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < SegmentEnds.Length; ++i) {
                DestroySegmentEnd(i);
            }
        }
    }
}
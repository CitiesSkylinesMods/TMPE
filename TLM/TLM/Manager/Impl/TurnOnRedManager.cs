namespace TrafficManager.Manager.Impl {
    using CSUtil.Commons;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    public class TurnOnRedManager
        : AbstractGeometryObservingManager,
          ITurnOnRedManager
    {
        private TurnOnRedManager() {
            TurnOnRedSegments = new TurnOnRedSegments[2 * NetManager.MAX_SEGMENT_COUNT];
        }

        public static TurnOnRedManager Instance { get; } = new TurnOnRedManager();

        public TurnOnRedSegments[] TurnOnRedSegments { get; }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            // JunctionRestrictionsManager requires our data during loading of custom data
            for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                if (!netSegment.IsValid()) {
                    continue;
                }

                HandleValidSegment(ref Constants.ManagerFactory.ExtSegmentManager.ExtSegments[i]);
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();

            for (int i = 0; i < TurnOnRedSegments.Length; ++i) {
                TurnOnRedSegments[i].Reset();
            }
        }

        public int GetIndex(ushort segmentId, bool startNode) {
            return segmentId + (startNode ? 0 : NetManager.MAX_SEGMENT_COUNT);
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Turn-on-red segments:");

            for (int i = 0; i < TurnOnRedSegments.Length; ++i) {
                Log._Debug($"Segment end {i}: {TurnOnRedSegments[i]}");
            }
        }

        protected override void HandleValidSegment(ref ExtSegment seg) {
            UpdateSegment(ref seg);
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            ResetSegment(seg.segmentId);
        }

        protected void UpdateSegment(ref ExtSegment seg) {
#if DEBUG
            if (DebugSwitch.TurnOnRed.Get()) {
                Log._Debug($"TurnOnRedManager.UpdateSegment({seg.segmentId}) called.");
            }
#endif
            ref NetSegment netSegment = ref seg.segmentId.ToSegment();

            ResetSegment(seg.segmentId);

            IExtSegmentEndManager extSegmentEndManager =
                Constants.ManagerFactory.ExtSegmentEndManager;

            if (netSegment.m_startNode != 0) {
                int index0 = extSegmentEndManager.GetIndex(seg.segmentId, true);
                UpdateSegmentEnd(
                    ref seg,
                    ref extSegmentEndManager.ExtSegmentEnds[index0]);
            }

            if (netSegment.m_endNode != 0) {
                int index1 = extSegmentEndManager.GetIndex(seg.segmentId, false);
                UpdateSegmentEnd(
                    ref seg,
                    ref extSegmentEndManager.ExtSegmentEnds[index1]);
            }
        }

        protected void UpdateSegmentEnd(ref ExtSegment seg, ref ExtSegmentEnd end) {
#if DEBUG
            bool logTurnOnRed = DebugSwitch.TurnOnRed.Get();
#else
            const bool logTurnOnRed = false;
#endif
            if (logTurnOnRed) {
                Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}) called.");
            }

            IExtSegmentManager segmentManager = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segmentEndManager = Constants.ManagerFactory.ExtSegmentEndManager;

            ushort segmentId = seg.segmentId;
            ushort nodeId = end.nodeId;
            bool hasOutgoingSegment = false;

            ref NetNode endNode = ref end.nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort otherSegmentId = endNode.GetSegment(i);
                if (otherSegmentId != 0) {
                    int index0 = segmentEndManager.GetIndex(otherSegmentId, otherSegmentId.ToSegment().m_startNode == nodeId);

                    if (otherSegmentId != segmentId
                        && segmentEndManager.ExtSegmentEnds[index0].outgoing) {
                        hasOutgoingSegment = true;
                        break;
                    }
                }
            }

            // check if traffic can flow to the node and that there is at least one left segment
            if (!end.incoming || !hasOutgoingSegment) {
                if (logTurnOnRed) {
                    Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                               "outgoing one-way or insufficient number of outgoing segments.");
                }

                return;
            }

            // check node
            // note that we must not check for the `TrafficLights` flag here because the flag might not be loaded yet
            ref NetNode node = ref nodeId.ToNode();
            bool nodeValid =
                (node.m_flags & NetNode.Flags.LevelCrossing) ==
                NetNode.Flags.None &&
                node.Info?.m_class?.m_service != ItemClass.Service.Beautification;

            if (!nodeValid) {
                if (logTurnOnRed) {
                    Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): node invalid");
                }

                return;
            }

            // get left/right segments
            end.segmentId
                .ToSegment()
                .GetLeftAndRightSegments(nodeId, out ushort leftSegmentId, out ushort rightSegmentId);

            if (logTurnOnRed) {
                Log._Debug(
                    $"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                    $"got left/right segments: {leftSegmentId}/{rightSegmentId}");
            }

            // validate left/right segments according to geometric properties
            if (leftSegmentId != 0
                && segmentEndManager.GetDirection(ref end, leftSegmentId) != ArrowDirection.Left)
            {
                if (logTurnOnRed) {
                    Log._Debug(
                        $"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                        "left segment is not geometrically left");
                }

                leftSegmentId = 0;
            }

            if (rightSegmentId != 0
                && segmentEndManager.GetDirection(ref end, rightSegmentId) != ArrowDirection.Right)
            {
                if (logTurnOnRed) {
                    Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                               "right segment is not geometrically right");
                }

                rightSegmentId = 0;
            }

            // check for incoming one-ways
            if (leftSegmentId != 0
                && !segmentEndManager.ExtSegmentEnds[segmentEndManager.GetIndex(leftSegmentId, nodeId)].outgoing)
            {
                if (logTurnOnRed) {
                    Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                               "left segment is incoming one-way");
                }

                leftSegmentId = 0;
            }

            if (rightSegmentId != 0
                && !segmentEndManager.ExtSegmentEnds[segmentEndManager.GetIndex(rightSegmentId, nodeId)].outgoing)
            {
                if (logTurnOnRed) {
                    Log._Debug($"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                               "right segment is incoming one-way");
                }

                rightSegmentId = 0;
            }

            bool lht = Shortcuts.LHT;
            if (seg.oneWay) {
                if ((lht && rightSegmentId != 0) || (!lht && leftSegmentId != 0)) {
                    // special case: one-way to one-way in non-preferred direction
                    if (logTurnOnRed) {
                        Log._Debug(
                            $"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                            "source is incoming one-way. checking for one-way in non-preferred direction");
                    }

                    ushort targetSegmentId = lht ? rightSegmentId : leftSegmentId;

                    if (!segmentManager.ExtSegments[targetSegmentId].oneWay) {
                        // disallow turn in non-preferred direction
                        if (logTurnOnRed) {
                            Log._Debug(
                                $"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                                $"turn in non-preferred direction {(lht ? "right" : "left")} disallowed");
                        }

                        if (lht) {
                            rightSegmentId = 0;
                        } else {
                            leftSegmentId = 0;
                        }
                    }
                }
            } else if (lht) {
                // default case (LHD): turn in preferred direction
                rightSegmentId = 0;
            } else {
                // default case (RHD): turn in preferred direction
                leftSegmentId = 0;
            }

            int index = GetIndex(end.segmentId, end.startNode);
            TurnOnRedSegments[index].leftSegmentId = leftSegmentId;
            TurnOnRedSegments[index].rightSegmentId = rightSegmentId;

            if (logTurnOnRed) {
                Log._Debug(
                    $"TurnOnRedManager.UpdateSegmentEnd({end.segmentId}, {end.startNode}): " +
                    $"Finished calculation. leftSegmentId={leftSegmentId}, rightSegmentId={rightSegmentId}");
            }
        }

        protected void ResetSegment(ushort segmentId) {
            TurnOnRedSegments[GetIndex(segmentId, true)].Reset();
            TurnOnRedSegments[GetIndex(segmentId, false)].Reset();
        }
    }
}
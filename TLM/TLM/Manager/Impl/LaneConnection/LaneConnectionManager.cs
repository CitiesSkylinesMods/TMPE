namespace TrafficManager.Manager.Impl.LaneConnection {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
#if DEBUG
#endif

    public class LaneConnectionManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneConnection>>,
          ILaneConnectionManager {
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car
                                                             | VehicleInfo.VehicleType.Train
                                                             | VehicleInfo.VehicleType.Tram
                                                             | VehicleInfo.VehicleType.Metro
                                                             | VehicleInfo.VehicleType.Monorail
                                                             | VehicleInfo.VehicleType.Trolleybus;

        public LaneConnectionSubManager Sub = // TODO #354 divide into Road/Track
            new LaneConnectionSubManager(LaneEndTransitionGroup.All);

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        static LaneConnectionManager() {
            Instance = new LaneConnectionManager();
        }

        public static LaneConnectionManager Instance { get; }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            Sub.OnBeforeLoadData();
        }
        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            Sub.OnLevelUnloading();
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Sub.PrintDebugInfo();
        }

        /// <summary>
        /// Checks if traffic may flow from source lane to target lane according to setup lane connections
        /// </summary>
        /// <param name="sourceStartNode">check at start node of source lane?</param>
        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            return Sub.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode);
        }

        /// <summary>
        /// Determines if the given lane has incoming/outgoing connections
        /// Performance note: This act as HasOutgoingConnections for uni-directional lanes but faster
        /// </summary>
        public bool HasConnections(uint laneId, bool startNode) =>
            Sub.HasConnections(laneId, startNode);

        /// <summary>
        /// Determines if there exist custom lane connections at the specified node
        /// </summary>
        public bool HasNodeConnections(ushort nodeId) => Sub.HasNodeConnections(nodeId);

        // Note: Not performance critical
        public bool HasUturnConnections(ushort segmentId, bool startNode) =>
            Sub.HasUturnConnections(segmentId, startNode);

        /// <summary>
        /// Removes all lane connections at the specified node
        /// </summary>
        /// <param name="nodeId">Affected node</param>
        internal void RemoveLaneConnectionsFromNode(ushort nodeId) {
            Sub.RemoveLaneConnectionsFromNode(nodeId);
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            Sub.HandleInvalidSegmentImpl(seg.segmentId);
        }

        internal bool GetLaneEndPoint(ushort segmentId,
                                      bool startNode,
                                      byte laneIndex,
                                      uint? laneId,
                                      NetInfo.Lane laneInfo,
                                      out bool outgoing,
                                      out bool incoming,
                                      out Vector3? pos) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            pos = null;
            outgoing = false;
            incoming = false;

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return false;
            }

            if (laneId == null) {
                laneId = FindLaneId(segmentId, laneIndex);
                if (laneId == null) {
                    return false;
                }
            }

            ref NetLane netLane = ref ((uint)laneId).ToLane();

            if ((netLane.m_flags &
                 ((ushort)NetLane.Flags.Created | (ushort)NetLane.Flags.Deleted)) !=
                (ushort)NetLane.Flags.Created) {
                return false;
            }

            if (laneInfo == null) {
                if (laneIndex < netSegment.Info.m_lanes.Length) {
                    laneInfo = netSegment.Info.m_lanes[laneIndex];
                } else {
                    return false;
                }
            }

            NetInfo.Direction laneDir = ((netSegment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None)
                    ? laneInfo.m_finalDirection
                    : NetInfo.InvertDirection(laneInfo.m_finalDirection);

            if (startNode) {
                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = netLane.m_bezier.a;
            } else {
                if ((laneDir & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
                    outgoing = true;
                }

                if ((laneDir & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
                    incoming = true;
                }

                pos = netLane.m_bezier.d;
            }

            return true;
        }

        private uint? FindLaneId(ushort segmentId, byte laneIndex) {
            ref NetSegment netSegment = ref segmentId.ToSegment();

            NetInfo.Lane[] lanes = netSegment.Info.m_lanes;
            uint laneId = netSegment.m_lanes;

            for (byte i = 0; i < lanes.Length && laneId != 0; i++) {
                if (i == laneIndex) {
                    return laneId;
                }

                laneId = laneId.ToLane().m_nextLane;
            }

            return null;
        }

        public bool LoadData(List<Configuration.LaneConnection> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} lane connections");
            success = Sub.LoadData(data);
            return success;
        }

        public List<Configuration.LaneConnection> SaveData(ref bool success) {
            return Sub.SaveData(ref success);
        }
    }
}
namespace TrafficManager.Manager.Impl.LaneConnection {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;
#if DEBUG
#endif

    public class LaneConnectionManager
        : AbstractCustomManager,
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

        public LaneConnectionSubManager Road = new LaneConnectionSubManager(LaneEndTransitionGroup.Road);
        public LaneConnectionSubManager Track = new LaneConnectionSubManager(LaneEndTransitionGroup.Track);

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        static LaneConnectionManager() {
            Instance = new LaneConnectionManager();
        }

        public static LaneConnectionManager Instance { get; }

        public LaneConnectionSubManager SubManager(bool track) => track ? Track : Road;

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            Road.OnBeforeLoadData();
            Track.OnBeforeLoadData();
        }
        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            Road.OnLevelUnloading();
            Track.OnLevelUnloading();
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Road.PrintDebugInfo();
            Track.PrintDebugInfo();
        }

        /// <summary>
        /// Checks if traffic may flow from source lane to target lane according to setup lane connections
        /// </summary>
        /// <param name="sourceStartNode">check at start node of source lane?</param>
        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
            return Road.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode) ||
                  Track.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode);
        }

        public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode, LaneEndTransitionGroup group) {
            bool ret = Road.Supports(group) &&
                Road.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode);
            if (!ret) {
                ret = Track.Supports(group) &&
                    Track.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode);
            }
            return ret;
        }


        /// <summary>
        /// Adds a lane connection between two lanes.
        /// pass in <c>LaneEndTransitionGroup.All</c> to add lane connections in every sub manager that supports both lanes.
        /// </summary>
        /// <param name="sourceLaneId">From lane id</param>
        /// <param name="targetLaneId">To lane id</param>
        /// <param name="sourceStartNode">The affected node</param>
        /// <param name="group">lane or track</param>
        /// <returns><c>true</c> if any connection was added, <c>falsse</c> otherwise</returns>
        public bool AddLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode, LaneEndTransitionGroup group) {
            bool success = true;
            if (Road.Supports(group)) {
                success = Road.AddLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            }
            if (Track.Supports(group)) {
                success &= Track.AddLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            }
            return success;
        }

        public bool RemoveLaneConnection(uint sourceLaneId, uint targetLaneId, bool sourceStartNode, LaneEndTransitionGroup group) {
            bool success = true;
            var sourceLaneInfo = ExtLaneManager.Instance.GetLaneInfo(sourceLaneId);
            var targetLaneInfo = ExtLaneManager.Instance.GetLaneInfo(targetLaneId);
            if (Road.Supports(group)) {
                success = Road.RemoveLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            } else {
                bool canConnect = Road.Supports(sourceLaneInfo) && Road.Supports(targetLaneInfo);
                if (!canConnect)
                    Road.RemoveLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            }
            if (Track.Supports(group)) {
                success |= Track.RemoveLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            } else {
                bool canConnect = Track.Supports(sourceLaneInfo) && Track.Supports(targetLaneInfo);
                if (!canConnect)
                    Track.RemoveLaneConnection(sourceLaneId, targetLaneId, sourceStartNode);
            }
            return success;
        }

        /// <summary>
        /// Determines if the given lane has incoming/outgoing connections
        /// Performance note: This act as HasOutgoingConnections for uni-directional lanes but faster
        /// </summary>
        public bool HasConnections(uint laneId, bool startNode) =>
            Road.HasConnections(laneId, startNode) || Track.HasConnections(laneId, startNode);

        /// <summary>
        /// Determines if there exist custom lane connections at the specified node
        /// </summary>
        public bool HasNodeConnections(ushort nodeId) =>
            Road.HasNodeConnections(nodeId) || Track.HasNodeConnections(nodeId);

        // Note: Not performance critical
        public bool HasUturnConnections(ushort segmentId, bool startNode) =>
            Road.HasUturnConnections(segmentId, startNode);

        /// <summary>
        /// Removes all lane connections at the specified node
        /// </summary>
        /// <param name="nodeId">Affected node</param>
        internal void RemoveLaneConnectionsFromNode(ushort nodeId) {
            Road.RemoveLaneConnectionsFromNode(nodeId);
            Track.RemoveLaneConnectionsFromNode(nodeId);
        }


        /// <summary>
        /// Checks if the turning angle between two segments at the given node is within bounds.
        /// </summary>
        public static bool CheckSegmentsTurningAngle(ushort sourceSegmentId,
                                                      bool sourceStartNode,
                                                      ushort targetSegmentId,
                                                      bool targetStartNode) {
            if (sourceSegmentId == targetSegmentId) {
                return false;
            }

            ref NetSegment sourceSegment = ref sourceSegmentId.ToSegment();
            ref NetSegment targetSegment = ref targetSegmentId.ToSegment();
            float turningAngle = 0.01f - Mathf.Min(
                sourceSegment.Info.m_maxTurnAngleCos,
                targetSegment.Info.m_maxTurnAngleCos);

            if (turningAngle < 1f) {
                Vector3 sourceDirection = sourceStartNode
                                              ? sourceSegment.m_startDirection
                                              : sourceSegment.m_endDirection;

                Vector3 targetDirection = targetStartNode
                                              ? targetSegment.m_startDirection
                                              : targetSegment.m_endDirection;

                float dirDotProd = (sourceDirection.x * targetDirection.x) +
                                   (sourceDirection.z * targetDirection.z);
                return dirDotProd < turningAngle;
            }

            return true;
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

        internal void RemoveAllLaneConnections() {
            Log.Info("LaneConnectionManager(): Removing all lane connections...");
            Road.ResetLaneConnections();
            Track.ResetLaneConnections();
            OptionsManager.UpdateRoutingManager();
            Log.Info("LaneConnectionManager(): All lane connections have been removed!");
        }

        public bool LoadData(List<Configuration.LaneConnection> data) {
            bool success;
            Log.Info($"Loading {data.Count} lane connections");
            success = Road.LoadData(data);
            success &= Track.LoadData(data);
            return success;
        }

        public List<Configuration.LaneConnection> SaveData(ref bool success) {
            var ret = Road.SaveData(ref success);
            ret.AddRange(Track.SaveData(ref success));
            return ret;
        }
    }
}
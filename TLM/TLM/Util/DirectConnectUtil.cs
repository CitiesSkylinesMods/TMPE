namespace TrafficManager.Util {
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using static TrafficManager.Util.Shortcuts;

    public static class DirectConnectUtil {
        static LaneConnectionManager LCMan => LaneConnectionManager.Instance;
        static bool verbose_ => DebugSwitch.LaneConnections.Get();

        internal static VehicleInfo.VehicleType GetVehicleType(NetInfo.Node nodeInfo, NetInfo.ConnectGroup flags) {
            VehicleInfo.VehicleType ret = 0;
            const NetInfo.ConnectGroup TRAM =
                NetInfo.ConnectGroup.CenterTram |
                NetInfo.ConnectGroup.NarrowTram |
                NetInfo.ConnectGroup.SingleTram |
                NetInfo.ConnectGroup.WideTram;
            const NetInfo.ConnectGroup TRAIN =
                NetInfo.ConnectGroup.DoubleTrain |
                NetInfo.ConnectGroup.SingleTrain |
                NetInfo.ConnectGroup.TrainStation;
            const NetInfo.ConnectGroup MONO_RAIL =
                NetInfo.ConnectGroup.DoubleMonorail |
                NetInfo.ConnectGroup.SingleMonorail |
                NetInfo.ConnectGroup.MonorailStation;

            if ((flags & TRAM) != 0) {
                ret |= VehicleInfo.VehicleType.Tram;
            }
            if ((flags & TRAIN) != 0) {
                ret |= VehicleInfo.VehicleType.Train;
            }
            if ((flags & MONO_RAIL) != 0) {
                ret |= VehicleInfo.VehicleType.Monorail;
            }

            return ret;
        }

        /// <summary>
        /// Checks if any lanes from source segment can go to target segment.
        /// Precondition: assuming that the segments can have connected lanes.
        /// </summary>
        /// <param name="sourceSegmentId"></param>
        /// <param name="targetSegmentId"></param>
        /// <param name="nodeId"></param>
        /// <param name="laneType"></param>
        /// <param name="vehicleType"></param>
        /// <returns></returns>
        internal static bool HasDirectConnect(
            ushort segmentId1,
            ushort segmentId2,
            ushort nodeId,
            int nodeInfoIDX) {
            NetInfo.Node nodeInfo = segmentId1.ToSegment().Info.m_nodes[nodeInfoIDX];
            VehicleInfo.VehicleType vehicleType = GetVehicleType(nodeInfo, nodeInfo.m_connectGroup);
            if (vehicleType == 0)
                return true;
            return HasDirectConnect(
                segmentId1,
                segmentId2,
                nodeId,
                NetInfo.LaneType.All,
                vehicleType);
        }

        /// <summary>
        /// Checks if any lanes from source segment can go to target segment.
        /// Precondition: assuming that the segments can have connected lanes.
        /// </summary>
        /// <param name="sourceSegmentId"></param>
        /// <param name="targetSegmentId"></param>
        /// <param name="nodeId"></param>
        /// <param name="laneType"></param>
        /// <param name="vehicleType"></param>
        /// <returns></returns>
        internal static bool HasDirectConnect(
            ushort sourceSegmentId,
            ushort targetSegmentId,
            ushort nodeId,
            NetInfo.LaneType laneType,
            VehicleInfo.VehicleType vehicleType) {
            bool sourceStartNode = (bool)netService.IsStartNode(sourceSegmentId, nodeId);
            var sourceLaneInfos = sourceSegmentId.ToSegment().Info.m_lanes;
            int nSource = sourceLaneInfos.Length;

            var targetLaneInfos = targetSegmentId.ToSegment().Info.m_lanes;
            int nTarget = targetLaneInfos.Length;

            uint sourceLaneId, targetLaneId;
            int sourceLaneIndex, targetLaneIndex;
            for (sourceLaneIndex = 0, sourceLaneId = sourceSegmentId.ToSegment().m_lanes;
                sourceLaneIndex < nSource;
                ++sourceLaneIndex, sourceLaneId = laneBuffer[sourceLaneId].m_nextLane) {
                //Extensions.Log($"sourceLaneId={sourceLaneId} {sourceLaneInfos[sourceLaneIndex].m_laneType} & {laneType} = {sourceLaneInfos[sourceLaneIndex].m_laneType & laneType}\n" +
                //    $"{sourceLaneInfos[sourceLaneIndex].m_vehicleType} & {vehicleType} = {sourceLaneInfos[sourceLaneIndex].m_vehicleType & vehicleType}");

                if ((sourceLaneInfos[sourceLaneIndex].m_laneType & laneType) == 0 ||
                    (sourceLaneInfos[sourceLaneIndex].m_vehicleType & vehicleType) == 0) {
                    continue;
                }
                //Extensions.Log($"POINT A> ");
                for (targetLaneIndex = 0, targetLaneId = targetSegmentId.ToSegment().m_lanes;
                    targetLaneIndex < nTarget;
                    ++targetLaneIndex, targetLaneId = laneBuffer[targetLaneId].m_nextLane) {
                    //Extensions.Log($"targetLaneId={targetLaneId} {targetLaneInfos[targetLaneIndex].m_laneType} & {laneType} = {targetLaneInfos[targetLaneIndex].m_laneType & laneType}\n" +
                    //    $"{targetLaneInfos[targetLaneIndex].m_vehicleType} & {vehicleType} = {targetLaneInfos[targetLaneIndex].m_vehicleType & vehicleType}");
                    if ((targetLaneInfos[targetLaneIndex].m_laneType & laneType) == 0 ||
                        (targetLaneInfos[targetLaneIndex].m_vehicleType & vehicleType) == 0) {
                        continue;
                    }

                    bool connected = AreLanesConnected(
                        sourceSegmentId, sourceLaneId, (byte)sourceLaneIndex,
                        targetSegmentId, targetLaneId, (byte)targetLaneIndex,
                        nodeId);

                    if(verbose_)
                        Log._Debug($"sourceLaneId={sourceLaneId} targetLaneId={targetLaneId} sourceStartNode={sourceStartNode} connected={connected}");
                    if (connected) {
                        return true;
                    }

                }
            }
            return false;
        }

        static bool AreLanesConnected(
            ushort segmentId1, uint laneId1, byte laneIndex1,
            ushort segmentId2, uint laneId2, byte laneIndex2,
            ushort nodeId) {
            bool startNode1 = (bool)netService.IsStartNode(segmentId1, nodeId);
            bool startNode2 = (bool)netService.IsStartNode(segmentId2, nodeId);
            LCMan.GetLaneEndPoint(
                segmentId1,
                startNode1,
                laneIndex1,
                laneId1,
                segmentId1.ToSegment().Info.m_lanes[laneIndex1],
                out bool isSource1,
                out bool isTarget1,
                out _);
            LCMan.GetLaneEndPoint(
                segmentId2,
                startNode2,
                laneIndex2,
                laneId2,
                segmentId2.ToSegment().Info.m_lanes[laneIndex2],
                out bool isSource2,
                out bool isTarget2,
                out _);

            if ((isSource1 && isTarget2)) {

                bool b1 = LCMan.HasConnections(laneId1, startNode1);
                bool b2 = LCMan.AreLanesConnected(laneId1, laneId2, startNode1);
                return !b1 || b2;
            } else if (isTarget1 && isSource2) {
                bool b1 = LCMan.HasConnections(laneId2, startNode2);
                bool b2 = LCMan.AreLanesConnected(laneId2, laneId1, startNode2);
                return !b1 || b2;
            } else {
                return false;
            }
        }
    }
}

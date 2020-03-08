namespace TrafficManager.Util {
    using CSUtil.Commons;
    using TrafficManager.Manager.Impl;
    using static TrafficManager.Util.Shortcuts;

    public static class DirectConnectUtil {
        static LaneConnectionManager LCMan => LaneConnectionManager.Instance;

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
            VehicleInfo.VehicleType vehicleType = DirectConnectUtil.GetVehicleType(nodeInfo, nodeInfo.m_connectGroup);
            if (vehicleType == 0)
                return true;
            return DirectConnectUtil.HasDirectConnect(
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
                    bool b1 = LCMan.HasConnections(sourceLaneId, sourceStartNode);
                    bool b2 = LCMan.AreLanesConnected(sourceLaneId, targetLaneId, sourceStartNode);
                    Log._Debug($"sourceLaneId={sourceLaneId} targetLaneId={targetLaneId} sourceStartNode={sourceStartNode} b1={b1} b2={b2}");

                    if (!b1 || b2) {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}

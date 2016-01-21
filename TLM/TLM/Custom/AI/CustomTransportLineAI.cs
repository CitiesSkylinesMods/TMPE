using ColossalFramework;
using TrafficManager.Custom.Manager;

namespace TrafficManager.Custom.AI
{
    class CustomTransportLineAI
    {
        /*protected static bool StartPathFind(ushort segmentId, ref NetSegment data, ItemClass.Service netService, VehicleInfo.VehicleType vehicleType, bool skipQueue)
        {
            if (data.m_path != 0u)
            {
                Singleton<PathManager>.instance.ReleasePath(data.m_path);
                data.m_path = 0u;
            }
            NetManager instance = Singleton<NetManager>.instance;
            if ((instance.m_nodes.m_buffer[data.m_startNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None)
            {
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = instance.m_nodes.m_buffer[data.m_startNode].GetSegment(i);
                    if (segment != 0 && segment != segmentId && instance.m_segments.m_buffer[segment].m_path != 0u)
                    {
                        return true;
                    }
                }
            }
            if ((instance.m_nodes.m_buffer[data.m_endNode].m_flags & NetNode.Flags.Ambiguous) != NetNode.Flags.None)
            {
                for (int j = 0; j < 8; j++)
                {
                    ushort segment2 = instance.m_nodes.m_buffer[data.m_endNode].GetSegment(j);
                    if (segment2 != 0 && segment2 != segmentId && instance.m_segments.m_buffer[segment2].m_path != 0u)
                    {
                        return true;
                    }
                }
            }
            var position = instance.m_nodes.m_buffer[data.m_startNode].m_position;
            var position2 = instance.m_nodes.m_buffer[data.m_endNode].m_position;
            PathUnit.Position startPosA;
            PathUnit.Position startPosB;
            float num;
            float num2;
            if (!PathManager.FindPathPosition(position, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, true, true, 32f, out startPosA, out startPosB, out num, out num2))
            {
                return true;
            }
            PathUnit.Position endPosA;
            PathUnit.Position endPosB;
            float num3;
            float num4;
            if (!PathManager.FindPathPosition(position2, netService, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, true, true, 32f, out endPosA, out endPosB, out num3, out num4))
            {
                return true;
            }
            if ((instance.m_nodes.m_buffer[data.m_startNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None)
            {
                startPosB = default(PathUnit.Position);
            }
            if ((instance.m_nodes.m_buffer[data.m_endNode].m_flags & NetNode.Flags.Fixed) != NetNode.Flags.None)
            {
                endPosB = default(PathUnit.Position);
            }
            startPosA.m_offset = 128;
            startPosB.m_offset = 128;
            endPosA.m_offset = 128;
            endPosB.m_offset = 128;
            bool stopLane = GetStopLane(ref startPosA, vehicleType);
            bool stopLane2 = GetStopLane(ref startPosB, vehicleType);
            bool stopLane3 = GetStopLane(ref endPosA, vehicleType);
            bool stopLane4 = GetStopLane(ref endPosB, vehicleType);
            if ((!stopLane && !stopLane2) || (!stopLane3 && !stopLane4))
            {
                return true;
            }
            uint path;
            if (Singleton<CustomPathManager>.instance.CreatePath(out path, ref Singleton<SimulationManager>.instance.m_randomizer, Singleton<SimulationManager>.instance.m_currentBuildIndex, startPosA, startPosB, endPosA, endPosB, NetInfo.LaneType.Vehicle, vehicleType, 20000f, false, true, true, skipQueue, ItemClass.Service.PublicTransport))
            {
                if (startPosA.m_segment != 0 && startPosB.m_segment != 0)
                {
                    var expr_2D9Cp0 = instance.m_nodes.m_buffer;
                    var expr_2D9Cp1 = data.m_startNode;
                    expr_2D9Cp0[expr_2D9Cp1].m_flags = (expr_2D9Cp0[expr_2D9Cp1].m_flags | NetNode.Flags.Ambiguous);
                }
                else
                {
                    var expr305Cp0 = instance.m_nodes.m_buffer;
                    var expr305Cp1 = data.m_startNode;
                    expr305Cp0[expr305Cp1].m_flags = (expr305Cp0[expr305Cp1].m_flags & ~NetNode.Flags.Ambiguous);
                }
                if (endPosA.m_segment != 0 && endPosB.m_segment != 0)
                {
                    var expr344Cp0 = instance.m_nodes.m_buffer;
                    var expr344Cp1 = data.m_endNode;
                    expr344Cp0[expr344Cp1].m_flags = (expr344Cp0[expr344Cp1].m_flags | NetNode.Flags.Ambiguous);
                }
                else
                {
                    var expr370Cp0 = instance.m_nodes.m_buffer;
                    var expr370Cp1 = data.m_endNode;
                    expr370Cp0[expr370Cp1].m_flags = (expr370Cp0[expr370Cp1].m_flags & ~NetNode.Flags.Ambiguous);
                }
                data.m_path = path;
                data.m_flags |= NetSegment.Flags.WaitingPath;
                return false;
            }
            return true;
        }

        private static bool GetStopLane(ref PathUnit.Position pos, VehicleInfo.VehicleType vehicleType)
        {
            if (pos.m_segment != 0)
            {
                var instance = Singleton<NetManager>.instance;
                int num;
                uint num2;
                if (instance.m_segments.m_buffer[pos.m_segment].GetClosestLane(pos.m_lane, NetInfo.LaneType.Vehicle, vehicleType, out num, out num2))
                {
                    pos.m_lane = (byte)num;
                    return true;
                }
            }
            pos = default(PathUnit.Position);
            return false;
        }*/
    }
}

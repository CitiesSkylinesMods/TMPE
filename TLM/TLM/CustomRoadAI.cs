using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    class CustomRoadAI : RoadBaseAI
    {
        public static Dictionary<ushort, TrafficLightSimulation> nodeDictionary = new Dictionary<ushort, TrafficLightSimulation>();

        private uint lastFrame = 0;

        public void Awake()
        {

        }

        public void Update()
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;

            if (lastFrame < currentFrameIndex)
            {
                lastFrame = currentFrameIndex;

                foreach (var nodeID in nodeDictionary.Keys)
                {
                    var node = GetNodeSimulation(nodeID);

                    if (node.FlagManualTrafficLights || (node.FlagTimedTrafficLights && node.TimedTrafficLightsActive))
                    {
                        var data = TrafficLightTool.GetNetNode(nodeID);
                        node.SimulationStep(ref data);
                        TrafficLightTool.SetNetNode(nodeID, data);
                    }
                }
            }
        }

        private void SimulationStep(ushort nodeID, ref NetNode data)
        {
            var node = GetNodeSimulation(nodeID);

            if (node == null || (node.FlagTimedTrafficLights && !node.TimedTrafficLightsActive))
            {
                OriginalSimulationStep(nodeID, ref data);
            }
        }

        public void OriginalSimulationStep(ushort nodeID, ref NetNode data)
        {
            NetManager instance = Singleton<NetManager>.instance;
            if ((data.m_flags & NetNode.Flags.TrafficLights) != NetNode.Flags.None)
            {
                uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
                int num = (int)(data.m_maxWaitTime & 3);
                int num2 = data.m_maxWaitTime >> 2 & 7;
                int num3 = data.m_maxWaitTime >> 5;
                int num4 = -1;
                int num5 = -1;
                int num6 = -1;
                int num7 = -1;
                int num8 = -1;
                int num9 = -1;
                int num10 = 0;
                int num11 = 0;
                int num12 = 0;
                int num13 = 0;
                int num14 = 0;
                for (int i = 0; i < 8; i++)
                {
                    ushort segment = data.GetSegment(i);
                    if (segment != 0)
                    {
                        int num15 = 0;
                        int num16 = 0;
                        instance.m_segments.m_buffer[(int)segment].CountLanes(segment, NetInfo.LaneType.Vehicle, VehicleInfo.VehicleType.Car, ref num15, ref num16);
                        bool flag = instance.m_segments.m_buffer[(int)segment].m_startNode == nodeID;
                        bool flag2 = (!flag) ? (num15 != 0) : (num16 != 0);
                        bool flag3 = (!flag) ? (num16 != 0) : (num15 != 0);
                        if (flag2)
                        {
                            num10 |= 1 << i;
                        }
                        if (flag3)
                        {
                            num11 |= 1 << i;
                            num13++;
                        }
                        RoadBaseAI.TrafficLightState trafficLightState;
                        RoadBaseAI.TrafficLightState trafficLightState2;
                        bool flag4;
                        bool flag5;
                        RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment], currentFrameIndex - 256u, out trafficLightState, out trafficLightState2, out flag4, out flag5);
                        if ((trafficLightState2 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green && flag5)
                        {
                            if (num7 == -1)
                            {
                                num7 = i;
                            }
                            if (num9 == -1 && num14 >= num3)
                            {
                                num9 = i;
                            }
                        }
                        num14++;
                        if (flag2 || flag4)
                        {
                            if ((trafficLightState & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
                            {
                                num5 = i;
                                if (flag4)
                                {
                                    num4 = i;
                                }
                            }
                            else if (flag4)
                            {
                                if (num6 == -1)
                                {
                                    num6 = i;
                                }
                                if (num8 == -1 && num12 >= num2)
                                {
                                    num8 = i;
                                }
                            }
                            num12++;
                        }
                    }
                }
                if (num8 == -1)
                {
                    num8 = num6;
                }
                if (num9 == -1)
                {
                    num9 = num7;
                }
                if (num5 != -1 && num4 != -1 && num <= 1)
                {
                    num8 = -1;
                    num9 = -1;
                    num++;
                }
                if (num9 != -1 && num8 != -1 && Singleton<SimulationManager>.instance.m_randomizer.Int32(3u) != 0)
                {
                    num9 = -1;
                }
                if (num8 != -1)
                {
                    num5 = num8;
                }
                if (num9 == num5)
                {
                    num5 = -1;
                }
                Vector3 vector = Vector3.zero;
                if (num9 != -1)
                {
                    ushort segment2 = data.GetSegment(num9);
                    vector = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
                    if (num5 != -1)
                    {
                        segment2 = data.GetSegment(num5);
                        Vector3 direction = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
                        if (direction.x * vector.x + direction.z * vector.z < -0.5f)
                        {
                            num5 = -1;
                        }
                    }
                    if (num5 == -1)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            if (j != num9 && (num10 & 1 << j) != 0)
                            {
                                segment2 = data.GetSegment(j);
                                if (segment2 != 0)
                                {
                                    Vector3 direction2 = instance.m_segments.m_buffer[(int)segment2].GetDirection(nodeID);
                                    if (direction2.x * vector.x + direction2.z * vector.z >= -0.5f)
                                    {
                                        num5 = j;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                int num17 = -1;
                Vector3 vector2 = Vector3.zero;
                Vector3 vector3 = Vector3.zero;
                if (num5 != -1)
                {
                    ushort segment3 = data.GetSegment(num5);
                    vector2 = instance.m_segments.m_buffer[(int)segment3].GetDirection(nodeID);
                    if ((num10 & num11 & 1 << num5) != 0)
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            if (k != num5 && k != num9 && (num10 & num11 & 1 << k) != 0)
                            {
                                segment3 = data.GetSegment(k);
                                if (segment3 != 0)
                                {
                                    vector3 = instance.m_segments.m_buffer[(int)segment3].GetDirection(nodeID);
                                    if (num9 == -1 || vector3.x * vector.x + vector3.z * vector.z >= -0.5f)
                                    {
                                        if (num13 == 2)
                                        {
                                            num17 = k;
                                            break;
                                        }
                                        if (vector3.x * vector2.x + vector3.z * vector2.z < -0.9396926f)
                                        {
                                            num17 = k;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                for (int l = 0; l < 8; l++)
                {
                    ushort segment4 = data.GetSegment(l);
                    if (segment4 != 0)
                    {
                        RoadBaseAI.TrafficLightState trafficLightState3;
                        RoadBaseAI.TrafficLightState trafficLightState4;
                        RoadBaseAI.GetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment4], currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4);
                        trafficLightState3 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
                        trafficLightState4 &= ~RoadBaseAI.TrafficLightState.RedToGreen;
                        if (num5 == l || num17 == l)
                        {
                            if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green)
                            {
                                trafficLightState3 = RoadBaseAI.TrafficLightState.RedToGreen;
                                num = 0;
                                if (++num2 >= num12)
                                {
                                    num2 = 0;
                                }
                            }
                            if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
                            {
                                trafficLightState4 = RoadBaseAI.TrafficLightState.GreenToRed;
                            }
                        }
                        else
                        {
                            if ((trafficLightState3 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
                            {
                                trafficLightState3 = RoadBaseAI.TrafficLightState.GreenToRed;
                            }
                            Vector3 direction3 = instance.m_segments.m_buffer[(int)segment4].GetDirection(nodeID);
                            if ((num11 & 1 << l) != 0 && num9 != l && ((num5 != -1 && direction3.x * vector2.x + direction3.z * vector2.z < -0.5f) || (num17 != -1 && direction3.x * vector3.x + direction3.z * vector3.z < -0.5f)))
                            {
                                if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) == RoadBaseAI.TrafficLightState.Green)
                                {
                                    trafficLightState4 = RoadBaseAI.TrafficLightState.GreenToRed;
                                }
                            }
                            else if ((trafficLightState4 & RoadBaseAI.TrafficLightState.Red) != RoadBaseAI.TrafficLightState.Green)
                            {
                                trafficLightState4 = RoadBaseAI.TrafficLightState.RedToGreen;
                                if (++num3 >= num14)
                                {
                                    num3 = 0;
                                }
                            }
                        }
                        RoadBaseAI.SetTrafficLightState(nodeID, ref instance.m_segments.m_buffer[(int)segment4], currentFrameIndex, trafficLightState3, trafficLightState4, false, false);
                    }
                }
                data.m_maxWaitTime = (byte)(num3 << 5 | num2 << 2 | num);
            }
            int num18 = 0;
            if (this.m_noiseAccumulation != 0)
            {
                int num19 = 0;
                for (int m = 0; m < 8; m++)
                {
                    ushort segment5 = data.GetSegment(m);
                    if (segment5 != 0)
                    {
                        num18 += (int)instance.m_segments.m_buffer[(int)segment5].m_trafficDensity;
                        num19++;
                    }
                }
                if (num19 != 0)
                {
                    num18 /= num19;
                }
            }
            int num20 = 100 - (num18 - 100) * (num18 - 100) / 100;
            int num21 = this.m_noiseAccumulation * num20 / 100;
            if (num21 != 0)
            {
                Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num21, data.m_position, this.m_noiseRadius);
            }
            if ((data.m_problems & Notification.Problem.RoadNotConnected) != Notification.Problem.None && (data.m_flags & NetNode.Flags.Original) != NetNode.Flags.None)
            {
                GuideController properties = Singleton<GuideManager>.instance.m_properties;
                if (properties != null)
                {
                    instance.m_outsideNodeNotConnected.Activate(properties.m_outsideNotConnected, nodeID, Notification.Problem.RoadNotConnected);
                }
            }
        }

        public static void AddNodeToSimulation(ushort nodeID)
        {
            nodeDictionary.Add(nodeID, new TrafficLightSimulation(nodeID));
        }

        public static void RemoveNodeFromSimulation(ushort nodeID)
        {
            nodeDictionary.Remove(nodeID);
        }

        public static TrafficLightSimulation GetNodeSimulation(ushort nodeID)
        {
            if (nodeDictionary.ContainsKey(nodeID))
            {
                return nodeDictionary[nodeID];
            }

            return null;
        }
    }
}

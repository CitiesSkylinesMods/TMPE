using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;

namespace TrafficManager
{
    class CustomHumanAI
    {
        public bool CheckTrafficLights(ushort node, ushort segment)
        {
            var nodeSimulation = CustomRoadAI.GetNodeSimulation(node);

            NetManager instance = Singleton<NetManager>.instance;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            uint num = (uint)(((int)node << 8) / 32768);
            uint num2 = currentFrameIndex - num & 255u;
            RoadBaseAI.TrafficLightState vehicleLightState;
            RoadBaseAI.TrafficLightState pedestrianLightState;
            bool vehicles;
            bool flag;

            if (nodeSimulation == null || (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive))
            {
                RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[(int) segment],
                    currentFrameIndex - num, out vehicleLightState, out pedestrianLightState, out vehicles, out flag);
                switch (pedestrianLightState)
                {
                    case RoadBaseAI.TrafficLightState.RedToGreen:
                        if (num2 < 60u)
                        {
                            return false;
                        }
                        break;
                    case RoadBaseAI.TrafficLightState.Red:
                    case RoadBaseAI.TrafficLightState.GreenToRed:
                        if (!flag && num2 >= 196u)
                        {
                            flag = true;
                            RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[(int) segment],
                                currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, flag);
                        }
                        return false;
                }
                return true;
            }
            else
            {
                if (TrafficLightsManual.IsSegmentLight(node, segment) && TrafficLightsManual.GetSegmentLight(node, segment).GetLightPedestrian() ==
                    RoadBaseAI.TrafficLightState.Red)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}

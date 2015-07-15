using ColossalFramework;
using TrafficManager.TrafficLight;

namespace TrafficManager.CustomAI
{
    class CustomHumanAI
    {
        public bool CheckTrafficLights(ushort node, ushort segment)
        {
            var nodeSimulation = CustomRoadAI.GetNodeSimulation(node);

            var instance = Singleton<NetManager>.instance;
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            var num = (uint)((node << 8) / 32768);
            var num2 = currentFrameIndex - num & 255u;

            if (nodeSimulation == null || (nodeSimulation.FlagTimedTrafficLights && !nodeSimulation.TimedTrafficLightsActive))
            {
                RoadBaseAI.TrafficLightState vehicleLightState;
                RoadBaseAI.TrafficLightState pedestrianLightState;
                bool vehicles;
                bool flag;
                RoadBaseAI.GetTrafficLightState(node, ref instance.m_segments.m_buffer[segment],
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
                            RoadBaseAI.SetTrafficLightState(node, ref instance.m_segments.m_buffer[segment],
                                currentFrameIndex - num, vehicleLightState, pedestrianLightState, vehicles, pedestrians: true);
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

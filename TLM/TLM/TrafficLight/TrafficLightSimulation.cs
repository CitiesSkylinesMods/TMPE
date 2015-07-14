using ColossalFramework;

namespace TrafficManager.TrafficLight
{
    public class TrafficLightSimulation
    {
        public ushort NodeId;

        public bool _manualTrafficLights = false;

        public bool _timedTrafficLights = false;

        public bool TimedTrafficLightsActive = false;

        public bool FlagManualTrafficLights
        {
            get { return _manualTrafficLights; }
            set
            {
                _manualTrafficLights = value;

                if (value == false)
                {
                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    for (int s = 0; s < 8; s++)
                    {
                        var segment = node.GetSegment(s);

                        if (segment != 0)
                        {
                            if (TrafficLightsManual.IsSegmentLight(NodeId, segment))
                            {
                                TrafficLightsManual.ClearSegment(NodeId, segment);
                            }
                        }
                    }
                }
                else
                {
                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (int s = 0; s < 8; s++)
                    {
                        var segment = node.GetSegment(s);

                        if (segment != 0)
                        {
                            RoadBaseAI.TrafficLightState trafficLightState3;
                            RoadBaseAI.TrafficLightState trafficLightState4;
                            bool vehicles;
                            bool pedestrians;

                            RoadBaseAI.GetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[(int) segment],
                                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles,
                                out pedestrians);

                            if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green)
                            {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Green);
                            }
                            else
                            {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Red);
                            }
                        }
                    }
                }
            }
        }

        public bool FlagTimedTrafficLights
        {
            get { return _timedTrafficLights; }
            set
            {
                _timedTrafficLights = value;

                if (value == false)
                {
                    TimedTrafficLightsActive = false;
                    TrafficLightsTimed.RemoveTimedLight(NodeId);

                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    for (int s = 0; s < 8; s++)
                    {
                        var segment = node.GetSegment(s);

                        if (segment != 0)
                        {
                            if (TrafficLightsManual.IsSegmentLight(NodeId, segment))
                            {
                                TrafficLightsManual.ClearSegment(NodeId, segment);
                            }
                        }
                    }
                }
                else
                {
                    TrafficLightsTimed.AddTimedLight(NodeId, TrafficLightTool.SelectedNodeIndexes);

                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (int s = 0; s < 8; s++)
                    {
                        var segment = node.GetSegment(s);

                        if (segment != 0)
                        {
                            RoadBaseAI.TrafficLightState trafficLightState3;
                            RoadBaseAI.TrafficLightState trafficLightState4;
                            bool vehicles;
                            bool pedestrians;

                            RoadBaseAI.GetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[(int)segment],
                                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles,
                                out pedestrians);

                            if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green)
                            {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Green);
                            }
                            else
                            {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Red);
                            }
                        }
                    }
                }
            }
        }

        public TrafficLightSimulation(ushort nodeID)
        {
            this.NodeId = nodeID;
        }

        public void SimulationStep(ref NetNode data)
        {
            NetManager instance = Singleton<NetManager>.instance;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            if (TrafficLightsTimed.IsTimedLight(NodeId) && TimedTrafficLightsActive)
            {
                var timedNode = TrafficLightsTimed.GetTimedLight(NodeId);
                timedNode.checkStep(currentFrameIndex >> 6);
            }

            for (int l = 0; l < 8; l++)
            {
                ushort segment = data.GetSegment(l);
                if (segment != 0)
                {
                    if (TrafficLightsManual.IsSegmentLight(NodeId, segment))
                    {
                        var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segment);

                        segmentLight.lastChange = (currentFrameIndex >> 6) - segmentLight.lastChangeFrame;
                    }
                }
            }
        }
    }
}

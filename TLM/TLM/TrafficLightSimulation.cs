using System.Collections.Generic;
using ColossalFramework;

namespace TrafficManager
{
    public class TrafficLightSimulation
    {
        protected ushort NodeId;

        private bool _manualTrafficLights = false;

        private static Dictionary<int, RoadBaseAI.TrafficLightState> segmentLights = new Dictionary<int, RoadBaseAI.TrafficLightState>();

        public bool ManualTrafficLights
        {
            get { return _manualTrafficLights; }
            set
            {
                _manualTrafficLights = value;

                if (value == false)
                {
                    segmentLights.Clear();
                }
                else
                {
                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (int s = 0; s < node.CountSegments(); s++)
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

                            if (trafficLightState3 != RoadBaseAI.TrafficLightState.Green)
                            {
                                segmentLights.Add(segment, RoadBaseAI.TrafficLightState.Green);
                            }
                            else
                            {
                                segmentLights.Add(segment, RoadBaseAI.TrafficLightState.Red);
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
            if (_manualTrafficLights)
            {
                NetManager instance = Singleton<NetManager>.instance;
                uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                for (int l = 0; l < 8; l++)
                {
                    ushort segment = data.GetSegment(l);
                    if (segment != 0)
                    {
                        RoadBaseAI.TrafficLightState trafficLightState3;
                        RoadBaseAI.TrafficLightState trafficLightState4;
                        bool vehicles;
                        bool pedestrians;
                        RoadBaseAI.GetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[(int)segment], currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles, out pedestrians);

                        if (segmentLights.ContainsKey(segment))
                        {
                            trafficLightState3 = segmentLights[segment];

                            if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green)
                            {
                                trafficLightState4 = RoadBaseAI.TrafficLightState.Red;
                            }
                            else
                            {
                                trafficLightState4 = RoadBaseAI.TrafficLightState.Green;
                            }
                        }

                        RoadBaseAI.SetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[(int)segment], currentFrameIndex, trafficLightState3, trafficLightState4, vehicles, pedestrians);
                    }
                }
            }
            else
            {
                NetManager instance = Singleton<NetManager>.instance;
                var logg = "";

                for (int l = 0; l < 1; l++)
                {
                    ushort segment = data.GetSegment(l);
                    if (segment != 0)
                    {
                        logg += "Segment " + segment + ": " + instance.m_segments.m_buffer[(int)segment].m_trafficLightState0 + " " + instance.m_segments.m_buffer[(int)segment].m_trafficLightState1 + "; ";
                    }
                }

                Log.Warning(logg);
            }
        }

        public void ForceTrafficLights( ref NetNode data, int segmentId)
        {
            if (!segmentLights.ContainsKey(segmentId))
            {
                Log.Warning("No such segment");
            }
            else
            {
                if (segmentLights[segmentId] == RoadBaseAI.TrafficLightState.Green)
                {
                    segmentLights[segmentId] = RoadBaseAI.TrafficLightState.Red;
                }
                else
                {
                    segmentLights[segmentId] = RoadBaseAI.TrafficLightState.Green;
                }
            }

            Log.Warning(segmentLights[segmentId]);

            this.SimulationStep( ref data );
            this.UpdateColorMap();
        }

        public void UpdateColorMap()
        {
            var flag = false;
            var res = Singleton<NetManager>.instance.UpdateColorMap(Singleton<RenderManager>.instance.m_objectColorMap);

            if (res)
		    {
                Log.Warning("success");
			    flag = true;
		    }

            if (flag)
            {
                Singleton<RenderManager>.instance.m_objectColorMap.Apply(false);
            }
        }

        //public void GetTrafficLightState(ushort nodeID, ref NetSegment segmentData, uint frame,
        //    out RoadBaseAI.TrafficLightState vehicleLightState, out RoadBaseAI.TrafficLightState pedestrianLightState,
        //    out bool vehicles, out bool pedestrians)
        //{
        //    Log.Warning((frame >> 8) + " " + segmentData.m_trafficLightState0 + " " + segmentData.m_trafficLightState1);
        //    int num;
        //    if ((frame >> 8 & 1u) == 0u)
        //    {
        //        num = (int)segmentData.m_trafficLightState0;
        //    }
        //    else
        //    {
        //        num = (int)segmentData.m_trafficLightState1;
        //    }
        //    if (segmentData.m_startNode == nodeID)
        //    {
        //        num &= 15;
        //        vehicles = ((segmentData.m_flags & NetSegment.Flags.TrafficStart) != NetSegment.Flags.None);
        //        pedestrians = ((segmentData.m_flags & NetSegment.Flags.CrossingStart) != NetSegment.Flags.None);
        //    }
        //    else
        //    {
        //        num >>= 4;
        //        vehicles = ((segmentData.m_flags & NetSegment.Flags.TrafficEnd) != NetSegment.Flags.None);
        //        pedestrians = ((segmentData.m_flags & NetSegment.Flags.CrossingEnd) != NetSegment.Flags.None);
        //    }
        //    vehicleLightState = (RoadBaseAI.TrafficLightState)(num & 3);
        //    pedestrianLightState = (RoadBaseAI.TrafficLightState)(num >> 2);
        //}
    }
}

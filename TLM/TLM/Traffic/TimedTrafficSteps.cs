using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;

namespace TrafficManager.Traffic
{
    public class TimedTrafficSteps : ICloneable
    {
        public ushort NodeId;
        public int NumSteps;
        public uint Frame;

        public List<int> Segments = new List<int>();

        public List<RoadBaseAI.TrafficLightState> LightMain = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> LightLeft = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> LightRight = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> LightPedestrian = new List<RoadBaseAI.TrafficLightState>(); 

        public TimedTrafficSteps(int num, ushort nodeId)
        {
            NodeId = nodeId;
            NumSteps = num;

            var node = TrafficLightTool.GetNetNode(nodeId);

            for (var s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segment);

                    Segments.Add(segment);
                    LightMain.Add(segmentLight.GetLightMain());
                    LightLeft.Add(segmentLight.GetLightLeft());
                    LightRight.Add(segmentLight.GetLightRight());
                    LightPedestrian.Add(segmentLight.GetLightPedestrian());
                }
            }
        }

        public RoadBaseAI.TrafficLightState GetLight(int segment, int lightType)
        {
            for (var i = 0; i < Segments.Count; i++)
            {
                if (Segments[i] == segment)
                {
                    if (lightType == 0)
                        return LightMain[i];
                    if (lightType == 1)
                        return LightLeft[i];
                    if (lightType == 2)
                        return LightRight[i];
                    if (lightType == 3)
                        return LightPedestrian[i];
                }
            }

            return RoadBaseAI.TrafficLightState.Green;
        }

        public void SetFrame(uint frame)
        {
            Frame = frame;
        }

        public void SetLights()
        {
            for (var s = 0; s < Segments.Count; s++)
            {
                var segment = Segments[s];

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segment);

                    segmentLight.lightMain = LightMain[s];
                    segmentLight.lightLeft = LightLeft[s];
                    segmentLight.lightRight = LightRight[s];
                    segmentLight.lightPedestrian = LightPedestrian[s];
                    segmentLight.UpdateVisuals();
                }
            }
        }

        public void UpdateLights()
        {
            for (var s = 0; s < Segments.Count; s++)
            {
                var segment = Segments[s];

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segment);

                    LightMain[s] = segmentLight.lightMain;
                    LightLeft[s] = segmentLight.lightLeft;
                    LightRight[s] = segmentLight.lightRight;
                    LightPedestrian[s] = segmentLight.lightPedestrian;
                }
            }
        }

        public long CurrentStep()
        {
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            return Frame + NumSteps - (currentFrameIndex >> 6);
        }

        public bool StepDone(uint frame)
        {
            if (Frame + NumSteps <= frame)
            {
                return true;
            }

            return false;
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
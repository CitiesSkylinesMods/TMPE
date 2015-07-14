using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;

namespace TrafficManager.Traffic
{
    public class TimedTrafficSteps : ICloneable
    {
        public ushort nodeID;
        public int numSteps;
        public uint frame;

        public List<int> segments = new List<int>();

        public List<RoadBaseAI.TrafficLightState> lightMain = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> lightLeft = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> lightRight = new List<RoadBaseAI.TrafficLightState>();
        public List<RoadBaseAI.TrafficLightState> lightPedestrian = new List<RoadBaseAI.TrafficLightState>(); 

        public TimedTrafficSteps(int num, ushort nodeID)
        {
            this.nodeID = nodeID;
            this.numSteps = num;

            var node = TrafficLightTool.GetNetNode(nodeID);

            for (int s = 0; s < 8; s++)
            {
                var segment = node.GetSegment(s);

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(nodeID, segment);

                    segments.Add(segment);
                    lightMain.Add(segmentLight.GetLightMain());
                    lightLeft.Add(segmentLight.GetLightLeft());
                    lightRight.Add(segmentLight.GetLightRight());
                    lightPedestrian.Add(segmentLight.GetLightPedestrian());
                }
            }
        }

        public RoadBaseAI.TrafficLightState getLight(int segment, int lightType)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i] == segment)
                {
                    if (lightType == 0)
                        return lightMain[i];
                    else if (lightType == 1)
                        return lightLeft[i];
                    else if (lightType == 2)
                        return lightRight[i];
                    else if (lightType == 3)
                        return lightPedestrian[i];
                }
            }

            return RoadBaseAI.TrafficLightState.Green;
        }

        public void setFrame(uint frame)
        {
            this.frame = frame;
        }

        public void setLights()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(nodeID, segment);

                    segmentLight.lightMain = lightMain[s];
                    segmentLight.lightLeft = lightLeft[s];
                    segmentLight.lightRight = lightRight[s];
                    segmentLight.lightPedestrian = lightPedestrian[s];
                    segmentLight.UpdateVisuals();
                }
            }
        }

        public void updateLights()
        {
            for (int s = 0; s < segments.Count; s++)
            {
                var segment = segments[s];

                if (segment != 0)
                {
                    var segmentLight = TrafficLightsManual.GetSegmentLight(nodeID, segment);

                    lightMain[s] = segmentLight.lightMain;
                    lightLeft[s] = segmentLight.lightLeft;
                    lightRight[s] = segmentLight.lightRight;
                    lightPedestrian[s] = segmentLight.lightPedestrian;
                }
            }
        }

        public long currentStep()
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            return this.frame + this.numSteps - (currentFrameIndex >> 6);
        }

        public bool stepDone(uint frame)
        {
            if (this.frame + this.numSteps <= frame)
            {
                return true;
            }

            return false;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
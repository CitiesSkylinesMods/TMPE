using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;

namespace TrafficManager
{
    class ManualSegment
    {
        public ushort node_1 = 0;
        public ushort node_2 = 0;

        public ManualSegmentLight instance_1;
        public ManualSegmentLight instance_2;
    }

    class ManualSegmentLight
    {
        public enum Mode
        {
            Simple = 1,
            LeftForwardR = 2,
            RightForwardL = 3,
            All = 4
        }

        public ushort node;
        public int segment;

        public Mode currentMode = Mode.Simple;

        public RoadBaseAI.TrafficLightState lightLeft;
        public RoadBaseAI.TrafficLightState lightMain;
        public RoadBaseAI.TrafficLightState lightRight;
        public RoadBaseAI.TrafficLightState lightPedestrian;

        public uint lastChange;
        public uint lastChangeFrame;

        public bool pedestrianEnabled = false;

        public ManualSegmentLight(ushort node, int segment, RoadBaseAI.TrafficLightState mainLight)
        {
            this.node = node;
            this.segment = segment;

            lightMain = mainLight;
            lightLeft = mainLight;
            lightRight = mainLight;
            lightPedestrian = mainLight == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            UpdateVisuals();
        }

        public RoadBaseAI.TrafficLightState GetLightMain()
        {
            return lightMain;
        }

        public RoadBaseAI.TrafficLightState GetLightLeft()
        {
            return lightLeft;
        }

        public RoadBaseAI.TrafficLightState GetLightRight()
        {
            return lightRight;
        }
        public RoadBaseAI.TrafficLightState GetLightPedestrian()
        {
            return lightPedestrian;
        }

        public void ChangeMode()
        {
            var hasLeftSegment = TrafficPriority.HasLeftSegment(this.segment, this.node, false) && TrafficPriority.hasLeftLane(this.node, this.segment);
            var hasForwardSegment = TrafficPriority.HasForwardSegment(this.segment, this.node, false) && TrafficPriority.hasForwardLane(this.node, this.segment);
            var hasRightSegment = TrafficPriority.HasRightSegment(this.segment, this.node, false) && TrafficPriority.hasRightLane(this.node, this.segment);

            if (currentMode == ManualSegmentLight.Mode.Simple)
            {
                if (!hasLeftSegment)
                {
                    currentMode = ManualSegmentLight.Mode.RightForwardL;
                }
                else
                {
                    currentMode = ManualSegmentLight.Mode.LeftForwardR;
                }
            }
            else if (currentMode == ManualSegmentLight.Mode.LeftForwardR)
            {
                if (!hasForwardSegment || !hasRightSegment)
                {
                    currentMode = ManualSegmentLight.Mode.Simple;
                }
                else
                {
                    currentMode = ManualSegmentLight.Mode.RightForwardL;
                }
            }
            else if (currentMode == ManualSegmentLight.Mode.RightForwardL)
            {
                if (!hasLeftSegment)
                {
                    currentMode = ManualSegmentLight.Mode.Simple;
                }
                else
                {
                    currentMode = ManualSegmentLight.Mode.All;
                }
            }
            else
            {
                currentMode = ManualSegmentLight.Mode.Simple;
            }

            if (currentMode == Mode.Simple)
            {
                lightLeft = lightMain;
                lightRight = lightMain;
                lightPedestrian = _checkPedestrianLight();
            }
        }

        public void ManualPedestrian()
        {
            pedestrianEnabled = !pedestrianEnabled;
        }

        public void ChangeLightMain()
        {
            var invertedLight = lightMain == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            if (currentMode == Mode.Simple)
            {
                lightLeft = invertedLight;
                lightRight = invertedLight;
                lightPedestrian = !pedestrianEnabled ? lightMain : lightPedestrian;
                lightMain = invertedLight;
            }
            else if (currentMode == Mode.LeftForwardR)
            {
                lightRight = invertedLight;
                lightMain = invertedLight;
            }
            else if (currentMode == Mode.RightForwardL)
            {
                lightLeft = invertedLight;
                lightMain = invertedLight;
            }
            else
            {
                lightMain = invertedLight;
            }

            if (!pedestrianEnabled)
            {
                lightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightLeft()
        {
            var invertedLight = lightLeft == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            lightLeft = invertedLight;

            if (!pedestrianEnabled)
            {
                lightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightRight()
        {
            var invertedLight = lightRight == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            lightRight = invertedLight;

            if (!pedestrianEnabled)
            {
                lightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightPedestrian()
        {
            if (pedestrianEnabled)
            {
                var invertedLight = lightPedestrian == RoadBaseAI.TrafficLightState.Green
                    ? RoadBaseAI.TrafficLightState.Red
                    : RoadBaseAI.TrafficLightState.Green;

                lightPedestrian = invertedLight;
                UpdateVisuals();
            }
        }

        public void UpdateVisuals()
        {
            NetManager instance = Singleton<NetManager>.instance;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            lastChange = 0u;
            lastChangeFrame = currentFrameIndex >> 6;

            RoadBaseAI.TrafficLightState trafficLightState3;
            RoadBaseAI.TrafficLightState trafficLightState4;
            bool vehicles;
            bool pedestrians;
            RoadBaseAI.GetTrafficLightState(this.node, ref instance.m_segments.m_buffer[(int)this.segment],
                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles, out pedestrians);

            if (lightMain == RoadBaseAI.TrafficLightState.Red && lightLeft == RoadBaseAI.TrafficLightState.Red && lightRight == RoadBaseAI.TrafficLightState.Red)
            {
                trafficLightState3 = RoadBaseAI.TrafficLightState.Red;
            }
            else
            {
                trafficLightState3 = RoadBaseAI.TrafficLightState.Green;
            }

            trafficLightState4 = lightPedestrian;

            RoadBaseAI.SetTrafficLightState(this.node, ref instance.m_segments.m_buffer[(int)this.segment], currentFrameIndex,
                trafficLightState3, trafficLightState4, vehicles, pedestrians);
        }

        private RoadBaseAI.TrafficLightState _checkPedestrianLight()
        {
            if (lightLeft == RoadBaseAI.TrafficLightState.Red && lightMain == RoadBaseAI.TrafficLightState.Red &&
                lightRight == RoadBaseAI.TrafficLightState.Red)
            {
                return RoadBaseAI.TrafficLightState.Green;
            }
            else
            {
                return RoadBaseAI.TrafficLightState.Red;
            }
        }
    }
    class TrafficLightsManual
    {

        public static Dictionary<int, ManualSegment> ManualSegments =
            new Dictionary<int, ManualSegment>();

        public static bool segmentIsIncomingOneWay(int segmentid, ushort nodeID)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (segment.m_startNode == nodeID)
                dir = NetInfo.Direction.Backward;
            var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
            var dir3 = TrafficPriority.leftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

            var isOneWay = true;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == dir3))
                {
                    isOneWay = false;
                }

                num2 = instance.m_lanes.m_buffer[(int) ((UIntPtr) num2)].m_nextLane;
                num3++;
            }

            return isOneWay;
        }

        public static bool segmentIsOneWay(int segmentid)
        {
            NetManager instance = Singleton<NetManager>.instance;

            var segment = instance.m_segments.m_buffer[segmentid];
            var info = segment.Info;

            uint num2 = segment.m_lanes;
            int num3 = 0;

            var isOneWay = true;
            var hasForward = false;
            var hasBackward = false;

            while (num3 < info.m_lanes.Length && num2 != 0u)
            {
                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == NetInfo.Direction.Forward))
                {
                    hasForward = true;
                }

                if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
                    (info.m_lanes[num3].m_direction == NetInfo.Direction.Backward))
                {
                    hasBackward = true;
                }

                if (hasForward && hasBackward)
                {
                    isOneWay = false;
                    return isOneWay;
                }

                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num3++;
            }

            return isOneWay;
        }

        public static void AddSegmentLight(ushort nodeID, int segmentID, RoadBaseAI.TrafficLightState light)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                ManualSegments[segmentID].node_2 = nodeID;
                ManualSegments[segmentID].instance_2 = new ManualSegmentLight(nodeID, segmentID, light);
            }
            else
            {
                ManualSegments.Add(segmentID, new ManualSegment());
                ManualSegments[segmentID].node_1 = nodeID;
                ManualSegments[segmentID].instance_1 = new ManualSegmentLight(nodeID, segmentID, light);
            }
        }

        public static void RemoveSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments[segmentID].node_1 == nodeID)
            {
                ManualSegments[segmentID].node_1 = 0;
                ManualSegments[segmentID].instance_1 = null;
            }
            else
            {
                ManualSegments[segmentID].node_2 = 0;
                ManualSegments[segmentID].instance_2 = null;
            }

            if (ManualSegments[segmentID].node_1 == 0 && ManualSegments[segmentID].node_2 == 0)
            {
                ManualSegments.Remove(segmentID);
            }
        }

        public static bool IsSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                var manualSegment = ManualSegments[segmentID];

                if (manualSegment.node_1 == nodeID || manualSegment.node_2 == nodeID)
                {
                    return true;
                }
            }

            return false;
        }

        public static ManualSegmentLight GetSegmentLight(ushort nodeID, int segmentID)
        {
            if (ManualSegments.ContainsKey(segmentID))
            {
                var manualSegment = ManualSegments[segmentID];

                if (manualSegment.node_1 == nodeID)
                {
                    return manualSegment.instance_1;
                }
                if (manualSegment.node_2 == nodeID)
                {
                    return manualSegment.instance_2;
                }
            }

            return null;
        }

        public static void ClearSegment(ushort nodeID, int segmentID)
        {
            var manualSegment = ManualSegments[segmentID];

            if (manualSegment.node_1 == nodeID)
            {
                manualSegment.node_1 = 0;
                manualSegment.instance_1 = null;
            }

            if (manualSegment.node_2 == nodeID)
            {
                manualSegment.node_2 = 0;
                manualSegment.instance_2 = null;
            }

            if (manualSegment.node_1 == 0 && manualSegment.node_2 == 0)
            {
                ManualSegments.Remove(segmentID);
            }
        }
    }
}

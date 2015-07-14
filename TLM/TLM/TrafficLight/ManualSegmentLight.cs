using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight
{
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
            var hasLeftSegment = TrafficPriority.HasLeftSegment(this.segment, this.node) && TrafficPriority.HasLeftLane(this.node, this.segment);
            var hasForwardSegment = TrafficPriority.HasForwardSegment(this.segment, this.node) && TrafficPriority.HasForwardLane(this.node, this.segment);
            var hasRightSegment = TrafficPriority.HasRightSegment(this.segment, this.node) && TrafficPriority.HasRightLane(this.node, this.segment);

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
}
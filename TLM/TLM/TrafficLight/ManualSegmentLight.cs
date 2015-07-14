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

        public ushort Node;
        public int Segment;

        public Mode CurrentMode = Mode.Simple;

        public RoadBaseAI.TrafficLightState LightLeft;
        public RoadBaseAI.TrafficLightState LightMain;
        public RoadBaseAI.TrafficLightState LightRight;
        public RoadBaseAI.TrafficLightState LightPedestrian;

        public uint LastChange;
        public uint LastChangeFrame;

        public bool PedestrianEnabled;

        public ManualSegmentLight(ushort node, int segment, RoadBaseAI.TrafficLightState mainLight)
        {
            Node = node;
            Segment = segment;

            LightMain = mainLight;
            LightLeft = mainLight;
            LightRight = mainLight;
            LightPedestrian = mainLight == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            UpdateVisuals();
        }

        public RoadBaseAI.TrafficLightState GetLightMain()
        {
            return LightMain;
        }

        public RoadBaseAI.TrafficLightState GetLightLeft()
        {
            return LightLeft;
        }

        public RoadBaseAI.TrafficLightState GetLightRight()
        {
            return LightRight;
        }
        public RoadBaseAI.TrafficLightState GetLightPedestrian()
        {
            return LightPedestrian;
        }

        public void ChangeMode()
        {
            var hasLeftSegment = TrafficPriority.HasLeftSegment(Segment, Node) && TrafficPriority.HasLeftLane(Node, Segment);
            var hasForwardSegment = TrafficPriority.HasForwardSegment(Segment, Node) && TrafficPriority.HasForwardLane(Node, Segment);
            var hasRightSegment = TrafficPriority.HasRightSegment(Segment, Node) && TrafficPriority.HasRightLane(Node, Segment);

            if (CurrentMode == Mode.Simple)
            {
                if (!hasLeftSegment)
                {
                    CurrentMode = Mode.RightForwardL;
                }
                else
                {
                    CurrentMode = Mode.LeftForwardR;
                }
            }
            else if (CurrentMode == Mode.LeftForwardR)
            {
                if (!hasForwardSegment || !hasRightSegment)
                {
                    CurrentMode = Mode.Simple;
                }
                else
                {
                    CurrentMode = Mode.RightForwardL;
                }
            }
            else if (CurrentMode == Mode.RightForwardL)
            {
                if (!hasLeftSegment)
                {
                    CurrentMode = Mode.Simple;
                }
                else
                {
                    CurrentMode = Mode.All;
                }
            }
            else
            {
                CurrentMode = Mode.Simple;
            }

            if (CurrentMode == Mode.Simple)
            {
                LightLeft = LightMain;
                LightRight = LightMain;
                LightPedestrian = _checkPedestrianLight();
            }
        }

        public void ManualPedestrian()
        {
            PedestrianEnabled = !PedestrianEnabled;
        }

        public void ChangeLightMain()
        {
            var invertedLight = LightMain == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            if (CurrentMode == Mode.Simple)
            {
                LightLeft = invertedLight;
                LightRight = invertedLight;
                LightPedestrian = !PedestrianEnabled ? LightMain : LightPedestrian;
                LightMain = invertedLight;
            }
            else if (CurrentMode == Mode.LeftForwardR)
            {
                LightRight = invertedLight;
                LightMain = invertedLight;
            }
            else if (CurrentMode == Mode.RightForwardL)
            {
                LightLeft = invertedLight;
                LightMain = invertedLight;
            }
            else
            {
                LightMain = invertedLight;
            }

            if (!PedestrianEnabled)
            {
                LightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightLeft()
        {
            var invertedLight = LightLeft == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            LightLeft = invertedLight;

            if (!PedestrianEnabled)
            {
                LightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightRight()
        {
            var invertedLight = LightRight == RoadBaseAI.TrafficLightState.Green
                ? RoadBaseAI.TrafficLightState.Red
                : RoadBaseAI.TrafficLightState.Green;

            LightRight = invertedLight;

            if (!PedestrianEnabled)
            {
                LightPedestrian = _checkPedestrianLight();
            }

            UpdateVisuals();
        }

        public void ChangeLightPedestrian()
        {
            if (PedestrianEnabled)
            {
                var invertedLight = LightPedestrian == RoadBaseAI.TrafficLightState.Green
                    ? RoadBaseAI.TrafficLightState.Red
                    : RoadBaseAI.TrafficLightState.Green;

                LightPedestrian = invertedLight;
                UpdateVisuals();
            }
        }

        public void UpdateVisuals()
        {
            NetManager instance = Singleton<NetManager>.instance;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            LastChange = 0u;
            LastChangeFrame = currentFrameIndex >> 6;

            RoadBaseAI.TrafficLightState trafficLightState3;
            RoadBaseAI.TrafficLightState trafficLightState4;
            bool vehicles;
            bool pedestrians;
            RoadBaseAI.GetTrafficLightState(Node, ref instance.m_segments.m_buffer[Segment],
                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles, out pedestrians);

            if (LightMain == RoadBaseAI.TrafficLightState.Red && LightLeft == RoadBaseAI.TrafficLightState.Red && LightRight == RoadBaseAI.TrafficLightState.Red)
            {
                trafficLightState3 = RoadBaseAI.TrafficLightState.Red;
            }
            else
            {
                trafficLightState3 = RoadBaseAI.TrafficLightState.Green;
            }

            trafficLightState4 = LightPedestrian;

            RoadBaseAI.SetTrafficLightState(Node, ref instance.m_segments.m_buffer[Segment], currentFrameIndex,
                trafficLightState3, trafficLightState4, vehicles, pedestrians);
        }

        private RoadBaseAI.TrafficLightState _checkPedestrianLight()
        {
            if (LightLeft == RoadBaseAI.TrafficLightState.Red && LightMain == RoadBaseAI.TrafficLightState.Red &&
                LightRight == RoadBaseAI.TrafficLightState.Red)
            {
                return RoadBaseAI.TrafficLightState.Green;
            }
            return RoadBaseAI.TrafficLightState.Red;
        }
    }
}
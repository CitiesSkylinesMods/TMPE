using ColossalFramework;

namespace TrafficManager.TrafficLight {
    public class TrafficLightSimulation {
        public readonly ushort NodeId;

        public bool ManualTrafficLights;

        public bool TimedTrafficLights;

        public bool TimedTrafficLightsActive;

        public bool FlagManualTrafficLights {
            get { return ManualTrafficLights; }
            set {
                ManualTrafficLights = value;

                if (value == false) {
                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    for (var s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        if (TrafficLightsManual.IsSegmentLight(NodeId, segment)) {
                            TrafficLightsManual.ClearSegment(NodeId, segment);
                        }
                    }
                } else {
                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (var s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        RoadBaseAI.TrafficLightState vehicleLightState;
                        RoadBaseAI.TrafficLightState pedestrianLightState;
                        bool vehicles;
                        bool pedestrians;

                        RoadBaseAI.GetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[segment],
                            currentFrameIndex - 256u, out vehicleLightState, out pedestrianLightState, out vehicles,
                            out pedestrians);

                        TrafficLightsManual.AddSegmentLight(NodeId, segment,
                            vehicleLightState == RoadBaseAI.TrafficLightState.Green
                                ? RoadBaseAI.TrafficLightState.Green
                                : RoadBaseAI.TrafficLightState.Red);
                    }
                }
            }
        }

        public bool FlagTimedTrafficLights {
            get { return TimedTrafficLights; }
            set {
                TimedTrafficLights = value;

                if (value == false) {
                    TimedTrafficLightsActive = false;
                    TrafficLightsTimed.RemoveTimedLight(NodeId);

                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    for (int s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        if (TrafficLightsManual.IsSegmentLight(NodeId, segment)) {
                            TrafficLightsManual.ClearSegment(NodeId, segment);
                        }
                    }
                } else {
                    TrafficLightsTimed.AddTimedLight(NodeId, TrafficLightTool.SelectedNodeIndexes);

                    var node = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId];

                    var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (int s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment != 0) {
                            RoadBaseAI.TrafficLightState trafficLightState3;
                            RoadBaseAI.TrafficLightState trafficLightState4;
                            bool vehicles;
                            bool pedestrians;

                            RoadBaseAI.GetTrafficLightState(NodeId, ref instance.m_segments.m_buffer[segment],
                                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles,
                                out pedestrians);

                            if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green) {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Green);
                            } else {
                                TrafficLightsManual.AddSegmentLight(NodeId, segment, RoadBaseAI.TrafficLightState.Red);
                            }
                        }
                    }
                }
            }
        }

        public TrafficLightSimulation(ushort nodeId) {
            NodeId = nodeId;
        }

        public void SimulationStep(ref NetNode data) {
            //Log.Warning("step: " + NodeId);
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (TimedTrafficLightsActive) {
				var timedNode = TrafficLightsTimed.GetTimedLight(NodeId);
				if (timedNode != null)
					timedNode.CheckStep(currentFrameIndex >> 6);
            }

            for (var l = 0; l < 8; l++) {
                var segment = data.GetSegment(l);
                if (segment == 0) continue;
                if (!TrafficLightsManual.IsSegmentLight(NodeId, segment)) continue;

                var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segment);

                segmentLight.LastChange = (currentFrameIndex >> 6) - segmentLight.LastChangeFrame;
            }
        }
    }
}

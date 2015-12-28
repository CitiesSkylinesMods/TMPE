using System;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
    public class TrafficLightSimulation {
        public readonly ushort nodeId;

        public bool ManualTrafficLights;
        public bool TimedTrafficLights;
        public bool TimedTrafficLightsActive;

        public bool FlagManualTrafficLights {
            get { return ManualTrafficLights; }
            set {
                ManualTrafficLights = value;

                if (value == false) {
                    var node = getNode();

                    for (var s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        if (TrafficLightsManual.IsSegmentLight(nodeId, segment)) {
                            TrafficLightsManual.ClearSegment(nodeId, segment);
                        }
                    }
                } else {
					var node = getNode();

					var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (var s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        RoadBaseAI.TrafficLightState vehicleLightState;
                        RoadBaseAI.TrafficLightState pedestrianLightState;
                        bool vehicles;
                        bool pedestrians;

                        RoadBaseAI.GetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segment],
                            currentFrameIndex - 256u, out vehicleLightState, out pedestrianLightState, out vehicles,
                            out pedestrians);

                        TrafficLightsManual.AddSegmentLight(nodeId, segment,
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
					TrafficLightsTimed timedLight = TrafficLightsTimed.GetTimedLight(nodeId);
					if (timedLight != null)
						timedLight.Stop();
					TrafficLightsTimed.RemoveTimedLight(nodeId);

					var node = getNode();

					for (int s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment == 0) continue;
                        if (TrafficLightsManual.IsSegmentLight(nodeId, segment)) {
                            TrafficLightsManual.ClearSegment(nodeId, segment);
                        }
                    }
                } else {
                    TrafficLightsTimed.AddTimedLight(nodeId, TrafficLightTool.SelectedNodeIndexes);

					var node = getNode();

					var instance = Singleton<NetManager>.instance;
                    var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

                    for (int s = 0; s < 8; s++) {
                        var segment = node.GetSegment(s);

                        if (segment != 0) {
                            RoadBaseAI.TrafficLightState trafficLightState3;
                            RoadBaseAI.TrafficLightState trafficLightState4;
                            bool vehicles;
                            bool pedestrians;

                            RoadBaseAI.GetTrafficLightState(nodeId, ref instance.m_segments.m_buffer[segment],
                                currentFrameIndex - 256u, out trafficLightState3, out trafficLightState4, out vehicles,
                                out pedestrians);

                            if (trafficLightState3 == RoadBaseAI.TrafficLightState.Green) {
                                TrafficLightsManual.AddSegmentLight(nodeId, segment, RoadBaseAI.TrafficLightState.Green);
                            } else {
                                TrafficLightsManual.AddSegmentLight(nodeId, segment, RoadBaseAI.TrafficLightState.Red);
                            }
                        }
                    }
                }
            }
        }

        public TrafficLightSimulation(ushort nodeId) {
            this.nodeId = nodeId;
		}

        public void SimulationStep() {
            //Log.Warning("step: " + NodeId);
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (TimedTrafficLightsActive) {
				var timedNode = TrafficLightsTimed.GetTimedLight(nodeId);
				if (timedNode != null)
					timedNode.CheckCurrentStep();
            }

			var node = getNode();
			for (var l = 0; l < 8; l++) {
                var segment = node.GetSegment(l);
                if (segment == 0) continue;
                if (!TrafficLightsManual.IsSegmentLight(nodeId, segment)) continue;

                var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segment);

                segmentLight.LastChange = (currentFrameIndex >> 6) - segmentLight.LastChangeFrame;
            }
        }

		public NetNode getNode() {
			return Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId];
		}

		/// <summary>
		/// Stops & destroys the traffic light simulation(s) at this node (group)
		/// </summary>
		public void Destroy() {
			var node = getNode();
			var timedNode = TrafficLightsTimed.GetTimedLight(nodeId);

			if (timedNode != null) {
				foreach (var timedNodeId in timedNode.NodeGroup) {
					var nodeSim = TrafficPriority.GetNodeSimulation(timedNodeId); // `this` is one of `nodeSim`
					TrafficLightsTimed.RemoveTimedLight(timedNodeId);
					if (nodeSim == null)
						continue;
					nodeSim.TimedTrafficLightsActive = false;
				}
			}
		}
	}
}

using System;
using ColossalFramework;
using TrafficManager.Traffic;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;

namespace TrafficManager.TrafficLight {
    public class TrafficLightSimulation {
		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		public static Dictionary<ushort, TrafficLightSimulation> LightSimulationByNodeId = new Dictionary<ushort, TrafficLightSimulation>();

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
                            TrafficLightsManual.RemoveSegmentLight(nodeId, segment);
                        }
                    }
                } else {
					var node = getNode();

                    for (var s = 0; s < 8; s++) {
                        var segmentId = node.GetSegment(s);

                        if (segmentId == 0)
							continue;
						CustomRoadAI.GetSegmentGeometry(segmentId).Recalculate(true, true);
						TrafficLightsManual.AddLiveSegmentLight(nodeId, segmentId);
                    }
                }
            }
        }

		public void setupTimedTrafficLight() {
			TimedTrafficLights = true;
			TrafficLightsTimed.AddTimedLight(nodeId, TrafficLightTool.SelectedNodeIndexes, Options.mayEnterBlockedJunctions);

			var node = getNode();

			for (int s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);

				if (segmentId == 0)
					continue;
				CustomRoadAI.GetSegmentGeometry(segmentId).Recalculate(true, true);
				TrafficLightsManual.AddLiveSegmentLight(nodeId, segmentId);
			}
		}

		public void destroyTimedTrafficLight() {
			TimedTrafficLights = false;
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
					TrafficLightsManual.RemoveSegmentLight(nodeId, segment);
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
				if (timedNode != null) {
					timedNode.SimulationStep();
				} else {
					RemoveNodeFromSimulation(nodeId, false);
					return;
				}
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
		public void Destroy(bool destroyGroup) {
			var node = getNode();
			var timedNode = TrafficLightsTimed.GetTimedLight(nodeId);

			if (timedNode != null) {
				foreach (var timedNodeId in timedNode.NodeGroup) {
					var otherTimedNode = TrafficLightsTimed.GetTimedLight(timedNodeId);
					var nodeSim = TrafficLightSimulation.GetNodeSimulation(timedNodeId); // `this` is one of `nodeSim`
					if (nodeSim == null) {
						if (otherTimedNode != null) {
							Log._Debug($"Removing loose timed light @ node {timedNodeId}");
							TrafficLightsTimed.RemoveTimedLight(timedNodeId);
						}
						continue;
					}

					if (otherTimedNode == null || destroyGroup || timedNodeId == nodeId) {
						Log._Debug($"Removing simulation @ node {timedNodeId}");
						nodeSim.destroyTimedTrafficLight();
						LightSimulationByNodeId.Remove(timedNodeId);
					} else {
						otherTimedNode.RemoveNodeFromGroup(nodeId);
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			FlagManualTrafficLights = false;
			LightSimulationByNodeId.Remove(nodeId);
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public static void AddNodeToSimulation(ushort nodeId) {
			if (LightSimulationByNodeId.ContainsKey(nodeId)) {
				return;
			}
			LightSimulationByNodeId.Add(nodeId, new TrafficLightSimulation(nodeId));
		}

		public static void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup) {
			if (!LightSimulationByNodeId.ContainsKey(nodeId))
				return;
			TrafficLightSimulation.LightSimulationByNodeId[nodeId].Destroy(destroyGroup);
		}

		public static TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
			if (LightSimulationByNodeId.ContainsKey(nodeId)) {
				return LightSimulationByNodeId[nodeId];
			}

			return null;
		}
	}
}

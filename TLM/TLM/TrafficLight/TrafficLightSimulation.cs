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

		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public TimedTrafficLights TimedLight {
			get; private set;
		} = null;

		public readonly ushort nodeId;

		private bool manualTrafficLights = false;

		public void SetupManualTrafficLight() {
			if (IsTimedLight())
				return;
			manualTrafficLights = true;

			setupLiveSegments();
		}

		public void DestroyManualTrafficLight() {
			if (IsTimedLight())
				return;
			manualTrafficLights = false;

			destroyLiveSegments();
		}

		public void SetupTimedTrafficLight(List<ushort> nodeGroup) {
			if (IsManualLight())
				DestroyManualTrafficLight();

			TimedLight = new TimedTrafficLights(nodeId, nodeGroup);

			setupLiveSegments();
		}

		public void DestroyTimedTrafficLight() {
			if (TimedLight != null)
				TimedLight.Stop();
			TimedLight = null;

			if (!IsManualLight())
				destroyLiveSegments();
		}

        public TrafficLightSimulation(ushort nodeId) {
            this.nodeId = nodeId;
		}

		public bool IsTimedLight() {
			return TimedLight != null;
		}

		public bool IsManualLight() {
			return manualTrafficLights;
		}

		public bool IsTimedLightActive() {
			return IsTimedLight() && TimedLight.IsStarted();
		}

		public bool IsSimulationActive() {
			return IsManualLight() || IsTimedLightActive();
		}

        public void SimulationStep() {
            //Log.Warning("step: " + NodeId);
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			if (IsTimedLightActive()) {
				TimedLight.SimulationStep();
            }

			// TODO check this
			/*for (var l = 0; l < 8; l++) {
                var segment = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(l);
                if (segment == 0) continue;
                if (!TrafficLight.CustomTrafficLights.IsSegmentLight(nodeId, segment)) continue;

                var segmentLights = TrafficLight.CustomTrafficLights.GetSegmentLights(nodeId, segment);

                segmentLights.LastChange = (currentFrameIndex >> 6) - segmentLights.LastChangeFrame;
            }*/
        }

		/// <summary>
		/// Stops & destroys the traffic light simulation(s) at this node (group)
		/// </summary>
		public void Destroy(bool destroyGroup) {
			if (TimedLight != null) {
				List<ushort> oldNodeGroup = new List<ushort>(TimedLight.NodeGroup);
				foreach (var timedNodeId in oldNodeGroup) {
					var otherNodeSim = GetNodeSimulation(timedNodeId);
					if (otherNodeSim == null) {
						continue;
					}

					if (destroyGroup || timedNodeId == nodeId) {
						Log._Debug($"Removing simulation @ node {timedNodeId}");
						otherNodeSim.DestroyTimedTrafficLight();
						LightSimulationByNodeId.Remove(timedNodeId);
					} else {
						if (!otherNodeSim.IsTimedLight()) {
							Log.Warning($"Unable to destroy timed traffic light of group. Node {timedNodeId} is not a timed traffic light.");
						} else {
							otherNodeSim.TimedLight.RemoveNodeFromGroup(nodeId);
						}
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			DestroyManualTrafficLight();
			LightSimulationByNodeId.Remove(nodeId);
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public static TrafficLightSimulation AddNodeToSimulation(ushort nodeId) {
			if (LightSimulationByNodeId.ContainsKey(nodeId)) {
				return LightSimulationByNodeId[nodeId];
			}
			LightSimulationByNodeId.Add(nodeId, new TrafficLightSimulation(nodeId));
			return LightSimulationByNodeId[nodeId];
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

		internal static void OnLevelUnloading() {
			LightSimulationByNodeId.Clear();
		}

		internal void handleNewSegments() {
			if (IsTimedLight())
				TimedLight.handleNewSegments();
		}

		internal void housekeeping(bool mayDeleteSegmentLights) {
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

				if (segmentId == 0) continue;
				if (TrafficLight.CustomTrafficLights.IsSegmentLight(nodeId, segmentId)) {
					TrafficLight.CustomTrafficLights.GetSegmentLights(nodeId, segmentId).housekeeping(mayDeleteSegmentLights);
				}
			}

			if (IsTimedLight()) {
				TimedLight.housekeeping(mayDeleteSegmentLights);
			}
		}

		private void setupLiveSegments() {
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

				if (segmentId == 0)
					continue;
				CustomRoadAI.GetSegmentGeometry(segmentId)?.Recalculate(true, true);
				TrafficLight.CustomTrafficLights.AddLiveSegmentLights(nodeId, segmentId);
			}
		}

		private void destroyLiveSegments() {
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].GetSegment(s);

				if (segmentId == 0) continue;
				if (TrafficLight.CustomTrafficLights.IsSegmentLight(nodeId, segmentId)) {
					TrafficLight.CustomTrafficLights.RemoveSegmentLight(nodeId, segmentId);
				}
			}
		}
	}
}

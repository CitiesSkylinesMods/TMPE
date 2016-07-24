using System;
using ColossalFramework;
using TrafficManager.Traffic;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using System.Linq;
using TrafficManager.Util;

namespace TrafficManager.TrafficLight {
	public class TrafficLightSimulation : IObserver<NodeGeometry> {
		/// <summary>
		/// For each node id: traffic light simulation assigned to the node
		/// </summary>
		//public static TrafficLightSimulation[] TrafficLightSimulations = new TrafficLightSimulation[NetManager.MAX_NODE_COUNT];
		public static Dictionary<ushort, TrafficLightSimulation> TrafficLightSimulations = new Dictionary<ushort, TrafficLightSimulation>();

		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public TimedTrafficLights TimedLight {
			get; private set;
		} = null;

		public ushort NodeId {
			get; private set;
		}

		private bool manualTrafficLights = false;

		private IDisposable nodeGeoUnsubscriber = null;

		public TrafficLightSimulation(ushort nodeId) {
			Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
			Flags.setNodeTrafficLight(nodeId, true);
			this.NodeId = nodeId;
			nodeGeoUnsubscriber = NodeGeometry.Get(nodeId).Subscribe(this);
		}

		~TrafficLightSimulation() {
			nodeGeoUnsubscriber?.Dispose();
		}

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

			TimedLight = new TimedTrafficLights(NodeId, nodeGroup);

			setupLiveSegments();
		}

		public void DestroyTimedTrafficLight() {
			var timedLight = TimedLight;
			TimedLight = null;

			if (timedLight != null) {
				timedLight.Destroy();
			}

			/*if (!IsManualLight() && timedLight != null)
				timedLight.Destroy();*/
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

		public static void SimulationStep() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficLightSimulation.SimulationStep");
#endif
			try {
				foreach (KeyValuePair<ushort, TrafficLightSimulation> e in TrafficLightSimulations) {
					try {
						var nodeSim = e.Value;
						var nodeId = e.Key;
						if (nodeSim.IsTimedLightActive()) {
							Flags.applyNodeTrafficLightFlag(nodeId);
							nodeSim.TimedLight.SimulationStep();
						}
					} catch (Exception ex) {
						Log.Warning($"Error occured while simulating traffic light @ node {e.Key}: {ex.ToString()}");
					}
				}
			} catch (Exception ex) {
				// TODO the dictionary was modified (probably a segment connected to a traffic light was changed/removed). rework this
				Log.Warning($"Error occured while iterating over traffic light simulations: {ex.ToString()}");
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficLightSimulation.SimulationStep");
#endif
		}

		/// <summary>
		/// Adds a traffic light simulation to the node with the given id
		/// </summary>
		/// <param name="nodeId"></param>
		public static TrafficLightSimulation AddNodeToSimulation(ushort nodeId) {
			if (TrafficLightSimulations.ContainsKey(nodeId)) {
				return TrafficLightSimulations[nodeId];
			}
			TrafficLightSimulations.Add(nodeId, new TrafficLightSimulation(nodeId));
			return TrafficLightSimulations[nodeId];
		}

		/// <summary>
		/// Destroys the traffic light and removes it
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="destroyGroup"></param>
		public static void RemoveNodeFromSimulation(ushort nodeId, bool destroyGroup, bool removeTrafficLight) {
			if (!TrafficLightSimulations.ContainsKey(nodeId))
				return;

			TrafficLightSimulation sim = TrafficLightSimulation.TrafficLightSimulations[nodeId];

			if (sim.TimedLight != null) {
				// remove/destroy other timed traffic lights in group
				List<ushort> oldNodeGroup = new List<ushort>(sim.TimedLight.NodeGroup);
				foreach (var timedNodeId in oldNodeGroup) {
					var otherNodeSim = GetNodeSimulation(timedNodeId);
					if (otherNodeSim == null) {
						continue;
					}

					if (destroyGroup || timedNodeId == nodeId) {
						//Log._Debug($"Slave: Removing simulation @ node {timedNodeId}");
						otherNodeSim.DestroyTimedTrafficLight();
						otherNodeSim.DestroyManualTrafficLight();
						otherNodeSim.nodeGeoUnsubscriber.Dispose();
						TrafficLightSimulations.Remove(timedNodeId);
						if (removeTrafficLight)
							Flags.setNodeTrafficLight(timedNodeId, false);
					} else {
						otherNodeSim.TimedLight.RemoveNodeFromGroup(nodeId);
					}
				}
			}

			//Flags.setNodeTrafficLight(nodeId, false);
			sim.DestroyTimedTrafficLight();
			sim.DestroyManualTrafficLight();
			sim.nodeGeoUnsubscriber?.Dispose();
			TrafficLightSimulations.Remove(nodeId);
			if (removeTrafficLight)
				Flags.setNodeTrafficLight(nodeId, false);
		}

		public static TrafficLightSimulation GetNodeSimulation(ushort nodeId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficLightSimulation.GetNodeSimulation");
#endif

			TrafficLightSimulation ret = null;
			if (TrafficLightSimulations.ContainsKey(nodeId)) {
				ret = TrafficLightSimulations[nodeId];
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficLightSimulation.GetNodeSimulation");
#endif
			return ret;
		}

		internal static void OnLevelUnloading() {
			TrafficLightSimulations.Clear();
			/*for (ushort nodeId = 0; nodeId < NetManager.MAX_NODE_COUNT; ++nodeId) {
				TrafficLightSimulations[nodeId] = null;
			}*/
		}

		public void OnUpdate(NodeGeometry nodeGeometry) {
#if DEBUG
			Log._Debug($"TrafficLightSimulation: OnUpdate @ node {NodeId} ({nodeGeometry.NodeId})");
#endif

			if (!Flags.mayHaveTrafficLight(NodeId)) {
				Log.Warning($"Housekeeping: Node {NodeId} has traffic light simulation but must not have a traffic light!");
				TrafficLightSimulation.RemoveNodeFromSimulation(NodeId, false, true);
			}

			if (!IsManualLight() && !IsTimedLight())
				return;

			if (!nodeGeometry.IsValid()) {
				// node has become invalid. Remove manual/timed traffic light and destroy custom lights
				RemoveNodeFromSimulation(NodeId, false, false);
				return;
			}

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0) continue;

#if DEBUG
				Log._Debug($"TrafficLightSimulation: OnUpdate @ node {NodeId}: Adding live traffic lights to segment {segmentId}");
#endif

				// add custom lights
				if (!TrafficLight.CustomTrafficLights.IsSegmentLight(NodeId, segmentId)) {
					TrafficLight.CustomTrafficLights.AddSegmentLights(NodeId, segmentId);
				}

				// housekeep timed light
				TrafficLight.CustomTrafficLights.GetSegmentLights(NodeId, segmentId).housekeeping(true);
			}

			// ensure there is a physical traffic light
			Flags.setNodeTrafficLight(NodeId, true);

			TimedLight?.handleNewSegments();
			TimedLight?.housekeeping();
		}

		internal void housekeeping() {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("TrafficLightSimulation.housekeeping");
#endif
			TimedLight?.StepHousekeeping(); // removes unused step lights
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("TrafficLightSimulation.housekeeping");
#endif
		}

		private void setupLiveSegments() {
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0)
					continue;
				//SegmentGeometry.Get(segmentId)?.Recalculate(true, true);
				if (!TrafficLight.CustomTrafficLights.IsSegmentLight(NodeId, segmentId)) {
					TrafficLight.CustomTrafficLights.AddSegmentLights(NodeId, segmentId);
				}
			}
		}

		private void destroyLiveSegments() {
			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0) continue;
				if (TrafficLight.CustomTrafficLights.IsSegmentLight(NodeId, segmentId)) {
					TrafficLight.CustomTrafficLights.RemoveSegmentLight(NodeId, segmentId);
				}
			}
		}
	}
}

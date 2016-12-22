using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.Manager;

namespace TrafficManager.TrafficLight {
	public class TrafficLightSimulation : IObserver<NodeGeometry> {
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

		internal IDisposable NodeGeoUnsubscriber {
			get; private set;
		} = null;

		public TrafficLightSimulation(ushort nodeId) {
			Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
			Flags.setNodeTrafficLight(nodeId, true);
			this.NodeId = nodeId;
			NodeGeoUnsubscriber = NodeGeometry.Get(nodeId).Subscribe(this);
		}

		~TrafficLightSimulation() {
			NodeGeoUnsubscriber?.Dispose();
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

		public void OnUpdate(NodeGeometry nodeGeometry) {
#if DEBUG
			Log._Debug($"TrafficLightSimulation: OnUpdate @ node {NodeId} ({nodeGeometry.NodeId})");
#endif

			if (!IsManualLight() && !IsTimedLight())
				return;

			if (!nodeGeometry.IsValid()) {
				// node has become invalid. Remove manual/timed traffic light and destroy custom lights
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, false, false);
				return;
			}

			if (!Flags.mayHaveTrafficLight(NodeId)) {
				Log.Warning($"Housekeeping: Node {NodeId} has traffic light simulation but must not have a traffic light!");
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, false, true);
				return;
			}

			CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0) continue;

#if DEBUG
				Log._Debug($"TrafficLightSimulation: OnUpdate @ node {NodeId}: Adding live traffic lights to segment {segmentId}");
#endif

				// add custom lights
				if (!customTrafficLightsManager.IsSegmentLight(NodeId, segmentId)) {
					customTrafficLightsManager.AddSegmentLights(NodeId, segmentId);
				}

				// housekeep timed light
				customTrafficLightsManager.GetSegmentLights(NodeId, segmentId).housekeeping(true);
			}

			// ensure there is a physical traffic light
			Flags.setNodeTrafficLight(NodeId, true);

			TimedLight?.handleNewSegments();
			TimedLight?.housekeeping();
		}

		internal void housekeeping() {
			TimedLight?.housekeeping(); // removes unused step lights
		}

		private void setupLiveSegments() {
			CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0)
					continue;
				//SegmentGeometry.Get(segmentId)?.Recalculate(true, true);
				if (!customTrafficLightsManager.IsSegmentLight(NodeId, segmentId)) {
					customTrafficLightsManager.AddSegmentLights(NodeId, segmentId);
				}
			}
		}

		private void destroyLiveSegments() {
			CustomTrafficLightsManager customTrafficLightsManager = CustomTrafficLightsManager.Instance;

			for (var s = 0; s < 8; s++) {
				var segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);

				if (segmentId == 0) continue;
				if (customTrafficLightsManager.IsSegmentLight(NodeId, segmentId)) {
					customTrafficLightsManager.RemoveSegmentLight(NodeId, segmentId);
				}
			}
		}
	}
}

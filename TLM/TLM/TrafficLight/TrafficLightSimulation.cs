using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.Manager;
using CSUtil.Commons;

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

		public override string ToString() {
			return $"[TrafficLightSimulation\n" +
				"\t" + $"NodeId = {NodeId}\n" +
				"\t" + $"manualTrafficLights = {manualTrafficLights}\n" +
				"\t" + $"TimedLight = {TimedLight}\n" +
				"TrafficLightSimulation]";
		}

		public TrafficLightSimulation(ushort nodeId) {
			Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
			this.NodeId = nodeId;
			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
				TrafficLightManager.Instance.AddTrafficLight(NodeId, ref node);
				return true;
			});
			NodeGeoUnsubscriber = NodeGeometry.Get(nodeId).Subscribe(this);
		}

		~TrafficLightSimulation() {
			NodeGeoUnsubscriber?.Dispose();
		}

		public void SetupManualTrafficLight() {
			if (IsTimedLight())
				return;
			manualTrafficLights = true;

			CustomSegmentLightsManager.Instance.AddNodeLights(NodeId);
		}

		internal void DestroyManualTrafficLight() {
			if (IsTimedLight())
				return;
			if (!IsManualLight())
				return;
			manualTrafficLights = false;

			CustomSegmentLightsManager.Instance.RemoveNodeLights(NodeId);
		}

		public void SetupTimedTrafficLight(List<ushort> nodeGroup) {
			if (IsManualLight())
				DestroyManualTrafficLight();

			TimedLight = new TimedTrafficLights(NodeId, nodeGroup);
		}

		internal void DestroyTimedTrafficLight() {
			if (!IsTimedLight())
				return;
			var timedLight = TimedLight;
			TimedLight = null;

			if (timedLight != null) {
				timedLight.Destroy();
			}

			/*if (!IsManualLight() && timedLight != null)
				timedLight.Destroy();*/
		}

		public void Destroy() {
			DestroyTimedTrafficLight();
			DestroyManualTrafficLight();
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
				Log._Debug($"Housekeeping: Node {NodeId} has traffic light simulation but must not have a traffic light!");
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, false, true);
				return;
			}

			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;

#if DEBUG
				Log._Debug($"TrafficLightSimulation: OnUpdate @ node {NodeId}: Adding live traffic lights to segment {end.SegmentId}");
#endif

				// add custom lights
				/*if (!customTrafficLightsManager.IsSegmentLight(end.SegmentId, end.StartNode)) {
					customTrafficLightsManager.AddSegmentLights(end.SegmentId, end.StartNode);
				}*/

				// housekeep timed light
				customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode).housekeeping(true, true);
			}

			// ensure there is a physical traffic light
			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nodeId, ref NetNode node) {
				TrafficLightManager.Instance.AddTrafficLight(NodeId, ref node);
				return true;
			});

			TimedLight?.OnGeometryUpdate();
			TimedLight?.housekeeping();
		}

		internal void housekeeping() {
			TimedLight?.housekeeping(); // removes unused step lights
		}
	}
}

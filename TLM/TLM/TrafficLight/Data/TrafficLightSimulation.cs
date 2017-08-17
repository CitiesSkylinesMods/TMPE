using System;
using ColossalFramework;
using TrafficManager.Geometry;
using System.Collections.Generic;
using TrafficManager.State;
using TrafficManager.Custom.AI;
using TrafficManager.Util;
using TrafficManager.Manager;
using CSUtil.Commons;
using TrafficManager.Geometry.Impl;
using TrafficManager.TrafficLight.Impl;

namespace TrafficManager.TrafficLight.Data {
	public struct TrafficLightSimulation {
		/// <summary>
		/// Timed traffic light by node id
		/// </summary>
		public ITimedTrafficLights TimedLight {
			get; private set;
		}

		public ushort NodeId {
			get; private set;
		}

		public TrafficLightSimulationType Type {
			get; private set;
		}

		public override string ToString() {
			return $"[TrafficLightSimulation\n" +
				"\t" + $"NodeId = {NodeId}\n" +
				"\t" + $"Type = {Type}\n" +
				"\t" + $"TimedLight = {TimedLight}\n" +
				"TrafficLightSimulation]";
		}

		public TrafficLightSimulation(ushort nodeId) {
			//Log._Debug($"TrafficLightSimulation: Constructor called @ node {nodeId}");
			this.NodeId = nodeId;
			TimedLight = null;
			Type = TrafficLightSimulationType.None;
		}

		public bool SetUpManualTrafficLight() {
			if (IsTimedLight()) {
				return false;
			}

			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
				Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(nId, ref node);
				return true;
			});

			Constants.ManagerFactory.CustomSegmentLightsManager.AddNodeLights(NodeId);
			Type = TrafficLightSimulationType.Manual;
			return true;
		}

		public bool DestroyManualTrafficLight() {
			if (IsTimedLight()) {
				return false;
			}
			if (! IsManualLight()) {
				return false;
			}

			Type = TrafficLightSimulationType.None;
			Constants.ManagerFactory.CustomSegmentLightsManager.RemoveNodeLights(NodeId);
			return true;
		}

		public bool SetUpTimedTrafficLight(IList<ushort> nodeGroup) {
			if (IsManualLight()) {
				DestroyManualTrafficLight();
			}

			if (IsTimedLight()) {
				return false;
			}

			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
				Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(nId, ref node);
				return true;
			});

			Constants.ManagerFactory.CustomSegmentLightsManager.AddNodeLights(NodeId);
			TimedLight = new TimedTrafficLights(NodeId, nodeGroup);
			Type = TrafficLightSimulationType.Timed;
			return true;
		}

		public bool DestroyTimedTrafficLight() {
			if (! IsTimedLight()) {
				return false;
			}

			Type = TrafficLightSimulationType.None;
			var timedLight = TimedLight;
			TimedLight = null;

			if (timedLight != null) {
				timedLight.Destroy();
			}
			return true;
		}

		public void Destroy() {
			DestroyTimedTrafficLight();
			DestroyManualTrafficLight();
		}

		public bool IsTimedLight() {
			return Type == TrafficLightSimulationType.Timed && TimedLight != null;
		}

		public bool IsManualLight() {
			return Type == TrafficLightSimulationType.Manual;
		}

		public bool IsTimedLightRunning() {
			return IsTimedLight() && TimedLight.IsStarted();
		}

		public bool IsSimulationRunning() {
			return IsManualLight() || IsTimedLightRunning();
		}

		public bool HasSimulation() {
			return IsManualLight() || IsTimedLight();
		}

		public void SimulationStep() {
			if (! HasSimulation()) {
				return;
			}

			if (IsTimedLightRunning()) {
				TimedLight.SimulationStep();
			}
		}

		public void Update() {
			Log._Debug($"TrafficLightSimulation.Update(): called for node {NodeId}");

			if (IsTimedLight()) {
				TimedLight.OnGeometryUpdate();
				TimedLight.Housekeeping();
			}
		}

		public void Housekeeping() { // TODO improve & remove
			TimedLight?.Housekeeping(); // removes unused step lights
		}
	}
}

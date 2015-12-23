using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.TrafficLight;

namespace TrafficManager.Traffic {
	public class TimedTrafficSteps : ICloneable {
		public ushort NodeId;
		/// <summary>
		/// The number of time units this traffic light steps remains in the current state
		/// </summary>
		public int timeUnits;
		public uint Frame;

		public Dictionary<int, ManualSegmentLight> segmentLightStates = new Dictionary<int, ManualSegmentLight>();
		/// <summary>
		/// list of segment ids connected to the node
		/// </summary>
		public List<int> segmentIds = new List<int>();

		public TimedTrafficSteps(int timeUnits, ushort nodeId) {
			NodeId = nodeId;
			this.timeUnits = timeUnits;

			var node = TrafficLightTool.GetNetNode(nodeId);

			for (var s = 0; s < 8; s++) {
				var segmentId = node.GetSegment(s);

				if (segmentId != 0) {
					segmentIds.Add(segmentId);
					var segmentLight = TrafficLightsManual.GetSegmentLight(nodeId, segmentId);
					if (segmentLight != null)
						segmentLightStates[segmentId] = (ManualSegmentLight)segmentLight.Clone();
				}
			}
		}

		public RoadBaseAI.TrafficLightState GetLight(int segment, int lightType) {
			ManualSegmentLight segLight = segmentLightStates[segment];
			if (segLight != null) {
				switch (lightType) {
					case 0:
						return segLight.LightMain;
					case 1:
						return segLight.LightLeft;
					case 2:
						return segLight.LightRight;
					case 3:
						return segLight.LightPedestrian;
				}
			}

			return RoadBaseAI.TrafficLightState.Green;
		}

		public void SetFrame(uint frame) {
			Frame = frame;
		}

		/// <summary>
		/// Updates "real-world" traffic light states according to the timed scripts
		/// </summary>
		public void SetLights() {
			foreach (KeyValuePair<int, ManualSegmentLight> e in segmentLightStates) {
				var segmentId = e.Key;
				var segLightState = e.Value;
				//if (segment == 0) continue;

				var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segmentId);
				if (segmentLight == null)
					continue;

				segmentLight.LightMain = segLightState.LightMain;
				segmentLight.LightLeft = segLightState.LightLeft;
				segmentLight.LightRight = segLightState.LightRight;
				segmentLight.LightPedestrian = segLightState.LightPedestrian;
				segmentLight.UpdateVisuals();
			}
		}

		/// <summary>
		/// Updates timed segment lights according to "real-world" traffic light states
		/// </summary>
		public void UpdateLights() {
			foreach (KeyValuePair<int, ManualSegmentLight> e in segmentLightStates) {
				var segmentId = e.Key;
				var segLightState = e.Value;
				
				//if (segment == 0) continue;
				var segmentLight = TrafficLightsManual.GetSegmentLight(NodeId, segmentId);
				if (segmentLight == null)
					continue;

				segLightState.LightMain = segmentLight.LightMain;
				segLightState.LightLeft = segmentLight.LightLeft;
				segLightState.LightRight = segmentLight.LightRight;
				segLightState.LightPedestrian = segmentLight.LightPedestrian;
			}
		}

		public long CurrentStep() {
			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			return Frame + timeUnits - (currentFrameIndex >> 6);
		}

		public bool StepDone(uint frame) {
			if (Frame + timeUnits <= frame) {
				return true;
			}

			return false;
		}

		public object Clone() {
			return MemberwiseClone();
		}
	}
}

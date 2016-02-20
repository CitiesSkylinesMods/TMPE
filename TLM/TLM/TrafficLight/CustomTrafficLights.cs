using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class CustomTrafficLights {

		/// <summary>
		/// Manual light by segment id
		/// </summary>
		public static Dictionary<ushort, CustomSegment> ManualSegments = new Dictionary<ushort, CustomSegment>();		

		internal static void AddLiveSegmentLights(ushort nodeId, ushort segmentId) {
			if (IsSegmentLight(nodeId, segmentId))
				return;

			//Log.Message($"Adding live segment light: {segmentId} @ {nodeId}");

			var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

			RoadBaseAI.TrafficLightState vehicleLightState;
			RoadBaseAI.TrafficLightState pedestrianLightState;
			bool vehicles;
			bool pedestrians;

			RoadBaseAI.GetTrafficLightState(nodeId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
				currentFrameIndex - 256u, out vehicleLightState, out pedestrianLightState, out vehicles,
				out pedestrians);

			AddSegmentLights(nodeId, segmentId,
				vehicleLightState == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Green
					: RoadBaseAI.TrafficLightState.Red);
		}

		public static void AddSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState light) {
			//Log.Message($"Adding segment light: {segmentId} @ {nodeId}");

			if (ManualSegments.ContainsKey(segmentId)) {
				ManualSegments[segmentId].Node2Lights = new CustomSegmentLights(nodeId, segmentId, light);
				ManualSegments[segmentId].Node2 = nodeId;
			} else {
				ManualSegments.Add(segmentId, new CustomSegment());
				ManualSegments[segmentId].Node1Lights = new CustomSegmentLights(nodeId, segmentId, light);
				ManualSegments[segmentId].Node1 = nodeId;
			}
		}

		public static void RemoveSegmentLight(ushort segmentId) {
			//Log.Message($"Removing segment light: {segmentId}");
			ManualSegments.Remove(segmentId);
		}

		public static void RemoveSegmentLight(ushort nodeId, ushort segmentId) {
			//Log.Message($"Removing segment light: {segmentId} @ {nodeId}");
			if (ManualSegments[segmentId].Node1 == nodeId) {
				ManualSegments[segmentId].Node1 = 0;
				ManualSegments[segmentId].Node1Lights = null;
			} else if (ManualSegments[segmentId].Node2 == nodeId) {
				ManualSegments[segmentId].Node2 = 0;
				ManualSegments[segmentId].Node2Lights = null;
			}

			if (ManualSegments[segmentId].Node1 == 0 && ManualSegments[segmentId].Node2 == 0) {
				ManualSegments.Remove(segmentId);
			}
		}

		public static bool IsSegmentLight(ushort nodeId, ushort segmentId) {
			if (ManualSegments.ContainsKey(segmentId)) {
				var manualSegment = ManualSegments[segmentId];

				if (manualSegment.Node1 == nodeId || manualSegment.Node2 == nodeId) {
					return true;
				}
			}

			return false;
		}

		public static CustomSegmentLights GetOrLiveSegmentLights(ushort nodeId, ushort segmentId) {
			if (! IsSegmentLight(nodeId, segmentId))
				AddLiveSegmentLights(nodeId, segmentId);

			return GetSegmentLights(nodeId, segmentId);
		}

		public static CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
			//Log.Message($"Get segment light: {segmentId} @ {nodeId}");
			if (ManualSegments.ContainsKey(segmentId)) {
				var manualSegment = ManualSegments[segmentId];

				if (manualSegment.Node1 == nodeId) {
					return manualSegment.Node1Lights;
				}
				if (manualSegment.Node2 == nodeId) {
					return manualSegment.Node2Lights;
				}
			}

			return null;
		}

		internal static void OnLevelUnloading() {
			ManualSegments.Clear();
		}
	}
}

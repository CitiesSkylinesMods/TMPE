using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class ManualTrafficLights {

		/// <summary>
		/// Manual light by segment id
		/// </summary>
		public static Dictionary<ushort, ManualSegment> ManualSegments = new Dictionary<ushort, ManualSegment>();		

		internal static void AddLiveSegmentLight(ushort nodeId, ushort segmentId) {
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

			AddSegmentLight(nodeId, segmentId,
				vehicleLightState == RoadBaseAI.TrafficLightState.Green
					? RoadBaseAI.TrafficLightState.Green
					: RoadBaseAI.TrafficLightState.Red);
		}

		public static void AddSegmentLight(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState light) {
			//Log.Message($"Adding segment light: {segmentId} @ {nodeId}");

			if (ManualSegments.ContainsKey(segmentId)) {
				ManualSegments[segmentId].Instance2 = new ManualSegmentLight(nodeId, segmentId, light);
				ManualSegments[segmentId].Node2 = nodeId;
			} else {
				ManualSegments.Add(segmentId, new ManualSegment());
				ManualSegments[segmentId].Instance1 = new ManualSegmentLight(nodeId, segmentId, light);
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
				ManualSegments[segmentId].Instance1 = null;
			} else {
				ManualSegments[segmentId].Node2 = 0;
				ManualSegments[segmentId].Instance2 = null;
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

		public static ManualSegmentLight GetOrLiveSegmentLight(ushort nodeId, ushort segmentId) {
			if (! IsSegmentLight(nodeId, segmentId))
				AddLiveSegmentLight(nodeId, segmentId);

			return GetSegmentLight(nodeId, segmentId);
		}

		public static ManualSegmentLight GetSegmentLight(ushort nodeId, ushort segmentId) {
			//Log.Message($"Get segment light: {segmentId} @ {nodeId}");
			if (ManualSegments.ContainsKey(segmentId)) {
				var manualSegment = ManualSegments[segmentId];

				if (manualSegment.Node1 == nodeId) {
					return manualSegment.Instance1;
				}
				if (manualSegment.Node2 == nodeId) {
					return manualSegment.Instance2;
				}
			}

			return null;
		}

		internal static void OnLevelUnloading() {
			ManualSegments.Clear();
		}
	}
}

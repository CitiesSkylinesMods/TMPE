using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class CustomTrafficLights {

		/// <summary>
		/// Manual light by segment id
		/// </summary>
		private static Dictionary<ushort, CustomSegment> CustomSegments = new Dictionary<ushort, CustomSegment>();		

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

		public static void AddSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState light=RoadBaseAI.TrafficLightState.Red) {
#if DEBUG
			Log._Debug($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ {nodeId}");
#endif
			Cleanup(segmentId);

			if (CustomSegments.ContainsKey(segmentId)) {
				if (CustomSegments[segmentId].Node1 == nodeId || CustomSegments[segmentId].Node2 == nodeId)
					return;

#if DEBUG
				Log._Debug($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ {nodeId} -- Node1={CustomSegments[segmentId].Node1} Node2={CustomSegments[segmentId].Node2}");
#endif

				if (CustomSegments[segmentId].Node1 == 0) {
					CustomSegments[segmentId].Node1Lights = new CustomSegmentLights(nodeId, segmentId, light);
					CustomSegments[segmentId].Node1 = nodeId;
				} else {
					CustomSegments[segmentId].Node2Lights = new CustomSegmentLights(nodeId, segmentId, light);
					CustomSegments[segmentId].Node2 = nodeId;
				}
			} else {
				CustomSegments.Add(segmentId, new CustomSegment());
				CustomSegments[segmentId].Node1Lights = new CustomSegmentLights(nodeId, segmentId, light);
				CustomSegments[segmentId].Node1 = nodeId;
			}
		}

		public static void RemoveSegmentLights(ushort segmentId) {
#if DEBUG
			Log.Warning($"Removing all segment lights from segment {segmentId}");
#endif
			CustomSegments.Remove(segmentId);
		}

		public static void RemoveSegmentLight(ushort nodeId, ushort segmentId) {
#if DEBUG
			Log.Warning($"Removing segment light: {segmentId} @ {nodeId}");
#endif

			if (CustomSegments[segmentId].Node1 == nodeId) {
				CustomSegments[segmentId].Node1 = 0;
				CustomSegments[segmentId].Node1Lights = null;
			} else if (CustomSegments[segmentId].Node2 == nodeId) {
				CustomSegments[segmentId].Node2 = 0;
				CustomSegments[segmentId].Node2Lights = null;
			}

			Cleanup(segmentId);
		}

		private static void Cleanup(ushort segmentId) {
			if (!CustomSegments.ContainsKey(segmentId))
				return;

			NetManager netManager = Singleton<NetManager>.instance;
			if (CustomSegments[segmentId].Node1 != 0 && (netManager.m_nodes.m_buffer[CustomSegments[segmentId].Node1].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
				CustomSegments[segmentId].Node1 = 0;
				CustomSegments[segmentId].Node1Lights = null;
			}

			if (CustomSegments[segmentId].Node2 != 0 && (netManager.m_nodes.m_buffer[CustomSegments[segmentId].Node2].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
				CustomSegments[segmentId].Node2 = 0;
				CustomSegments[segmentId].Node2Lights = null;
			}

			if (CustomSegments[segmentId].Node1 == 0 && CustomSegments[segmentId].Node2 == 0)
				CustomSegments.Remove(segmentId);
		}

		public static bool IsSegmentLight(ushort nodeId, ushort segmentId) {
			if (CustomSegments.ContainsKey(segmentId)) {
				var manualSegment = CustomSegments[segmentId];

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
			if (CustomSegments.ContainsKey(segmentId)) {
				var manualSegment = CustomSegments[segmentId];

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
			CustomSegments.Clear();
		}
	}
}

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class CustomTrafficLights {

		/// <summary>
		/// Manual light by segment id
		/// </summary>
		private static CustomSegment[] CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
		
		internal static void AddLiveSegmentLights(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.AddLiveSegmentLights");
#endif
			if (IsSegmentLight(nodeId, segmentId)) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddLiveSegmentLights");
#endif
				return;
			}

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
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddLiveSegmentLights");
#endif
		}

		public static void AddSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState light=RoadBaseAI.TrafficLightState.Red) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.AddSegmentLights");
#endif
#if DEBUG
			Log._Debug($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ {nodeId}");
#endif
			Cleanup(segmentId);

			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment != null) {
				if (customSegment.Node1 == nodeId || customSegment.Node2 == nodeId) {
#if TRACE
					Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddSegmentLights");
#endif
					return;
				}

#if DEBUG
				Log._Debug($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ {nodeId} -- Node1={CustomSegments[segmentId].Node1} Node2={CustomSegments[segmentId].Node2}");
#endif

				if (customSegment.Node1 == 0) {
					customSegment.Node1Lights = new CustomSegmentLights(nodeId, segmentId, light);
					customSegment.Node1 = nodeId;
				} else {
					customSegment.Node2Lights = new CustomSegmentLights(nodeId, segmentId, light);
					customSegment.Node2 = nodeId;
				}
			} else {
				customSegment = new CustomSegment();
				customSegment.Node1Lights = new CustomSegmentLights(nodeId, segmentId, light);
				customSegment.Node1 = nodeId;
				CustomSegments[segmentId] = customSegment;
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddSegmentLights");
#endif
		}

		public static void RemoveSegmentLights(ushort segmentId) {
#if DEBUG
			Log.Warning($"Removing all segment lights from segment {segmentId}");
#endif
			CustomSegments[segmentId] = null;
		}

		public static void RemoveSegmentLight(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.RemoveSegmentLight");
#endif
#if DEBUG
			Log.Warning($"Removing segment light: {segmentId} @ {nodeId}");
#endif

			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.RemoveSegmentLight");
#endif
				return;
			}

			if (customSegment.Node1 == nodeId) {
				customSegment.Node1 = 0;
				customSegment.Node1Lights = null;
			} else if (customSegment.Node2 == nodeId) {
				customSegment.Node2 = 0;
				customSegment.Node2Lights = null;
			}

			Cleanup(segmentId);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.RemoveSegmentLight");
#endif
		}

		private static void Cleanup(ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.Cleanup");
#endif
			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.Cleanup");
#endif
				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;
			if (customSegment.Node1 != 0 && (netManager.m_nodes.m_buffer[customSegment.Node1].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
				customSegment.Node1 = 0;
				customSegment.Node1Lights = null;
			}

			if (customSegment.Node2 != 0 && (netManager.m_nodes.m_buffer[customSegment.Node2].m_flags & (NetNode.Flags.Created | NetNode.Flags.Deleted)) != NetNode.Flags.Created) {
				customSegment.Node2 = 0;
				customSegment.Node2Lights = null;
			}

			if (customSegment.Node1 == 0 && customSegment.Node2 == 0) {
				CustomSegments[segmentId] = null;
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.Cleanup");
#endif
		}

		public static bool IsSegmentLight(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.IsSegmentLight");
#endif
			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.IsSegmentLight");
#endif
				return false;
			}

			if (customSegment.Node1 == nodeId || customSegment.Node2 == nodeId) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.IsSegmentLight");
#endif
				return true;
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.IsSegmentLight");
#endif
			return false;
		}

		public static CustomSegmentLights GetOrLiveSegmentLights(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.GetOrLiveSegmentLights");
#endif
			if (! IsSegmentLight(nodeId, segmentId))
				AddLiveSegmentLights(nodeId, segmentId);

			CustomSegmentLights ret = GetSegmentLights(nodeId, segmentId);
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.GetOrLiveSegmentLights");
#endif
			return ret;
		}

		public static CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.GetSegmentLights");
#endif
			CustomSegment customSegment = CustomSegments[segmentId];
			if (customSegment == null) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.GetSegmentLights");
#endif
				return null;
			}

			//Log.Message($"Get segment light: {segmentId} @ {nodeId}");

			if (customSegment.Node1 == nodeId) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.GetSegmentLights");
#endif
				return customSegment.Node1Lights;
			}
			if (customSegment.Node2 == nodeId) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.GetSegmentLights");
#endif
				return customSegment.Node2Lights;
			}

#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.GetSegmentLights");
#endif
			return null;
		}

		internal static void OnLevelUnloading() {
			CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
		}
	}
}

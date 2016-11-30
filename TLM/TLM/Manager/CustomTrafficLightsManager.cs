using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Geometry;
using TrafficManager.Util;
using TrafficManager.TrafficLight;

namespace TrafficManager.Manager {
	/// <summary>
	/// Manages the states of all custom traffic lights on the map
	/// </summary>
	public class CustomTrafficLightsManager : ICustomManager {
		public static CustomTrafficLightsManager Instance { get; private set; } = null;

		static CustomTrafficLightsManager() {
			Instance = new CustomTrafficLightsManager();
		}

		/// <summary>
		/// custom traffic lights by segment id
		/// </summary>
		private CustomSegment[] CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
		
		/// <summary>
		/// Adds custom traffic lights at the specified node and segment.
		/// Light states (red, yellow, green) are taken from the "live" state, that is the traffic light's light state right before the custom light takes control.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		internal void AddLiveSegmentLights(ushort nodeId, ushort segmentId) {
#if TRACE
			Singleton<CodeProfiler>.instance.Start("CustomTrafficLights.AddLiveSegmentLights");
#endif
			if (IsSegmentLight(nodeId, segmentId)) {
#if TRACE
				Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddLiveSegmentLights");
#endif
				return;
			}

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

		/// <summary>
		/// Adds custom traffic lights at the specified node and segment.
		/// Light stats are set to the given light state, or to "Red" if no light state is given.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		/// <param name="lightState">(optional) light state to set</param>
		public void AddSegmentLights(ushort nodeId, ushort segmentId, RoadBaseAI.TrafficLightState lightState=RoadBaseAI.TrafficLightState.Red) {
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
					customSegment.Node1Lights = new CustomSegmentLights(nodeId, segmentId, lightState);
					customSegment.Node1 = nodeId;
				} else {
					customSegment.Node2Lights = new CustomSegmentLights(nodeId, segmentId, lightState);
					customSegment.Node2 = nodeId;
				}
			} else {
				customSegment = new CustomSegment();
				customSegment.Node1Lights = new CustomSegmentLights(nodeId, segmentId, lightState);
				customSegment.Node1 = nodeId;
				CustomSegments[segmentId] = customSegment;
			}
#if TRACE
			Singleton<CodeProfiler>.instance.Stop("CustomTrafficLights.AddSegmentLights");
#endif
		}

		/// <summary>
		/// Removes all custom traffic lights at both ends of the given segment.
		/// </summary>
		/// <param name="segmentId"></param>
		public void RemoveSegmentLights(ushort segmentId) {
#if DEBUG
			Log.Warning($"Removing all segment lights from segment {segmentId}");
#endif
			CustomSegments[segmentId] = null;
		}

		/// <summary>
		/// Removes the custom traffic light at the given segment end.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		public void RemoveSegmentLight(ushort nodeId, ushort segmentId) {
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

		/// <summary>
		/// Performs a housekeeping operation for all custom traffic lights at the given segment.
		/// It is checked wheter the segment and connected nodes are still valid. If not, corresponding custom traffic lights are removed.
		/// </summary>
		/// <param name="segmentId"></param>
		private void Cleanup(ushort segmentId) {
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

		/// <summary>
		/// Checks if a custom traffic light is present at the given segment end.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		/// <returns></returns>
		public bool IsSegmentLight(ushort nodeId, ushort segmentId) {
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

		/// <summary>
		/// Retrieves the custom traffic light at the given segment end. If none exists, a new custom traffic light is created and returned.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		/// <returns>existing or new custom traffic light at segment end</returns>
		public CustomSegmentLights GetOrLiveSegmentLights(ushort nodeId, ushort segmentId) {
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

		/// <summary>
		/// Retrieves the custom traffic light at the given segment end.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="segmentId"></param>
		/// <returns>existing custom traffic light at segment end, <code>null</code> if none exists</returns>
		public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
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

		public void OnLevelUnloading() {
			CustomSegments = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
		}
	}
}

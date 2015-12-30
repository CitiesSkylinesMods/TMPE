using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class TrafficLightsManual {

		/// <summary>
		/// Manual light by segment id
		/// </summary>
		public static Dictionary<ushort, ManualSegment> ManualSegments = new Dictionary<ushort, ManualSegment>();

		// TODO refactor
		public static bool SegmentIsOutgoingOneWay(ushort segmentid, ushort nodeId) {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentid];
			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			var dir = NetInfo.Direction.Forward;
			if (segment.m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
			var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

			var isOneWay = true;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[num3].m_direction == dir3)) {
					isOneWay = false;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			return isOneWay;
		}

		// TODO refactor
		public static bool SegmentIsOneWay(ushort segmentid) {
			var instance = Singleton<NetManager>.instance;

			var segment = instance.m_segments.m_buffer[segmentid];
			var info = segment.Info;

			var num2 = segment.m_lanes;
			var num3 = 0;

			var hasForward = false;
			var hasBackward = false;

			while (num3 < info.m_lanes.Length && num2 != 0u) {
				if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[num3].m_direction == NetInfo.Direction.Forward)) {
					hasForward = true;
				}

				if (info.m_lanes[num3].m_laneType != NetInfo.LaneType.Pedestrian &&
					(info.m_lanes[num3].m_direction == NetInfo.Direction.Backward)) {
					hasBackward = true;
				}

				if (hasForward && hasBackward) {
					return false;
				}

				num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
				num3++;
			}

			return true;
		}

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

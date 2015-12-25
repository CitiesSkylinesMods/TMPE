using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight {
	class TrafficLightsManual {

		public static Dictionary<int, ManualSegment> ManualSegments =
			new Dictionary<int, ManualSegment>();

		public static bool SegmentIsIncomingOneWay(int segmentid, ushort nodeId) {
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

		public static bool SegmentIsOneWay(int segmentid) {
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

		public static void AddSegmentLight(ushort nodeId, int segmentId, RoadBaseAI.TrafficLightState light) {
			if (ManualSegments.ContainsKey(segmentId)) {
				ManualSegments[segmentId].Node2 = nodeId;
				ManualSegments[segmentId].Instance2 = new ManualSegmentLight(nodeId, segmentId, light);
			} else {
				ManualSegments.Add(segmentId, new ManualSegment());
				ManualSegments[segmentId].Node1 = nodeId;
				ManualSegments[segmentId].Instance1 = new ManualSegmentLight(nodeId, segmentId, light);
			}
		}

		public static void RemoveSegmentLight(ushort nodeId, int segmentId) {
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

		public static bool IsSegmentLight(ushort nodeId, int segmentId) {
			if (ManualSegments.ContainsKey(segmentId)) {
				var manualSegment = ManualSegments[segmentId];

				if (manualSegment.Node1 == nodeId || manualSegment.Node2 == nodeId) {
					return true;
				}
			}

			return false;
		}

		public static ManualSegmentLight GetSegmentLight(ushort nodeId, int segmentId) {
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

		public static void ClearSegment(ushort nodeId, int segmentId) {
			var manualSegment = ManualSegments[segmentId];

			if (manualSegment.Node1 == nodeId) {
				manualSegment.Node1 = 0;
				manualSegment.Instance1 = null;
			}

			if (manualSegment.Node2 == nodeId) {
				manualSegment.Node2 = 0;
				manualSegment.Instance2 = null;
			}

			if (manualSegment.Node1 == 0 && manualSegment.Node2 == 0) {
				ManualSegments.Remove(segmentId);
			}
		}
	}
}

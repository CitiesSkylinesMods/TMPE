using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Manager;

namespace TrafficManager.Util {
	// TODO Make separate classes for segments, nodes, lanes, etc. and create an abstraction layer for game interfacing utilities
	public static class NetUtil {
		public delegate void NetSegmentHandler(ushort segmentId, ref NetSegment segment);
		public delegate void NetNodeHandler(ushort nodeId, ref NetNode node);
		public delegate void NetLaneHandler(uint laneId, ref NetLane lane);
		public delegate void NetSegmentLaneHandler(uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segmentId, ref NetSegment segment, byte laneIndex);

		public static bool IsSegmentValid(ushort segmentId) {
			return CheckSegmentFlags(segmentId, NetSegment.Flags.Created | NetSegment.Flags.Deleted, NetSegment.Flags.Created);
		}

		public static void ProcessSegment(ushort segmentId, NetSegmentHandler handler) {
			ProcessSegment(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], handler);
		}

		public static void ProcessSegment(ushort segmentId, ref NetSegment segment, NetSegmentHandler handler) {
			handler(segmentId, ref segment);
		}

		public static bool IsNodeValid(ushort nodeId) {
			return CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted, NetNode.Flags.Created);
		}

		public static void ProcessNode(ushort nodeId, NetNodeHandler handler) {
			ProcessNode(nodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId], handler);
		}

		public static void ProcessNode(ushort nodeId, ref NetNode node, NetNodeHandler handler) {
			handler(nodeId, ref node);
		}

		[Obsolete]
		static bool IsLaneValid(ref NetLane lane) {
			if ((lane.m_flags & (uint)(NetLane.Flags.Created | NetLane.Flags.Deleted)) != (uint)NetLane.Flags.Created) {
				return false;
			}
			return IsSegmentValid(lane.m_segment);
		}
		
		public static bool IsLaneValid(uint laneId) {
			if (!CheckLaneFlags(laneId, NetLane.Flags.Created | NetLane.Flags.Deleted, NetLane.Flags.Created)) {
				return false;
			}

			bool ret = false;
			ProcessLane(laneId, delegate(uint lId, ref NetLane lane) {
				ret = IsSegmentValid(lane.m_segment);
			});
			return ret;
		}

		public static void ProcessLane(uint laneId, NetLaneHandler handler) {
			ProcessLane(laneId, ref Singleton<NetManager>.instance.m_lanes.m_buffer[laneId], handler);
		}

		public static void ProcessLane(uint laneId, ref NetLane lane, NetLaneHandler handler) {
			handler(laneId, ref lane);
		}

		public static void IterateSegmentLanes(ushort segmentId, NetSegmentLaneHandler handler) {
			IterateSegmentLanes(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], handler);
		}

		public static void IterateSegmentLanes(ushort segmentId, ref NetSegment segment, NetSegmentLaneHandler handler) {
			NetInfo segmentInfo = segment.Info;
			if (segmentInfo == null)
				return;

			byte laneIndex = 0;
			uint curLaneId = segment.m_lanes;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				handler(curLaneId, ref Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId], laneInfo, segmentId, ref segment, laneIndex);

				curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}
		}

		public static NetInfo.Direction GetSegmentEndDirection(ushort segmentId, bool startNode) {
			return GetSegmentEndDirection(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], startNode);
		}

		public static NetInfo.Direction GetSegmentEndDirection(ushort segmentId, ref NetSegment segment, bool startNode) {
			NetInfo segmentInfo = segment.Info;

			var dir = startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
			if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None ^ TrafficPriorityManager.IsLeftHandDrive())
				dir = NetInfo.InvertDirection(dir);

			return dir;
		}

		public static bool CheckNodeFlags(ushort nodeId, NetNode.Flags flagMask, NetNode.Flags? expectedResult=null) {
			bool ret = false;
			ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				ret = LogicUtil.CheckFlags((uint)node.m_flags, (uint)flagMask, (uint?)expectedResult);
			});
			return ret;
		}

		public static bool CheckSegmentFlags(ushort segmentId, NetSegment.Flags flagMask, NetSegment.Flags? expectedResult=null) {
			bool ret = false;
			ProcessSegment(segmentId, delegate (ushort sId, ref NetSegment segment) {
				ret = LogicUtil.CheckFlags((uint)segment.m_flags, (uint)flagMask, (uint?)expectedResult);
			});
			return ret;
		}

		public static bool CheckLaneFlags(uint laneId, NetLane.Flags flagMask, NetLane.Flags? expectedResult=null) {
			bool ret = false;
			ProcessLane(laneId, delegate (uint lId, ref NetLane lane) {
				ret = LogicUtil.CheckFlags((uint)lane.m_flags, (uint)flagMask, (uint?)expectedResult);
			});
			return ret;
		}

		public struct LanePos {
			public uint laneId;
			public byte laneIndex;
			public float position;

			public LanePos(uint laneId, byte laneIndex, float position) {
				this.laneId = laneId;
				this.laneIndex = laneIndex;
				this.position = position;
			}
		}

		/// <summary>
		/// Assembles a geometrically sorted list of lanes for the given segment.
		/// If the <paramref name="startNode"/> parameter is set only lanes supporting traffic to flow towards the given node are added to the list, otherwise all matched lanes are added.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="segment">segment data</param>
		/// <param name="startNode">reference node (optional)</param>
		/// <param name="laneTypeFilter">lane type filter, lanes must match this filter mask</param>
		/// <param name="vehicleTypeFilter">vehicle type filter, lanes must match this filter mask</param>
		/// <param name="reverse">if true, lanes are ordered from right to left (relative to the segment's start node / the given node), otherwise from left to right</param>
		/// <returns>sorted list of lanes for the given segment</returns>
		public static IList<LanePos> GetSortedVehicleLanes(ushort segmentId, ref NetSegment segment, bool? startNode, NetInfo.LaneType? laneTypeFilter = null, VehicleInfo.VehicleType? vehicleTypeFilter = null, bool reverse = false) { // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
			NetManager netManager = Singleton<NetManager>.instance;
			var laneList = new List<LanePos>();

			bool inverted = ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None);

			NetInfo.Direction? filterDir = null;
			NetInfo.Direction sortDir = NetInfo.Direction.Forward;
			if (startNode != null) {
				filterDir = (bool)startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
				filterDir = inverted ? NetInfo.InvertDirection((NetInfo.Direction)filterDir) : filterDir;
				sortDir = NetInfo.InvertDirection((NetInfo.Direction)filterDir);
			} else if (inverted) {
				sortDir = NetInfo.Direction.Backward;
			}

			if (reverse) {
				sortDir = NetInfo.InvertDirection(sortDir);
			}

			NetInfo segmentInfo = segment.Info;
			uint curLaneId = segment.m_lanes;
			byte laneIndex = 0;
			while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
				NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
				if ((laneTypeFilter == null || (laneInfo.m_laneType & laneTypeFilter) != NetInfo.LaneType.None) &&
					(vehicleTypeFilter == null || (laneInfo.m_vehicleType & vehicleTypeFilter) != VehicleInfo.VehicleType.None) &&
					(filterDir == null || segmentInfo.m_lanes[laneIndex].m_finalDirection == filterDir)) {
					laneList.Add(new LanePos(curLaneId, laneIndex, segmentInfo.m_lanes[laneIndex].m_position));
				}

				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}

			laneList.Sort(delegate (LanePos x, LanePos y) {
				if (x.position == y.position) {
					return 0;
				}

				if ((x.position < y.position) == (sortDir == NetInfo.Direction.Forward)) {
					return -1;
				}
				return 1;
			});
			return laneList;
		}
	}
}

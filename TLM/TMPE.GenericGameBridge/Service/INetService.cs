using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GenericGameBridge.Service {
	public delegate bool NetSegmentHandler(ushort segmentId, ref NetSegment segment);
	public delegate bool NetNodeHandler(ushort nodeId, ref NetNode node);
	public delegate bool NetLaneHandler(uint laneId, ref NetLane lane);
	public delegate bool NetSegmentLaneHandler(uint laneId, ref NetLane lane, NetInfo.Lane laneInfo, ushort segmentId, ref NetSegment segment, byte laneIndex);

	public struct LanePos {
		public uint laneId;
		public byte laneIndex;
		public float position;
		public VehicleInfo.VehicleType vehicleType;
		public NetInfo.LaneType laneType;

		public LanePos(uint laneId, byte laneIndex, float position, VehicleInfo.VehicleType vehicleType, NetInfo.LaneType laneType) {
			this.laneId = laneId;
			this.laneIndex = laneIndex;
			this.position = position;
			this.vehicleType = vehicleType;
			this.laneType = laneType;
		}
	}

	public enum ClockDirection {
		None,
		Clockwise,
		CounterClockwise
	}

	public interface INetService {
		bool CheckLaneFlags(uint laneId, NetLane.Flags flagMask, NetLane.Flags? expectedResult = default(NetLane.Flags?));
		bool CheckNodeFlags(ushort nodeId, NetNode.Flags flagMask, NetNode.Flags? expectedResult = default(NetNode.Flags?));
		bool CheckSegmentFlags(ushort segmentId, NetSegment.Flags flagMask, NetSegment.Flags? expectedResult = default(NetSegment.Flags?));
		NetInfo.Direction GetFinalSegmentEndDirection(ushort segmentId, bool startNode);
		NetInfo.Direction GetFinalSegmentEndDirection(ushort segmentId, ref NetSegment segment, bool startNode);
		ushort GetSegmentNodeId(ushort segmentId, bool startNode);
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
		IList<LanePos> GetSortedLanes(ushort segmentId, ref NetSegment segment, bool? startNode, NetInfo.LaneType? laneTypeFilter = default(NetInfo.LaneType?), VehicleInfo.VehicleType? vehicleTypeFilter = default(VehicleInfo.VehicleType?), bool reverse = false);
		bool IsLaneValid(uint laneId);
		bool IsNodeValid(ushort nodeId);
		bool IsSegmentValid(ushort segmentId);
		void IterateNodeSegments(ushort nodeId, NetSegmentHandler handler);
		void IterateNodeSegments(ushort nodeId, ClockDirection dir, NetSegmentHandler handler);
		void IterateSegmentLanes(ushort segmentId, NetSegmentLaneHandler handler);
		void IterateSegmentLanes(ushort segmentId, ref NetSegment segment, NetSegmentLaneHandler handler);
		void ProcessLane(uint laneId, NetLaneHandler handler);
		void ProcessLane(uint laneId, ref NetLane lane, NetLaneHandler handler);
		void ProcessNode(ushort nodeId, NetNodeHandler handler);
		void ProcessNode(ushort nodeId, ref NetNode node, NetNodeHandler handler);
		void ProcessSegment(ushort segmentId, NetSegmentHandler handler);
		void ProcessSegment(ushort segmentId, ref NetSegment segment, NetSegmentHandler handler);
		void PublishSegmentChanges(ushort segmentId);
	}
}

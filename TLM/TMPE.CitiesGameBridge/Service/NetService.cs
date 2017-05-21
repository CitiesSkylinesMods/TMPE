using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;

namespace CitiesGameBridge.Service {
	public class NetService : INetService {
		public static readonly INetService Instance = new NetService();
		
		private NetService() {
			
		}

		public bool IsSegmentValid(ushort segmentId) {
			return CheckSegmentFlags(segmentId, NetSegment.Flags.Created | NetSegment.Flags.Deleted, NetSegment.Flags.Created);
		}

		public void ProcessSegment(ushort segmentId, NetSegmentHandler handler) {
			ProcessSegment(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], handler);
		}

		public void ProcessSegment(ushort segmentId, ref NetSegment segment, NetSegmentHandler handler) {
			handler(segmentId, ref segment);
		}

		public bool IsNodeValid(ushort nodeId) {
			return CheckNodeFlags(nodeId, NetNode.Flags.Created | NetNode.Flags.Deleted, NetNode.Flags.Created);
		}

		public void ProcessNode(ushort nodeId, NetNodeHandler handler) {
			ProcessNode(nodeId, ref Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId], handler);
		}

		public void ProcessNode(ushort nodeId, ref NetNode node, NetNodeHandler handler) {
			handler(nodeId, ref node);
		}

		[Obsolete]
		bool IsLaneValid(ref NetLane lane) {
			if ((lane.m_flags & (uint)(NetLane.Flags.Created | NetLane.Flags.Deleted)) != (uint)NetLane.Flags.Created) {
				return false;
			}
			return IsSegmentValid(lane.m_segment);
		}
		
		public bool IsLaneValid(uint laneId) {
			if (!CheckLaneFlags(laneId, NetLane.Flags.Created | NetLane.Flags.Deleted, NetLane.Flags.Created)) {
				return false;
			}

			bool ret = false;
			ProcessLane(laneId, delegate(uint lId, ref NetLane lane) {
				ret = IsSegmentValid(lane.m_segment);
				return true;
			});
			return ret;
		}

		public void ProcessLane(uint laneId, NetLaneHandler handler) {
			ProcessLane(laneId, ref Singleton<NetManager>.instance.m_lanes.m_buffer[laneId], handler);
		}

		public void ProcessLane(uint laneId, ref NetLane lane, NetLaneHandler handler) {
			handler(laneId, ref lane);
		}

		public ushort GetSegmentNodeId(ushort segmentId, bool startNode) {
			ushort nodeId = 0;
			ProcessSegment(segmentId, delegate(ushort segId, ref NetSegment segment) {
				nodeId = startNode ? segment.m_startNode : segment.m_endNode;
				return true;
			});
			return nodeId;
		}

		public void IterateNodeSegments(ushort nodeId, NetSegmentHandler handler) {
			IterateNodeSegments(nodeId, ClockDirection.None, handler);
		}

		public void IterateNodeSegments(ushort nodeId, ClockDirection dir, NetSegmentHandler handler) {
			NetManager netManager = Singleton<NetManager>.instance;

			ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				if (dir == ClockDirection.None) {
					for (int i = 0; i < 8; ++i) {
						ushort segmentId = node.GetSegment(i);
						if (segmentId != 0) {
							if (!handler(segmentId, ref netManager.m_segments.m_buffer[segmentId])) {
								break;
							}
						}
					}
				} else {
					ushort segmentId = node.GetSegment(0);
					ushort initSegId = segmentId;

					while (true) {
						if (segmentId != 0) {
							if (!handler(segmentId, ref netManager.m_segments.m_buffer[segmentId])) {
								break;
							}
						}

						switch (dir) {
							case ClockDirection.Clockwise:
							default:
								segmentId = netManager.m_segments.m_buffer[segmentId].GetLeftSegment(nodeId);
								break;
							case ClockDirection.CounterClockwise:
								segmentId = netManager.m_segments.m_buffer[segmentId].GetRightSegment(nodeId);
								break;
						}

						if (segmentId == initSegId || segmentId == 0) {
							break;
						}
					}
				}
				return true;
			});
		}

		public void IterateSegmentLanes(ushort segmentId, NetSegmentLaneHandler handler) {
			IterateSegmentLanes(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], handler);
		}

		public void IterateSegmentLanes(ushort segmentId, ref NetSegment segment, NetSegmentLaneHandler handler) {
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

		public NetInfo.Direction GetFinalSegmentEndDirection(ushort segmentId, bool startNode) {
			return GetFinalSegmentEndDirection(segmentId, ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId], startNode);
		}

		public NetInfo.Direction GetFinalSegmentEndDirection(ushort segmentId, ref NetSegment segment, bool startNode) {
			NetInfo segmentInfo = segment.Info;

			var dir = startNode ? NetInfo.Direction.Backward : NetInfo.Direction.Forward;
			if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None /*^ SimulationService.Instance.LeftHandDrive*/)
				dir = NetInfo.InvertDirection(dir);

			return dir;
		}

		public bool CheckNodeFlags(ushort nodeId, NetNode.Flags flagMask, NetNode.Flags? expectedResult=null) {
			bool ret = false;
			ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
				ret = LogicUtil.CheckFlags((uint)node.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool CheckSegmentFlags(ushort segmentId, NetSegment.Flags flagMask, NetSegment.Flags? expectedResult=null) {
			bool ret = false;
			ProcessSegment(segmentId, delegate (ushort sId, ref NetSegment segment) {
				ret = LogicUtil.CheckFlags((uint)segment.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
		}

		public bool CheckLaneFlags(uint laneId, NetLane.Flags flagMask, NetLane.Flags? expectedResult=null) {
			bool ret = false;
			ProcessLane(laneId, delegate (uint lId, ref NetLane lane) {
				ret = LogicUtil.CheckFlags((uint)lane.m_flags, (uint)flagMask, (uint?)expectedResult);
				return true;
			});
			return ret;
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
		public IList<LanePos> GetSortedLanes(ushort segmentId, ref NetSegment segment, bool? startNode, NetInfo.LaneType? laneTypeFilter = null, VehicleInfo.VehicleType? vehicleTypeFilter = null, bool reverse = false) { // TODO refactor together with getSegmentNumVehicleLanes, especially the vehicle type and lane type checks
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
					laneList.Add(new LanePos(curLaneId, laneIndex, segmentInfo.m_lanes[laneIndex].m_position, laneInfo.m_vehicleType, laneInfo.m_laneType));
				}

				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				++laneIndex;
			}

			laneList.Sort(delegate (LanePos x, LanePos y) {
				bool fwd = sortDir == NetInfo.Direction.Forward;
				if (x.position == y.position) {
					if (x.position > 0) {
						// mirror type-bound lanes (e.g. for coherent disply of lane-wise speed limits)
						fwd = !fwd;
					}

					if (x.laneType == y.laneType) {
						if (x.vehicleType == y.vehicleType) {
							return 0;
						} else if ((x.vehicleType < y.vehicleType) == fwd) {
							return -1;
						} else {
							return 1;
						}
					} else if ((x.laneType < y.laneType) == fwd) {
						return -1;
					} else {
						return 1;
					}
				}

				if ((x.position < y.position) == fwd) {
					return -1;
				}
				return 1;
			});
			return laneList;
		}

		public void PublishSegmentChanges(ushort segmentId) {
			ISimulationService simService = SimulationService.Instance;

			ProcessSegment(segmentId, delegate (ushort sId, ref NetSegment segment) {
				uint currentBuildIndex = simService.CurrentBuildIndex;
				simService.CurrentBuildIndex = currentBuildIndex + 1;
				segment.m_modifiedIndex = currentBuildIndex;

				++segment.m_buildIndex;
				return true;
			});
		}
	}
}

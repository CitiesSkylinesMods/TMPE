#define DEBUGCONNx

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager {
	public class LaneConnectionManager : AbstractSegmentGeometryObservingManager, ICustomDataManager<List<Configuration.LaneConnection>> {
		public static LaneConnectionManager Instance { get; private set; } = null;
		
		static LaneConnectionManager() {
			Instance = new LaneConnectionManager();
		}

		/// <summary>
		/// Checks if traffic may flow from source lane to target lane according to setup lane connections
		/// </summary>
		/// <param name="sourceLaneId"></param>
		/// <param name="targetLaneId"></param>
		/// <param name="sourceStartNode">(optional) check at start node of source lane?</param>
		/// <returns></returns>
		public bool AreLanesConnected(uint sourceLaneId, uint targetLaneId, bool sourceStartNode) {
			if (targetLaneId == 0 || Flags.laneConnections[sourceLaneId] == null) {
				return false;
			}
			
			int nodeArrayIndex = sourceStartNode ? 0 : 1;

			uint[] connectedLanes = Flags.laneConnections[sourceLaneId][nodeArrayIndex];
			if (connectedLanes == null) {
				return false;
			}

			bool ret = false;
			foreach (uint laneId in connectedLanes)
				if (laneId == targetLaneId) {
					ret = true;
					break;
				}

			return ret;
		}

		/// <summary>
		/// Determines if the given lane has outgoing connections
		/// </summary>
		/// <param name="sourceLaneId"></param>
		/// <returns></returns>
		public bool HasConnections(uint sourceLaneId, bool startNode) {
			int nodeArrayIndex = startNode ? 0 : 1;

			bool ret = Flags.laneConnections[sourceLaneId] != null && Flags.laneConnections[sourceLaneId][nodeArrayIndex] != null;
			return ret;
		}

		public bool HasUturnConnections(ushort segmentId, bool startNode) {
			NetManager netManager = Singleton<NetManager>.instance;
			int nodeArrayIndex = startNode ? 0 : 1;

			uint sourceLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			while (sourceLaneId != 0) {
				uint[] targetLaneIds = GetLaneConnections(sourceLaneId, startNode);

				if (targetLaneIds != null) {
					foreach (uint targetLaneId in targetLaneIds) {
						if (netManager.m_lanes.m_buffer[targetLaneId].m_segment == segmentId) {
							return true;
						}
					}
				}
				sourceLaneId = netManager.m_lanes.m_buffer[sourceLaneId].m_nextLane;
			}
			return false;
		}

		internal int CountConnections(uint sourceLaneId, bool startNode) {
			if (Flags.laneConnections[sourceLaneId] == null)
				return 0;
			int nodeArrayIndex = startNode ? 0 : 1;
			if (Flags.laneConnections[sourceLaneId][nodeArrayIndex] == null)
				return 0;
			
			return Flags.laneConnections[sourceLaneId][nodeArrayIndex].Length;
		}

		/// <summary>
		/// Gets all lane connections for the given lane
		/// </summary>
		/// <param name="laneId"></param>
		/// <returns></returns>
		internal uint[] GetLaneConnections(uint laneId, bool startNode) {
			if (Flags.laneConnections[laneId] == null)
				return null;

			int nodeArrayIndex = startNode ? 0 : 1;
			return Flags.laneConnections[laneId][nodeArrayIndex];
		}

		/// <summary>
		/// Removes a lane connection between two lanes
		/// </summary>
		/// <param name="laneId1"></param>
		/// <param name="laneId2"></param>
		/// <param name="startNode1"></param>
		/// <returns></returns>
		internal bool RemoveLaneConnection(uint laneId1, uint laneId2, bool startNode1) {
#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RemoveLaneConnection({laneId1}, {laneId2}, {startNode1}) called.");
#endif
			bool ret = Flags.RemoveLaneConnection(laneId1, laneId2, startNode1);

#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RemoveLaneConnection({laneId1}, {laneId2}, {startNode1}): ret={ret}");
#endif
			if (ret) {
				NetManager netManager = Singleton<NetManager>.instance;
				ushort segmentId1 = netManager.m_lanes.m_buffer[laneId1].m_segment;
				ushort segmentId2 = netManager.m_lanes.m_buffer[laneId2].m_segment;

				ushort commonNodeId;
				bool startNode2;
				GetCommonNodeId(laneId1, laneId2, startNode1, out commonNodeId, out startNode2);

				RecalculateLaneArrows(laneId1, commonNodeId, startNode1);
				RecalculateLaneArrows(laneId2, commonNodeId, startNode2);
				
				UnsubscribeFromSegmentGeometry(segmentId1);
				UnsubscribeFromSegmentGeometry(segmentId2);
			}

			return ret;
		}

		/// <summary>
		/// Removes all lane connections at the specified node
		/// </summary>
		/// <param name="nodeId"></param>
		internal void RemoveLaneConnectionsFromNode(ushort nodeId) {
#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RemoveLaneConnectionsFromNode({nodeId}) called.");
#endif
			NetManager netManager = Singleton<NetManager>.instance;
			for (int i = 0; i < 8; ++i) {
				ushort segmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(i);
				if (segmentId == 0)
					continue;

				RemoveLaneConnectionsFromSegment(segmentId, netManager.m_segments.m_buffer[segmentId].m_startNode == nodeId);
			}
		}

		/// <summary>
		/// Removes all lane connections at the specified segment end
		/// </summary>
		/// <param name="segmentId"></param>
		/// <param name="startNode"></param>
		internal void RemoveLaneConnectionsFromSegment(ushort segmentId, bool startNode) {
#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RemoveLaneConnectionsFromSegment({segmentId}, {startNode}) called.");
#endif
			NetManager netManager = Singleton<NetManager>.instance;

			uint curLaneId = netManager.m_segments.m_buffer[segmentId].m_lanes;
			while (curLaneId != 0) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RemoveLaneConnectionsFromSegment: Removing lane connections from segment {segmentId}, lane {curLaneId}.");
#endif
				RemoveLaneConnections(curLaneId, startNode);
				curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
			}
		}

		/// <summary>
		/// Removes all lane connections from the specified lane
		/// </summary>
		/// <param name="laneId"></param>
		/// <param name="startNode"></param>
		internal void RemoveLaneConnections(uint laneId, bool startNode) {
#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RemoveLaneConnections({laneId}, {startNode}) called.");
#endif

			if (Flags.laneConnections[laneId] == null)
				return;

			int nodeArrayIndex = startNode ? 0 : 1;

			if (Flags.laneConnections[laneId][nodeArrayIndex] == null)
				return;

			NetManager netManager = Singleton<NetManager>.instance;

			/*for (int i = 0; i < Flags.laneConnections[laneId][nodeArrayIndex].Length; ++i) {
				uint otherLaneId = Flags.laneConnections[laneId][nodeArrayIndex][i];
				if (Flags.laneConnections[otherLaneId] != null) {
					if ((Flags.laneConnections[otherLaneId][0] != null && Flags.laneConnections[otherLaneId][0].Length == 1 && Flags.laneConnections[otherLaneId][0][0] == laneId && Flags.laneConnections[otherLaneId][1] == null) ||
						Flags.laneConnections[otherLaneId][1] != null && Flags.laneConnections[otherLaneId][1].Length == 1 && Flags.laneConnections[otherLaneId][1][0] == laneId && Flags.laneConnections[otherLaneId][0] == null) {

						ushort otherSegmentId = netManager.m_lanes.m_buffer[otherLaneId].m_segment;
						UnsubscribeFromSegmentGeometry(otherSegmentId);
					}
				}
			}*/

			Flags.RemoveLaneConnections(laneId, startNode);

			if (Flags.laneConnections[laneId] == null) {
				ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;
				UnsubscribeFromSegmentGeometry(segmentId);
			}
		}

		/// <summary>
		/// Adds a lane connection between two lanes
		/// </summary>
		/// <param name="laneId1"></param>
		/// <param name="laneId2"></param>
		/// <param name="startNode1"></param>
		/// <returns></returns>
		internal bool AddLaneConnection(uint laneId1, uint laneId2, bool startNode1) {
			bool ret = Flags.AddLaneConnection(laneId1, laneId2, startNode1);

#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.AddLaneConnection({laneId1}, {laneId2}, {startNode1}): ret={ret}");
#endif
			if (ret) {
				ushort commonNodeId;
				bool startNode2;
				GetCommonNodeId(laneId1, laneId2, startNode1, out commonNodeId, out startNode2);
				RecalculateLaneArrows(laneId1, commonNodeId, startNode1);
				RecalculateLaneArrows(laneId2, commonNodeId, startNode2);

				NetManager netManager = Singleton<NetManager>.instance;

				ushort segmentId1 = netManager.m_lanes.m_buffer[laneId1].m_segment;
				ushort segmentId2 = netManager.m_lanes.m_buffer[laneId2].m_segment;

				if (segmentId1 == segmentId2) {
					JunctionRestrictionsManager.Instance.SetUturnAllowed(segmentId1, startNode1, true);
				}

				SubscribeToSegmentGeometry(segmentId1);
				SubscribeToSegmentGeometry(segmentId2);
			}

			return ret;
		}

		protected override void HandleInvalidSegment(SegmentGeometry geometry) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.OnUpdate({geometry.SegmentId}): Segment has become invalid. Removing lane connections.");
#endif
			RemoveLaneConnectionsFromSegment(geometry.SegmentId, false);
			RemoveLaneConnectionsFromSegment(geometry.SegmentId, true);
		}

		protected override void HandleValidSegment(SegmentGeometry geometry) {

		}

		/// <summary>
		/// Given two lane ids and node of the first lane, determines the node id to which both lanes are connected to
		/// </summary>
		/// <param name="laneId1"></param>
		/// <param name="laneId2"></param>
		/// <returns></returns>
		internal void GetCommonNodeId(uint laneId1, uint laneId2, bool startNode1, out ushort commonNodeId, out bool startNode2) {
			NetManager netManager = Singleton<NetManager>.instance;
			ushort segmentId1 = netManager.m_lanes.m_buffer[laneId1].m_segment;
			ushort segmentId2 = netManager.m_lanes.m_buffer[laneId2].m_segment;

			ushort nodeId2Start = netManager.m_segments.m_buffer[segmentId2].m_startNode;
			ushort nodeId2End = netManager.m_segments.m_buffer[segmentId2].m_endNode;

			ushort nodeId1 = startNode1 ? netManager.m_segments.m_buffer[segmentId1].m_startNode : netManager.m_segments.m_buffer[segmentId1].m_endNode;

			startNode2 = (nodeId1 == nodeId2Start);
			if (!startNode2 && nodeId1 != nodeId2End)
				commonNodeId = 0;
			else
				commonNodeId = nodeId1;
		}

		internal bool GetLaneEndPoint(ushort segmentId, bool startNode, byte laneIndex, uint? laneId, NetInfo.Lane laneInfo, out bool outgoing, out Vector3? pos) {
			NetManager netManager = Singleton<NetManager>.instance;

			pos = null;
			outgoing = false;

			if ((netManager.m_segments.m_buffer[segmentId].m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created)
				return false;

			if (laneId == null) {
				laneId = FindLaneId(segmentId, laneIndex);
				if (laneId == null)
					return false;
			}

			if ((netManager.m_lanes.m_buffer[(uint)laneId].m_flags & ((ushort)NetLane.Flags.Created | (ushort)NetLane.Flags.Deleted)) != (ushort)NetLane.Flags.Created)
				return false;

			if (laneInfo == null) {
				if (laneIndex < netManager.m_segments.m_buffer[segmentId].Info.m_lanes.Length)
					laneInfo = netManager.m_segments.m_buffer[segmentId].Info.m_lanes[laneIndex];
				else
					return false;
			}

			NetInfo.Direction laneDir = ((NetManager.instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);

			if (startNode) {
				if ((laneDir & (NetInfo.Direction.Backward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Backward)
					outgoing = true;
				pos = NetManager.instance.m_lanes.m_buffer[(uint)laneId].m_bezier.a;
			} else {
				if ((laneDir & (NetInfo.Direction.Forward | NetInfo.Direction.Avoid)) == NetInfo.Direction.Forward)
					outgoing = true;
				pos = NetManager.instance.m_lanes.m_buffer[(uint)laneId].m_bezier.d;
			}

			return true;
		}

		private uint? FindLaneId(ushort segmentId, byte laneIndex) {
			NetInfo.Lane[] lanes = NetManager.instance.m_segments.m_buffer[segmentId].Info.m_lanes;
			uint laneId = NetManager.instance.m_segments.m_buffer[segmentId].m_lanes;
			for (byte i = 0; i < lanes.Length && laneId != 0; i++) {
				if (i == laneIndex)
					return laneId;

				laneId = NetManager.instance.m_lanes.m_buffer[laneId].m_nextLane;
			}
			return null;
		}

		/// <summary>
		/// Recalculates lane arrows based on present lane connections.
		/// </summary>
		/// <param name="laneId"></param>
		/// <param name="nodeId"></param>
		private void RecalculateLaneArrows(uint laneId, ushort nodeId, bool startNode) {
#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}) called");
#endif
			if (!Flags.mayHaveLaneArrows(laneId, startNode)) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): lane {laneId}, startNode? {startNode} must not have lane arrows");
#endif
				return;
			}

			if (!HasConnections(laneId, startNode)) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): lane {laneId} does not have outgoing connections");
#endif
				return;
			}

			if (nodeId == 0) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): invalid node");
#endif
				return;
			}

			Flags.LaneArrows arrows = Flags.LaneArrows.None;

			NetManager netManager = Singleton<NetManager>.instance;
			ushort segmentId = netManager.m_lanes.m_buffer[laneId].m_segment;

			if (segmentId == 0) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): invalid segment");
#endif
				return;
			}

#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): startNode? {startNode}");
#endif

			NodeGeometry nodeGeo = NodeGeometry.Get(nodeId);
			if (!nodeGeo.IsValid()) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): invalid node geometry");
#endif
				return;
			}

			SegmentGeometry segmentGeo = SegmentGeometry.Get(segmentId);
			if (!segmentGeo.IsValid()) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): invalid segment geometry");
#endif
				return;
			}

			ushort[] connectedSegmentIds = segmentGeo.GetConnectedSegments(startNode);
			if (connectedSegmentIds == null) {
#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): connectedSegmentIds is null");
#endif
				return;
			}

			foreach (ushort connectedSegmentId in connectedSegmentIds) {
				if (connectedSegmentId == 0)
					continue;
				ArrowDirection dir = segmentGeo.GetDirection(connectedSegmentId, startNode);

#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}. dir={dir}");
#endif

				// check if arrow has already been set for this direction
				switch (dir) {
					case ArrowDirection.Turn:
						if (TrafficPriorityManager.IsLeftHandDrive()) {
							if ((arrows & Flags.LaneArrows.Right) != Flags.LaneArrows.None)
								continue;
						} else {
							if ((arrows & Flags.LaneArrows.Left) != Flags.LaneArrows.None)
								continue;
						}
						break;
					case ArrowDirection.Forward:
						if ((arrows & Flags.LaneArrows.Forward) != Flags.LaneArrows.None)
							continue;
						break;
					case ArrowDirection.Left:
						if ((arrows & Flags.LaneArrows.Left) != Flags.LaneArrows.None)
							continue;
						break;
					case ArrowDirection.Right:
						if ((arrows & Flags.LaneArrows.Right) != Flags.LaneArrows.None)
							continue;
						break;
					default:
						continue;
				}

#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}: need to determine arrows");
#endif

				bool addArrow = false;

				uint curLaneId = netManager.m_segments.m_buffer[connectedSegmentId].m_lanes;
				while (curLaneId != 0) {
#if DEBUGCONN
					Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}: checking lane {curLaneId}");
#endif
					if (AreLanesConnected(laneId, curLaneId, startNode)) {
#if DEBUGCONN
						Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}: checking lane {curLaneId}: lanes are connected");
#endif
						addArrow = true;
						break;
					}

					curLaneId = netManager.m_lanes.m_buffer[curLaneId].m_nextLane;
				}

#if DEBUGCONN
				Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}: finished processing lanes. addArrow={addArrow} arrows (before)={arrows}");
#endif
				if (addArrow) {
					switch (dir) {
						case ArrowDirection.Turn:
							if (TrafficPriorityManager.IsLeftHandDrive()) {
								arrows |= Flags.LaneArrows.Right;
							} else {
								arrows |= Flags.LaneArrows.Left;
							}
							break;
						case ArrowDirection.Forward:
							arrows |= Flags.LaneArrows.Forward;
							break;
						case ArrowDirection.Left:
							arrows |= Flags.LaneArrows.Left;
							break;
						case ArrowDirection.Right:
							arrows |= Flags.LaneArrows.Right;
							break;
						default:
							continue;
					}

#if DEBUGCONN
					Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): processing connected segment {connectedSegmentId}: arrows={arrows}");
#endif
				}
			}

#if DEBUGCONN
			Log._Debug($"LaneConnectionManager.RecalculateLaneArrows({laneId}, {nodeId}): setting lane arrows to {arrows}");
#endif

			LaneArrowManager.Instance.SetLaneArrows(laneId, arrows, true);
		}

		public bool LoadData(List<Configuration.LaneConnection> data) {
			bool success = true;
			Log.Info($"Loading {data.Count} lane connections");
			foreach (Configuration.LaneConnection conn in data) {
				try {
					if (!NetUtil.IsLaneValid(conn.lowerLaneId))
						continue;
					if (!NetUtil.IsLaneValid(conn.higherLaneId))
						continue;

					Log._Debug($"Loading lane connection: lane {conn.lowerLaneId} -> {conn.higherLaneId}");
					AddLaneConnection(conn.lowerLaneId, conn.higherLaneId, conn.lowerStartNode);
				} catch (Exception e) {
					// ignore, as it's probably corrupt save data. it'll be culled on next save
					Log.Error("Error loading data from lane connection: " + e.ToString());
					success = false;
				}
			}
			return success;
		}

		public List<Configuration.LaneConnection> SaveData(ref bool success) {
			List<Configuration.LaneConnection> ret = new List<Configuration.LaneConnection>();
			for (uint i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
				try {
					if (Flags.laneConnections[i] == null)
						continue;
					
					for (int nodeArrayIndex = 0; nodeArrayIndex <= 1; ++nodeArrayIndex) {
						uint[] connectedLaneIds = Flags.laneConnections[i][nodeArrayIndex];
						bool startNode = nodeArrayIndex == 0;
						if (connectedLaneIds != null) {
							foreach (uint otherHigherLaneId in connectedLaneIds) {
								if (otherHigherLaneId <= i)
									continue;
								if (!NetUtil.IsLaneValid(otherHigherLaneId))
									continue;

								Log._Debug($"Saving lane connection: lane {i} -> {otherHigherLaneId}");
								ret.Add(new Configuration.LaneConnection(i, (uint)otherHigherLaneId, startNode));
							}
						}
					}
				} catch (Exception e) {
					Log.Error($"Exception occurred while saving lane data @ {i}: {e.ToString()}");
					success = false;
				}
			}
			return ret;
		}
	}
}

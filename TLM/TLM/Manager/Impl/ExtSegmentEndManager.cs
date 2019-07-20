using ColossalFramework;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry.Impl;
using TrafficManager.State;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Data;
using TrafficManager.Traffic.Enums;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Manager.Impl {
	using State.ConfigData;

	public class ExtSegmentEndManager : AbstractCustomManager, IExtSegmentEndManager {
		public static ExtSegmentEndManager Instance { get; private set; } = null;

		static ExtSegmentEndManager() {
			Instance = new ExtSegmentEndManager();
		}

		/// <summary>
		/// All additional data for segment ends
		/// </summary>
		public ExtSegmentEnd[] ExtSegmentEnds { get; private set; } = null;

		private ExtSegmentEndManager() {
			ExtSegmentEnds = new ExtSegmentEnd[NetManager.MAX_SEGMENT_COUNT * 2];
			for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				ExtSegmentEnds[GetIndex((ushort)i, true)] = new ExtSegmentEnd((ushort)i, true);
				ExtSegmentEnds[GetIndex((ushort)i, false)] = new ExtSegmentEnd((ushort)i, false);
			}
		}

#if DEBUG
		public string GenerateVehicleChainDebugInfo(ushort segmentId, bool startNode) {
			int index = GetIndex(segmentId, startNode);
			ushort vehicleId = ExtSegmentEnds[index].firstVehicleId;
			string ret = "";
			int numIter = 0;
			while (vehicleId != 0) {
				ret += $" -> {vehicleId} (seg: {Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].currentSegmentId}@{Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].currentStartNode} , adj: {Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].previousVehicleIdOnSegment}..{Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment})";
				vehicleId = Constants.ManagerFactory.ExtVehicleManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment;

				if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return ret;
		}
#endif

		public void Reset(ushort segmentId) {
			Reset(ref ExtSegmentEnds[GetIndex(segmentId, true)]);
			Reset(ref ExtSegmentEnds[GetIndex(segmentId, false)]);
		}

		protected void Reset(ref ExtSegmentEnd extSegmentEnd) {
			IExtVehicleManager extVehicleMan = Constants.ManagerFactory.ExtVehicleManager;
			int numIter = 0;
			while (extSegmentEnd.firstVehicleId != 0) {
				extVehicleMan.Unlink(ref extVehicleMan.ExtVehicles[extSegmentEnd.firstVehicleId]);

				if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}

			extSegmentEnd.nodeId = 0;
			extSegmentEnd.outgoing = false;
			extSegmentEnd.incoming = false;
			extSegmentEnd.firstVehicleId = 0;
		}

		public int GetIndex(ushort segmentId, bool startNode) {
			return (int)segmentId * 2 + (startNode ? 0 : 1);
		}

		public int GetIndex(ushort segmentId, ushort nodeId) {
			bool found = false;
			bool startNode = false;
			Services.NetService.ProcessSegment(segmentId, (ushort _, ref NetSegment seg) => {
				if (seg.m_startNode == nodeId) {
					found = true;
					startNode = true;
				} else if (seg.m_endNode == nodeId) {
					found = true;
				}
				return true;
			});

			if (!found) {
				Log.Warning($"ExtSegmentEndManager.GetIndex({segmentId}, {nodeId}): Node is not connected to segment.");
				return -1;
			} else {
				return GetIndex(segmentId, startNode);
			}
		}

		public uint GetRegisteredVehicleCount(ref ExtSegmentEnd end) {
			IExtVehicleManager vehStateManager = Constants.ManagerFactory.ExtVehicleManager;

			ushort vehicleId = end.firstVehicleId;
			uint ret = 0;
			int numIter = 0;
			while (vehicleId != 0) {
				++ret;
				vehicleId = vehStateManager.ExtVehicles[vehicleId].nextVehicleIdOnSegment;

				if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
					CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
					break;
				}
			}
			return ret;
		}

		public ArrowDirection GetDirection(ref ExtSegmentEnd sourceEnd, ushort targetSegmentId) {
			IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;
			if (! extSegMan.IsValid(sourceEnd.segmentId) || ! extSegMan.IsValid(targetSegmentId)) {
				return ArrowDirection.None;
			}

			bool? targetStartNode = Services.NetService.IsStartNode(targetSegmentId, sourceEnd.nodeId);
			if (targetStartNode == null) {
				return ArrowDirection.None;
			}

			bool sourceStartNode = sourceEnd.startNode;

			Vector3 sourceDir = Vector3.zero;
			Services.NetService.ProcessSegment(sourceEnd.segmentId, delegate (ushort segId, ref NetSegment seg) {
				sourceDir = sourceStartNode ? seg.m_startDirection : seg.m_endDirection;
				return true;
			});

			Vector3 targetDir = Vector3.zero;
			Services.NetService.ProcessSegment(targetSegmentId, delegate (ushort segId, ref NetSegment seg) {
				targetDir = (bool)targetStartNode ? seg.m_startDirection : seg.m_endDirection;
				return true;
			});

			return CalculateArrowDirection(sourceDir, targetDir);
		}

		protected ArrowDirection CalculateArrowDirection(Vector3 sourceDir, Vector3 targetDir) {
			sourceDir.y = 0;
			sourceDir.Normalize();

			targetDir.y = 0;
			targetDir.Normalize();
			float c = Vector3.Cross(sourceDir, targetDir).y;

			if (c >= 0.5f) { // [+30°, +150°]
				return ArrowDirection.Left;
			} else if (c <= -0.5f) { // [-30°, -150°]
				return ArrowDirection.Right;
			} else { // (-30°, +30°) / (-150°, -180°] / (+150°, +180°]
				float d = Vector3.Dot(sourceDir, targetDir);
				if (d > 0) { // (-30°, +30°)
					if (c > 0) { // (0°, 30°]
						return ArrowDirection.Left;
					} else if (c < 0) { // (0°, -30°]
						return ArrowDirection.Right;
					} else { // [0°]
						return ArrowDirection.Turn;
					}
				} else { // (-150°, -180°] / (+150°, +180°]
					return ArrowDirection.Forward;
				}
			}
		}

		private static Vector3 GetSegmentDir(ref NetSegment segment, bool startNode) {
			return startNode ? segment.m_startDirection : segment.m_endDirection;
		}

		public void Recalculate(ushort segmentId) {
			Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, true)]);
			Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, false)]);
		}

		public void Recalculate(ushort segmentId, bool startNode) {
			Recalculate(ref ExtSegmentEnds[GetIndex(segmentId, startNode)]);
		}

		protected void Recalculate(ref ExtSegmentEnd segEnd) {
			IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;
			ushort segmentId = segEnd.segmentId;
			bool startNode = segEnd.startNode;

#if DEBUG
			bool output = DebugSwitch.GeometryDebug.Get();

			if (output)
				Log._Debug($"ExtSegmentEndManager.Recalculate({segmentId}, {startNode}) called.");
#endif

			ushort nodeIdBeforeRecalc = segEnd.nodeId;
			Reset(ref segEnd);

			if (! Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
				if (nodeIdBeforeRecalc != 0) {
					Constants.ManagerFactory.ExtNodeManager.RemoveSegment(nodeIdBeforeRecalc, segmentId);
				}
				return;
			}

			ushort nodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, startNode);
			segEnd.nodeId = nodeId;
			CalculateIncomingOutgoing(segmentId, nodeId, out segEnd.incoming, out segEnd.outgoing);

			if (nodeIdBeforeRecalc != 0 && nodeIdBeforeRecalc != nodeId) {
				Constants.ManagerFactory.ExtNodeManager.RemoveSegment(nodeIdBeforeRecalc, segmentId);
			}

			Constants.ManagerFactory.ExtNodeManager.AddSegment(nodeId, segmentId);

#if DEBUG
			if (output) {
				Log.Info($"ExtSegmentEndManager.Recalculate({segmentId}, {startNode}): Recalculated ext. segment end: {segEnd}");
			}
#endif
		}

		protected void CalculateIncomingOutgoing(ushort segmentId, ushort nodeId, out bool incoming, out bool outgoing) {
			var instance = Singleton<NetManager>.instance;

			var info = instance.m_segments.m_buffer[segmentId].Info;

			var dir = NetInfo.Direction.Forward;
			if (instance.m_segments.m_buffer[segmentId].m_startNode == nodeId)
				dir = NetInfo.Direction.Backward;
			var dir2 = ((instance.m_segments.m_buffer[segmentId].m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);

			var hasForward = false;
			var hasBackward = false;
			bool isOutgoingOneWay = true;
			var laneId = instance.m_segments.m_buffer[segmentId].m_lanes;
			var laneIndex = 0;
			while (laneIndex < info.m_lanes.Length && laneId != 0u) {
				bool validLane = (info.m_lanes[laneIndex].m_laneType & (NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle)) != NetInfo.LaneType.None &&
					(info.m_lanes[laneIndex].m_vehicleType & (VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Train | VehicleInfo.VehicleType.Tram | VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Monorail)) != VehicleInfo.VehicleType.None;
				// TODO the lane types and vehicle types should be specified to make it clear which lanes we need to check

				if (validLane) {
					if ((info.m_lanes[laneIndex].m_finalDirection & dir2) != NetInfo.Direction.None) {
						isOutgoingOneWay = false;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Forward) != NetInfo.Direction.None) {
						hasForward = true;
					}

					if ((info.m_lanes[laneIndex].m_direction & NetInfo.Direction.Backward) != NetInfo.Direction.None) {
						hasBackward = true;
					}
				}

				laneId = instance.m_lanes.m_buffer[laneId].m_nextLane;
				laneIndex++;
			}

			bool isOneway = !(hasForward && hasBackward);
			if (!isOneway)
				isOutgoingOneWay = false;

			incoming = (hasForward || hasBackward) && !isOutgoingOneWay;
			outgoing = (hasForward || hasBackward) && (isOutgoingOneWay || !isOneway);
		}

		public bool CalculateOnlyHighways(ushort segmentId, bool startNode) {
			ushort nodeId = Services.NetService.GetSegmentNodeId(segmentId, startNode);
#if DEBUG
			bool output = DebugSwitch.GeometryDebug.Get();

			if (output)
				Log._Debug($"Checking if segment {segmentId} is connected to highways only at node {nodeId}.");
#endif

			bool hasOtherSegments = false;
			for (var s = 0; s < 8; s++) {
				ushort otherSegmentId = 0;
				Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
					otherSegmentId = node.GetSegment(s);
					return true;
				});
				if (otherSegmentId == 0 || otherSegmentId == segmentId)
					continue;

				hasOtherSegments = true;

				// determine geometry
				bool otherIsIncoming;
				bool otherIsOutgoing;
				CalculateIncomingOutgoing(otherSegmentId, nodeId, out otherIsIncoming, out otherIsOutgoing);
				bool otherIsOneWay = otherIsIncoming ^ otherIsOutgoing;
				bool otherIsHighway = Constants.ManagerFactory.ExtSegmentManager.CalculateIsHighway(otherSegmentId);

#if DEBUG
				if (output)
					Log._Debug($"Segment {segmentId} is connected to segment {otherSegmentId} at node {nodeId}. otherIsOneWay={otherIsOneWay} otherIsIncoming={otherIsIncoming} otherIsOutgoing={otherIsOutgoing} otherIsHighway={otherIsHighway}");
#endif
				if (!otherIsHighway || !otherIsOneWay) {
					return false;
				}
			}

			return hasOtherSegments;
		}

		public void CalculateOutgoingLeftStraightRightSegments(ref ExtSegmentEnd segEnd, ref NetNode node, out bool left, out bool straight, out bool right) {
			IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;

			left = false;
			straight = false;
			right = false;

			for (int i = 0; i < 8; ++i) {
				ushort otherSegmentId = node.GetSegment(i);
				if (otherSegmentId == 0 || otherSegmentId == segEnd.segmentId) {
					continue;
				}

				bool otherStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(otherSegmentId, segEnd.nodeId);
				ExtSegmentEnd otherSegEnd = ExtSegmentEnds[GetIndex(otherSegmentId, otherStartNode)];

				if (!otherSegEnd.outgoing) {
					continue;
				}

				switch (GetDirection(ref segEnd, otherSegmentId)) {
					case ArrowDirection.Left:
						left = true;
						break;
					case ArrowDirection.Forward:
						straight = true;
						break;
					case ArrowDirection.Right:
						right = true;
						break;
				}
			}
		}

		protected override void InternalPrintDebugInfo() {
			base.InternalPrintDebugInfo();
			Log._Debug($"Extended segment end data:");
			for (uint i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
				if (!Constants.ManagerFactory.ExtSegmentManager.IsValid((ushort)i)) {
					continue;
				}
				Log._Debug($"Segment {i} @ start node: {ExtSegmentEnds[GetIndex((ushort)i, true)]}");
				Log._Debug($"Segment {i} @ end node: {ExtSegmentEnds[GetIndex((ushort)i, false)]}");
			}
		}

		public override void OnLevelUnloading() {
			base.OnLevelUnloading();
			for (int i = 0; i < ExtSegmentEnds.Length; ++i) {
				Reset(ref ExtSegmentEnds[i]);
			}
		}
	}
}

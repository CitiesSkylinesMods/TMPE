using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Geometry {
	public class SegmentEndGeometry {
		public ushort SegmentId {
			get; private set;
		} = 0;

		public bool StartNode { get; private set; }

		public ushort NodeId() {
			return StartNode ? Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_startNode : Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_endNode;
		}

		/// <summary>
		/// last known connected node
		/// </summary>
		public ushort LastKnownNodeId {
			get; private set;
		} = 0;

		public ushort[] ConnectedSegments {
			get; private set;
		} = new ushort[7];

		public byte NumConnectedSegments {
			get; private set;
		} = 0;

		public ushort[] LeftSegments {
			get; private set;
		} = new ushort[7];

		public byte NumLeftSegments {
			get; private set;
		} = 0;

		public ushort[] IncomingLeftSegments {
			get; private set;
		} = new ushort[7];

		public byte NumIncomingLeftSegments {
			get; private set;
		} = 0;

		public ushort[] OutgoingLeftSegments {
			get; private set;
		} = new ushort[7];

		public byte NumOutgoingLeftSegments {
			get; private set;
		} = 0;

		public ushort[] RightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumRightSegments {
			get; private set;
		} = 0;

		public byte NumIncomingSegments {
			get; private set;
		} = 0;

		public byte NumOutgoingSegments {
			get; private set;
		} = 0;

		public ushort[] IncomingRightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumIncomingRightSegments {
			get; private set;
		} = 0;

		public ushort[] OutgoingRightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumOutgoingRightSegments {
			get; private set;
		} = 0;

		public ushort[] StraightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumStraightSegments {
			get; private set;
		} = 0;

		public ushort[] IncomingStraightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumIncomingStraightSegments {
			get; private set;
		} = 0;

		public ushort[] OutgoingStraightSegments {
			get; private set;
		} = new ushort[7];

		public byte NumOutgoingStraightSegments {
			get; private set;
		} = 0;

		/// <summary>
		/// Indicates that the managed segment is only connected to highway segments at the start node
		/// </summary>
		public bool OnlyHighways {
			get; private set;
		} = false;

		/// <summary>
		/// Indicates that the managed segment is an outgoing one-way segment at start node. That means vehicles may come from the node.
		/// </summary>
		public bool OutgoingOneWay {
			get; private set;
		} = false;

		/// <summary>
		/// Indicates that the managed segment is an incoming one-way segment at start node. That means vehicles may go to the node.
		/// </summary>
		public bool IncomingOneWay {
			get; private set;
		} = false;

		public SegmentEndGeometry(ushort segmentId, bool startNode) {
			SegmentId = segmentId;
			StartNode = startNode;
		}

		internal void Cleanup() {
			for (int i = 0; i < 7; ++i) {
				ConnectedSegments[i] = 0;

				LeftSegments[i] = 0;
				IncomingLeftSegments[i] = 0;
				OutgoingLeftSegments[i] = 0;

				RightSegments[i] = 0;
				IncomingRightSegments[i] = 0;
				OutgoingRightSegments[i] = 0;

				StraightSegments[i] = 0;
				IncomingStraightSegments[i] = 0;
				OutgoingStraightSegments[i] = 0;
			}
			NumConnectedSegments = 0;

			NumLeftSegments = 0;
			NumIncomingLeftSegments = 0;
			NumOutgoingLeftSegments = 0;

			NumRightSegments = 0;
			NumIncomingRightSegments = 0;
			NumOutgoingRightSegments = 0;

			NumStraightSegments = 0;
			NumIncomingStraightSegments = 0;
			NumOutgoingStraightSegments = 0;

			NumIncomingSegments = 0;
			NumOutgoingSegments = 0;

			OnlyHighways = false;
			OutgoingOneWay = false;
			IncomingOneWay = false;

			LastKnownNodeId = 0;
		}

		public bool IsValid() {
			bool valid = GetSegmentGeometry() == null || GetSegmentGeometry().IsValid();
			return valid && NodeId() != 0;
		}

		public bool IsConnectedTo(ushort otherSegmentId) {
			if (! IsValid())
				return false;

			foreach (ushort segId in ConnectedSegments)
				if (segId == otherSegmentId)
					return true;
			return false;
		}

		public ushort[] GetIncomingSegments() {
			ushort[] ret = new ushort[NumIncomingLeftSegments + NumIncomingRightSegments + NumIncomingStraightSegments];
			int i = 0;

			for (int k = 0; k < 7; ++k) {
				if (IncomingLeftSegments[k] == 0)
					break;
				ret[i++] = IncomingLeftSegments[k];
			}

			for (int k = 0; k < 7; ++k) {
				if (IncomingStraightSegments[k] == 0)
					break;
				ret[i++] = IncomingStraightSegments[k];
			}

			for (int k = 0; k < 7; ++k) {
				if (IncomingRightSegments[k] == 0)
					break;
				ret[i++] = IncomingRightSegments[k];
			}

			return ret;
		}

		public ushort[] GetOutgoingSegments() {
			ushort[] ret = new ushort[NumOutgoingLeftSegments + NumOutgoingRightSegments + NumOutgoingStraightSegments];
			int i = 0;

			for (int k = 0; k < 7; ++k) {
				if (OutgoingLeftSegments[k] == 0)
					break;
				ret[i++] = OutgoingLeftSegments[k];
			}

			for (int k = 0; k < 7; ++k) {
				if (OutgoingRightSegments[k] == 0)
					break;
				ret[i++] = OutgoingRightSegments[k];
			}

			for (int k = 0; k < 7; ++k) {
				if (OutgoingStraightSegments[k] == 0)
					break;
				ret[i++] = OutgoingStraightSegments[k];
			}

			return ret;
		}

		public SegmentGeometry GetSegmentGeometry() {
			return SegmentGeometry.Get(SegmentId);
		}

		internal void Recalculate(bool propagate) {
#if DEBUG
			//Log._Debug($"SegmentEndGeometry: Recalculate seg. {SegmentId} @ node {NodeId()}, propagate={propagate}");
#endif

			ushort nodeIdBeforeRecalc = LastKnownNodeId;
			Cleanup();

			if (!IsValid()) {
				if (nodeIdBeforeRecalc != 0)
					NodeGeometry.Get(nodeIdBeforeRecalc).RemoveSegment(SegmentId, propagate);

				return;
			}

			NetManager netManager = Singleton<NetManager>.instance;

			ushort nodeId = NodeId();
			LastKnownNodeId = nodeId;

			bool oneway;
			bool outgoingOneWay;
			SegmentGeometry.calculateOneWayAtNode(SegmentId, nodeId, out oneway, out outgoingOneWay);
			OutgoingOneWay = outgoingOneWay;
			if (oneway && ! OutgoingOneWay) {
				IncomingOneWay = true;
			}
			OnlyHighways = true;

			//ItemClass connectionClass = netManager.m_segments.m_buffer[SegmentId].Info.GetConnectionClass();

			bool hasOtherSegments = false;
			for (var s = 0; s < 8; s++) {
				ushort otherSegmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == SegmentId || ! SegmentGeometry.IsValid(otherSegmentId))
					continue;
				/*ItemClass otherConnectionClass = Singleton<NetManager>.instance.m_segments.m_buffer[otherSegmentId].Info.GetConnectionClass();
				if (otherConnectionClass.m_service != connectionClass.m_service)
					continue;*/
				hasOtherSegments = true;

				// determine geometry
				bool otherIsOneWay;
				bool otherIsOutgoingOneWay;
				SegmentGeometry.calculateOneWayAtNode(otherSegmentId, nodeId, out otherIsOneWay, out otherIsOutgoingOneWay);

				if (!(SegmentGeometry.calculateIsHighway(otherSegmentId) && otherIsOneWay))
					OnlyHighways = false;

				if (IsRightSegment(SegmentId, otherSegmentId, nodeId)) {
					RightSegments[NumRightSegments++] = otherSegmentId;
					if (!otherIsOutgoingOneWay) {
						IncomingRightSegments[NumIncomingRightSegments++] = otherSegmentId;
						if (!otherIsOneWay)
							OutgoingRightSegments[NumOutgoingRightSegments++] = otherSegmentId;
					} else {
						OutgoingRightSegments[NumOutgoingRightSegments++] = otherSegmentId;
					}
				} else if (IsLeftSegment(SegmentId, otherSegmentId, nodeId)) {
					LeftSegments[NumLeftSegments++] = otherSegmentId;
					if (!otherIsOutgoingOneWay) {
						IncomingLeftSegments[NumIncomingLeftSegments++] = otherSegmentId;
						if (!otherIsOneWay)
							OutgoingLeftSegments[NumOutgoingLeftSegments++] = otherSegmentId;
					} else {
						OutgoingLeftSegments[NumOutgoingLeftSegments++] = otherSegmentId;
					}
				} else {
					StraightSegments[NumStraightSegments++] = otherSegmentId;
					if (!otherIsOutgoingOneWay) {
						IncomingStraightSegments[NumIncomingStraightSegments++] = otherSegmentId;
						if (!otherIsOneWay)
							OutgoingStraightSegments[NumOutgoingStraightSegments++] = otherSegmentId;
					} else {
						OutgoingStraightSegments[NumOutgoingStraightSegments++] = otherSegmentId;
					}
				}

				// reset highway lane arrows
				Flags.removeHighwayLaneArrowFlagsAtSegment(otherSegmentId); // TODO refactor

				ConnectedSegments[NumConnectedSegments++] = otherSegmentId;
			}

			NumIncomingSegments = (byte)(NumIncomingLeftSegments + NumIncomingStraightSegments + NumIncomingRightSegments);
			NumOutgoingSegments = (byte)(NumOutgoingLeftSegments + NumOutgoingStraightSegments + NumOutgoingRightSegments);

			if (!hasOtherSegments)
				OnlyHighways = false;

			// propagate information to other segments
			if (nodeIdBeforeRecalc != nodeId) {
				if (nodeIdBeforeRecalc != 0)
					NodeGeometry.Get(nodeIdBeforeRecalc).RemoveSegment(SegmentId, propagate);

				NodeGeometry.Get(nodeId).AddSegment(SegmentId, StartNode, propagate);
			}
		}

		// static methods

		private static bool IsRightSegment(ushort fromSegment, ushort toSegment, ushort nodeid) {
			if (fromSegment <= 0 || toSegment <= 0)
				return false;

			return IsLeftSegment(toSegment, fromSegment, nodeid);
		}

		private static bool IsLeftSegment(ushort fromSegment, ushort toSegment, ushort nodeid) {
			if (fromSegment <= 0 || toSegment <= 0)
				return false;

			Vector3 fromDir = GetSegmentDir(fromSegment, nodeid);
			fromDir.y = 0;
			fromDir.Normalize();
			Vector3 toDir = GetSegmentDir(toSegment, nodeid);
			toDir.y = 0;
			toDir.Normalize();
			return Vector3.Cross(fromDir, toDir).y >= 0.5;
		}

		private static Vector3 GetSegmentDir(int segment, ushort nodeid) {
			var instance = Singleton<NetManager>.instance;

			Vector3 dir;

			dir = instance.m_segments.m_buffer[segment].m_startNode == nodeid ?
				instance.m_segments.m_buffer[segment].m_startDirection :
				instance.m_segments.m_buffer[segment].m_endDirection;

			return dir;
		}
	}
}

using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class SegmentEndGeometry {
		public ushort SegmentId {
			get; private set;
		} = 0;

		public bool StartNode;

		public ushort NodeId() {
			return StartNode ? Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_startNode : Singleton<NetManager>.instance.m_segments.m_buffer[SegmentId].m_endNode;
		}

		/// <summary>
		/// last known connected node
		/// </summary>
		public ushort LastKnownNodeId {
			get; private set;
		} = 0;

		private ushort[] connectedSegments = new ushort[7];
		private byte numConnectedSegments = 0;

		private ushort[] leftSegments = new ushort[7];
		private byte numLeftSegments = 0;

		private ushort[] incomingLeftSegments = new ushort[7];
		private byte numIncomingLeftSegments = 0;

		private ushort[] outgoingLeftSegments = new ushort[7];
		private byte numOutgoingLeftSegments = 0;

		private ushort[] rightSegments = new ushort[7];
		private byte numRightSegments = 0;

		private byte numIncomingSegments = 0;
		private byte numOutgoingSegments = 0;

		private ushort[] incomingRightSegments = new ushort[7];
		private byte numIncomingRightSegments = 0;

		private ushort[] outgoingRightSegments = new ushort[7];
		private byte numOutgoingRightSegments = 0;

		private ushort[] straightSegments = new ushort[7];
		private byte numStraightSegments = 0;

		private ushort[] incomingStraightSegments = new ushort[7];
		private byte numIncomingStraightSegments = 0;

		private ushort[] outgoingStraightSegments = new ushort[7];
		private byte numOutgoingStraightSegments = 0;

		/// <summary>
		/// Indicates that the managed segment is only connected to highway segments at the start node
		/// </summary>
		private bool onlyHighways = false;

		/// <summary>
		/// Indicates that the managed segment is an outgoing one-way segment at start node. That means vehicles may come from the node.
		/// </summary>
		private bool outgoingOneWay = false;

		public ushort[] ConnectedSegments {
			get { return connectedSegments; }
			private set { connectedSegments = value; }
		}

		public byte NumConnectedSegments {
			get { return numConnectedSegments; }
			private set { numConnectedSegments = value; }
		}

		public ushort[] LeftSegments {
			get { return leftSegments; }
			private set { leftSegments = value; }
		}

		public byte NumLeftSegments {
			get { return numLeftSegments; }
			private set { numLeftSegments = value; }
		}

		public ushort[] IncomingLeftSegments {
			get { return incomingLeftSegments; }
			private set { incomingLeftSegments = value; }
		}

		public byte NumIncomingLeftSegments {
			get { return numIncomingLeftSegments; }
			private set { numIncomingLeftSegments = value; }
		}

		public ushort[] OutgoingLeftSegments {
			get { return outgoingLeftSegments; }
			private set { outgoingLeftSegments = value; }
		}

		public byte NumOutgoingLeftSegments {
			get { return numOutgoingLeftSegments; }
			private set { numOutgoingLeftSegments = value; }
		}

		public ushort[] RightSegments {
			get { return rightSegments; }
			private set { rightSegments = value; }
		}

		public byte NumRightSegments {
			get { return numRightSegments; }
			private set { numRightSegments = value; }
		}

		public byte NumIncomingSegments {
			get { return numIncomingSegments; }
			private set { numIncomingSegments = value; }
		}

		public byte NumOutgoingSegments {
			get { return numOutgoingSegments; }
			private set { numOutgoingSegments = value; }
		}

		public ushort[] IncomingRightSegments {
			get { return incomingRightSegments; }
			private set { incomingRightSegments = value; }
		}

		public byte NumIncomingRightSegments {
			get { return numIncomingRightSegments; }
			private set { numIncomingRightSegments = value; }
		}

		public ushort[] OutgoingRightSegments {
			get { return outgoingRightSegments; }
			private set { outgoingRightSegments = value; }
		}

		public byte NumOutgoingRightSegments {
			get { return numOutgoingRightSegments; }
			private set { numOutgoingRightSegments = value; }
		}

		public ushort[] StraightSegments {
			get { return straightSegments; }
			private set { straightSegments = value; }
		}

		public byte NumStraightSegments {
			get { return numStraightSegments; }
			private set { numStraightSegments = value; }
		}

		public ushort[] IncomingStraightSegments {
			get { return incomingStraightSegments; }
			private set { incomingStraightSegments = value; }
		}

		public byte NumIncomingStraightSegments {
			get { return numIncomingStraightSegments; }
			private set { numIncomingStraightSegments = value; }
		}

		public ushort[] OutgoingStraightSegments {
			get { return outgoingStraightSegments; }
			private set { outgoingStraightSegments = value; }
		}

		public byte NumOutgoingStraightSegments {
			get { return numOutgoingStraightSegments; }
			private set { numOutgoingStraightSegments = value; }
		}

		public bool OnlyHighways {
			get { return onlyHighways; }
			private set { onlyHighways = value; }
		}

		public bool OutgoingOneWay {
			get { return outgoingOneWay; }
			private set { outgoingOneWay = value; }
		}

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

			LastKnownNodeId = 0;
		}

		public bool IsValid() {
			bool valid = GetSegmentGeometry() == null || GetSegmentGeometry().IsValid();
			return valid && NodeId() != 0;
		}

		public bool IsConnectedTo(ushort otherSegmentId) {
			return IsValid() && ConnectedSegments.Contains(otherSegmentId);
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
				if (IncomingRightSegments[k] == 0)
					break;
				ret[i++] = IncomingRightSegments[k];
			}

			for (int k = 0; k < 7; ++k) {
				if (IncomingStraightSegments[k] == 0)
					break;
				ret[i++] = IncomingStraightSegments[k];
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

			outgoingOneWay = SegmentGeometry.calculateIsOutgoingOneWay(SegmentId, nodeId);
			onlyHighways = true;

			//ItemClass connectionClass = netManager.m_segments.m_buffer[SegmentId].Info.GetConnectionClass();

			bool hasOtherSegments = false;
			for (var s = 0; s < 8; s++) {
				var otherSegmentId = netManager.m_nodes.m_buffer[nodeId].GetSegment(s);
				if (otherSegmentId == 0 || otherSegmentId == SegmentId)
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
					onlyHighways = false;

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
			numOutgoingSegments = (byte)(NumOutgoingLeftSegments + NumOutgoingStraightSegments + NumOutgoingRightSegments);

			if (!hasOtherSegments)
				onlyHighways = false;

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

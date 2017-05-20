using ColossalFramework;
using CSUtil.Commons;
using GenericGameBridge.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.State;
using TrafficManager.Util;
using UnityEngine;

namespace TrafficManager.Geometry {
	public class SegmentEndGeometry : SegmentEndId {
		public ushort NodeId() {
			return Constants.ServiceFactory.NetService.GetSegmentNodeId(SegmentId, StartNode);
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

		public override string ToString() {
			return $"[SegmentEndGeometry {base.ToString()}\n" +
				"\t" + $"NodeId() = {NodeId()}\n" +
				"\t" + $"IsValid() = {IsValid()}\n" +
				"\t" + $"LastKnownNodeId = {LastKnownNodeId}\n" +
				"\t" + $"ConnectedSegments = {ConnectedSegments.ArrayToString()}\n" +
				"\t" + $"NumConnectedSegments = {NumConnectedSegments}\n" +
				"\t" + $"LeftSegments = {LeftSegments.ArrayToString()}\n" +
				"\t" + $"NumLeftSegments = {NumLeftSegments}\n" +
				"\t" + $"IncomingLeftSegments = {IncomingLeftSegments.ArrayToString()}\n" +
				"\t" + $"NumIncomingLeftSegments = {NumIncomingLeftSegments}\n" +
				"\t" + $"OutgoingLeftSegments = {OutgoingLeftSegments.ArrayToString()}\n" +
				"\t" + $"NumOutgoingLeftSegments = {NumOutgoingLeftSegments}\n" +
				"\t" + $"RightSegments = {RightSegments.ArrayToString()}\n" +
				"\t" + $"NumRightSegments = {NumRightSegments}\n" +
				"\t" + $"NumIncomingSegments = {NumIncomingSegments}\n" +
				"\t" + $"NumOutgoingSegments = {NumOutgoingSegments}\n" +
				"\t" + $"IncomingRightSegments = {IncomingRightSegments.ArrayToString()}\n" +
				"\t" + $"NumIncomingRightSegments = {NumIncomingRightSegments}\n" +
				"\t" + $"OutgoingRightSegments = {OutgoingRightSegments.ArrayToString()}\n" +
				"\t" + $"NumOutgoingRightSegments = {NumOutgoingRightSegments}\n" +
				"\t" + $"StraightSegments = {StraightSegments.ArrayToString()}\n" +
				"\t" + $"NumStraightSegments = {NumStraightSegments}\n" +
				"\t" + $"IncomingStraightSegments = {IncomingStraightSegments.ArrayToString()}\n" +
				"\t" + $"NumIncomingStraightSegments = {NumIncomingStraightSegments}\n" +
				"\t" + $"OutgoingStraightSegments = {OutgoingStraightSegments.ArrayToString()}\n" +
				"\t" + $"NumOutgoingStraightSegments = {NumOutgoingStraightSegments}\n" +
				"\t" + $"OnlyHighways = {OnlyHighways}\n" +
				"\t" + $"OutgoingOneWay = {OutgoingOneWay}\n" +
				"\t" + $"IncomingOneWay = {IncomingOneWay}\n" +
				"\t" + $"GetClockwiseIndex() = {GetClockwiseIndex()}\n" +
				"SegmentEndGeometry]";
		}

		public SegmentEndGeometry(ushort segmentId, bool startNode) : base(segmentId, startNode) {

		}

		public static SegmentEndGeometry Get(SegmentEndId endId) {
			return Get(endId.SegmentId, endId.StartNode);
		}

		public static SegmentEndGeometry Get(ushort segmentId, bool startNode) {
			return SegmentGeometry.Get(segmentId)?.GetEnd(startNode);
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
			SegmentGeometry segGeo = GetSegmentGeometry();
			bool valid = segGeo != null && segGeo.IsValid();
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

		public SegmentGeometry GetSegmentGeometry(bool ignoreInvalid=false) {
			return SegmentGeometry.Get(SegmentId, ignoreInvalid);
		}

		public short GetClockwiseIndex() {
			// calculate clockwise index
			short clockwiseIndex = -1;
			Constants.ServiceFactory.NetService.IterateNodeSegments(NodeId(), ClockDirection.Clockwise, delegate (ushort sId, ref NetSegment segment) {
				++clockwiseIndex;
				//Log._Debug($"SegmentEndGeometry.Recalculate: Setting clockwise index of seg. {sId} to {clockwiseIndex} (we are @ seg. {SegmentId})");

				if (sId == SegmentId) {
					return false;
				}
				return true;
			});

			return clockwiseIndex;
		}

		internal void Recalculate(GeometryCalculationMode calcMode) {
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($">>> SegmentEndGeometry.Recalculate({calcMode}): seg. {SegmentId} @ node {NodeId()}");
#endif

			ushort nodeIdBeforeRecalc = LastKnownNodeId;
			Cleanup();

			if (!IsValid()) {
				if (calcMode == GeometryCalculationMode.Propagate && nodeIdBeforeRecalc != 0) {
#if DEBUGGEO
					if (GlobalConfig.Instance.DebugSwitches[5])
						Log._Debug($"SegmentEndGeometry.Recalculate({calcMode}): seg. {SegmentId} is not valid. nodeIdBeforeRecalc={nodeIdBeforeRecalc}. Removing segment from node.");
#endif
					NodeGeometry.Get(nodeIdBeforeRecalc).RemoveSegmentEnd(this, GeometryCalculationMode.Propagate);
				}

				return;
			}

			//NetManager netManager = Singleton<NetManager>.instance;

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
#if DEBUGGEO
			if (GlobalConfig.Instance.DebugSwitches[5])
				Log._Debug($"Checking if segment {SegmentId} is connected to highways only at node {NodeId()}. OnlyHighways={OnlyHighways}");
#endif

			//ItemClass connectionClass = netManager.m_segments.m_buffer[SegmentId].Info.GetConnectionClass();

			ushort firstClockwiseSegmentId = 0;
			bool hasOtherSegments = false;
			for (var s = 0; s < 8; s++) {
				ushort otherSegmentId = 0;
				Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
					otherSegmentId = node.GetSegment(s);
					if (s == 0) {
						firstClockwiseSegmentId = otherSegmentId;
					}
					return true;
				});
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
				bool otherIsHighway = SegmentGeometry.calculateIsHighway(otherSegmentId);

#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"Segment {SegmentId} is connected to segment {otherSegmentId} at node {NodeId()}. otherIsOneWay={otherIsOneWay} otherIsOutgoingOneWay={otherIsOutgoingOneWay} otherIsHighway={otherIsHighway}");
#endif
				if (! otherIsHighway || ! otherIsOneWay)
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
				//Flags.removeHighwayLaneArrowFlagsAtSegment(otherSegmentId); // TODO refactor

				ConnectedSegments[NumConnectedSegments++] = otherSegmentId;
			}

			NumIncomingSegments = (byte)(NumIncomingLeftSegments + NumIncomingStraightSegments + NumIncomingRightSegments);
			NumOutgoingSegments = (byte)(NumOutgoingLeftSegments + NumOutgoingStraightSegments + NumOutgoingRightSegments);

			if (!hasOtherSegments) {
#if DEBUGGEO
				if (GlobalConfig.Instance.DebugSwitches[5])
					Log._Debug($"Segment {SegmentId} is not connected to any other segments at node {NodeId()}.");
#endif
				OnlyHighways = false;
			}

			// propagate information to other segments
			if (calcMode == GeometryCalculationMode.Init || (calcMode == GeometryCalculationMode.Propagate && nodeIdBeforeRecalc != nodeId)) {
				if (calcMode == GeometryCalculationMode.Propagate && nodeIdBeforeRecalc != 0) {
					NodeGeometry.Get(nodeIdBeforeRecalc).RemoveSegmentEnd(this, GeometryCalculationMode.Propagate);
				}

				NodeGeometry.Get(nodeId).AddSegmentEnd(this, calcMode);
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

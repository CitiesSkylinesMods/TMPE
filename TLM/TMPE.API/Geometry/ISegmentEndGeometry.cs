using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Geometry {
	public interface ISegmentEndGeometry : ISegmentEndId {
		/// <summary>
		/// Holds the connected node id
		/// </summary>
		ushort NodeId {
			get;
		}

		/// <summary>
		/// Holds the last known connected node id
		/// </summary>
		ushort LastKnownNodeId {
			get;
		}

		/// <summary>
		/// Holds all connected segment ids
		/// </summary>
		ushort[] ConnectedSegments {
			get;
		}

		/// <summary>
		/// Holds the number of connected segments
		/// </summary>
		byte NumConnectedSegments {
			get;
		}

		/// <summary>
		/// Holds the number of incoming segments
		/// </summary>
		byte NumIncomingSegments {
			get;
		}

		/// <summary>
		/// Holds the number of outgoing segments
		/// </summary>
		byte NumOutgoingSegments {
			get;
		}

		/// <summary>
		/// Holds all left segment ids
		/// </summary>
		ushort[] LeftSegments {
			get;
		}

		/// <summary>
		/// Holds the number of left segments
		/// </summary>
		byte NumLeftSegments {
			get;
		}

		/// <summary>
		/// Holds all incoming left segment ids
		/// </summary>
		ushort[] IncomingLeftSegments {
			get;
		}

		/// <summary>
		/// Holds the number of incoming left segments
		/// </summary>
		byte NumIncomingLeftSegments {
			get;
		}

		/// <summary>
		/// Holds all outgoing left segment ids
		/// </summary>
		ushort[] OutgoingLeftSegments {
			get;
		}

		/// <summary>
		/// Holds the number of outgoing left segments
		/// </summary>
		byte NumOutgoingLeftSegments {
			get;
		}

		/// <summary>
		/// Holds all right segment ids
		/// </summary>
		ushort[] RightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of right segments
		/// </summary>
		byte NumRightSegments {
			get;
		}

		/// <summary>
		/// Holds all incoming right segment ids
		/// </summary>
		ushort[] IncomingRightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of incoming right segments
		/// </summary>
		byte NumIncomingRightSegments {
			get;
		}

		/// <summary>
		/// Holds all outgoing right segment ids
		/// </summary>
		ushort[] OutgoingRightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of outgoing right segments
		/// </summary>
		byte NumOutgoingRightSegments {
			get;
		}

		/// <summary>
		/// Holds all straight segment ids
		/// </summary>
		ushort[] StraightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of straight segments
		/// </summary>
		byte NumStraightSegments {
			get;
		}

		/// <summary>
		/// Holds all incoming straight segment ids
		/// </summary>
		ushort[] IncomingStraightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of incoming straight segments
		/// </summary>
		byte NumIncomingStraightSegments {
			get;
		}

		/// <summary>
		/// Holds all outgoing straight segment ids
		/// </summary>
		ushort[] OutgoingStraightSegments {
			get;
		}

		/// <summary>
		/// Holds the number of outgoing straight segments
		/// </summary>
		byte NumOutgoingStraightSegments {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is only connected to highway segments
		/// </summary>
		bool OnlyHighways {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is an outgoing one-way
		/// </summary>
		bool OutgoingOneWay {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is an incoming one-way
		/// </summary>
		bool IncomingOneWay {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is incoming
		/// </summary>
		bool Incoming {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is outgoing
		/// </summary>
		bool Outgoing {
			get;
		}

		/// <summary>
		/// Holds all incoming segment ids
		/// </summary>
		ushort[] IncomingSegments {
			get;
		}

		/// <summary>
		/// Holds all outgoing segment ids
		/// </summary>
		ushort[] OutgoingSegments {
			get;
		}

		/// <summary>
		/// Holds the clockwise index of the segment end
		/// </summary>
		short ClockwiseIndex {
			get;
		}

		/// <summary>
		/// Holds whether the segment end is valid
		/// </summary>
		bool Valid {
			get;
		}

		/// <summary>
		/// Recalculates the segment end
		/// </summary>
		/// <param name="calcMode">propagation mode</param>
		void Recalculate(GeometryCalculationMode calcMode);

		/// <summary>
		/// Determines wheter the given segment is right to this segment end
		/// </summary>
		/// <param name="toSegmentId">segment id</param>
		bool IsRightSegment(ushort toSegmentId);

		/// <summary>
		/// Determines wheter the given segment is left to this segment end
		/// </summary>
		/// <param name="toSegmentId">segment id</param>
		bool IsLeftSegment(ushort toSegmentId);

		/// <summary>
		/// Determines wheter the given segment is straight to this segment end
		/// </summary>
		/// <param name="toSegmentId">segment id</param>
		bool IsStraightSegment(ushort toSegmentId);

		/// <summary>
		/// Calculates the direction of the given segment relative to this segment end
		/// </summary>
		/// <param name="otherSegmentId">segment id</param>
		ArrowDirection GetDirection(ushort otherSegmentId);
	}
}

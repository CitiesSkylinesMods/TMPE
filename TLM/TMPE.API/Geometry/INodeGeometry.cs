using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Geometry {
	public interface INodeGeometry {
		/// <summary>
		/// Holds the node id
		/// </summary>
		ushort NodeId {
			get;
		}

		/// <summary>
		/// Holds whether this node is a simple junction
		/// </summary>
		bool SimpleJunction {
			get;
		}

		/// <summary>
		/// Holds the number of incoming segments
		/// </summary>
		int NumIncomingSegments {
			get;
		}

		/// <summary>
		/// Holds the number of outgoing segments
		/// </summary>
		int NumOutgoingSegments {
			get;
		}

		/// <summary>
		/// Holds all connected segment ends
		/// </summary>
		ISegmentEndGeometry[] SegmentEndGeometries {
			get;
		}

		/// <summary>
		/// Holds the number of the connected segment ends
		/// </summary>
		byte NumSegmentEnds {
			get;
		}

		/// <summary>
		/// Holds whether the node is valid
		/// </summary>
		bool Valid {
			get;
		}

		/// <summary>
		/// Recalculates the geometry
		/// </summary>
		void Recalculate();
	}
}

using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;

namespace TrafficManager.Util {
	public class SegmentTraverser {
		public enum TraverseDirection {
			Both,
			Incoming,
			Outgoing
		}

		/// <summary>
		/// Performs a Depth-First traversal over the cached segment geometry structure. At each traversed segment, the given `visitor` is notified. It then can update the current `state`.
		/// </summary>
		/// <param name="initialSegmentGeometry">Specifies the segment at which the traversal should start.</param>
		/// <param name="nextNodeIsStartNode">Specifies if the next node to traverse is the start node of the initial segment.</param>
		/// <param name="direction">Specifies if traffic should be able to flow towards the initial segment (Incoming) or should be able to flow from the initial segment (Outgoing) or in both directions (Both).</param>
		/// <param name="maximumDepth">Specifies the maximum depth to explore. At a depth of 0, no segment will be traversed (event the initial segment will be omitted).</param>
		/// <param name="visitor">Specifies the stateful visitor that should be notified as soon as a traversable segment (which has not been traversed before) is found.</param>
		public static void Traverse(ushort initialSegmentId, TraverseDirection direction, IVisitor<SegmentGeometry> visitor) {
			SegmentGeometry initialGeometry = SegmentGeometry.Get(initialSegmentId);
			if (initialGeometry == null)
				return;

			if (visitor.Visit(initialGeometry)) {
				HashSet<ushort> visitedSegmentIds = new HashSet<ushort>();
				visitedSegmentIds.Add(initialSegmentId);

				TraverseRec(initialGeometry, true, direction, visitor, visitedSegmentIds);
				TraverseRec(initialGeometry, false, direction, visitor, visitedSegmentIds);
			}
		}

		private static void TraverseRec(SegmentGeometry prevGeometry, bool exploreStartNode, TraverseDirection direction, IVisitor<SegmentGeometry> visitor, HashSet<ushort> visitedSegmentIds) {
			// collect next segment ids to traverse

			ushort[] nextSegmentIds;
			switch (direction) {
				case TraverseDirection.Both:
				default:
					nextSegmentIds = prevGeometry.GetConnectedSegments(exploreStartNode);
					break;
				case TraverseDirection.Incoming:
					nextSegmentIds = prevGeometry.GetIncomingSegments(exploreStartNode);
					break;
				case TraverseDirection.Outgoing:
					nextSegmentIds = prevGeometry.GetOutgoingSegments(exploreStartNode);
					break;
			}

			ushort prevNodeId = prevGeometry.GetNodeId(exploreStartNode);

			// explore next segments
			foreach (ushort nextSegmentId in nextSegmentIds) {
				if (nextSegmentId == 0 || visitedSegmentIds.Contains(nextSegmentId))
					continue;
				visitedSegmentIds.Add(nextSegmentId);

				SegmentGeometry nextSegmentGeometry = SegmentGeometry.Get(nextSegmentId);
				if (nextSegmentGeometry != null && visitor.Visit(nextSegmentGeometry)) {
					bool nextNodeIsStartNode = nextSegmentGeometry.StartNodeId() != prevNodeId;
					TraverseRec(nextSegmentGeometry, nextNodeIsStartNode, direction, visitor, visitedSegmentIds);
				}
			}
		}
	}
}

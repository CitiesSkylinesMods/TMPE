using CSUtil.Commons;
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

		public delegate bool SegmentVisitor(SegmentVisitData data);

		[Flags]
		public enum SegmentStopCriterion {
			/// <summary>
			/// Traversal stops when the whole network has been visited
			/// </summary>
			None = 0,
			/// <summary>
			/// Traversal stops when a node with more than two connected segments has been reached
			/// </summary>
			Junction = 1
		}

		public class SegmentVisitData {
			/// <summary>
			/// Previously visited segment geometry
			/// </summary>
			public SegmentGeometry prevGeo;

			/// <summary>
			/// Current segment geometry
			/// </summary>
			public SegmentGeometry curGeo;

			/// <summary>
			/// If true the current segment geometry has been reached on a path via the initial segment's start node
			/// </summary>
			public bool viaInitialStartNode;

			/// <summary>
			/// If true the current segment geometry has been reached on a path via the current segment's start node
			/// </summary>
			public bool viaStartNode;

			/// <summary>
			/// If true this is the initial segment
			/// </summary>
			public bool initial;

			public SegmentVisitData(SegmentGeometry prevGeo, SegmentGeometry curGeo, bool viaInitialStartNode, bool viaStartNode, bool initial) {
				this.prevGeo = prevGeo;
				this.curGeo = curGeo;
				this.viaInitialStartNode = viaInitialStartNode;
				this.viaStartNode = viaStartNode;
				this.initial = initial;
			}
		}

		/// <summary>
		/// Performs a Depth-First traversal over the cached segment geometry structure. At each traversed segment, the given `visitor` is notified. It then can update the current `state`.
		/// </summary>
		/// <param name="initialSegmentGeometry">Specifies the segment at which the traversal should start.</param>
		/// <param name="nextNodeIsStartNode">Specifies if the next node to traverse is the start node of the initial segment.</param>
		/// <param name="direction">Specifies if traffic should be able to flow towards the initial segment (Incoming) or should be able to flow from the initial segment (Outgoing) or in both directions (Both).</param>
		/// <param name="maximumDepth">Specifies the maximum depth to explore. At a depth of 0, no segment will be traversed (event the initial segment will be omitted).</param>
		/// <param name="visitor">Specifies the stateful visitor that should be notified as soon as a traversable segment (which has not been traversed before) is found.</param>
		public static void Traverse(ushort initialSegmentId, TraverseDirection direction, SegmentStopCriterion stopCrit, SegmentVisitor visitor) {
			SegmentGeometry initialSegGeometry = SegmentGeometry.Get(initialSegmentId);
			if (initialSegGeometry == null)
				return;

			Log._Debug($"SegmentTraverser: Traversing initial segment {initialSegGeometry.SegmentId}");
			if (visitor(new SegmentVisitData(initialSegGeometry, initialSegGeometry, false, false, true))) {
				HashSet<ushort> visitedSegmentIds = new HashSet<ushort>();
				visitedSegmentIds.Add(initialSegmentId);

				TraverseRec(initialSegGeometry, true, true, direction, stopCrit, visitor, visitedSegmentIds);
				TraverseRec(initialSegGeometry, false, false, direction, stopCrit, visitor, visitedSegmentIds);
			}
			Log._Debug($"SegmentTraverser: Traversal finished.");
		}

		private static void TraverseRec(SegmentGeometry prevSegGeometry, bool exploreStartNode, bool viaInitialStartNode, TraverseDirection direction, SegmentStopCriterion stopCrit, SegmentVisitor visitor, HashSet<ushort> visitedSegmentIds) {
			// collect next segment ids to traverse

			ushort[] nextSegmentIds;
			int numConnectedSegments;
			switch (direction) {
				case TraverseDirection.Both:
				default:
					nextSegmentIds = prevSegGeometry.GetConnectedSegments(exploreStartNode);
					numConnectedSegments = prevSegGeometry.CountOtherSegments(exploreStartNode);
					break;
				case TraverseDirection.Incoming:
					nextSegmentIds = prevSegGeometry.GetIncomingSegments(exploreStartNode);
					numConnectedSegments = prevSegGeometry.CountIncomingSegments(exploreStartNode);
					break;
				case TraverseDirection.Outgoing:
					nextSegmentIds = prevSegGeometry.GetOutgoingSegments(exploreStartNode);
					numConnectedSegments = prevSegGeometry.CountOutgoingSegments(exploreStartNode);
					break;
			}

			if (numConnectedSegments >= 2 && (stopCrit & SegmentStopCriterion.Junction) != SegmentStopCriterion.None) {
				Log._Debug($"SegmentTraverser: Stop criterion reached @ {prevSegGeometry.SegmentId}: {numConnectedSegments} connected segments");
				return;
			}

			ushort prevNodeId = prevSegGeometry.GetNodeId(exploreStartNode);

			// explore next segments
			foreach (ushort nextSegmentId in nextSegmentIds) {
				if (nextSegmentId == 0 || visitedSegmentIds.Contains(nextSegmentId))
					continue;
				visitedSegmentIds.Add(nextSegmentId);

				SegmentGeometry nextSegGeometry = SegmentGeometry.Get(nextSegmentId);
				if (nextSegGeometry != null) {
					Log._Debug($"SegmentTraverser: Traversing segment {nextSegGeometry.SegmentId}");

					if (visitor(new SegmentVisitData(prevSegGeometry, nextSegGeometry, viaInitialStartNode, prevNodeId == nextSegGeometry.StartNodeId(), false))) {
						bool nextNodeIsStartNode = nextSegGeometry.StartNodeId() != prevNodeId;
						TraverseRec(nextSegGeometry, nextNodeIsStartNode, viaInitialStartNode, direction, stopCrit, visitor, visitedSegmentIds);
					}
				}
			}
		}
	}
}

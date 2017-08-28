using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;

namespace TrafficManager.Util {
	public class SegmentTraverser {
		[Flags]
		public enum TraverseDirection {
			None = 0,
			Incoming = 1,
			Outgoing = 1 << 1,
			AnyDirection = Incoming | Outgoing
		}

		[Flags]
		public enum TraverseSide {
			None = 0,
			Left = 1,
			Straight = 1 << 1,
			Right = 1 << 2,
			AnySide = Left | Straight | Right
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
			Junction = 1,
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
		public static void Traverse(ushort initialSegmentId, TraverseDirection direction, TraverseSide side, SegmentStopCriterion stopCrit, SegmentVisitor visitor) {
			SegmentGeometry initialSegGeometry = SegmentGeometry.Get(initialSegmentId);
			if (initialSegGeometry == null)
				return;

			Log._Debug($"SegmentTraverser: Traversing initial segment {initialSegGeometry.SegmentId}");
			if (visitor(new SegmentVisitData(initialSegGeometry, initialSegGeometry, false, false, true))) {
				HashSet<ushort> visitedSegmentIds = new HashSet<ushort>();
				visitedSegmentIds.Add(initialSegmentId);

				TraverseRec(initialSegGeometry, true, true, direction, side, stopCrit, visitor, visitedSegmentIds);
				TraverseRec(initialSegGeometry, false, false, direction, side, stopCrit, visitor, visitedSegmentIds);
			}
			Log._Debug($"SegmentTraverser: Traversal finished.");
		}

		private static void TraverseRec(SegmentGeometry prevSegGeometry, bool exploreStartNode, bool viaInitialStartNode, TraverseDirection direction, TraverseSide side, SegmentStopCriterion stopCrit, SegmentVisitor visitor, HashSet<ushort> visitedSegmentIds) {
			Log._Debug($"SegmentTraverser: Traversing segment {prevSegGeometry.SegmentId}");

			// collect next segment ids to traverse

			if (direction == TraverseDirection.None) {
				throw new ArgumentException($"Invalid direction {direction} given.");
			}
			
			if (side == TraverseSide.None) {
				throw new ArgumentException($"Invalid side {side} given.");
			}

			HashSet<ushort> nextSegmentIds = new HashSet<ushort>();
			switch (direction) {
				case TraverseDirection.AnyDirection:
				default:
					if (side == TraverseSide.AnySide) {
						nextSegmentIds.UnionWith(prevSegGeometry.GetConnectedSegments(exploreStartNode));
					} else {
						if ((side & TraverseSide.Left) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetLeftSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Straight) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetStraightSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Right) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetRightSegments(exploreStartNode));
						}
					}
					break;
				case TraverseDirection.Incoming:
					if (side == TraverseSide.AnySide) {
						nextSegmentIds.UnionWith(prevSegGeometry.GetIncomingSegments(exploreStartNode));
					} else {
						if ((side & TraverseSide.Left) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetIncomingLeftSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Straight) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetIncomingStraightSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Right) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetIncomingRightSegments(exploreStartNode));
						}
					}
					break;
				case TraverseDirection.Outgoing:
					if (side == TraverseSide.AnySide) {
						nextSegmentIds.UnionWith(prevSegGeometry.GetOutgoingSegments(exploreStartNode));
					} else {
						if ((side & TraverseSide.Left) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetOutgoingLeftSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Straight) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetOutgoingStraightSegments(exploreStartNode));
						}

						if ((side & TraverseSide.Right) != TraverseSide.None) {
							nextSegmentIds.UnionWith(prevSegGeometry.GetOutgoingRightSegments(exploreStartNode));
						}
					}
					break;
			}
			nextSegmentIds.Remove(0);

			Log._Debug($"SegmentTraverser: Fetched next segments to traverse: {nextSegmentIds.CollectionToString()}");

			if (nextSegmentIds.Count >= 2 && (stopCrit & SegmentStopCriterion.Junction) != SegmentStopCriterion.None) {
				Log._Debug($"SegmentTraverser: Stop criterion reached @ {prevSegGeometry.SegmentId}: {nextSegmentIds.Count} connected segments");
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
						TraverseRec(nextSegGeometry, nextNodeIsStartNode, viaInitialStartNode, direction, side, stopCrit, visitor, visitedSegmentIds);
					}
				}
			}
		}
	}
}

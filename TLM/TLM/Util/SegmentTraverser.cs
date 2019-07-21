using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Text;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Geometry.Impl;
using TrafficManager.Manager;
using TrafficManager.Traffic.Data;

namespace TrafficManager.Util {
    using API.Manager;

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
			/// Previously visited ext. segment
			/// </summary>
			public ExtSegment prevSeg;

			/// <summary>
			/// Current ext. segment
			/// </summary>
			public ExtSegment curSeg;

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

			public SegmentVisitData(ref ExtSegment prevSeg, ref ExtSegment curSeg, bool viaInitialStartNode, bool viaStartNode, bool initial) {
				this.prevSeg = prevSeg;
				this.curSeg = curSeg;
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
			ExtSegment initialSeg = Constants.ManagerFactory.ExtSegmentManager.ExtSegments[initialSegmentId];
			if (! initialSeg.valid)
				return;

			//Log._Debug($"SegmentTraverser: Traversing initial segment {initialSegmentId}");
			if (visitor(new SegmentVisitData(ref initialSeg, ref initialSeg, false, false, true))) {
				HashSet<ushort> visitedSegmentIds = new HashSet<ushort>();
				visitedSegmentIds.Add(initialSegmentId);

				IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

				ushort startNodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(initialSegmentId, true);
				Constants.ServiceFactory.NetService.ProcessNode(startNodeId, delegate (ushort nId, ref NetNode node) {
					TraverseRec(ref initialSeg, ref extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(initialSegmentId, true)], ref node, true, direction, side, stopCrit, visitor, visitedSegmentIds);
					return true;
				});

				ushort endNodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(initialSegmentId, false);
				Constants.ServiceFactory.NetService.ProcessNode(endNodeId, delegate (ushort nId, ref NetNode node) {
					TraverseRec(ref initialSeg, ref extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(initialSegmentId, false)], ref node, false, direction, side, stopCrit, visitor, visitedSegmentIds);
					return true;
				});
			}
			//Log._Debug($"SegmentTraverser: Traversal finished.");
		}

		private static void TraverseRec(ref ExtSegment prevSeg, ref ExtSegmentEnd prevSegEnd, ref NetNode node, bool viaInitialStartNode, TraverseDirection direction, TraverseSide side, SegmentStopCriterion stopCrit, SegmentVisitor visitor, HashSet<ushort> visitedSegmentIds) {
			//Log._Debug($"SegmentTraverser: Traversing segment {prevSegEnd.segmentId}");

			// collect next segment ids to traverse

			if (direction == TraverseDirection.None) {
				throw new ArgumentException($"Invalid direction {direction} given.");
			}
			
			if (side == TraverseSide.None) {
				throw new ArgumentException($"Invalid side {side} given.");
			}

			IExtSegmentManager extSegMan = Constants.ManagerFactory.ExtSegmentManager;
			IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

			HashSet<ushort> nextSegmentIds = new HashSet<ushort>();
			for (int i = 0; i < 8; ++i) {
				ushort nextSegmentId = node.GetSegment(i);
				if (nextSegmentId == 0 || nextSegmentId == prevSegEnd.segmentId) {
					continue;
				}

				bool nextIsStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(nextSegmentId, prevSegEnd.nodeId);
				ExtSegmentEnd nextSegEnd = extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(nextSegmentId, nextIsStartNode)];

				if (
					direction == TraverseDirection.AnyDirection ||
					(direction == TraverseDirection.Incoming && nextSegEnd.incoming) ||
					(direction == TraverseDirection.Outgoing && nextSegEnd.outgoing)
				) {
					if (side == TraverseSide.AnySide) {
						nextSegmentIds.Add(nextSegmentId);
					} else {
						ArrowDirection dir = extSegEndMan.GetDirection(ref prevSegEnd, nextSegmentId);
						if (
							((side & TraverseSide.Left) != TraverseSide.None && dir == ArrowDirection.Left) ||
							((side & TraverseSide.Straight) != TraverseSide.None && dir == ArrowDirection.Forward) ||
							((side & TraverseSide.Right) != TraverseSide.None && dir == ArrowDirection.Right)
						) {
							nextSegmentIds.Add(nextSegmentId);
						}
					}
				}
			}
			nextSegmentIds.Remove(0);
			//Log._Debug($"SegmentTraverser: Fetched next segments to traverse: {nextSegmentIds.CollectionToString()}");

			if (nextSegmentIds.Count >= 2 && (stopCrit & SegmentStopCriterion.Junction) != SegmentStopCriterion.None) {
				//Log._Debug($"SegmentTraverser: Stop criterion reached @ {prevSegEnd.segmentId}: {nextSegmentIds.Count} connected segments");
				return;
			}

			// explore next segments
			foreach (ushort nextSegmentId in nextSegmentIds) {
				if (visitedSegmentIds.Contains(nextSegmentId))
					continue;
				visitedSegmentIds.Add(nextSegmentId);
				//Log._Debug($"SegmentTraverser: Traversing segment {nextSegmentId}");

				ushort nextStartNodeId = Constants.ServiceFactory.NetService.GetSegmentNodeId(nextSegmentId, true);
				if (visitor(new SegmentVisitData(ref prevSeg, ref extSegMan.ExtSegments[nextSegmentId], viaInitialStartNode, prevSegEnd.nodeId == nextStartNodeId, false))) {
					bool nextNodeIsStartNode = nextStartNodeId != prevSegEnd.nodeId;

					ExtSegmentEnd nextSegEnd = extSegEndMan.ExtSegmentEnds[extSegEndMan.GetIndex(nextSegmentId, nextNodeIsStartNode)];
					Constants.ServiceFactory.NetService.ProcessNode(nextSegEnd.nodeId, delegate (ushort nId, ref NetNode nextNode) {
						TraverseRec(ref extSegMan.ExtSegments[nextSegmentId], ref nextSegEnd, ref nextNode, viaInitialStartNode, direction, side, stopCrit, visitor, visitedSegmentIds);
						return true;
					});
				}
			}
		}
	}
}

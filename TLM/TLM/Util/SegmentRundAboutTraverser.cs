namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using API.Manager;
    using API.Traffic.Data;
    using CSUtil.Commons;
    using ColossalFramework;

    public class SegmentRoundAboutTraverser {

        public delegate bool SegmentVisitor(ushort segmentId);


        /// <summary>
        /// Traverses around a roundabout. At each
        /// traversed segment, the given `visitor` is notified.
        /// </summary>
        /// <param name="initialSegmentGeometry">Specifies the segment at which the traversal
        ///     should start.</param>
        /// <param name="visitorFun">Specifies the stateful visitor that should be notified as soon as
        ///     a traversable segment (which has not been traversed before) is found.
        /// pass null if you are trying to see if segment is part of a round about.
        /// </param>
        /// <returns>true if its a roundabout</returns>

       public static bool TraverseAround(ushort segmentId,SegmentVisitor visitorFun) {
            if (segmentId == 0 || !DirectionUtil.IsOneWay(segmentId)) {
                return false;
            }
            visitorFun?.Invoke(segmentId);
            ref NetSegment firstSegment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];

            do {
                ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
                ref NetNode endNode = ref NetManager.instance.m_nodes.m_buffer[segment.m_endNode];

                List<ushort> nextSegmentIds = new List<ushort>();
                for (int i = 0; i < 8; ++i) {
                    ushort nextSegmentId = endNode.GetSegment(i);
                    ref NetSegment nextSegment = ref Singleton<NetManager>.instance.m_segments.m_buffer[nextSegmentId];
                    if (nextSegmentId == 0 || nextSegmentId == segmentId) {
                        //not a roundabout;
                        return false;
                    }

                    bool isOneWay =  DirectionUtil.IsOneWay(segmentId);
                    bool nextIsStartNode = segment.m_endNode == nextSegment.m_startNode;
                    ArrowDirection dir = DirectionUtil.GetDirection(segmentId, nextSegmentId, segment.m_endNode);
                    bool isForward = dir == ArrowDirection.Forward;
                    if (isForward && nextIsStartNode && isOneWay) {
                        nextSegmentIds.Add(nextSegmentId);
                    }
                }
                if (nextSegmentIds.Count == 0) {
                    //not a round about
                    return false;
                } else {
                    //TODO: add code for LHD and RHD
                    //TODO: consider multiple forward branches
                    ushort nextSegmentId = nextSegmentIds[0];
                    visitorFun?.Invoke(segmentId);

                    ref NetSegment nextSegment = ref Singleton<NetManager>.instance.m_segments.m_buffer[nextSegmentId];
                    bool formsLoop = nextSegment.m_endNode == firstSegment.m_startNode;
                    if (formsLoop) {
                        return true;
                    }
                }
            } while (true);
        }
    } // end class
}
namespace TrafficManager.Util {
    using System;
    using System.Collections.Generic;
    using API.Manager;
    using API.Traffic.Data;
    using CSUtil.Commons;
    using ColossalFramework;

    public class SegmentRoundAboutTraverser {
        public List<ushort> segmentList = null;

        public SegmentRoundAboutTraverser Instance = new SegmentRoundAboutTraverser();


        /// <summary>
        /// tail node>-------->head node
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns></returns>
        private static ushort GetHeadNode(ushort segmentId) {
            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None;
            if(invert) {
                return segment.m_startNode;
            } else {
                return segment.m_endNode;
            }
        }
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
        public bool TraverseAround(ushort segmentId) {
            segmentList.Clear();
            if (segmentId == 0 || !DirectionUtil.IsOneWay(segmentId)) {
                return false;
            }
            return TraverseAroundRecursive(segmentId);
        }
        private bool TraverseAroundRecursive(ushort segmentId) {
            segmentList.Add(segmentId);
            ushort headNodeId = GetHeadNode(segmentId);
            ref NetNode headNode = ref NetManager.instance.m_nodes.m_buffer[headNodeId];
            for (int i = 0; i < 8; ++i) {
                ushort nextSegmentId = headNode.GetSegment(i);
                if (DirectionUtil.IsOneWay(segmentId, nextSegmentId)) {
                    bool isRAbout = false;
                    if(nextSegmentId == segmentList[0])
                        isRAbout = true;
                    else if( segmentList.Contains(nextSegmentId)) {
                        isRAbout = false;
                    } else {
                        isRAbout = TraverseAroundRecursive(nextSegmentId);
                    }
                    if (isRAbout) {
                        return true;
                    } //end if
                } // end if
            } // end for
            segmentList.Remove(segmentId);
            return false;
        }
    } // end class
}//end namespace
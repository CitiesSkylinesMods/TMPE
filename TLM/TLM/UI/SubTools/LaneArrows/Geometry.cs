namespace TrafficManager.UI.SubTools.LaneArrows {
    using ColossalFramework;
    using ColossalFramework.Math;
    using UnityEngine;

    /// <summary>
    /// Lane Arrows, a geometry helper
    /// </summary>
    public class Geometry {
        /// <summary>
        /// For given segment and one of its end nodes, get the direction vector.
        /// </summary>
        /// <param name="nodeId">The node, at which we need to know the tangent</param>
        /// <param name="segment">The segment, possibly a curve</param>
        /// <returns>Direction of the road at the given end of the segment.</returns>
        public static Vector3 GetSegmentTangent(ushort nodeId, NetSegment segment) {
            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var otherNodeId = segment.GetOtherNode(nodeId);
            var nodePos = nodesBuffer[nodeId].m_position;
            var otherNodePos = nodesBuffer[otherNodeId].m_position;

            if (segment.IsStraight()) {
                return (nodePos - otherNodePos).normalized;
            }

            // Handle some curvature, take the last tangent
            var bezier = default(Bezier3);
            bezier.a = nodesBuffer[segment.m_startNode].m_position;
            bezier.d = nodesBuffer[segment.m_endNode].m_position;
            NetSegment.CalculateMiddlePoints(bezier.a, segment.m_startDirection,
                                             bezier.d, segment.m_endDirection,
                                             false, false,
                                             out bezier.b, out bezier.c);
            var isStartNode = nodeId == segment.m_startNode;
            var tangent = bezier.Tangent(isStartNode ? 0f : 1f);

            // Some segments appear inverted. Perform a safety check that the angle
            // between vector Middle→B and the tangent is < 90°
            //
            // Correct situation:
            // <A>———————<Middle>————————<B>
            //                          ——→ tangent; GUI oriented with top towards B
            //
            // Inverted (bad) situation:
            // <A>———————<Middle>————————<B>
            //                          ←—— tangent; GUI upside down, bottom towards B
            //                  —————————→
            var middleToB = nodesBuffer[nodeId].m_position - segment.m_middlePosition;
            var product = Vector3.Dot(middleToB, tangent);
            if (product < 0f) {
                // For vectors with angle > 90° between them, the dot product is negative
                tangent *= -1f;
            }

            return tangent;
        }


        /// <summary>
        /// Distance from world position to the mouse pointer position, in screen pixels
        /// </summary>
        /// <param name="b">Point in the world</param>
        /// <returns>Distance in pixels on screen</returns>
        public static float ScreenDistanceToMouse(Vector3 b) {
            var bScreen = Camera.main.WorldToScreenPoint(b);
            return (Input.mousePosition - bScreen).magnitude;
        }
    }
}
namespace TrafficManager.UI.SubTools.LaneArrows {
    using CanvasGUI;
    using ColossalFramework;
    using UnityEngine;

    /// <summary>
    /// A struct for Lane Arrow tool, owning its world-space canvas
    /// </summary>
    public class LaneArrowsEditor {
        /// <summary>
        /// Canvas oriented along the segment tangent, in the ground plane
        /// </summary>
        public WorldSpaceGUI Gui;

        /// <summary>
        /// Contains the leaving lanes for the current nodeid, grouped by direction
        /// </summary>
        public OutgoingTurnsCollection? PossibleTurns;

        /// <summary>
        /// End of life, the LaneArrowsEditor will be destroyed in a moment
        /// </summary>
        public void Destroy() {
            PossibleTurns = null; // no more overlay rendering
            Gui?.DestroyCanvas();
            Gui = null;
        }

        /// <summary>
        /// Given segment being edited, and globals (selected node id) create canvas centered at that node.
        /// </summary>
        /// <param name="segment">Segment being edited</param>
        /// <param name="node">The node serving as the center of everything</param>
        /// <param name="guiOriginWorldPos">Returns position in the world where the canvas is centered</param>
        /// <param name="inverse">Returns inverse rotation quaternion for later use</param>
        public void CreateWorldSpaceCanvas(NetSegment segment,
                                           ushort nodeId,
                                           ushort segmentId,
                                           out Vector3 guiOriginWorldPos,
                                           out Quaternion inverse) {
            var nodesBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

            // Forward is the direction of the selected segment, even if it's a curve
            var forwardVector = Geometry.GetSegmentTangent(nodeId, segment);
            var rot = Quaternion.LookRotation(Vector3.down, forwardVector.normalized);
            inverse = Quaternion.Inverse(rot); // for projecting stuff from world into the canvas

            // UI is floating 5 metres above the ground
            const float UI_FLOAT_HEIGHT = 5f;
            var adjustFloat = Vector3.up * UI_FLOAT_HEIGHT;

            // Adjust UI vertically
            guiOriginWorldPos = nodesBuffer[nodeId].m_position + adjustFloat;
            Gui?.DestroyCanvas();
            Gui = new WorldSpaceGUI($"LaneArrowTool-{segmentId}", guiOriginWorldPos, rot);
        }
    }
}
namespace TrafficManager.UI {
    using ColossalFramework;
    using ColossalFramework.Math;
    using TrafficManager.Util;
    using UnityEngine;

    public static class Highlight {
        public static void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                          ushort nodeId,
                                          bool warning = false,
                                          bool alpha = false) {
            DrawNodeCircle(
                cameraInfo: cameraInfo,
                nodeId: nodeId,
                color: ModUI.GetTrafficManagerTool(createIfRequired: false)
                            .GetToolColor(warning: warning, error: false),
                alpha: alpha);
            // TODO: Potentially we do not need to refer to a TrafficManagerTool object
        }

        /// <summary>
        /// Gets the coordinates of the given node.
        /// </summary>
        private static Vector3 GetNodePos(ushort nodeId) {
            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            Vector3 pos = nodeBuffer[nodeId].m_position;
            float terrainY = Singleton<TerrainManager>.instance.SampleDetailHeightSmooth(pos);
            if (terrainY > pos.y) {
                pos.y = terrainY;
            }

            return pos;
        }

        /// <returns>the average half width of all connected segments</returns>
        private static float CalculateNodeRadius(ushort nodeId) {
            float sumHalfWidth = 0;
            int count = 0;
            Constants.ServiceFactory.NetService.IterateNodeSegments(
                nodeId,
                (ushort segmentId, ref NetSegment segment) => {
                    sumHalfWidth += segment.Info.m_halfWidth;
                    count++;
                    return true;
                });
            return sumHalfWidth / count;
        }

        // TODO: move to UI.Helpers (Highlight)
        public static void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                          ushort nodeId,
                                          Color color,
                                          bool alpha = false) {
            float r = CalculateNodeRadius(nodeId);
            Vector3 pos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
            DrawOverlayCircle(cameraInfo, color, pos, r * 2, alpha);
        }

        /// <summary>
        /// Draws a half sausage at segment end.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="cut">The lenght of the highlight [0~1] </param>
        /// <param name="bStartNode">Determines the direction of the half sausage.</param>
        // TODO: move to UI.Helpers (Highlight)
        public static void DrawCutSegmentEnd(RenderManager.CameraInfo cameraInfo,
                                             ushort segmentId,
                                             float cut,
                                             bool bStartNode,
                                             Color color,
                                             bool alpha = false) {
            if (segmentId == 0) {
                return;
            }

            ref NetSegment segment =
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            float width = segment.Info.m_halfWidth;

            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

            bool IsMiddle(ushort nodeId) =>
                (nodeBuffer[nodeId].m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = GetNodePos(segment.m_startNode);
            bezier.d = GetNodePos(segment.m_endNode);

            NetSegment.CalculateMiddlePoints(
                startPos: bezier.a,
                startDir: segment.m_startDirection,
                endPos: bezier.d,
                endDir: segment.m_endDirection,
                smoothStart: IsMiddle(segment.m_startNode),
                smoothEnd: IsMiddle(segment.m_endNode),
                middlePos1: out bezier.b,
                middlePos2: out bezier.c);

            if (bStartNode) {
                bezier = bezier.Cut(0, cut);
            } else {
                bezier = bezier.Cut(1 - cut, 1);
            }

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                size: width * 2f,
                cutStart: bStartNode ? 0 : width,
                cutEnd: bStartNode ? width : 0,
                minY: -1f,
                maxY: 1280f,
                renderLimits: false,
                alpha);
        }

        /// <summary>
        /// similar to NetTool.RenderOverlay()
        /// but with additional control over alphaBlend.
        /// </summary>
        // TODO: move to UI.Helpers (Highlight)
        internal static void DrawSegmentOverlay(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            Color color,
            bool alphaBlend) {
            if (segmentId == 0) {
                return;
            }

            ref NetSegment segment =
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            float width = segment.Info.m_halfWidth;

            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;

            bool IsMiddle(ushort nodeId) =>
                (nodeBuffer[nodeId].m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = GetNodePos(segment.m_startNode);
            bezier.d = GetNodePos(segment.m_endNode);

            NetSegment.CalculateMiddlePoints(
                startPos: bezier.a,
                startDir: segment.m_startDirection,
                endPos: bezier.d,
                endDir: segment.m_endDirection,
                smoothStart: IsMiddle(segment.m_startNode),
                smoothEnd: IsMiddle(segment.m_endNode),
                middlePos1: out bezier.b,
                middlePos2: out bezier.c);

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                size: width * 2f,
                cutStart: 0,
                cutEnd: 0,
                minY: -1f,
                maxY: 1280f,
                renderLimits: false,
                alphaBlend);
        }

        private static void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo,
                                              Color color,
                                              Vector3 position,
                                              float width,
                                              bool alpha) {
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                position,
                size: width,
                minY: position.y - 100f,
                maxY: position.y + 100f,
                renderLimits: false,
                alpha);
        }

        public static void DrawStaticSquareOverlayGridTexture(Texture2D texture,
                                                              Vector3 camPos,
                                                              Vector3 gridOrigin,
                                                              float cellSize,
                                                              Vector3 xu,
                                                              Vector3 yu,
                                                              uint x,
                                                              uint y,
                                                              float size,
                                                              out Rect screenRect) {
            DrawGenericSquareOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellSize,
                xu,
                yu,
                x,
                y,
                size,
                canHover: false,
                out screenRect);
        }

        // // TODO: move to UI.Helpers (Highlight)
        // public bool DrawHoverableSquareOverlayGridTexture(Texture2D texture,
        //                                                   Vector3 camPos,
        //                                                   Vector3 gridOrigin,
        //                                                   float cellSize,
        //                                                   Vector3 xu,
        //                                                   Vector3 yu,
        //                                                   uint x,
        //                                                   uint y,
        //                                                   float size) {
        //     return DrawGenericSquareOverlayGridTexture(
        //         texture,
        //         camPos,
        //         gridOrigin,
        //         cellSize,
        //         xu,
        //         yu,
        //         x,
        //         y,
        //         size,
        //         canHover: true);
        // }

        // TODO: Wrap with a grid struct holding origin, xu,yu bases, and cell size values
        public static bool DrawGenericSquareOverlayGridTexture(Texture2D texture,
                                                               Vector3 camPos,
                                                               Vector3 gridOrigin,
                                                               float cellSize,
                                                               Vector3 xu,
                                                               Vector3 yu,
                                                               uint x,
                                                               uint y,
                                                               float size,
                                                               bool canHover,
                                                               out Rect screenRect) {
            return DrawGenericOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellWidth: cellSize,
                cellHeight: cellSize,
                xu,
                yu,
                x,
                y,
                width: size,
                height: size,
                canHover,
                screenRect: out screenRect);
        }

        public static void DrawStaticOverlayGridTexture(Texture2D texture,
                                                        Vector3 camPos,
                                                        Vector3 gridOrigin,
                                                        float cellWidth,
                                                        float cellHeight,
                                                        Vector3 xu,
                                                        Vector3 yu,
                                                        uint x,
                                                        uint y,
                                                        float width,
                                                        float height,
                                                        out Rect screenRect) {
            DrawGenericOverlayGridTexture(
                texture,
                camPos,
                gridOrigin,
                cellWidth,
                cellHeight,
                xu,
                yu,
                x,
                y,
                width,
                height,
                canHover: false,
                out screenRect);
        }

        // public bool DrawHoverableOverlayGridTexture(Texture2D texture,
        //                                             Vector3 camPos,
        //                                             Vector3 gridOrigin,
        //                                             float cellWidth,
        //                                             float cellHeight,
        //                                             Vector3 xu,
        //                                             Vector3 yu,
        //                                             uint x,
        //                                             uint y,
        //                                             float width,
        //                                             float height) {
        //     return DrawGenericOverlayGridTexture(
        //         texture,
        //         camPos,
        //         gridOrigin,
        //         cellWidth,
        //         cellHeight,
        //         xu,
        //         yu,
        //         x,
        //         y,
        //         width,
        //         height,
        //         canHover: true);
        // }

        /// <summary>
        /// Position a texture rectangle in a "grid cell" of a regular grid with center in the
        /// GridOrigin, and basis xu,yu. The draw box is not rotated together with the grid basis
        /// and is aligned with screen axes.
        /// </summary>
        /// <param name="texture">Draw this.</param>
        /// <param name="camPos">From here.</param>
        /// <param name="gridOrigin">Grid starts here.</param>
        /// <param name="cellWidth">Grid cell width.</param>
        /// <param name="cellHeight">Grid cell height.</param>
        /// <param name="xu">Grid basis vector for X axis.</param>
        /// <param name="yu">Grid basis vector for Y axis.</param>
        /// <param name="x">X position in grid.</param>
        /// <param name="y">Y position in grid.</param>
        /// <param name="width">Draw box size x.</param>
        /// <param name="height">Draw box size y.</param>
        /// <param name="canHover">Whether the icon is interacting with the mouse.</param>
        /// <returns>Whether mouse hovers the icon.</returns>
        // TODO: move to UI.Helpers (Highlight)
        // TODO: Refactor to a new struct which will hold the grid origin, xu, yu, cell sizes
        public static bool DrawGenericOverlayGridTexture(Texture2D texture,
                                                         Vector3 camPos,
                                                         Vector3 gridOrigin,
                                                         float cellWidth,
                                                         float cellHeight,
                                                         Vector3 xu,
                                                         Vector3 yu,
                                                         uint x,
                                                         uint y,
                                                         float width,
                                                         float height,
                                                         bool canHover,
                                                         out Rect screenRect) {
            Vector3 worldPos =
                gridOrigin + (cellWidth * x * xu) +
                (cellHeight * y * yu); // grid position in game coordinates
            return DrawGenericOverlayTexture(
                texture,
                camPos,
                worldPos,
                width,
                height,
                canHover,
                out screenRect);
        }

        // TODO: move to UI.Helpers (Highlight)
        public static void DrawStaticSquareOverlayTexture(Texture2D texture,
                                                          Vector3 camPos,
                                                          Vector3 worldPos,
                                                          float size) {
            DrawGenericOverlayTexture(
                texture,
                camPos,
                worldPos,
                width: size,
                height: size,
                canHover: false,
                screenRect: out Rect _);
        }

        public static bool DrawHoverableSquareOverlayTexture(Texture2D texture,
                                                             Vector3 camPos,
                                                             Vector3 worldPos,
                                                             float size) {
            return DrawGenericOverlayTexture(
                texture,
                camPos,
                worldPos,
                width: size,
                height: size,
                canHover: true,
                screenRect: out Rect _);
        }

        public static bool DrawGenericSquareOverlayTexture(Texture2D texture,
                                                           Vector3 camPos,
                                                           Vector3 worldPos,
                                                           float size,
                                                           bool canHover) {
            return DrawGenericOverlayTexture(
                texture,
                camPos,
                worldPos,
                width: size,
                height: size,
                canHover,
                screenRect: out Rect _);
        }

        public static void DrawStaticOverlayTexture(Texture2D texture,
                                                    Vector3 camPos,
                                                    Vector3 worldPos,
                                                    float width,
                                                    float height) {
            DrawGenericOverlayTexture(
                texture,
                camPos,
                worldPos,
                width,
                height,
                canHover: false,
                screenRect: out Rect _);
        }

        // public bool DrawHoverableOverlayTexture(Texture2D texture,
        //                                         Vector3 camPos,
        //                                         Vector3 worldPos,
        //                                         float width,
        //                                         float height) {
        //     return DrawGenericOverlayTexture(
        //         texture,
        //         camPos,
        //         worldPos,
        //         width,
        //         height,
        //         canHover: true);
        // }

        public static bool DrawGenericOverlayTexture(Texture2D texture,
                                                     Vector3 camPos,
                                                     Vector3 worldPos,
                                                     float width,
                                                     float height,
                                                     bool canHover,
                                                     out Rect screenRect) {
            // Is point in screen?
            if (!GeometryUtil.WorldToScreenPoint(worldPos, out Vector3 screenPos)) {
                screenRect = default;
                return false;
            }

            float zoom = 1.0f / (worldPos - camPos).magnitude * 100f * U.UIScaler.GetScale();
            width *= zoom;
            height *= zoom;

            screenRect = new Rect(
                x: screenPos.x - (width / 2f),
                y: screenPos.y - (height / 2f),
                width: width,
                height: height);

            Color guiColor = GUI.color;
            bool hovered = false;

            if (canHover) {
                hovered = TrafficManagerTool.IsMouseOver(screenRect);
            }

            guiColor.a = TrafficManagerTool.GetHandleAlpha(hovered);

            GUI.color = guiColor;
            GUI.DrawTexture(screenRect, texture);

            return hovered;
        }
    }
}
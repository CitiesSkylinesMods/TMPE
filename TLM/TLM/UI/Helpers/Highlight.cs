namespace TrafficManager.UI.Helpers {
    using ColossalFramework;
    using ColossalFramework.Math;
    using ColossalFramework.UI;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    /// <summary>
    /// Provides static functions for drawing overlay textures.
    /// Must be called from GUI callbacks only, will not work from other code.
    /// </summary>
    public static class Highlight {
        public enum Shape {
            Circle,
            Square,
            Diamond,
        }

        /// <summary>
        /// Create this to describe a grid for rendering multiple icons.
        /// Icons are positioned in the XZ plane in the world around the GridOrigin, but rendered
        /// normally in screen space with their sides axis aligned.
        /// </summary>
        public class Grid {
            /// <summary>Grid starts here.</summary>
            public Vector3 GridOrigin;

            /// <summary>Grid cell width.</summary>
            public float CellWidth;

            /// <summary>Grid cell height.</summary>
            public float CellHeight;

            /// <summary>Grid basis vector for X axis.</summary>
            public Vector3 Xu;

            /// <summary>Grid basis vector for Y axis.</summary>
            public Vector3 Yu;

            public Grid(Vector3 gridOrigin,
                        float cellWidth,
                        float cellHeight,
                        Vector3 xu,
                        Vector3 yu) {
                GridOrigin = gridOrigin;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                Xu = xu;
                Yu = yu;
            }

            /// <summary>Grid position in game coordinates for row and column.</summary>
            /// <param name="col">Column.</param>
            /// <param name="row">Row.</param>
            /// <returns>World position.</returns>
            public Vector3 GetPositionForRowCol(float col, float row) {
                return this.GridOrigin
                       + (this.CellWidth * col * this.Xu)
                       + (this.CellHeight * row * this.Yu);
            }

            /// <summary>
            /// Position a texture rectangle in a "grid cell" of a regular grid with center in the
            /// GridOrigin, and basis xu,yu. The draw box is not rotated together with the grid basis
            /// and is aligned with screen axes.
            /// </summary>
            /// <param name="texture">Draw this.</param>
            /// <param name="camPos">Visible from here.</param>
            /// <param name="x">Column in grid.</param>
            /// <param name="y">Row in grid.</param>
            /// <param name="size">Square draw size (axis aligned).</param>
            /// <param name="screenRect">Output visible screen rect.</param>
            public void DrawStaticSquareOverlayGridTexture(Texture2D texture,
                                                           Vector3 camPos,
                                                           float x,
                                                           float y,
                                                           float size,
                                                           out Rect screenRect) {
                DrawGenericOverlayGridTexture(
                    texture: texture,
                    camPos: camPos,
                    x: x,
                    y: y,
                    width: size,
                    height: size,
                    canHover: false,
                    screenRect: out screenRect);
            }

            /// <summary>
            /// Position a texture rectangle in a "grid cell" of a regular grid with center in the
            /// GridOrigin, and basis xu,yu. The draw box is not rotated together with the grid basis
            /// and is aligned with screen axes.
            /// </summary>
            /// <param name="texture">Draw this.</param>
            /// <param name="camPos">Visible from here.</param>
            /// <param name="x">X position in grid.</param>
            /// <param name="y">Y position in grid.</param>
            /// <param name="width">Draw box size x.</param>
            /// <param name="height">Draw box size y.</param>
            /// <param name="canHover">Whether the icon is interacting with the mouse.</param>
            /// <param name="screenRect">Output visible screen rect.</param>
            /// <returns>Whether mouse hovers the icon.</returns>
            public bool DrawGenericOverlayGridTexture(Texture2D texture,
                                                      Vector3 camPos,
                                                      float x,
                                                      float y,
                                                      float width,
                                                      float height,
                                                      bool canHover,
                                                      out Rect screenRect) {
                Vector3 worldPos = this.GetPositionForRowCol(x, y);

                return Highlight.DrawGenericOverlayTexture(
                    texture,
                    camPos,
                    worldPos,
                    width,
                    height,
                    canHover,
                    out screenRect);
            }
        }

        public static void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                          ushort nodeId,
                                          bool warning = false,
                                          bool alpha = false) {
            DrawNodeCircle(
                cameraInfo: cameraInfo,
                nodeId: nodeId,
                color: ModUI.GetTrafficManagerTool().GetToolColor(warning, error: false),
                alpha: alpha);
            // TODO: Potentially we do not need to refer to a TrafficManagerTool object
        }

        /// <returns>the average half width of all connected segments</returns>
        private static float CalculateNodeRadius(ushort nodeId) {
            float sumHalfWidth = 0;
            int count = 0;
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    sumHalfWidth += segmentId.ToSegment().Info.m_halfWidth;
                    count++;
                }
            }

            return sumHalfWidth / count;
        }

        public static bool IsUndergroundMode =>
            InfoManager.instance.CurrentMode == InfoManager.InfoMode.Underground;

        public static bool IsNodeVisible(ushort nodeId) {
            return nodeId.ToNode().IsUnderground() == IsUndergroundMode;
        }

        /// <param name="subDivide">for sharp beziers subdivide should be set to work around CS inability to render sharp beziers.</param>
        public static void DrawBezier(
            RenderManager.CameraInfo cameraInfo,
            ref Bezier3 bezier,
            Color color,
            float size,
            float cutStart,
            float cutEnd,
            float minY,
            float maxY,
            bool renderLimits = true,
            bool alphaBlend = true,
            bool subDivide = false) {
            if (!subDivide) {
                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                RenderManager.instance.OverlayEffect.DrawBezier(
                    cameraInfo: cameraInfo,
                    color: color,
                    bezier: bezier,
                    size: size,
                    cutStart: cutStart,
                    cutEnd: cutEnd,
                    minY: minY,
                    maxY: maxY,
                    renderLimits: renderLimits,
                    alphaBlend: alphaBlend);
            } else {
                const float step = 0.5f;
                for (float t0 = 0; t0 < 1; t0 += step) {
                    float t1 = t0 + step;
                    Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                    RenderManager.instance.OverlayEffect.DrawBezier(
                        cameraInfo: cameraInfo,
                        color: color,
                        bezier: bezier.Cut(t0, t1),
                        size: size,
                        cutStart: cutStart,
                        cutEnd: cutEnd,
                        minY: minY,
                        maxY: maxY,
                        renderLimits: renderLimits,
                        alphaBlend: alphaBlend);
                }
            }
        }

        /// <summary>Draws the given texture on the network</summary>
        public static void DrawTextureAt(
            RenderManager.CameraInfo cameraInfo,
            Vector3 pos,
            Vector3 dir,
            Color color,
            Texture2D texture,
            float size,
            float minY,
            float maxY,
            bool renderLimits,
            bool alphaBlend = true) {
            dir = dir.normalized * size;
            Vector3 dir90 = dir.RotateXZ90CW();

            Quad3 quad = new Quad3 {
                a = pos - dir + dir90,
                b = pos + dir + dir90,
                c = pos + dir - dir90,
                d = pos - dir - dir90,
            };
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawQuad(
                cameraInfo,
                texture,
                color,
                quad,
                minY,
                maxY,
                renderLimits: renderLimits,
                alphaBlend: alphaBlend);
        }

        /// <summary>
        /// draw triangular (sides = 2, 2.24, 2.24) arrow head texture at the given <paramref name="t"/> of the <paramref name="bezier"/>
        /// </summary>
        public static void DrawArrowHead(
            RenderManager.CameraInfo cameraInfo,
            ref Bezier3 bezier,
            float t,
            Color color,
            Texture2D texture,
            float size,
            float minY,
            float maxY,
            bool renderLimits,
            bool alphaBlend = true) {
            Vector3 center = bezier.Position(t);
            Vector3 dir = bezier.Tangent(t).normalized * size;
            DrawTextureAt(cameraInfo, center, dir, color, texture, size, minY, maxY, renderLimits, alphaBlend);
        }

        /// <summary>
        /// draw triangular (sides = 2, 2.24, 2.24) arrow head at the given <paramref name="t"/> of the <paramref name="bezier"/>
        /// </summary>
        public static void DrawArrowHead(
            RenderManager.CameraInfo cameraInfo,
            ref Bezier3 bezier,
            float t,
            Color color,
            float size,
            float minY,
            float maxY,
            bool renderLimits,
            bool alphaBlend = false) {
            Vector3 center = bezier.Position(t);
            Vector3 dir = bezier.Tangent(t).normalized * size;
            Vector3 dir90 = dir.RotateXZ90CW();

            Quad3 quad = new Quad3 {
                a = center - dir + dir90,
                b = center - dir - dir90,
                c = center + dir,
                d = center + dir,
            };

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawQuad(
                cameraInfo,
                color,
                quad,
                minY,
                maxY,
                renderLimits: renderLimits,
                alphaBlend: alphaBlend);
        }

        public static void DrawShape(
            RenderManager.CameraInfo cameraInfo,
            Shape shape,
            Color color,
            Vector3 center,
            Vector3 tangent,
            float size,
            float minY,
            float maxY,
            bool renderLimits,
            bool alphaBlend) {
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            if (shape is Shape.Circle) {
                RenderManager.instance.OverlayEffect.DrawCircle(
                    cameraInfo: cameraInfo,
                    color: color,
                    center: center,
                    size: size,
                    minY: minY,
                    maxY: maxY,
                    renderLimits: renderLimits,
                    alphaBlend: alphaBlend);
            } else {
                size *= 0.5f;
                Vector3 dir = tangent * size;
                Vector3 dir90 = dir.RotateXZ90CW();
                Quad3 quad = shape switch {
                    Shape.Square => new Quad3 {
                        a = center - dir + dir90,
                        b = center + dir + dir90,
                        c = center + dir - dir90,
                        d = center - dir - dir90,
                    },
                    Shape.Diamond => new Quad3 {
                        a = center - dir,
                        b = center + dir90,
                        c = center + dir,
                        d = center - dir90,
                    },
                };
                RenderManager.instance.OverlayEffect.DrawQuad(
                    cameraInfo,
                    color,
                    quad,
                    minY,
                    maxY,
                    renderLimits: renderLimits,
                    alphaBlend: alphaBlend);
            }
        }

        public static void DrawNodeCircle(RenderManager.CameraInfo cameraInfo,
                                          ushort nodeId,
                                          Color color,
                                          bool alpha = false,
                                          bool overrideRenderLimits = false) {
            float r = CalculateNodeRadius(nodeId);
            Vector3 pos = Singleton<NetManager>.instance.m_nodes.m_buffer[nodeId].m_position;
            bool renderLimits = TerrainManager.instance.SampleDetailHeightSmooth(pos) > pos.y;
            DrawOverlayCircle(
                cameraInfo,
                color,
                pos,
                width: r * 2,
                alpha,
                renderLimits: renderLimits || overrideRenderLimits);
        }

        private static void DrawOverlayCircle(RenderManager.CameraInfo cameraInfo,
                                              Color color,
                                              Vector3 position,
                                              float width,
                                              bool alpha,
                                              bool renderLimits = false) {
            float overdrawHeight = renderLimits ? 0f : 5f;
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo,
                color,
                position,
                size: width,
                minY: position.y - overdrawHeight,
                maxY: position.y + overdrawHeight,
                renderLimits,
                alpha);
        }

        /// <summary>
        /// Draws a half sausage at segment end.
        /// </summary>
        /// <param name="cut">The length of the highlight [0~1] </param>
        /// <param name="bStartNode">Determines the direction of the half sausage.</param>
        public static void DrawCutSegmentEnd(RenderManager.CameraInfo cameraInfo,
                                             ushort segmentId,
                                             float cut,
                                             bool bStartNode,
                                             Color color,
                                             bool alpha = false) {
            if (segmentId == 0) {
                return;
            }

            ref NetSegment segment = ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId];
            float width = segment.Info.m_halfWidth;

            NetNode[] nodeBuffer = Singleton<NetManager>.instance.m_nodes.m_buffer;
            bool IsMiddle(ushort nodeId) => (nodeBuffer[nodeId].m_flags & NetNode.Flags.Middle) != 0;

            Bezier3 bezier;
            bezier.a = segment.m_startNode.ToNode().GetPositionOnTerrain();
            bezier.d = segment.m_endNode.ToNode().GetPositionOnTerrain();

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
                cameraInfo: cameraInfo,
                color: color,
                bezier: bezier,
                size: width * 2f,
                cutStart: bStartNode ? 0 : width,
                cutEnd: bStartNode ? width : 0,
                minY: -1f,
                maxY: 1280f,
                renderLimits: false,
                alphaBlend: alpha);
        }

        /// <summary>
        /// similar to NetTool.RenderOverlay()
        /// but with additional control over alphaBlend.
        /// </summary>
        internal static void DrawSegmentOverlay(
            RenderManager.CameraInfo cameraInfo,
            ushort segmentId,
            Color color,
            bool alphaBlend) {
            if (segmentId == 0) {
                return;
            }

            ref NetSegment segment = ref segmentId.ToSegment();
            ref NetNode startNode = ref segment.m_startNode.ToNode();
            ref NetNode endNode = ref segment.m_endNode.ToNode();

            Bezier3 bezier;
            bezier.a = startNode.GetPositionOnTerrain();
            bezier.d = endNode.GetPositionOnTerrain();

            NetSegment.CalculateMiddlePoints(
                startPos: bezier.a,
                startDir: segment.m_startDirection,
                endPos: bezier.d,
                endDir: segment.m_endDirection,
                smoothStart: startNode.IsMiddle(),
                smoothEnd: endNode.IsMiddle(),
                middlePos1: out bezier.b,
                middlePos2: out bezier.c);

            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            Singleton<RenderManager>.instance.OverlayEffect.DrawBezier(
                cameraInfo,
                color,
                bezier,
                size: segment.Info.m_halfWidth * 2f,
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

            // UI Scale should not affect the overlays (no multiplication by U.UIScaler.GetScale())
            float visibleScale = 1.0f / (worldPos - camPos).magnitude * 100f;
            width *= visibleScale;
            height *= visibleScale;

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

            if (hovered) {
                UIInput.MouseUsed();
            }

            return hovered;
        }
    }
}
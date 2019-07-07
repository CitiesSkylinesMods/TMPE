﻿namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using Texture;
    using UnityEngine;

    public abstract class SubTool {
        /// <summary>
        /// Parent Main Tool, one for all subtools
        /// </summary>
        internal TrafficManagerTool MainTool { get; }

        protected Texture2D WindowTexture {
            get {
                if (windowTexture == null) {
                    windowTexture =
                        TrafficManagerTool.AdjustAlpha(TextureResources.WindowBackgroundTexture2D,
                                                       MainTool.GetWindowAlpha());
                }

                return windowTexture;
            }
        }

        private Texture2D windowTexture = null;

        protected GUIStyle WindowStyle {
            get {
                if (windowStyle == null) {
                    windowStyle = CreateGUIStyle();
                }

                return windowStyle;
            }
        }

        private GUIStyle CreateGUIStyle() {
            return new GUIStyle {
                normal = {
                    background = WindowTexture,
                    textColor = Color.white
                },
                alignment = TextAnchor.UpperCenter,
                fontSize = 20,
                border = {
                    left = 4,
                    top = 41,
                    right = 4,
                    bottom = 8
                },
                overflow = {
                    bottom = 0,
                    top = 0,
                    right = 12,
                    left = 12
                },
                contentOffset = new Vector2(0, -44),
                padding = {
                    top = 55
                }
            };
        }

        private GUIStyle windowStyle = null;

        protected Texture2D BorderlessTexture {
            get {
                if (borderlessTexture == null) {
                    var color = new Color(0.5f, 0.5f, 0.5f, MainTool.GetWindowAlpha());
                    borderlessTexture = TrafficManagerTool.MakeTex(1, 1, color);
                }

                return borderlessTexture;
            }
        }

        private Texture2D borderlessTexture;

        // ReSharper disable once UnusedMember.Global
        protected GUIStyle BorderlessStyle {
            get {
                if (borderlessStyle == null) {
                    borderlessStyle = new GUIStyle {
                        normal = { background = BorderlessTexture },
                        alignment = TextAnchor.MiddleCenter,
                        border = {
                            bottom = 2,
                            top = 2,
                            right = 2,
                            left = 2
                        }
                    };
                }

                return borderlessStyle;
            }
        }

        private GUIStyle borderlessStyle;

        protected ushort HoveredNodeId => MainTool.HoveredNodeId;

        protected ushort HoveredSegmentId => MainTool.HoveredSegmentId;

        protected uint HoveredLaneId => MainTool.HoveredLaneId;

        protected ushort SelectedNodeId {
            get => MainTool.SelectedNodeId;
            set => MainTool.SelectedNodeId = value;
        }

        protected ushort SelectedSegmentId {
            get => MainTool.SelectedSegmentId;
            set => MainTool.SelectedSegmentId = value;
        }

        protected uint SelectedLaneId {
            get => MainTool.SelectedLaneId;
            set => MainTool.SelectedLaneId = value;
        }

        public SubTool(TrafficManagerTool mainTool) {
            MainTool = mainTool;
        }

        public void OnToolUpdate() {
            //OnLeftClickOverlay();
        }

        const float NATIVE_WIDTH = 1920;
        const float NATIVE_HEIGHT = 1200;

        /// <summary>
        /// Called whenever left mouse is clicked on the screen, over the GUI or the world
        /// </summary>
        public abstract void OnPrimaryClickOverlay();

        public virtual void OnSecondaryClickOverlay() { }

        /// <summary>
        /// Hovered node is changed to a different value than before (also 0)
        /// </summary>
        /// <param name="oldNodeId">The previously hovered node</param>
        /// <param name="newNodeId">New hovered node</param>
        internal virtual void OnChangeHoveredNode(ushort oldNodeId, ushort newNodeId) { }

        /// <summary>
        /// Hovered segment is changed to a different value than before (also 0)
        /// </summary>
        /// <param name="oldSegmentId">The previously hovered segment</param>
        /// <param name="newSegmentId">New hovered segment</param>
        internal virtual void OnChangeHoveredSegment(ushort oldSegmentId, ushort newSegmentId) { }

        /// <summary>
        /// Hovered lane is changed a different value than before (also 0)
        /// </summary>
        /// <param name="oldLaneId">The previously hovered lane</param>
        /// <param name="newLaneId">New hovered lane</param>
        internal virtual void OnChangeHoveredLane(uint oldLaneId, uint newLaneId) { }

        public virtual void OnToolGUI(Event e) {
            // set up scaling
            /*Vector2 resolution = UIView.GetAView().GetScreenResolution();
            float rx = resolution.x / nativeWidth;
            float ry = resolution.y / nativeHeight;
            GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(rx, ry, 1));*/
        }

        public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo);

        public virtual void Initialize() {
            borderlessTexture = null;
            borderlessStyle = null;
            windowTexture = null;
            windowStyle = null;
        }

        public virtual void Cleanup() { }

        public virtual void OnActivate() { }

        public virtual void RenderInfoOverlay(RenderManager.CameraInfo cameraInfo) { }

        public virtual void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) { }

        public virtual bool IsCursorInPanel() {
            return LoadingExtension.BaseUI.GetMenu().containsMouse
#if DEBUG
                   || LoadingExtension.BaseUI.GetDebugMenu().containsMouse
#endif
                ;
        }

        public virtual string GetTutorialKey() {
            return this.GetType().Name;
        }

        protected void DragWindow(ref Rect window) {
            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            window.x = Mathf.Clamp(window.x, 0, resolution.x - window.width);
            window.y = Mathf.Clamp(window.y, 0, resolution.y - window.height);

            bool primaryMouseDown = Input.GetMouseButton(0);
            if (primaryMouseDown) {
                GUI.DragWindow();
            }
        }
    }
}
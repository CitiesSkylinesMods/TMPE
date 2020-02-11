namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using UnityEngine;

    public abstract class SubTool {
        protected TrafficManagerTool MainTool { get; }

        private Texture2D WindowTexture {
            get {
                if (windowTexture_ == null) {
                    windowTexture_ = TrafficManagerTool.AdjustAlpha(
                        Textures.MainMenu.WindowBackground,
                        TrafficManagerTool.GetWindowAlpha());
                }

                return windowTexture_;
            }
        }

        private Texture2D windowTexture_;

        protected GUIStyle WindowStyle =>
            windowStyle_ ?? (windowStyle_ = new GUIStyle {
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
                                });

        private GUIStyle windowStyle_;

        private Texture2D BorderlessTexture {
            get {
                if (borderlessTexture_ == null) {
                    borderlessTexture_ = TrafficManagerTool.CreateSolidColorTexture(
                        1,
                        1,
                        new Color(0.5f, 0.5f, 0.5f, TrafficManagerTool.GetWindowAlpha()));
                }

                return borderlessTexture_;
            }
        }

        private Texture2D borderlessTexture_;

        protected GUIStyle BorderlessStyle =>
            borderlessStyle_ ?? (borderlessStyle_ = new GUIStyle {
                                        normal = { background = BorderlessTexture },
                                        alignment = TextAnchor.MiddleCenter,
                                        border = {
                                            bottom = 2,
                                            top = 2,
                                            right = 2,
                                            left = 2
                                        }
                                    });

        private GUIStyle borderlessStyle_;

        protected virtual Vector3 HitPos => TrafficManagerTool.HitPos;

        protected virtual Vector3 MousePosition => MainTool.MousePosition;

        protected virtual ushort HoveredNodeId {
            get => TrafficManagerTool.HoveredNodeId;
            set => TrafficManagerTool.HoveredNodeId = value;
        }

        protected virtual ushort HoveredSegmentId {
            get => TrafficManagerTool.HoveredSegmentId;
            set => TrafficManagerTool.HoveredSegmentId = value;
        }

        protected ushort SelectedNodeId {
            get => TrafficManagerTool.SelectedNodeId;
            set => TrafficManagerTool.SelectedNodeId = value;
        }

        protected ushort SelectedSegmentId {
            get => TrafficManagerTool.SelectedSegmentId;
            set => TrafficManagerTool.SelectedSegmentId = value;
        }

        public SubTool(TrafficManagerTool mainTool) {
            MainTool = mainTool;
        }

        [UsedImplicitly]
        public void OnToolUpdate() {
            // OnLeftClickOverlay();
        }

        /// <summary>
        /// Called whenever the
        /// </summary>
        public abstract void OnPrimaryClickOverlay();

        public virtual void OnSecondaryClickOverlay() { }

        public virtual void OnToolGUI(Event e) {
            // set up scaling
            // Vector2 resolution = UIView.GetAView().GetScreenResolution();
            // float rx = resolution.x / nativeWidth;
            // float ry = resolution.y / nativeHeight;
            // GUI.matrix = Matrix4x4.TRS(new Vector3(0, 0, 0), Quaternion.identity, new Vector3(rx, ry, 1));
        }

        public abstract void RenderOverlay(RenderManager.CameraInfo cameraInfo);

        public virtual void Initialize() {
            borderlessTexture_ = null;
            borderlessStyle_ = null;
            windowTexture_ = null;
            windowStyle_ = null;
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
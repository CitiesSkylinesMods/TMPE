namespace TrafficManager.UI {
    using System;
    using API.Util;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.U;
    using UnityEngine;

    [Obsolete("Refactor tools to the new TrafficManagerSubTool class instead of LegacySubTool")]
    public abstract class LegacySubTool : IObserver<ModUI.EventPublishers.UIOpacityNotification> {
        public LegacySubTool(TrafficManagerTool mainTool) {
            MainTool = mainTool;
            uiTransparencyUnbsubscriber_ = ModUI.Instance.Events.UiOpacity.Subscribe(this);
        }

        protected TrafficManagerTool MainTool { get; }
        protected GUILayoutOption[] EmptyOptionsArray = new GUILayoutOption[0];

        private Texture2D WindowTexture {
            get {
                if (windowTexture_ == null) {
                    windowTexture_ = TextureUtil.AdjustAlpha(
                        Textures.MainMenu.WindowBackground,
                        TrafficManagerTool.GetWindowAlpha());
                }

                return windowTexture_;
            }
        }

        private Texture2D windowTexture_;

        protected GUIStyle WindowStyle =>
            // ReSharper disable once ConvertToNullCoalescingCompoundAssignment
            windowStyle_ ??
            (windowStyle_
                 = new GUIStyle {
                                    normal = {
                                                 background = WindowTexture,
                                                 textColor = Color.white,
                                             },
                                    alignment = TextAnchor.UpperCenter,
                                    fontSize = 20,
                                    border = {
                                                 left = 4,
                                                 top = 41,
                                                 right = 4,
                                                 bottom = 8,
                                             },
                                    overflow = {
                                                   bottom = 0,
                                                   top = 0,
                                                   right = 12,
                                                   left = 12,
                                               },
                                    contentOffset = new Vector2(0, -44),
                                    padding = {
                                                  top = 55,
                                              },
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
            // ReSharper disable once ConvertToNullCoalescingCompoundAssignment
            borderlessStyle_
            ?? (borderlessStyle_
                    = new GUIStyle {
                                       normal = { background = BorderlessTexture },
                                       alignment = TextAnchor.MiddleCenter,
                                       border = {
                                                    bottom = 2,
                                                    top = 2,
                                                    right = 2,
                                                    left = 2,
                                                },
                                   });

        private GUIStyle borderlessStyle_;

        private readonly IDisposable uiTransparencyUnbsubscriber_;

        public CursorInfo OverrideCursor;

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

        [UsedImplicitly]
        public void OnToolUpdate() {
            // OnLeftClickOverlay();
        }

        /// <summary>
        /// Called whenever the mouse left click happened on the world, while the tool was active.
        /// </summary>
        public abstract void OnPrimaryClickOverlay();

        /// <summary>
        /// Called whenever the mouse right click happened on the world, while the tool was active.
        /// </summary>
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

        public virtual void OnActivate() {
        }

        public virtual void OnDestroy() {
            if (borderlessTexture_) {
                UnityEngine.Object.Destroy(borderlessTexture_);
                borderlessTexture_ = null;
            }
            borderlessStyle_ = null;

            if (windowTexture_) {
                UnityEngine.Object.Destroy(windowTexture_);
                windowTexture_ = null;
            }
            windowStyle_ = null;
            uiTransparencyUnbsubscriber_.Dispose();
        }

        /// <summary>
        /// Renders current situation overlay, called while the tool is not active to assist
        /// other tools.
        /// </summary>
        /// <param name="cameraInfo">The camera.</param>
        public virtual void RenderOverlayForOtherTools(RenderManager.CameraInfo cameraInfo) { }

        public virtual void ShowGUIOverlay(ToolMode toolMode, bool viewOnly) { }

        public virtual bool IsCursorInPanel() {
            return ModUI.Instance.GetMenu().containsMouse
#if DEBUG
                   || ModUI.Instance.GetDebugMenu().containsMouse
#endif
                ;
        }

        public virtual string GetTutorialKey() {
            return this.GetType().Name;
        }

        protected void DragWindow(ref Rect window) {
            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            window.x = Mathf.Clamp(window.x, 0, UIScaler.MaxWidth - window.width);
            window.y = Mathf.Clamp(window.y, 0, UIScaler.MaxHeight - window.height);

            bool primaryMouseDown = Input.GetMouseButton(0);
            if (primaryMouseDown) {
                GUI.DragWindow();
            }
        }

        public void OnUpdate(ModUI.EventPublishers.UIOpacityNotification subject) {
            Texture2D windowTexture = windowTexture_;
            windowTexture_ = null;
            windowStyle_ = null; // rebuild style with new window texture
            if (windowTexture) {
                UnityEngine.Object.Destroy(windowTexture);
            }
            Texture2D borderlessTexture = borderlessTexture_;
            borderlessTexture_ = null;
            borderlessStyle_ = null; // rebuild style with new borderless texture
            if (borderlessTexture) {
                UnityEngine.Object.Destroy(borderlessTexture);
            }
        }
    }
}
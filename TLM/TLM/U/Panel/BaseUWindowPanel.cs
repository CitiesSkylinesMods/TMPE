namespace TrafficManager.U.Panel {
    using System;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using TrafficManager.API.Util;
    using TrafficManager.State;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Base panel for all TMPE UI panels which represent UI window roots (not in-window panels).
    /// Aware of some things such as rescaling on resolution or UI scale change.
    /// </summary>
    public abstract class BaseUWindowPanel
        : UIPanel,
          ISmartSizableControl,
          IObserver<ModUI.EventPublishers.UIScaleNotification>,
          IObserver<ModUI.EventPublishers.UIOpacityNotification> {
        private readonly UResizerConfig resizerConfig_ = new();

        /// <summary>On destroy this will unsubscribe from the UI Scale observable.</summary>
        [UsedImplicitly]
        private IDisposable uiScaleUnsubscriber_;

        /// <summary>On destroy this will unsubscribe from the UI Transparency observable.</summary>
        [UsedImplicitly]
        private IDisposable uiTransparencyUnsubscriber_;

        /// <summary>Call this from your form constructor to enable tracking UI Scale changes.</summary>
        public override void Start() {
            base.Start();
            uiScaleUnsubscriber_ = ModUI.Instance.Events.UiScale.Subscribe(this);
            uiTransparencyUnsubscriber_ = ModUI.Instance.Events.UiOpacity.Subscribe(this);
            atlas = TextureUtil.Ingame;
        }

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }

        /// <summary>Called by UResizer for every control before it is to be 'resized'.</summary>
        public virtual void OnBeforeResizerUpdate() { }

        /// <summary>Called by UResizer for every control after it is to be 'resized'.</summary>
        public virtual void OnAfterResizerUpdate() { }

        /// <summary>Invoke rescaling handler, because possibly it has the new size now.</summary>
        /// <param name="previousResolution">Previous.</param>
        /// <param name="currentResolution">New.</param>
        protected override void OnResolutionChanged(Vector2 previousResolution,
                                                    Vector2 currentResolution) {
            UResizer.UpdateControl(this); // force window relayout
        }

        /// <summary>
        /// Impl. <see cref="IObserver{T}"/> for UI Scale changes.
        /// Called from ModUI when UI scale slider in General tab was modified.
        /// </summary>
        /// <param name="optionsEvent">New UI scale.</param>
        public void OnUpdate(ModUI.EventPublishers.UIScaleNotification optionsEvent) {
            UResizer.UpdateControl(this); // force window relayout
        }

        /// <summary>
        /// Impl. <see cref="IObserver{T}"/> for UI Scale changes.
        /// Called from ModUI when UI scale slider in General tab was modified.
        /// </summary>
        /// <param name="optionsEvent">Event with the new UI opacity.</param>
        public void OnUpdate(ModUI.EventPublishers.UIOpacityNotification optionsEvent) {
            // incoming range: 0..100 convert to 0..1f
            SetOpacity(optionsEvent.Opacity);
        }

        /// <summary>
        /// Rewrite window color to become less or more opaque.
        /// NOTE: If the call has no effect, look for some other code rewriting the color after your call!
        /// </summary>
        /// <param name="opacity">Range 0..100, where 100 is solid and 0 invisible.</param>
        internal void SetOpacity(U.UOpacityValue opacity) {
            Color32 modified = this.color;
            modified.a = opacity.GetOpacityByte();
            this.color = modified;
        }

        /// <summary>
        /// Creates a drag handle gameobject child for this window, which can be enabled or disabled
        /// by the caller.
        /// </summary>
        /// <returns>New UIDragHandle object.</returns>
        public UIDragHandle CreateDragHandle() {
            GameObject dragHandler = new GameObject("TMPE_DragHandler");
            dragHandler.transform.parent = transform;
            dragHandler.transform.localPosition = Vector3.zero;

            return dragHandler.AddComponent<UIDragHandle>();
        }

        /// <summary>Make this panel use dark gray generic background and opacity from the GUI SavedGameOptions.Instance.</summary>
        internal void GenericBackgroundAndOpacity() {
            // the GenericPanel sprite is silver, make it dark
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);

            SetOpacity(UOpacityValue.FromOpacity(0.01f * GlobalConfig.Instance.Main.GuiOpacity));
        }

        /// <summary>Called by UnityEngine when component gets destroyed</summary>
        public override void OnDestroy() {
            uiScaleUnsubscriber_?.Dispose();
            uiTransparencyUnsubscriber_?.Dispose();
            base.OnDestroy();
        }

        /// <summary>
        /// Moves the center of the window to a position in the world (e.g. node).
        /// </summary>
        public void MoveCenterToWorldPosition(Vector3 worldPos) {
            GeometryUtil.WorldToScreenPoint(worldPos, out Vector3 screenPos);
            screenPos /= GetUIView().inputScale;
            screenPos -= (Vector3)size * 0.5f;
            relativePosition = screenPos.RoundToInt();
        }
    }
}

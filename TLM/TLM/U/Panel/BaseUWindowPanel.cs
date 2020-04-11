namespace TrafficManager.U.Panel {
    using System;
    using ColossalFramework.UI;
    using JetBrains.Annotations;
    using TrafficManager.API.Util;
    using TrafficManager.State;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;
    using UnityEngine;

    /// <summary>
    /// Base panel for all TMPE UI panels which represent UI window roots (not in-window panels).
    /// Aware of some things such as rescaling on resolution or UI scale change.
    /// </summary>
    public abstract class BaseUWindowPanel
        : UIPanel,
          ISmartSizableControl,
          IObserver<ModUI.UIScaleNotification>,
          IObserver<ModUI.UITransparencyNotification>
    {
        private UResizerConfig resizerConfig_ = new UResizerConfig();

        /// <summary>On destroy this will unsubscribe from the UI Scale observable.</summary>
        [UsedImplicitly]
        private IDisposable uiScaleUnbsubscriber_;

        /// <summary>On destroy this will unsubscribe from the UI Transparency observable.</summary>
        [UsedImplicitly]
        private IDisposable uiTransparencyUnbsubscriber_;

        /// <summary>Call this from your form constructor to enable tracking UI Scale changes.</summary>
        public override void Start() {
            base.Start();
            uiScaleUnbsubscriber_ = ModUI.Instance.UiScaleObservable.Subscribe(this);
            uiTransparencyUnbsubscriber_ = ModUI.Instance.UiTransparencyObservable.Subscribe(this);
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
            // Call resize on all controls and recalculate again
            UResizer.UpdateControlRecursive(this, null);
        }

        /// <summary>
        /// Impl. <see cref="IObserver{T}"/> for UI Scale changes.
        /// Called from ModUI when UI scale slider in General tab was modified.
        /// </summary>
        /// <param name="optionsEvent">New UI scale.</param>
        public void OnUpdate(ModUI.UIScaleNotification optionsEvent) {
            // Call resize on all controls and recalculate again
            UResizer.UpdateControlRecursive(this, null);
        }

        /// <summary>
        /// Impl. <see cref="IObserver{T}"/> for UI Scale changes.
        /// Called from ModUI when UI scale slider in General tab was modified.
        /// </summary>
        /// <param name="uiScale">New UI scale.</param>
        public void OnUpdate(ModUI.UITransparencyNotification optionsEvent) {
            // incoming range: 0..100 convert to 0..1f
            SetTransparency(optionsEvent.NewTransparency);
        }

        /// <summary>
        /// Rewrite window color to become transparent.
        /// NOTE: If the call has no effect, look for some other code rewriting the color after your call!
        /// </summary>
        /// <param name="guiTransparencyPercent">Range 0..100.</param>
        internal void SetTransparency(float guiTransparencyPercent) {
            Color32 modified = this.color;

            // converting range 0..100f to range 0..255
            modified.a = (byte)(255f * 0.01f * guiTransparencyPercent);

            this.color = modified;
        }
    }
}
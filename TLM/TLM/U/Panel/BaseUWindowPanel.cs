namespace TrafficManager.U.Panel {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.API.Util;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI;
    using UnityEngine;

    /// <summary>
    /// Base panel for all TMPE UI panels which represent UI window roots (not in-window panels).
    /// Aware of some things such as rescaling on resolution or UI scale change.
    /// </summary>
    public abstract class BaseUWindowPanel
        : UIPanel, ISmartSizableControl, IObserver<ModUI.UIScaleNotification>
    {
        private UResizerConfig resizerConfig_ = new UResizerConfig();

        /// <summary>On destroy this will unsubscribe from the UI Scale observable.</summary>
        private IDisposable uiScaleUnbsubscriber_;

        /// <summary>Call this from your form constructor to enable tracking UI Scale changes.</summary>
        public override void Start() {
            base.Start();
            uiScaleUnbsubscriber_ = ModUI.Instance.UiScaleObservable.Subscribe(this);
        }

        public UResizerConfig GetResizerConfig() {
            return resizerConfig_;
        }

        /// <summary>Called on screen resolution and UI scale change.</summary>
        public abstract void OnRescaleRequested();

        /// <summary>Invoke rescaling handler, because possibly it has the new size now.</summary>
        /// <param name="previousResolution">Previous.</param>
        /// <param name="currentResolution">New.</param>
        protected override void OnResolutionChanged(Vector2 previousResolution,
                                                    Vector2 currentResolution) {
            this.OnRescaleRequested();
        }

        /// <summary>
        /// Impl. <see cref="IObserver{T}"/> for UI Scale changes.
        /// Called from ModUI when UI scale slider in General tab was modified.
        /// </summary>
        /// <param name="uiScale">New UI scale</param>
        public void OnUpdate(ModUI.UIScaleNotification uiScale) {
            this.OnRescaleRequested();
        }
    }
}
namespace TrafficManager.U.Panel {
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// Base panel for all TMPE UI panels which represent UI window roots (not in-window panels).
    /// Aware of some things such as rescaling on resolution or UI scale change.
    /// </summary>
    public abstract class BaseUWindowPanel : UIPanel {
        /// <summary>Called on screen resolution and UI scale change.</summary>
        public abstract void OnRescaleRequested();

        /// <summary>Invoke rescaling handler, because possibly it has the new size now.</summary>
        /// <param name="previousResolution">Previous.</param>
        /// <param name="currentResolution">New.</param>
        protected override void OnResolutionChanged(Vector2 previousResolution,
                                                    Vector2 currentResolution) {
            this.OnRescaleRequested();
        }
    }
}
namespace TrafficManager.UI.SubTools.SpeedLimits {
    using TrafficManager.U;
    using UnityEngine;

    /// <summary>
    /// Implements new style Speed Limits palette and speed limits management UI.
    /// </summary>
    public class SpeedLimitsTool
        : TrafficManagerSubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        //private const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        public const ushort KMPH_STEP = 10;

        //private const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>
        /// Finite State machine for the tool. Represents current UI state for Lane Arrows.
        /// </summary>
        private Util.GenericFsm<State, Trigger> fsm_;

        /// <summary>Tool states.</summary>
        private enum State {
            /// <summary>Clicking a segment will override speed limit on all lanes.
            /// Holding Alt will temporarily show the Defaults.
            /// </summary>
            EditSegments,

            /// <summary>Clicking a road type will override default.</summary>
            EditDefaults,

            /// <summary>The user requested to leave the tool.</summary>
            ToolDisabled,
        }

        /// <summary>Events which trigger state transitions.</summary>
        private enum Trigger {
            /// <summary>Mode 1 - Segment Edit Mode - clicked.</summary>
            SegmentsButtonClick,

            /// <summary>Mode 2 - Edit Defaults - clicked.</summary>
            DefaultsButtonClick,

            /// <summary>Right mouse has been clicked.</summary>
            RightMouseClick,
        }

        /// <summary>If exists, contains tool panel floating on the selected node.</summary>
        private SpeedLimitsWindow Window { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedLimitsTool"/> class.
        /// </summary>
        /// <param name="mainTool">Reference to the parent maintool.</param>
        public SpeedLimitsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
        }

        /// <summary>
        /// Creates FSM ready to begin editing. Or recreates it when ESC is pressed
        /// and the tool is canceled.
        /// </summary>
        /// <returns>The new FSM in the initial state.</returns>
        private Util.GenericFsm<State, Trigger> InitFiniteStateMachine() {
            var fsm = new Util.GenericFsm<State, Trigger>(State.EditSegments);

            fsm.Configure(State.EditSegments)
               // .OnEntry(this.OnEnterSelectState)
               // .OnLeave(this.OnLeaveSelectState)
               .TransitionOnEvent(Trigger.DefaultsButtonClick, State.EditDefaults)
               .TransitionOnEvent(Trigger.RightMouseClick, State.ToolDisabled);

            fsm.Configure(State.EditDefaults)
               .TransitionOnEvent(Trigger.SegmentsButtonClick, State.EditSegments)
               .TransitionOnEvent(Trigger.RightMouseClick, State.ToolDisabled);

            fsm.Configure(State.ToolDisabled)
               .OnEntry(
                   () => {
                       // We are done here, leave the tool.
                       // This will result in this.DeactivateTool being called.
                       ModUI.Instance.MainMenu.ClickToolButton(ToolMode.LaneArrows);
                   });

            return fsm;
        }

        private static string T(string key) => Translation.SpeedLimits.Get(key);

        public override void ActivateTool() {
            // Create a generic self-sizing window with padding of 4px.
            void SetupFn(UiBuilder<SpeedLimitsWindow> b) {
                b.SetPadding(UConst.UIPADDING);
                b.Control.SetupControls(b);
            }
            this.Window = UiBuilder<SpeedLimitsWindow>.CreateWindow<SpeedLimitsWindow>(setupFn: SetupFn);
            this.fsm_ = InitFiniteStateMachine();
        }

        public override void DeactivateTool() {
            Object.Destroy(this.Window);
            this.Window = null;
            this.fsm_ = null;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
        }

        public override void OnToolLeftClick() {
        }

        public override void OnToolRightClick() {
        }

        public override void UpdateEveryFrame() {
        }

        /// <summary>Called when the tool must update onscreen keyboard/mouse hints.</summary>
        public void UpdateOnscreenDisplayPanel() {
        }
    } // end class
}

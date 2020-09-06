namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using GenericGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.UI.MainMenu.OSD;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.UI.SubTools.SpeedLimits.Overlay;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Implements new style Speed Limits palette and speed limits management UI.
    /// </summary>
    public class SpeedLimitsTool
        : TrafficManagerSubTool,
          UI.MainMenu.IOnscreenDisplayProvider
    {
        public const ushort LOWER_KMPH = 10;
        public const ushort UPPER_KMPH = 140;
        public const ushort KMPH_STEP = 10;

        public const ushort LOWER_MPH = 5;
        public const ushort UPPER_MPH = 90;
        public const ushort MPH_STEP = 5;

        /// <summary>
        /// Currently selected speed limit on the limits palette.
        /// units less than 0: invalid (not selected)
        /// units = 0: no limit.
        /// </summary>
        public SpeedValue CurrentPaletteSpeedLimit = new SpeedValue(-1f);

        /// <summary>
        /// Will show and edit speed limits for each lane.
        /// This is toggled by the tool window button or by holding Ctrl temporarily.
        /// </summary>
        private bool showLimitsPerLane_;

        private bool ShowLimitsPerLane => showLimitsPerLane_ ^ Shortcuts.ControlIsPressed;

        /// <summary>
        /// True if user is editing road defaults. False if user is editing speed limit overrides.
        /// </summary>
        private bool editDefaultsMode_ = false;

        /// <summary>Will edit entire road between two junctions by holding Shift.</summary>
        private bool GetMultiSegmentMode() => Shortcuts.ShiftIsPressed;

        /// <summary>
        /// Finite State machine for the tool. Represents current UI state for Lane Arrows.
        /// </summary>
        private Util.GenericFsm<State, Trigger> fsm_;

        private SpeedLimitsOverlay.DrawArgs overlayDrawArgs_ = SpeedLimitsOverlay.DrawArgs.Create();
        private SpeedLimitsOverlay overlay_;

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
            overlay_ = new SpeedLimitsOverlay(mainTool: this.MainTool);
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
            RecreateToolWindow();

            this.fsm_ = InitFiniteStateMachine();
            MainTool.RequestOnscreenDisplayUpdate();
        }

        /// <summary>Drop tool window if it existed, and create again.</summary>
        internal void RecreateToolWindow() {
            // Create a generic self-sizing window with padding of 4px.
            void SetupFn(UiBuilder<SpeedLimitsWindow> b) {
                b.SetPadding(UConst.UIPADDING);
                b.Control.SetupControls(builder: b, parentTool: this);
            }

            if (this.Window) {
                this.Window.Hide();

                // The constructor of new window will try to delete it by name, but we can help it
                UnityEngine.Object.Destroy(this.Window);
            }

            this.Window = UiBuilder<SpeedLimitsWindow>.CreateWindow<SpeedLimitsWindow>(setupFn: SetupFn);

            //--------------------------------------------------
            // Click handlers for the window are located here
            // to have insight into SpeedLimits Tool internals
            //--------------------------------------------------
            this.Window.SegmentLaneModeToggleButton.uOnClick = (component, evt) => {
                this.showLimitsPerLane_ = !this.showLimitsPerLane_;
                MainTool.RequestOnscreenDisplayUpdate();
                // TODO: update button active/texture
            };
            this.Window.EditDefaultsModeButton.uOnClick = (component, evt) => {
                this.editDefaultsMode_ = !this.editDefaultsMode_;
                MainTool.RequestOnscreenDisplayUpdate();
                // TODO: update button active/texture
            };

            // Additional action to toggling MPH/kmph: Also to refresh the window
            this.Window.ToggleMphButton.uOnClick = (component, param) => {
                this.RecreateToolWindow();
            };

        }

        public override void DeactivateTool() {
            Object.Destroy(this.Window);
            this.Window = null;
            this.fsm_ = null;
        }

        /// <summary>Render overlay segments/lanes in non-GUI mode, as overlays.</summary>
        public override void RenderActiveToolOverlay(RenderManager.CameraInfo cameraInfo) {
            CreateOverlayDrawArgs(interactive: true);

            // Draw hovered lanes or segments
            overlay_.RenderHelperGraphics(cameraInfo: cameraInfo, args: this.overlayDrawArgs_);
        }

        /// <summary>Render overlay speed limit signs in GUI mode.</summary>
        public override void RenderActiveToolOverlay_GUI() {
            CreateOverlayDrawArgs(interactive: true);

            // Draw the clickable speed limit signs
            overlay_.ShowSigns_GUI(args: this.overlayDrawArgs_);
        }

        /// <summary>Copies important values for rendering the overlay into its args struct.</summary>
        /// <param name="interactive">True if icons will be clickable.</param>
        private void CreateOverlayDrawArgs(bool interactive) {
            overlayDrawArgs_.ClearHovered();

            overlayDrawArgs_.InteractiveSigns = interactive;
            overlayDrawArgs_.MultiSegmentMode = this.GetMultiSegmentMode();
            overlayDrawArgs_.ShowLimitsPerLane = this.ShowLimitsPerLane;
            overlayDrawArgs_.ShowDefaultsMode = this.editDefaultsMode_;
            overlayDrawArgs_.ShowOtherPerLaneModeTemporary = interactive && Shortcuts.AltIsPressed;
        }

        /// <summary>Render overlay for other tool modes, if speed limits overlay is on.</summary>
        /// <param name="cameraInfo">The camera.</param>
        public override void RenderGenericInfoOverlay(RenderManager.CameraInfo cameraInfo) {
            // No non-GUI overlays for other tools, we draw signs in the *_GUI variant
        }

        /// <summary>Called in the GUI mode for GUI.DrawTexture.</summary>
        /// <param name="cameraInfo">The camera.</param>
        public override void RenderGenericInfoOverlay_GUI() {
            if (!Options.speedLimitsOverlay && !MassEditOverlay.IsActive) {
                return;
            }

            CreateOverlayDrawArgs(interactive: false);

            // Draw the NON-clickable speed limit signs
            overlay_.ShowSigns_GUI(args: this.overlayDrawArgs_);
        }

        public override void OnToolLeftClick() {
            if (this.Window.containsMouse) {
                return; // no click in the window
            }

            // Go through recently rendered overlay speedlimit handles, which had mouse over them
            // Hovering multiple speed limits handles at once should set limits on multiple roads
            if (this.ShowLimitsPerLane) {
                SetSpeedLimitTarget target = this.editDefaultsMode_
                    ? SetSpeedLimitTarget.LaneDefault
                    : SetSpeedLimitTarget.LaneOverride;

                foreach (var h in overlayDrawArgs_.HoveredLaneHandles) {
                    // per lane
                    h.Click(
                        action: SetSpeedLimitAction.SetSpeed(this.CurrentPaletteSpeedLimit),
                        multiSegmentMode: this.GetMultiSegmentMode(),
                        target: target);
                }
            } else {
                // per segment
                SetSpeedLimitTarget target = this.editDefaultsMode_
                    ? SetSpeedLimitTarget.SegmentDefault
                    : SetSpeedLimitTarget.SegmentOverride;

                foreach (var h in overlayDrawArgs_.HoveredSegmentHandles) {
                    h.Click(
                        action: SetSpeedLimitAction.SetSpeed(this.CurrentPaletteSpeedLimit),
                        multiSegmentMode: this.GetMultiSegmentMode(),
                        target: target);
                }
            }

            this.overlayDrawArgs_.ClearHovered();
        }

        public override void OnToolRightClick() {
        }

        public override void UpdateEveryFrame() {
        }

        /// <summary>Called when the tool must update onscreen keyboard/mouse hints.</summary>
        public void UpdateOnscreenDisplayPanel() {
            //     t: "Hold [Alt] to see default speed limits temporarily",
            //     t: "Hold [Ctrl] to see per lane limits temporarily",
            //     t: "Hold [Shift] to modify entire road between two junctions",
            string toggleDefaultStr =
                this.editDefaultsMode_
                    ? T("SpeedLimits.Alt:See speed limits overrides temporarily")
                    : T("SpeedLimits.Alt:See default speed limits temporarily");
            string togglePerLaneStr =
                this.ShowLimitsPerLane
                    ? T("SpeedLimits.Ctrl:See speed limits per segment temporarily")
                    : T("SpeedLimits.Ctrl:See speed limits per lane temporarily");
            var items = new List<OsdItem> {
                new MainMenu.OSD.ModeDescription(localizedText: T("SpeedLimits.OSD:Select")),
                new MainMenu.OSD.HoldModifier(
                    alt: true,
                    localizedText: toggleDefaultStr),
                new MainMenu.OSD.HoldModifier(
                    ctrl: true,
                    localizedText: togglePerLaneStr),
                new MainMenu.OSD.HoldModifier(
                    shift: true,
                    localizedText: T("SpeedLimits.Shift:Modify road between two junctions")),
            };
            OnscreenDisplay.Display(items: items);
        }

        internal static void SetSpeedLimit(LanePos lane, SetSpeedLimitAction action) {
            ushort segmentId = lane.laneId.ToLane().m_segment;
            SpeedLimitManager.Instance.SetLaneSpeedLimit(
                segmentId: segmentId,
                laneIndex: lane.laneIndex,
                laneInfo: segmentId.ToSegment().Info.m_lanes[lane.laneIndex],
                laneId: lane.laneId,
                action: action);
        }

        /// <summary>When speed palette button clicked, touch all buttons forcing them to refresh.</summary>
        public void OnPaletteButtonClicked(SpeedValue speed) {
            this.CurrentPaletteSpeedLimit = speed;

            // Deactivate all palette buttons and highlight one
            Window.UpdatePaletteButtonsOnClick();
        }
    } // end class
}

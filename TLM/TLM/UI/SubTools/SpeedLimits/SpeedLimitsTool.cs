namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using GenericGameBridge.Service;
    using System;
    using System.Collections.Generic;
    using CitiesGameBridge.Service;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.UI.SubTools.SpeedLimits.Overlay;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Implements new style Speed Limits palette and speed limits management UI.
    /// </summary>
    public class SpeedLimitsTool
        : TrafficManagerSubTool,
          UI.MainMenu.IOnscreenDisplayProvider {
        private SetSpeedLimitAction selectedActionKmph_ = SetSpeedLimitAction.ResetToDefault();

        private SetSpeedLimitAction selectedActionMph_ = SetSpeedLimitAction.ResetToDefault();

        /// <summary>
        /// Gets currently selected speed limit on the limits palette.
        /// units less than 0: invalid (not selected)
        /// units = 0: no limit.
        /// </summary>
        public SetSpeedLimitAction SelectedAction {
            get =>
                GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                    ? this.selectedActionMph_
                    : this.selectedActionKmph_;
            private set {
                if (GlobalConfig.Instance.Main.DisplaySpeedLimitsMph) {
                    this.selectedActionMph_ = value;
                } else {
                    this.selectedActionKmph_ = value;
                }
            }
        }

        /// <summary>
        /// Will show and edit speed limits for each lane.
        /// This is toggled by the tool window button. Use <see cref="GetShowLimitsPerLane" /> to
        /// retrieve the value affected by the Ctrl button state.
        /// </summary>
        private bool showLimitsPerLane_;

        /// <summary>Whether limits per lane are to be shown.</summary>
        /// <returns>Gets <see cref="showLimitsPerLane_"/> but also holding Ctrl would invert it.</returns>
        private bool GetShowLimitsPerLane() => this.showLimitsPerLane_ ^ Shortcuts.ControlIsPressed;

        /// <summary>
        /// True if user is editing road defaults. False if user is editing speed limit overrides.
        /// </summary>
        private bool editDefaultsMode_;

        /// <summary>Will edit entire road between two junctions by holding Shift.</summary>
        private bool GetMultiSegmentMode() => Shortcuts.ShiftIsPressed;

        private SpeedLimitsOverlay.DrawArgs overlayDrawArgs_ = SpeedLimitsOverlay.DrawArgs.Create();
        private SpeedLimitsOverlay overlay_;

        /// <summary>Gets or sets the <see cref="ToolWindow"/> floating on the selected node.</summary>
        private ToolWindow Window { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedLimitsTool"/> class.
        /// </summary>
        /// <param name="mainTool">Reference to the parent maintool.</param>
        public SpeedLimitsTool(TrafficManagerTool mainTool)
            : base(mainTool) {
            this.overlay_ = new SpeedLimitsOverlay(mainTool: this.MainTool);
        }

        private static string T(string key) => Translation.SpeedLimits.Get(key);

        public override void ActivateTool() {
            this.RecreateToolWindow();

            // this.fsm_ = InitFiniteStateMachine();
            this.MainTool.RequestOnscreenDisplayUpdate();
        }

        /// <summary>Drop tool window if it existed, and create again.</summary>
        internal void RecreateToolWindow() {
            // Create a generic self-sizing window with padding of 4px.
            if (this.Window) {
                this.Window.Hide();

                // The constructor of new window will try to delete it by name, but we can help it
                UnityEngine.Object.Destroy(this.Window);
            }

            UBuilder b = new UBuilder();
            this.Window = b.CreateWindow<ToolWindow>();
            this.Window.SetPadding(UPadding.Default);
            this.Window.SetupControls(b, parentTool: this);

            this.UpdateModeInfoLabel();

            //--------------------------------------------------
            // Click handlers for the window are located here
            // to have insight into SpeedLimits Tool internals
            //--------------------------------------------------
            this.Window.modeButtonsPanel_.SegmentLaneModeToggleButton.SetupToggleButton(
                onClickFun: this.OnClickSegmentLaneModeButton,
                isActiveFun: _ => this.showLimitsPerLane_);

            this.Window.modeButtonsPanel_.EditDefaultsModeButton.SetupToggleButton(
                onClickFun: this.OnClickEditDefaultsButton,
                isActiveFun: _ => this.editDefaultsMode_);

            this.Window.modeButtonsPanel_.ToggleMphButton.uOnClick = this.OnClickToggleMphButton;
            this.UpdateCursorTooltip();
        }

        private void UpdateModeInfoLabel() {
            this.Window.modeDescriptionWrapPanel_.UpdateModeInfoLabel(
                multiSegmentMode: this.GetMultiSegmentMode(),
                editDefaults: this.editDefaultsMode_,
                showLanes: this.GetShowLimitsPerLane());
            this.Window.ForceUpdateLayout(); // The info label can get tall, need to move everything
        }

        /// <summary>
        /// Additional action to toggling MPH/kmph: Also to refresh the window
        /// The MPH toggling happens inside the custom button class MphToggleButton.
        /// </summary>
        private void OnClickToggleMphButton(UIComponent component, UIMouseEventParameter param) {
            this.RecreateToolWindow();
        }

        private void OnClickEditDefaultsButton(UIComponent component, UIMouseEventParameter evt) {
            this.editDefaultsMode_ = !this.editDefaultsMode_;
            this.MainTool.RequestOnscreenDisplayUpdate();

            this.UpdateCursorTooltip();

            this.UpdateModeInfoLabel();
        }

        private void OnClickSegmentLaneModeButton(UIComponent component,
                                                  UIMouseEventParameter evt) {
            this.showLimitsPerLane_ = !this.showLimitsPerLane_;
            this.MainTool.RequestOnscreenDisplayUpdate();

            this.UpdateCursorTooltip();
            this.UpdateModeInfoLabel();
        }

        private void UpdateCursorTooltip() {
            this.Window.cursorTooltip_.SetTooltip(this.SelectedAction.ToString());
        }

        public override void DeactivateTool() {
            UnityEngine.Object.Destroy(this.Window);
            this.Window = null;
        }

        /// <summary>Render overlay segments/lanes in non-GUI mode, as overlays.</summary>
        public override void RenderActiveToolOverlay(RenderManager.CameraInfo cameraInfo) {
            this.CreateOverlayDrawArgs(interactive: true);

            // Draw hovered lanes or segments
            this.overlay_.RenderHelperGraphics(cameraInfo: cameraInfo, args: this.overlayDrawArgs_);
        }

        /// <summary>Render overlay speed limit signs in GUI mode.</summary>
        public override void RenderActiveToolOverlay_GUI() {
            // TODO: Cache camera
            // if (!LastCachedCamera.Equals(currentCamera)) {
            //     // cache visible segments
            //     LastCachedCamera = currentCamera;
            //     CachedVisibleSegmentIds.Clear();
            //     ...
            //     for ... {
            //          CachedVisibleSegmentIds.Add((ushort)segmentId);
            //     } // end for all segments
            // }
            this.CreateOverlayDrawArgs(interactive: true);

            // Draw the clickable speed limit signs
            this.overlay_.ShowSigns_GUI(args: this.overlayDrawArgs_);
        }

        /// <summary>Copies important values for rendering the overlay into its args struct.</summary>
        /// <param name="interactive">True if icons will be clickable.</param>
        private void CreateOverlayDrawArgs(bool interactive) {
            this.overlayDrawArgs_.ClearHovered();

            this.overlayDrawArgs_.UiWindowRects.Clear();
            this.overlayDrawArgs_.UiWindowRects.Add(this.Window.GetScreenRectInGuiSpace());

            if (ModUI.Instance.MainMenu != null) { // can be null if no tool selected
                this.overlayDrawArgs_.UiWindowRects.Add(
                    ModUI.Instance.MainMenu.GetScreenRectInGuiSpace());
            }

            this.overlayDrawArgs_.Mouse = this.GetMouseForOverlay();
            this.overlayDrawArgs_.InteractiveSigns = interactive;
            this.overlayDrawArgs_.MultiSegmentMode = this.GetMultiSegmentMode();
            this.overlayDrawArgs_.ShowLimitsPerLane = this.GetShowLimitsPerLane();
            this.overlayDrawArgs_.ShowDefaultsMode = this.editDefaultsMode_;
            this.overlayDrawArgs_.ShowOtherPerLaneModeTemporary = interactive && Shortcuts.AltIsPressed;
        }

        /// <summary>Create value of null (if mouse is over some essential UI window) or return
        /// mouse coords.</summary>
        private Vector2? GetMouseForOverlay() {
            if (this.Window.containsMouse || ModUI.Instance.MainMenu.containsMouse) {
                return null;
            }

            return Event.current.mousePosition;
        }

        /// <summary>Render overlay for other tool modes, if speed limits overlay is on.</summary>
        /// <param name="cameraInfo">The camera.</param>
        public override void RenderGenericInfoOverlay(RenderManager.CameraInfo cameraInfo) {
            // No non-GUI overlays for other tools, we draw signs in the *_GUI variant
        }

        /// <summary>Called in the GUI mode for GUI.DrawTexture.</summary>
        public override void RenderGenericInfoOverlay_GUI() {
            if (!Options.speedLimitsOverlay && !MassEditOverlay.IsActive) {
                return;
            }

            this.CreateOverlayDrawArgs(interactive: false);

            // Draw the NON-clickable speed limit signs
            this.overlay_.ShowSigns_GUI(args: this.overlayDrawArgs_);
        }

        /// <inheritdoc/>
        public override void OnToolLeftClick() {
            if (this.Window == null || this.Window.containsMouse) {
                return; // no click in the window
            }

            // Go through recently rendered overlay speedlimit handles, which had mouse over them
            // Hovering multiple speed limits handles at once should set limits on multiple roads
            if (this.GetShowLimitsPerLane()) {
                SetSpeedLimitTarget target = this.editDefaultsMode_
                                                 ? SetSpeedLimitTarget.LaneDefault
                                                 : SetSpeedLimitTarget.LaneOverride;

                foreach (var h in this.overlayDrawArgs_.HoveredLaneHandles) {
                    // per lane
                    h.Click(
                        action: this.SelectedAction,
                        multiSegmentMode: this.GetMultiSegmentMode(),
                        target: target);
                }
            } else {
                // per segment
                SetSpeedLimitTarget target = this.editDefaultsMode_
                                                 ? SetSpeedLimitTarget.SegmentDefault
                                                 : SetSpeedLimitTarget.SegmentOverride;

                foreach (var h in this.overlayDrawArgs_.HoveredSegmentHandles) {
                    h.Click(
                        action: this.SelectedAction,
                        multiSegmentMode: this.GetMultiSegmentMode(),
                        target: target);
                }
            }

            this.overlayDrawArgs_.ClearHovered();
        }

        /// <inheritdoc/>
        public override void OnToolRightClick() {
            ModUI.Instance.MainMenu.ClickToolButton(ToolMode.SpeedLimits); // deactivate
        }

        /// <inheritdoc/>
        public override void UpdateEveryFrame() {
        }

        /// <summary>Called when the tool must update onscreen keyboard/mouse hints.</summary>
        public void UpdateOnscreenDisplayPanel() {
            // t: "Hold [Alt] to see default speed limits temporarily",
            // t: "Hold [Ctrl] to see per lane limits temporarily",
            // t: "Hold [Shift] to modify entire road between two junctions",
            string toggleDefaultStr =
                this.editDefaultsMode_
                    ? T("SpeedLimits.Alt:See speed limits overrides temporarily")
                    : T("SpeedLimits.Alt:See default speed limits temporarily");
            string togglePerLaneStr =
                this.GetShowLimitsPerLane()
                    ? T("SpeedLimits.Ctrl:See speed limits per segment temporarily")
                    : T("SpeedLimits.Ctrl:See speed limits per lane temporarily");
            var items = new List<MainMenu.OSD.OsdItem> {
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
            MainMenu.OSD.OnscreenDisplay.Display(items: items);
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
        public void OnPaletteButtonClicked(SetSpeedLimitAction action) {
            this.SelectedAction = action;
            this.Window.cursorTooltip_.SetTooltip(action.ToString());

            // Deactivate all palette buttons and highlight one
            this.Window.UpdatePaletteButtonsOnClick();
        }
    } // end class
}
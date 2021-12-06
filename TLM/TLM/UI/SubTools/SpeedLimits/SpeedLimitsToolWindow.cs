namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>Implements U window for Speed Limits palette and speed defaults.</summary>
    internal partial class SpeedLimitsToolWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_SpeedLimits";

        /// <summary>
        /// Stores copy of Mph config variable. Only used to monitor whether window buttons
        /// match the current global config MPH setting. If they don't, then on tool re-activation
        /// the button palette must be rebuilt.
        /// </summary>
        public bool DisplaySpeedLimitsMph;

        /// <summary>Stores window title label also overlaid with the drag handle. </summary>
        private ULabel windowTitleLabel_;

        /// <summary>Window drag handle.</summary>
        private UIDragHandle dragHandle_;

        private UITextureAtlas guiAtlas_;

        /// <summary>Wrapper panel around mode text (above the palette).</summary>
        internal ModeDescriptionPanel modeDescriptionWrapPanel_;

        /// <summary>Small left panel containing 3 mode buttons.</summary>
        internal ModeButtonsPanel modeButtonsPanel_;

        /// <summary>Contains speed limits button palette.</summary>
        internal PalettePanel palettePanel_;

        /// <summary>Floating label following the mouse cursor.</summary>
        internal U.UFloatingTooltip cursorTooltip_;

        private SpeedLimitsTool parentTool_;

        /// <summary>Called by Unity on instantiation once when the game is running.</summary>
        public override void Start() {
            base.Start();

            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);
            this.GenericBackgroundAndOpacity();

            this.position = new Vector3(
                GlobalConfig.Instance.Main.SpeedLimitsWindowX,
                GlobalConfig.Instance.Main.SpeedLimitsWindowY);
            this.ClampToScreen();
        }

        /// <summary>Populate the window using UIBuilder of the window panel.</summary>
        /// <param name="builder">The root builder of this window.</param>
        public void SetupControls(UBuilder builder, SpeedLimitsTool parentTool) {
            this.parentTool_ = parentTool;

            // "Speed Limits - Kilometers per Hour"
            // "Showing speed limit overrides per road segment."
            // [ Lane/Segment ] [ 10 20 30 40 50 ... 120 130 140 Max Reset]
            // [ Edit Default ] |   |  |  |  |  |   |   |   |   |   |     |
            // [_MPH/KM_______] [___+__+__+__+__+...+___+___+___+___+_____]

            // Goes first on top of the window
            SetupControls_TitleBar(builder);

            // Vertical panel goes under the titlebar
            SetupControls_ModeButtons(builder);

            // Text below for "Current mode: " and "Hold Alt, hold Shift, etc..."
            modeDescriptionWrapPanel_ =
                builder.Panel<ModeDescriptionPanel>(
                    parent: this,
                    stack: UStackMode.None);
            modeDescriptionWrapPanel_.SetupControls(window: this, builder, parentTool);

            // Palette: Goes right of the modebuttons panel
            palettePanel_ = builder.Panel<PalettePanel>(
                parent: this,
                stack: UStackMode.None);
            palettePanel_.SetupControls(window: this, builder, parentTool);

            // palette was built for the current configured MPH/KM display
            this.DisplaySpeedLimitsMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;

            cursorTooltip_ = builder.Label<UFloatingTooltip>(
                parent: this,
                t: string.Empty,
                stack: UStackMode.None);
            cursorTooltip_.SetTooltip(); // this will hide it, and update it after setup is done

            // Force buttons resize and show the current speed limit on the palette
            this.UpdatePaletteButtonsOnClick();
        }

        /// <summary>
        /// A flag to prevent multiple timed invocations when the window is dragged around.
        /// </summary>
        private bool queuedClampToScreen;

        /// <summary>Creates a draggable label with current unit (mph or km/h).</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_TitleBar(UBuilder builder) {
            string unitTitle = string.Format(
                format: "{0} - {1}",
                Translation.SpeedLimits.Get("Window.Title:Speed Limits"),
                GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                    ? Translation.SpeedLimits.Get("Miles per hour")
                    : Translation.SpeedLimits.Get("Kilometers per hour"));

            // The label will be repositioned to the top of the parent
            this.windowTitleLabel_ = builder.Label_(
                parent: this,
                t: unitTitle,
                stack: UStackMode.Below);

            this.dragHandle_ = this.CreateDragHandle();

            // On window drag - clamp to screen and then save in the config
            this.eventPositionChanged += (_, value) => {
                GlobalConfig.Instance.Main.SpeedLimitsWindowX = (int)value.x;
                GlobalConfig.Instance.Main.SpeedLimitsWindowY = (int)value.y;

                if (!queuedClampToScreen) {
                    // Prevent multiple invocations by setting a flag
                    queuedClampToScreen = true;
                    Invoke(eventName: "OnPeriodicClampToScreen", 2.0f);
                }
            };
        }

        public void OnPeriodicClampToScreen() {
            // Called after move to return the window to the screen rect
            this.ClampToScreen();
            queuedClampToScreen = false;
        }

        /// <inheritdoc/>
        public override void OnBeforeResizerUpdate() {
            if (this.dragHandle_ != null) {
                // Drag handle is manually resized to the label width in OnAfterResizerUpdate.
                this.dragHandle_.size = Vector2.one;
            }

            // shrink title label to allow window to resize down. After resize its set back to full width
            this.windowTitleLabel_.width = 100;
        }

        /// <summary>Called by UResizer for every control to be 'resized'.</summary>
        public override void OnAfterResizerUpdate() {
            if (this.dragHandle_ != null) {
                this.dragHandle_.size = this.windowTitleLabel_.size;

                // Push the window back into screen if the label/draghandle are partially offscreen
                UIUtil.ClampToScreen(
                    window: this,
                    alwaysVisible: windowTitleLabel_);
            }
            this.dragHandle_.size = new Vector2(this.width - (2 * UConst.UIPADDING), this.windowTitleLabel_.height);
        }

        /// <summary>Create mode buttons panel on the left side.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_ModeButtons(UBuilder builder) {
            modeButtonsPanel_ = builder.Panel<ModeButtonsPanel>(parent: this);
            modeButtonsPanel_.SetupControls(window: this, builder);
        }

        /// <summary>Format string to display under the speed limit button with miles per hour.</summary>
        /// <param name="speed">The speed.</param>
        /// <returns>The string formatted with miles: MM MPH.</returns>
        private static string ToMphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.SpeedLimits.Get("Unlimited")
                       : speed.ToMphPrecise().ToString();
        }

        /// <summary>Format string to display under the speed limit button with km/hour.</summary>
        /// <param name="speed">The speed.</param>
        /// <returns>The string formatted formatted with km: NN km/h.</returns>
        private static string ToKmphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                       ? Translation.SpeedLimits.Get("Unlimited")
                       : speed.ToKmphPrecise().ToIntegerString();
        }

        private UITextureAtlas GetUiAtlas() {
            if (guiAtlas_ != null) {
                return guiAtlas_;
            }

            // Create base atlas with backgrounds and no foregrounds
            var futureAtlas = new AtlasBuilder(
                atlasName: "SpeedLimits_Atlas",
                loadingPath: "SpeedLimits",
                sizeHint: new IntVector2(512));

            // Merge names of all button sprites atlasBuilder
            foreach (string prefix in new[]
                { "MphToggle", "EditSegments", "EditLanes", "EditDefaults" })
            {
                ButtonSkin skin = ButtonSkin.CreateSimple(
                                                foregroundPrefix: prefix,
                                                backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                            .CanActivate(background: false)
                                            .CanHover(foreground: false);

                // Create keysets for lane arrow button icons and merge to the shared atlas
                skin.UpdateAtlasBuilder(
                    atlasBuilder: futureAtlas,
                    spriteSize: new IntVector2(50));
            }

            // Load actual graphics into an atlas
            return futureAtlas.CreateAtlas();
        }

        /// <summary>
        /// Forces speedlimit palette buttons to be updated, and active button also is highlighted.
        /// </summary>
        public void UpdatePaletteButtonsOnClick() {
            foreach (SpeedLimitPaletteButton b in this.palettePanel_.PaletteButtons) {
                b.ApplyButtonSkin();
                b.UpdateSpeedlimitButton();
            }

            UResizer.UpdateControl(this); // force window relayout
        }

        protected override void OnResolutionChanged(Vector2 previousResolution,
                                                    Vector2 currentResolution) {
            if (this.parentTool_ != null) {
                this.parentTool_.OnResolutionChanged();
            }
            base.OnResolutionChanged(previousResolution, currentResolution);
        }
    }
    // end class
}
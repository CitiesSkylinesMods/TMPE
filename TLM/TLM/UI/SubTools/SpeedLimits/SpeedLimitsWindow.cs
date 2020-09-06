namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>Implements U window for Speed Limits palette and speed defaults.</summary>
    internal class SpeedLimitsWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_SpeedLimits";

        /// <summary>Stores window title label also overlaid with the drag handle. </summary>
        private ULabel titleLabel_;

        /// <summary>Window drag handle.</summary>
        private UIDragHandle dragHandle_;

        /// <summary>UI button which toggles per-segment or per-lane speed limits.</summary>
        public UButton SegmentLaneModeToggleButton { get; set; }

        private UITextureAtlas guiAtlas_;
        private List<SpeedLimitPaletteButton> paletteButtons_ = new List<SpeedLimitPaletteButton>();

        public UButton EditDefaultsModeButton { get; set; }
        public MphToggleButton ToggleMphButton { get; set; }

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
        public void SetupControls(UiBuilder<SpeedLimitsWindow> builder, SpeedLimitsTool parentTool) {
            //-------------------------------------
            // [Speed Limits - Kilometers per Hour]
            // [ Mode 1 ] [ 10 20 30 40 50 ... 120 130 140 ]
            // [ Mode 2 ] Hold Alt...
            //-------------------------------------
            // Goes first on top of the window
            SetupControls_TitleBar(builder);

            // Vertical panel goes under the titlebar
            SetupControls_ModeButtons(builder);

            // Goes right of the modebuttons panel
            SetupControls_SpeedPalette(builder, parentTool);

            // Text below for "Hold Alt, hold Shift, etc..."
            // SetupControls_InfoRow(builder);

            // Force buttons resize and show the current speed limit on the palette
            this.UpdatePaletteButtonsOnClick();
        }

        /// <summary>Creates a draggable label with current unit (mph or km/h).</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_TitleBar(UiBuilder<SpeedLimitsWindow> builder) {
            string unitTitle = string.Format(
                format: "{0} - {1}",
                Translation.SpeedLimits.Get("Window.Title:Speed Limits"),
                GlobalConfig.Instance.Main.DisplaySpeedLimitsMph
                    ? Translation.SpeedLimits.Get("Miles per hour")
                    : Translation.SpeedLimits.Get("Kilometers per hour"));

            // The label will be repositioned to the top of the parent
            this.titleLabel_ = builder.Label(t: unitTitle, stack: UStackMode.Below);
            this.dragHandle_ = this.CreateDragHandle();

            // On window drag - clamp to screen and then save in the config
            this.eventPositionChanged += (UIComponent component, Vector2 value) => {
                this.ClampToScreen();
                GlobalConfig.Instance.Main.SpeedLimitsWindowX = (int)value.x;
                GlobalConfig.Instance.Main.SpeedLimitsWindowY = (int)value.y;
            };
        }

        /// <inheritdoc/>
        public override void OnBeforeResizerUpdate() {
            if (this.dragHandle_ != null) {
                // Drag handle is manually resized to the label width in OnAfterResizerUpdate.
                this.dragHandle_.size = Vector2.one;
            }
        }

        /// <summary>Called by UResizer for every control to be 'resized'.</summary>
        public override void OnAfterResizerUpdate() {
            if (this.dragHandle_ != null) {
                this.dragHandle_.size = this.titleLabel_.size;

                // Push the window back into screen if the label/draghandle are partially offscreen
                UIUtil.ClampToScreen(
                    window: this,
                    alwaysVisible: titleLabel_);
            }
        }

        /// <summary>Create mode buttons panel on the left side.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_ModeButtons(UiBuilder<SpeedLimitsWindow> builder) {
            void ButtonpanelSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_ModesPanel";

            using (var modePanelB = builder.ChildPanel<UPanel>(ButtonpanelSetupFn)) {
                void ButtonpanelResizeFn(UResizer r) {
                    r.Stack(mode: UStackMode.Below, stackRef: this.titleLabel_);
                    r.FitToChildren();
                }

                modePanelB.ResizeFunction(ButtonpanelResizeFn);

                Vector2 buttonSize = new Vector2(50f, 50f);

                //----------------
                // Edit Segments/Lanes mode button
                //----------------
                using (var b = modePanelB.FixedSizeButton<UButton>(
                    text: string.Empty,
                    tooltip: "Edit segments. Click to edit lanes.",
                    size: buttonSize,
                    stack: UStackMode.Below))
                {
                    this.SegmentLaneModeToggleButton = b.Control;
                    b.Control.atlas = GetUiAtlas();
                    b.Control.Skin = ButtonSkin.CreateDefaultNoBackground("EditSegments");
                }

                //----------------
                // Edit Defaults mode button
                //----------------
                using (var defaultsB = modePanelB.FixedSizeButton<UButton>(
                    text: string.Empty,
                    tooltip: "Default speed limits per road type",
                    size: buttonSize,
                    stack: UStackMode.Below)) {
                    this.EditDefaultsModeButton = defaultsB.Control;
                }

                //----------------
                // MPH/Kmph switch
                //----------------
                bool displayMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                using (var b = modePanelB.FixedSizeButton<MphToggleButton>(
                    text: string.Empty,
                    tooltip: displayMph
                        ? Translation.SpeedLimits.Get("Miles per hour")
                        : Translation.SpeedLimits.Get("Kilometers per hour"),
                    size: buttonSize,
                    stack: UStackMode.Below))
                {
                    b.Control.atlas = GetUiAtlas();
                    b.Control.Skin = ButtonSkin.CreateDefaultNoBackground("MphToggle");
                    this.ToggleMphButton = b.Control;
                }
            }
        }

        /// <summary>Create speeds palette based on the current options choices.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_SpeedPalette(UiBuilder<SpeedLimitsWindow> builder, SpeedLimitsTool parentTool) {
            void PaletteSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_PalettePanel";
            using (var palettePanelB = builder.ChildPanel<UPanel>(PaletteSetupFn)) {
                palettePanelB.SetPadding(UConst.UIPADDING);

                void PaletteResizeFn(UResizer r) {
                    r.Stack(mode: UStackMode.ToTheRight);
                    r.FitToChildren();
                }

                palettePanelB.ResizeFunction(PaletteResizeFn);
                bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;

                // Fill with buttons
                // [ 10 20 30 ... 120 130 140 0(no limit) ]
                //-----------------------------------------
                // the Current Selected Speed is highlighted
                List<SpeedValue> values =
                    PaletteGenerator.AllSpeedLimits(SpeedUnit.CurrentlyConfigured);
                values.Add(new SpeedValue(0)); // add last item: no limit

                this.paletteButtons_.Clear();

                foreach (var speedValue in values) {
                    SpeedLimitPaletteButton nextButton = SetupControls_SpeedPalette_Button(
                        parentTool: parentTool,
                        builder: palettePanelB,
                        showMph: showMph,
                        speedValue: speedValue);
                    this.paletteButtons_.Add(nextButton);
                }
            } // end palette panel
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

        private SpeedLimitPaletteButton
            SetupControls_SpeedPalette_Button(UiBuilder<UPanel> builder,
                                              bool showMph,
                                              SpeedValue speedValue,
                                              SpeedLimitsTool parentTool)
        {
            int speedInteger = showMph
                ? speedValue.ToMphRounded(SpeedLimitsTool.MPH_STEP).Mph
                : speedValue.ToKmphRounded(SpeedLimitsTool.KMPH_STEP).Kmph;
            SpeedLimitPaletteButton control;

            //--- uncomment below to create a label under each button ---
            // Create vertical combo:
            // [ 100  ]
            //  65 mph
            // Create a small panel which stacks together with other button panels horizontally
            using (var buttonPanelB = builder.ChildPanel<U.UPanel>(
                p => p.name = $"{GAMEOBJECT_NAME}_Button_{speedInteger}")) {
                buttonPanelB.ResizeFunction(
                    r => {
                        r.Stack(UStackMode.ToTheRight);
                        r.FitToChildren();
                    });

                using (var buttonB = buttonPanelB.Button<SpeedLimitPaletteButton>()) {
                    control = buttonB.Control;
                    control.text = speedInteger == 0 ? "X" : speedInteger.ToString();
                    control.textHorizontalAlignment = UIHorizontalAlignment.Center;

                    control.AssignedValue = speedValue; // button must know its speed value
                    // The click events will be routed via the parent tool OnPaletteButtonClicked
                    control.ParentTool = parentTool;

                    buttonB.SetStacking(UStackMode.NewRowBelow);

                    // Width will be overwritten in SpeedLimitPaletteButton.UpdateSpeedLimitButton
                    buttonB.SetFixedSize(
                        new Vector2(SpeedLimitPaletteButton.DEFAULT_WIDTH,
                                    SpeedLimitPaletteButton.DEFAULT_HEIGHT));
                }

                //--- uncomment below to create a label under each button ---
                // Other speed unit info label
                string otherUnit = showMph
                    ? ToKmphPreciseString(speedValue)
                    : ToMphPreciseString(speedValue);

                ULabel label =
                    control.AltUnitsLabel =
                        buttonPanelB.Label(t: otherUnit, stack: UStackMode.Below);

                label.width = SpeedLimitPaletteButton.DEFAULT_WIDTH * 1.5f;
                label.textAlignment = UIHorizontalAlignment.Center;
                label.GetResizerConfig().ContributeToBoundingBox = false; // parent ignore our width
            } // end containing mini panel

            return control;
        }

        private UITextureAtlas GetUiAtlas() {
            if (guiAtlas_ != null) {
                return guiAtlas_;
            }

            // Create base atlas with backgrounds and no foregrounds
            var futureAtlas = new U.AtlasBuilder();

            // Merge names of all foreground sprites for 3 directions into atlasKeySet
            foreach (string prefix in new[] { "MphToggle", "EditSegments", }) {
                ButtonSkin skin = ButtonSkin.CreateDefaultNoBackground(prefix);

                // Create keysets for lane arrow button icons and merge to the shared atlas
                skin.UpdateAtlasBuilder(
                    atlasBuilder: futureAtlas,
                    spriteSize: new IntVector2(50));
            }

            // Load actual graphics into an atlas
            return futureAtlas.CreateAtlas(
                atlasName: "SpeedLimits_Atlas",
                loadingPath: "SpeedLimits",
                atlasSizeHint: new IntVector2(512));
        }

        /// <summary>
        /// Forces speedlimit palette buttons to be updated, and active button also is highlighted.
        /// </summary>
        public void UpdatePaletteButtonsOnClick() {
            foreach (SpeedLimitPaletteButton b in this.paletteButtons_) {
                b.UpdateButtonImage();
                b.UpdateSpeedlimitButton();
            }

            UResizer.UpdateControl(this); // force window relayout
        }
    }

    // end class
}
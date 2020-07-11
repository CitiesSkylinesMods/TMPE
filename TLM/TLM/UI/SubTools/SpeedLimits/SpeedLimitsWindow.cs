namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System;
    using System.Collections.Generic;
    using ColossalFramework.UI;
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

        /// <summary>
        /// Currently selected speed limit on the limits palette.
        /// units &lt; 0: invalid (something must be selected)
        /// units == 0: no limit
        /// </summary>
        [NonSerialized]
        public SpeedValue CurrentPaletteSpeedLimit = new SpeedValue(-1f);

        /// <summary>Called by Unity on instantiation once when the game is running.</summary>
        public override void Start() {
            base.Start();
            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);

            // the GenericPanel sprite is silver, make it dark
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
            this.SetOpacityFromGuiOpacity();
        }

        /// <summary>Populate the window using UIBuilder of the window panel.</summary>
        /// <param name="builder">The root builder of this window.</param>
        public void SetupControls(UiBuilder<SpeedLimitsWindow> builder) {
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
            SetupControls_SpeedPalette(builder);

            // Text below for "Hold Alt, hold Shift, etc..."
            SetupControls_InfoRow(builder);
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
        }

        /// <inheritdoc/>
        public override void OnBeforeResizerUpdate() {
            if (this.dragHandle_ != null) {
                // Drag handle is manually resized to the label width, but when the form is large,
                // the handle prevents it from shrinking. So shrink now, size properly after.
                this.dragHandle_.size = Vector2.one;
            }
        }

        /// <summary>Called by UResizer for every control to be 'resized'.</summary>
        public override void OnAfterResizerUpdate() {
            if (this.dragHandle_ != null) {
                this.dragHandle_.size = this.titleLabel_.size;

                // Push the window back into screen if the label/draghandle are partially offscreen
                U.UIUtil.ClampToScreen(
                    window: this,
                    alwaysVisible: titleLabel_);
            }
        }

        /// <summary>Create mode buttons panel on the left side.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_ModeButtons(UiBuilder<SpeedLimitsWindow> builder) {
            void ButtonpanelSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_ModesPanel";

            using (var modePanelB = builder.ChildPanel<U.UPanel>(ButtonpanelSetupFn)) {
                void ButtonpanelResizeFn(UResizer r) {
                    r.Stack(mode: UStackMode.Below, stackRef: this.titleLabel_);
                    r.FitToChildren();
                }

                modePanelB.ResizeFunction(ButtonpanelResizeFn);

                // Edit Segments/Lanes mode button
                modePanelB.FixedSizeButton<U.UButton>(
                        text: string.Empty,
                        tooltip: "Override speed limits for one road or segment",
                        size: new Vector2(40f, 40f),
                        stack: UStackMode.Below);

                // Edit Defaults mode button
                modePanelB.FixedSizeButton<U.UButton>(
                    text: string.Empty,
                    tooltip: "Edit default speed limits for all roads of that type",
                    size: new Vector2(40f, 40f),
                    stack: UStackMode.Below);

                // MPH/Kmph switch
                modePanelB.FixedSizeButton<U.UButton>(
                    text: "km/h",
                    tooltip: "Kilometers per hour",
                    size: new Vector2(40f, 40f),
                    stack: UStackMode.Below);
            }
        }

        /// <summary>Create speeds palette based on the current options choices.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_SpeedPalette(UiBuilder<SpeedLimitsWindow> builder) {
            void PaletteSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_PalettePanel";
            using (var palettePanelB = builder.ChildPanel<U.UPanel>(PaletteSetupFn)) {
                palettePanelB.SetPadding(U.UConst.UIPADDING);

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
                foreach (var speedValue in values) {
                    SetupControls_SpeedPalette_Button(
                        builder: palettePanelB,
                        showMph: showMph,
                        speedValue: speedValue);
                }
            } // end palette panel
        }

        private void SetupControls_SpeedPalette_Button(UiBuilder<UPanel> builder,
                                                       bool showMph,
                                                       SpeedValue speedValue) {
            int speedInteger = showMph
                ? speedValue.ToMphRounded(SpeedLimitsTool.MPH_STEP).Mph
                : speedValue.ToKmphRounded(SpeedLimitsTool.KMPH_STEP).Kmph;
            // Speeds over 100 have wider buttons
            float buttonWidth = speedInteger >= 100 ? 40f : 30f;

            bool isSelected = FloatUtil.NearlyEqual(
                this.CurrentPaletteSpeedLimit.GameUnits,
                speedValue.GameUnits);

            //--- uncomment below to create a label under each button ---
            // void ButtonSetupFn(UPanel p) => p.name = $"{GAMEOBJECT_NAME}_Button_{speedInteger}";
            // Create a small panel which stacks together with other button panels horizontally
            // using (var buttonPanelB = builder.ChildPanel<U.UPanel>(ButtonSetupFn)) {
            //     buttonPanelB.ResizeFunction(r => {
            //         r.Stack(UStackMode.ToTheRight);
            //         r.FitToChildren();
            //     });

            // Create vertical combo:
            // [ 100  ]
            //  65 mph
            using (var buttonB = builder.Button<U.UButton>()) {
                buttonB.Control.text = speedInteger == 0 ? "X" : speedInteger.ToString();
                buttonB.Control.textHorizontalAlignment = UIHorizontalAlignment.Center;

                buttonB.SetStacking(UStackMode.ToTheRight);
                buttonB.SetFixedSize(new Vector2(buttonWidth, 60f));

                if (isSelected) {
                    buttonB.Control.textScale = 2.0f;
                }
            }

            //--- uncomment below to create a label under each button ---
            //     // Other speed unit info label
            //     string otherUnit = showMph
            //          ? ToKmphPreciseString(speedValue)
            //          : ToMphPreciseString(speedValue);
            //     var label = buttonPanelB.Label(t: otherUnit, stack: UStackMode.Below);
            //     label.width = buttonWidth;
            //     label.textAlignment = UIHorizontalAlignment.Center;
            // } // end containing mini panel
        }

        /// <summary>
        /// Create info row under the speed buttons, which prompts to hold Alt, Shift etc.
        /// </summary>
        /// <param name="builder">The UI builder to use.</param>
        private void SetupControls_InfoRow(UiBuilder<SpeedLimitsWindow> builder) {
            builder.Label(
                t: "Hold Alt to modify default speed limits temporarily",
                stack: UStackMode.Below);
        }

        /// <summary>Converts speed value to string with units.</summary>
        /// <param name="speed">Speed value.</param>
        /// <returns>Formatted String "N MPH".</returns>
        private static string ToMphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                ? Translation.SpeedLimits.Get("Unlimited")
                : speed.ToMphPrecise().ToString();
        }

        /// <summary>Converts speed value to string with units.</summary>
        /// <param name="speed">Speed value.</param>
        /// <returns>Formatted String "N km/h".</returns>
        private static string ToKmphPreciseString(SpeedValue speed) {
            return FloatUtil.IsZero(speed.GameUnits)
                ? Translation.SpeedLimits.Get("Unlimited")
                : speed.ToKmphPrecise().ToString();
        }
    } // end class
}
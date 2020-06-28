namespace TrafficManager.UI.SubTools.SpeedLimits {
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
                U.UIUtil.ClampToScreen(window: this,
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
                using (var editmodeBtnB = modePanelB.Button<U.UButton>()) {
                    editmodeBtnB.Control.tooltip = "Override speed limits for one road or segment";

                    void EditmodeResizeFn(UResizer r) {
                        r.Width(UValue.FixedSize(40f));
                        r.Height(UValue.FixedSize(40f));
                        r.Stack(UStackMode.Below);
                    }

                    editmodeBtnB.ResizeFunction(EditmodeResizeFn);
                }

                // Edit Defaults mode button
                using (var defaultsmodeBtnB = modePanelB.Button<U.UButton>()) {
                    defaultsmodeBtnB.Control.tooltip =
                        "Edit default speed limits for all roads of that type";

                    void DefaultsmodeResizeFn(UResizer r) {
                        r.Width(UValue.FixedSize(40f));
                        r.Height(UValue.FixedSize(40f));
                        r.Stack(UStackMode.Below);
                    }

                    defaultsmodeBtnB.ResizeFunction(DefaultsmodeResizeFn);
                }
            }
        }

        /// <summary>Create speeds palette based on the current options choices.</summary>
        /// <param name="builder">The UI builder to use.</param>
        private static void SetupControls_SpeedPalette(UiBuilder<SpeedLimitsWindow> builder) {
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
                List<SpeedValue> values = PaletteGenerator.AllSpeedLimits(SpeedUnit.CurrentlyConfigured);
                values.Add(new SpeedValue(0)); // add last item: no limit
                foreach (var speedValue in values) {
                    using (var buttonB = palettePanelB.Button<U.UButton>()) {
                        int speedInteger = showMph
                            ? speedValue.ToMphRounded(SpeedLimitsTool.MPH_STEP).Mph
                            : speedValue.ToKmphRounded(SpeedLimitsTool.KMPH_STEP).Kmph;

                        buttonB.Control.text = speedInteger.ToString();
                        buttonB.Control.textHorizontalAlignment = UIHorizontalAlignment.Center;

                        // Speeds over 100 have wider buttons
                        float buttonWidth = speedInteger >= 100 ? 40f : 30f;

                        buttonB.ResizeFunction(r => {
                            r.Width(UValue.FixedSize(buttonWidth));
                            r.Height(UValue.FixedSize(60f));
                            r.Stack(UStackMode.ToTheRight);
                        });
                    }
                }
                // Opposite speed unit info label
                // buttonB.Control.text = showMph
                //     ? ToKmphPreciseString(speedValue)
                //     : ToMphPreciseString(speedValue);
            }
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
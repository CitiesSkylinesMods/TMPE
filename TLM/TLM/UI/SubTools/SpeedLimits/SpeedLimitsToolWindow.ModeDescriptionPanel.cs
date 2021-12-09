namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Text;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.Localization;
    using UnityEngine;

    internal partial class SpeedLimitsToolWindow {
        internal class ModeDescriptionPanel : UPanel {
            /// <summary>Describes what user currently sees. Can use color codes for keyboard hints.</summary>
            internal ULabel ModeDescriptionLabel;

            /// <summary>
            /// Sets up two stacked labels: for mode description (what the user sees) and for hint.
            /// The text is empty but is updated after every mode or view change.
            /// </summary>
            public void SetupControls(SpeedLimitsToolWindow window,
                                      UBuilder builder,
                                      SpeedLimitsTool parentTool) {
                this.position = Vector3.zero;

                this.backgroundSprite = "GenericPanel";
                this.color = new Color32(64, 64, 64, 255);

                this.SetPadding(UPadding.Default);
                this.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        r.Stack(
                            UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING,
                            stackRef: window.modeButtonsPanel_);
                        r.FitToChildren();
                    });

                this.ModeDescriptionLabel = builder.Label_(
                    parent: this,
                    t: string.Empty,
                    stack: UStackMode.Below,
                    processMarkup: true);
                this.ModeDescriptionLabel.SetPadding(
                    new UPadding(top: 12f, right: 0f, bottom: 0f, left: 0f));
            }

            /// <summary>
            /// Update the info label with explanation what the user currently sees.
            /// </summary>
            /// <param name="multiSegmentMode">Whether user is holding shift to edit road length.</param>
            /// <param name="editDefaults">Whether user is seeing segment speed limits.</param>
            /// <param name="showLanes">Whether separate limits per lane are visible.</param>
            public void UpdateModeInfoLabel(bool multiSegmentMode,
                                            bool editDefaults,
                                            bool showLanes) {
                var sb = new StringBuilder(15); // initial capacity of stringBuilder
                var translation = Translation.SpeedLimits;

                //--------------------------
                // Current editing mode
                //--------------------------
                sb.Append(editDefaults
                              ? translation.Get("Editing default limits per road type")
                              : (showLanes
                                     ? translation.Get("Editing lane speed limit overrides")
                                     : translation.Get("Editing speed limit overrides for segments")));
                sb.Append(".\n");

                // //--------------------------
                // // Keyboard modifier hints
                // //--------------------------
                // if (!editDefaults) {
                //     // In defaults and lanes mode Shift is not working
                //     sb.Append(translation.ColorizeKeybind("UI.Key:Shift edit multiple"));
                //     sb.Append(".\n");
                // }
                //
                // sb.Append(editDefaults
                //               ? translation.ColorizeKeybind("UI.Key:Alt show overrides")
                //               : translation.ColorizeKeybind("UI.Key:Alt show defaults"));
                // sb.Append(".\n");
                //
                // sb.Append(translation.ColorizeKeybind("UI.Key:PageUp/PageDown switch underground"));
                // sb.Append(". ");

                this.ModeDescriptionLabel.text = sb.ToString();
                UResizer.UpdateControl(this);
            }
        }
    }
}
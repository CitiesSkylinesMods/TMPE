namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Text;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;

    internal partial class ToolWindow {
        internal class ModeDescriptionPanel : UPanel {
            /// <summary>Describes what user currently sees. Can use color codes for keyboard hints.</summary>
            internal ULabel modeDescriptionLabel_;

            /// <summary>
            /// Sets up two stacked labels: for mode description (what the user sees) and for hint.
            /// The text is empty but is updated after every mode or view change.
            /// </summary>
            public void SetupControls(ToolWindow window,
                                      UBuilder builder,
                                      SpeedLimitsTool parentTool) {
                this.position = Vector3.zero;

                this.backgroundSprite = "GenericPanel";
                this.color = new Color32(64, 64, 64, 255);

                this.SetPadding(UPadding.Const());
                this.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        r.Stack(
                            UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING,
                            stackRef: window.modeButtonsPanel_);
                        r.FitToChildren();
                    });

                modeDescriptionLabel_ = builder.Label_(
                    parent: this,
                    t: string.Empty,
                    stack: UStackMode.Below,
                    processMarkup: true);
                modeDescriptionLabel_.SetPadding(
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

                //--------------------------
                // Current editing mode
                //--------------------------
                if (showLanes) {
                    sb.Append("Showing each lane.");
                } else {
                    sb.Append("Showing averaged per segment.");
                }

                sb.Append(" ");

                if (editDefaults) {
                    sb.Append("Editing default limits per road type.");
                } else {
                    sb.Append("Editing limits for specific segments.");
                }

                sb.Append("\n");

                //--------------------------
                // Keyboard modifier hints
                //--------------------------
                if (showLanes) {
                    sb.Append(UIUtil.ColorizeKeybind("[[Ctrl]] averaged limits."));
                } else {
                    sb.Append(UIUtil.ColorizeKeybind("[[Ctrl]] see each lane."));
                }

                sb.Append(" ");
                sb.Append(UIUtil.ColorizeKeybind("[[Shift]] edit multiple."));
                sb.Append(" ");

                if (editDefaults) {
                    sb.Append(UIUtil.ColorizeKeybind("[[Alt]] show overrides."));
                } else {
                    sb.Append(UIUtil.ColorizeKeybind("[[Alt]] show defaults."));
                }

                this.modeDescriptionLabel_.text = sb.ToString();
                UResizer.UpdateControl(this);
            }
        }
    }
}
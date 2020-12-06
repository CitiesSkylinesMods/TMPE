namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;

    internal partial class ToolWindow {
        internal class PalettePanel : UPanel {
            internal List<SpeedLimitPaletteButton> paletteButtons_ = new();

            /// <summary>Create speeds palette based on the current options choices.</summary>
            /// <param name="window">Containing <see cref="ToolWindow"/>.</param>
            /// <param name="builder">The UI builder to use.</param>
            /// <param name="parentTool">The tool object.</param>
            public void SetupControls(ToolWindow window, UBuilder builder, SpeedLimitsTool parentTool) {
                this.name = GAMEOBJECT_NAME + "_PalettePanel";
                this.position = Vector3.zero;
                this.SetPadding(UPadding.Const());

                this.ResizeFunction(
                    resizeFn: r => {
                        r.Stack(
                            mode: UStackMode.Below,
                            stackRef: window.modeDescriptionWrapPanel_);
                        r.FitToChildren();
                    });
                bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;

                // Fill with buttons
                // [ 10 20 30 ... 120 130 140 0(no limit) ]
                //-----------------------------------------
                // the Current Selected Speed is highlighted
                List<SetSpeedLimitAction> actions =
                    PaletteGenerator.AllSpeedLimits(SpeedUnit.CurrentlyConfigured);
                actions.Add(SetSpeedLimitAction.Unlimited()); // add: Unlimited
                actions.Add(SetSpeedLimitAction.ResetToDefault()); // add: Default

                this.paletteButtons_.Clear();

                foreach (SetSpeedLimitAction action in actions) {
                    SpeedLimitPaletteButton nextButton = SetupControls_SpeedPalette_Button(
                        builder: builder,
                        parent: this,
                        parentTool: parentTool,
                        showMph: showMph,
                        actionOnClick: action);
                    this.paletteButtons_.Add(nextButton);
                }
            }

            private SpeedLimitPaletteButton
                SetupControls_SpeedPalette_Button(UBuilder builder,
                                                  UIComponent parent,
                                                  bool showMph,
                                                  SetSpeedLimitAction actionOnClick,
                                                  SpeedLimitsTool parentTool) {
                SpeedValue speedValue =
                    actionOnClick.Type == SetSpeedLimitAction.ActionType.ResetToDefault
                        ? default
                        : actionOnClick.Override.Value;
                int speedInteger = showMph
                                       ? speedValue.ToMphRounded(SpeedLimitTextures.MPH_STEP).Mph
                                       : speedValue.ToKmphRounded(SpeedLimitTextures.KMPH_STEP).Kmph;

                //--------------------------------
                // Create vertical combo:
                // |[  100   ]|
                // | "65 mph" |
                //--------------------------------
                // Create a small panel which stacks together with other button panels horizontally
                var buttonPanel = builder.Panel_(parent: parent);
                buttonPanel.name = $"{GAMEOBJECT_NAME}_Button_{speedInteger}";
                buttonPanel.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        r.Stack(UStackMode.ToTheRight, spacing: 2f);
                        r.FitToChildren();
                    });

                SpeedLimitPaletteButton button = CreatePaletteButton(
                    builder,
                    actionOnClick,
                    parentTool,
                    buttonPanel,
                    speedInteger,
                    speedValue);

                CreatePaletteButtonHintLabel(builder, showMph, speedValue, button, buttonPanel);

                return button;
            }

            private void CreatePaletteButtonHintLabel(UBuilder builder,
                                                      bool showMph,
                                                      SpeedValue speedValue,
                                                      SpeedLimitPaletteButton button,
                                                      UPanel buttonPanel) {
                // Other speed unit info label
                string otherUnit = showMph
                                       ? ToKmphPreciseString(speedValue)
                                       : ToMphPreciseString(speedValue);

                // Choose label text under the button
                string GetSpeedButtonHintText() {
                    if (FloatUtil.NearlyEqual(speedValue.GameUnits, 0.0f)) {
                        return "Reset";
                    }

                    if (speedValue.GameUnits >= SpeedValue.SPECIAL_UNLIMITED_VALUE) {
                        return "No limit";
                    }

                    return otherUnit;
                }

                ULabel label = button.AltUnitsLabel =
                                   builder.Label_(
                                       parent: buttonPanel,
                                       t: GetSpeedButtonHintText(),
                                       stack: UStackMode.Below);

                label.width = SpeedLimitPaletteButton.SELECTED_WIDTH;
                label.textAlignment = UIHorizontalAlignment.Center;
                label.ContributeToBoundingBox(false); // parent ignore our width
            }

            /// <summary>
            /// Creates a button with speed value on it, and label under it, showing opposite units.
            /// Also can be zero (reset to default) and 1000 km/h (unlimited speed button).
            /// </summary>
            /// <param name="builder">UI builder.</param>
            /// <param name="actionOnClick">What happens if clicked.</param>
            /// <param name="parentTool">Parent speedlimits tool.</param>
            /// <param name="buttonPanel">Panel where buttons are added to.</param>
            /// <param name="speedInteger">Integer value of the speed in the selected units.</param>
            /// <param name="speedValue">Speed value of the button we're creating.</param>
            /// <returns>The new button.</returns>
            private SpeedLimitPaletteButton CreatePaletteButton(UBuilder builder,
                                                                SetSpeedLimitAction actionOnClick,
                                                                SpeedLimitsTool parentTool,
                                                                UPanel buttonPanel,
                                                                int speedInteger,
                                                                SpeedValue speedValue) {
                // Helper function to choose text for the button
                string GetSpeedButtonText() {
                    if (speedInteger == 0) {
                        return "X";
                    }

                    if (speedValue.GameUnits >= SpeedValue.SPECIAL_UNLIMITED_VALUE) {
                        return "MAX";
                    }

                    return speedInteger.ToString();
                }

                var button = builder.Button<SpeedLimitPaletteButton, UIComponent>(parent: buttonPanel);
                button.text = GetSpeedButtonText();
                button.textHorizontalAlignment = UIHorizontalAlignment.Center;

                button.normalBgSprite = button.hoveredBgSprite = "GenericPanel";
                button.color = new Color32(128, 128, 128, 240);

                button.AssignedAction =
                    actionOnClick; // button must know what to do with its speed value

                // The click events will be routed via the parent tool OnPaletteButtonClicked
                button.ParentTool = parentTool;

                button.SetStacking(UStackMode.NewRowBelow);

                // Width will be overwritten in SpeedLimitPaletteButton.UpdateSpeedLimitButton
                button.SetFixedSize(
                    new Vector2(
                        SpeedLimitPaletteButton.DEFAULT_WIDTH,
                        SpeedLimitPaletteButton.DEFAULT_HEIGHT));
                return button;
            }
        }
    }
}
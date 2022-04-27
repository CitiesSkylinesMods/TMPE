namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>Code handling speed palette panel.</summary>
    internal partial class SpeedLimitsToolWindow {
        internal class PalettePanel : UPanel {
            internal readonly List<SpeedLimitPaletteButton> PaletteButtons = new();
            private SpeedLimitsTool parentTool_;

            // Button quick access to use with keyboard shortcuts
            internal SpeedLimitPaletteButton resetToDefaultButton_;
            internal SpeedLimitPaletteButton unlimitedButton_;
            internal Dictionary<int, SpeedLimitPaletteButton> buttonsByNumber_;

            /// <summary>Create speeds palette based on the current options choices.</summary>
            /// <param name="window">Containing <see cref="SpeedLimitsToolWindow"/>.</param>
            /// <param name="builder">The UI builder to use.</param>
            /// <param name="parentTool">The tool object.</param>
            public void SetupControls(SpeedLimitsToolWindow window, UBuilder builder, SpeedLimitsTool parentTool) {
                this.parentTool_ = parentTool;
                this.name = GAMEOBJECT_NAME + "_PalettePanel";
                this.position = Vector3.zero;
                this.SetPadding(UPadding.Default);

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
                List<SetSpeedLimitAction> actions = new();

                actions.Add(SetSpeedLimitAction.ResetToDefault()); // add: Default
                actions.AddRange(PaletteGenerator.AllSpeedLimits(SpeedUnit.CurrentlyConfigured));
                actions.Add(SetSpeedLimitAction.Unlimited()); // add: Unlimited

                this.buttonsByNumber_ = new();
                this.PaletteButtons.Clear();

                foreach (SetSpeedLimitAction action in actions) {
                    SpeedLimitPaletteButton nextButton = this.SetupControls_SpeedPalette_Button(
                        builder: builder,
                        parent: this,
                        parentTool: parentTool,
                        showMph: showMph,
                        actionOnClick: action);
                    this.PaletteButtons.Add(nextButton);

                    // If this is a numbered button, and its a multiple of 10...
                    if (action.Type == SetSpeedLimitAction.ActionType.SetOverride) {
                        int number = (int)(showMph
                                               ? action.GuardedValue.Override.GetMph()
                                               : action.GuardedValue.Override.GetKmph());
                        this.buttonsByNumber_.Add(number, nextButton);
                    } else if (action.Type == SetSpeedLimitAction.ActionType.Unlimited) {
                        this.unlimitedButton_ = nextButton;
                    } else if (action.Type == SetSpeedLimitAction.ActionType.ResetToDefault) {
                        this.resetToDefaultButton_ = nextButton;
                    }
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
                        : actionOnClick.GuardedValue.Override;

                int speedInteger = showMph
                                       ? speedValue.ToMphRounded(RoadSignThemeManager.MPH_STEP).Mph
                                       : speedValue.ToKmphRounded(RoadSignThemeManager.KMPH_STEP).Kmph;

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

                SpeedLimitPaletteButton button = this.CreatePaletteButton(
                    builder,
                    actionOnClick,
                    parentTool,
                    buttonPanel,
                    speedInteger,
                    speedValue);

                this.CreatePaletteButtonHintLabel(builder, showMph, speedValue, button, buttonPanel);
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
                        return Translation.SpeedLimits.Get("Palette.Text:Default");
                    }

                    if (speedValue.GameUnits >= SpeedValue.UNLIMITED) {
                        return Translation.SpeedLimits.Get("Palette.Text:Unlimited");
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
                        return "✖"; // Unicode symbol U+2716 Heavy Multiplication X
                    }

                    if (speedValue.GameUnits >= SpeedValue.UNLIMITED) {
                        return "⊘"; // Unicode symbol U+2298 Circled Division Slash
                    }

                    return speedInteger.ToString();
                }

                var button = builder.Button<SpeedLimitPaletteButton>(parent: buttonPanel);
                button.text = GetSpeedButtonText();
                button.textScale = UIScaler.UIScale;
                button.textHorizontalAlignment = UIHorizontalAlignment.Center;

                button.normalBgSprite = button.hoveredBgSprite = "GenericPanel";
                button.color = new Color32(128, 128, 128, 240);

                // button must know what to do with its speed value
                button.AssignedAction = actionOnClick;

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

            public void TryClick(int speedNumber) {
                if (this.buttonsByNumber_.ContainsKey(speedNumber)) {
                    this.buttonsByNumber_[speedNumber].SimulateClick();
                }
            }

            public void TryDecreaseSpeed() {
                if (this.parentTool_.SelectedAction.Type != SetSpeedLimitAction.ActionType.SetOverride) {
                    return;
                }
                bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                int number = (int)(showMph
                                       ? this.parentTool_.SelectedAction.GuardedValue.Override.GetMph()
                                       : this.parentTool_.SelectedAction.GuardedValue.Override.GetKmph());
                int step = showMph ? RoadSignThemeManager.MPH_STEP : RoadSignThemeManager.KMPH_STEP;
                TryClick(number - step);
            }

            public void TryIncreaseSpeed() {
                if (this.parentTool_.SelectedAction.Type != SetSpeedLimitAction.ActionType.SetOverride) {
                    return;
                }
                bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                int number = (int)(showMph
                                       ? this.parentTool_.SelectedAction.GuardedValue.Override.GetMph()
                                       : this.parentTool_.SelectedAction.GuardedValue.Override.GetKmph());
                int step = showMph ? RoadSignThemeManager.MPH_STEP : RoadSignThemeManager.KMPH_STEP;
                TryClick(number + step);
            }
        }
    }
}
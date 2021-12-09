namespace TrafficManager.UI.SubTools.SpeedLimits {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.UI.Localization;
    using UnityEngine;

    internal partial class SpeedLimitsToolWindow {
        /// <summary>
        /// Wrapper panel around mode toggle buttons.
        /// <see cref="SegmentModeButton"/>, <see cref="DefaultsModeButton"/>,
        /// <see cref="MphToggleButton"/>.
        /// </summary>
        internal class ModeButtonsPanel : UPanel {
            /// <summary>UI button which toggles per-segment or per-lane speed limits.</summary>
            public UButton SegmentModeButton;
            public UButton LaneModeButton;
            public UButton DefaultsModeButton;

            public MphToggleButton ToggleMphButton;

            public void SetupControls(SpeedLimitsToolWindow window, UBuilder builder) {
                this.name = GAMEOBJECT_NAME + "_ModesPanel";

                void ButtonpanelResizeFn(UResizer r) {
                    r.Stack(
                        mode: UStackMode.NewRowBelow,
                        spacing: UConst.UIPADDING,
                        stackRef: window.windowTitleLabel_);
                    r.FitToChildren();
                }

                this.ResizeFunction(ButtonpanelResizeFn);

                Vector2 buttonSize = new Vector2(40f, 40f);
                UITextureAtlas uiAtlas = window.GetUiAtlas();
                LookupTable translation = Translation.SpeedLimits;

                //----------------
                // Edit Segments/Lanes mode button
                //----------------
                this.SegmentModeButton = builder.Button<UButton>(
                    parent: this,
                    text: string.Empty,
                    tooltip: translation.Get("Tooltip:Edit segment speed limits"),
                    size: buttonSize,
                    stack: UStackMode.Below);
                this.SegmentModeButton.atlas = uiAtlas;

                // Note the atlas is loaded before this skin is created in window.GetUiAtlas()
                this.SegmentModeButton.Skin =
                    ButtonSkin.CreateSimple(
                                  foregroundPrefix: "EditSegments",
                                  backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                              .CanActivate(background: false)
                              .CanHover(foreground: false);
                this.SegmentModeButton.ApplyButtonSkin();

                // the onclick handler is set by SpeedLimitsTool outside of this module

                //----------------
                // Edit Lanes mode button
                //----------------
                this.LaneModeButton = builder.Button<UButton>(
                    parent: this,
                    text: string.Empty,
                    tooltip: translation.Get("Tooltip:Edit lane speed limits"),
                    size: buttonSize,
                    stack: UStackMode.ToTheRight);
                this.LaneModeButton.atlas = uiAtlas;
                // Note the atlas is loaded before this skin is created in window.GetUiAtlas()
                this.LaneModeButton.Skin = ButtonSkin
                                           .CreateSimple(
                                               foregroundPrefix: "EditLanes",
                                               backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                           .CanActivate(background: false)
                                           .CanHover(foreground: false);
                this.LaneModeButton.ApplyButtonSkin();
                // the onclick handler is set by SpeedLimitsTool outside of this module

                //----------------
                // Edit Defaults mode button
                //----------------
                this.DefaultsModeButton = builder.Button<UButton>(
                    parent: this,
                    text: string.Empty,
                    tooltip: translation.Get("Tooltip:Default speed limits per road type"),
                    size: buttonSize,
                    stack: UStackMode.NewRowBelow);
                this.DefaultsModeButton.atlas = uiAtlas;

                // Note the atlas is loaded before this skin is created in window.GetUiAtlas()
                this.DefaultsModeButton.Skin = ButtonSkin
                                                   .CreateSimple(
                                                       foregroundPrefix: "EditDefaults",
                                                       backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                                   .CanActivate(background: false)
                                                   .CanHover(foreground: false);
                this.DefaultsModeButton.ApplyButtonSkin();

                // the onclick handler is set by SpeedLimitsTool outside of this module

                //----------------
                // MPH/Kmph switch
                //----------------
                bool displayMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                this.ToggleMphButton = builder.Button<MphToggleButton>(
                    parent: this,
                    text: string.Empty,
                    tooltip: displayMph
                                 ? translation.Get("Miles per hour")
                                 : translation.Get("Kilometers per hour"),
                    size: buttonSize,
                    stack: UStackMode.ToTheRight);
                this.ToggleMphButton.atlas = uiAtlas;

                // Note the atlas is loaded before this skin is created in window.GetUiAtlas()
                this.ToggleMphButton.Skin = ButtonSkin.CreateSimple(
                                                     foregroundPrefix: "MphToggle",
                                                     backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                                 .CanActivate(background: false)
                                                 .CanHover(foreground: false);
                this.ToggleMphButton.ApplyButtonSkin();

                // the onclick handler is set by SpeedLimitsTool outside of this module
            }

            public void UpdateTextures() {
                this.SegmentModeButton.UpdateButtonSkin();
                this.LaneModeButton.UpdateButtonSkin();
                this.DefaultsModeButton.UpdateButtonSkin();
            }
        }
    }
}
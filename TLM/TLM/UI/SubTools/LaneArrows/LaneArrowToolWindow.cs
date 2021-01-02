namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.Util;
    using UnityEngine;

    /// <summary>
    /// Lane Arrows control window floating above the node.
    /// </summary>
    public class LaneArrowToolWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_LaneArrow_ToolPanel";

        /// <summary>
        /// Contains atlas with button backgrounds and arrows.
        /// Static to prevent reloading on every window creation.
        /// </summary>
        private static UITextureAtlas laneArrowButtonAtlas_ = null;

        public List<LaneArrowButton> Buttons { get; set; }

        public override void Start() {
            base.Start();
            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);

            // the GenericPanel sprite is silver, make it dark
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
            this.SetOpacity(
                UOpacityValue.FromOpacity(0.01f * GlobalConfig.Instance.Main.GuiOpacity));
        }

        private UITextureAtlas GetAtlas() {
            if (laneArrowButtonAtlas_ != null) {
                return laneArrowButtonAtlas_;
            }

            // Create base atlas with backgrounds and no foregrounds
            ButtonSkin backgroundOnlySkin = ButtonSkin.CreateDefaultNoForeground("LaneArrow");
            var futureAtlas = new AtlasBuilder(
                atlasName: "TMPE_LaneArrowsTool_Atlas",
                loadingPath: "LaneArrows",
                sizeHint: new IntVector2(256));
            backgroundOnlySkin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(64));

            // Merge names of all foreground sprites for 3 directions into atlasKeySet
            foreach (string prefix in new[]
                { "LaneArrowLeft", "LaneArrowRight", "LaneArrowForward" })
            {
                ButtonSkin skin = ButtonSkin.CreateDefaultNoBackground(prefix);

                // Create keysets for lane arrow button icons and merge to the shared atlas
                skin.UpdateAtlasBuilder(
                    atlasBuilder: futureAtlas,
                    spriteSize: new IntVector2(64));
            }

            // Load actual graphics into an atlas
            laneArrowButtonAtlas_ = futureAtlas.CreateAtlas();
            return laneArrowButtonAtlas_;
        }

        private static ButtonSkin CreateDefaultButtonSkin() {
            return new ButtonSkin {
                                      BackgroundPrefix = "LaneArrow", // filename prefix

                                      BackgroundHovered = true,
                                      BackgroundActive = true,
                                      BackgroundDisabled = true,

                                      ForegroundNormal = true,
                                      ForegroundActive = true,
                                  };
        }

        /// <summary>
        /// Create button triples for number of lanes.
        /// Buttons are linked to lanes later by LaneArrowTool class.
        /// </summary>
        /// <param name="builder">The UI Builder.</param>
        /// <param name="numLanes">How many lane groups.</param>
        public void SetupControls(UBuilder builder, int numLanes) {
            Buttons = new List<LaneArrowButton>();

            var buttonRowPanel = builder.Panel<UPanel, UIComponent>(
                parent: this,
                setupFn: p => { p.name = "TMPE_ButtonRow"; });

            buttonRowPanel.ResizeFunction(
                resizeFn: (UResizer r) => {
                    r.Stack(
                        mode: UStackMode.Below,
                        spacing: UConst.UIPADDING);
                    r.FitToChildren();
                });

            // -----------------------------------
            // Create a row of button groups
            //      [ Lane 1      ] [ Lane 2 ] [ Lane 3 ] ...
            //      [ [←] [↑] [→] ] [...     ] [ ...    ]
            // -----------------------------------
            for (var i = 0; i < numLanes; i++) {
                string buttonName = $"TMPE_LaneArrow_ButtonGroup{i + 1}";
                UPanel buttonGroupPanel = builder.Panel<UPanel, UIComponent>(
                    parent: buttonRowPanel,
                    setupFn: (UPanel p) => {
                        p.name = buttonName;
                        p.atlas = TextureUtil.FindAtlas("Ingame");
                        p.backgroundSprite = "GenericPanel";
                    });
                int i1 = i; // copy of the loop variable, for the resizeFunction below

                buttonGroupPanel.SetPadding(UConst.UIPADDING);
                buttonGroupPanel.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        // attach below "Lane #" label,
                        // else: attach to the right of the previous button group
                        r.Stack(
                            mode: i1 == 0 ? UStackMode.Below : UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING);
                        r.FitToChildren();
                    });

                // Create a label with "Lane #" title
                string labelText = Translation.LaneRouting.Get("Format.Label:Lane") + " " +
                                   (i + 1);
                ULabel laneLabel = builder.Label<ULabel, UIComponent>(
                    parent: buttonGroupPanel,
                    t: labelText);

                // The label will be repositioned to the top of the parent
                laneLabel.ResizeFunction(r => { r.Stack(UStackMode.Below); });

                // Create and populate the panel with buttons
                // 3 buttons are created [←] [↑] [→],
                // The click event is assigned outside in LaneArrowTool.cs
                foreach (string prefix in new[] {
                    "LaneArrowLeft",
                    "LaneArrowForward",
                    "LaneArrowRight",
                }) {
                    var arrowButton = builder.Button<LaneArrowButton, UIComponent>(
                            parent: buttonGroupPanel,
                            text: string.Empty,
                            tooltip: null,
                            size: new Vector2(40f, 40f));

                    arrowButton.atlas = GetAtlas();
                    arrowButton.Skin = CreateDefaultButtonSkin();
                    arrowButton.Skin.Prefix = prefix;
                    Buttons.Add(arrowButton);

                    arrowButton.ResizeFunction(
                        r => {
                            // First button in the group will be stacking vertical
                            // under the "Lane #" label, while 2nd and 3rd will be
                            // stacking horizontal
                            r.Stack(
                                mode: prefix == "LaneArrowLeft"
                                          ? UStackMode.Below
                                          : UStackMode.ToTheRight,
                                spacing: UConst.UIPADDING);
                        });
                } // for each button
            } // end button loop, for each lane
        }
    }
}
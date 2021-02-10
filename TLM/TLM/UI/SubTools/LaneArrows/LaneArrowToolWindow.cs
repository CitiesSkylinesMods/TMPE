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
                U.UOpacityValue.FromOpacity(0.01f * GlobalConfig.Instance.Main.GuiOpacity));
        }

        private UITextureAtlas GetAtlas() {
            if (laneArrowButtonAtlas_ != null) {
                return laneArrowButtonAtlas_;
            }

            // Create base atlas with backgrounds and no foregrounds
            ButtonSkin backgroundOnlySkin = ButtonSkin.CreateDefaultNoForeground("LaneArrow");
            var futureAtlas = new U.AtlasBuilder();
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
            laneArrowButtonAtlas_ = futureAtlas.CreateAtlas(
                atlasName: "LaneArrowsTool_Atlas",
                loadingPath: "LaneArrows",
                atlasSizeHint: new IntVector2(256));
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
        public void SetupControls(UiBuilder<LaneArrowToolWindow> builder, int numLanes) {
            Buttons = new List<LaneArrowButton>();

            using (var buttonRowBuilder = builder.ChildPanel<U.UPanel>(
                setupFn: p => { p.name = "TMPE_ButtonRow"; })) {
                buttonRowBuilder.ResizeFunction(
                    r => {
                        r.Stack(mode: UStackMode.Below,
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
                    using (var buttonGroupBuilder = buttonRowBuilder.ChildPanel<U.UPanel>(
                            setupFn: p => {
                                p.name = buttonName;
                                p.atlas = TextureUtil.FindAtlas("Ingame");
                                p.backgroundSprite = "GenericPanel";
                            }))
                    {
                        int i1 = i; // copy of the loop variable, for the resizeFunction below

                        buttonGroupBuilder.SetPadding(UConst.UIPADDING);
                        buttonGroupBuilder.ResizeFunction(
                            r => {
                                // attach below "Lane #" label,
                                // else: attach to the right of the previous button group
                                r.Stack(
                                    mode: i1 == 0 ? UStackMode.Below : UStackMode.ToTheRight,
                                    spacing: UConst.UIPADDING);
                                r.FitToChildren();
                            });

                        // Create a label with "Lane #" title
                        string labelText = Translation.LaneRouting.Get("Format.Label:Lane") + " " + (i + 1);
                        using (var laneLabel = buttonGroupBuilder.Label<U.ULabel>(labelText))
                        {
                            // The label will be repositioned to the top of the parent
                            laneLabel.ResizeFunction(r => { r.Stack(UStackMode.Below); });
                        }

                        // Create and populate the panel with buttons
                        // 3 buttons are created [←] [↑] [→],
                        // The click event is assigned outside in LaneArrowTool.cs
                        foreach (string prefix in new[] {
                                                            "LaneArrowLeft",
                                                            "LaneArrowForward",
                                                            "LaneArrowRight",
                                                        }) {
                            using (UiBuilder<LaneArrowButton> buttonBuilder =
                                buttonGroupBuilder.Button<LaneArrowButton>())
                            {
                                buttonBuilder.Control.atlas = GetAtlas();
                                buttonBuilder.Control.Skin = CreateDefaultButtonSkin();
                                buttonBuilder.Control.Skin.Prefix = prefix;
                                Buttons.Add(buttonBuilder.Control);

                                buttonBuilder.ResizeFunction(
                                    r => {
                                        // First button in the group will be stacking vertical
                                        // under the "Lane #" label, while 2nd and 3rd will be
                                        // stacking horizontal
                                        r.Stack(
                                            mode: prefix == "LaneArrowLeft"
                                                      ? UStackMode.Below
                                                      : UStackMode.ToTheRight,
                                            spacing: UConst.UIPADDING);
                                        r.Width(UValue.FixedSize(40f));
                                        r.Height(UValue.FixedSize(40f));
                                    });
                            }
                        } // for each button
                    } // end button group panel
                } // end button loop, for each lane
            } // end button row
        }
    }
}
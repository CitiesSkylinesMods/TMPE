namespace TrafficManager.UI.SubTools.LaneArrows {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using TrafficManager.RedirectionFramework;
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
            this.GenericBackgroundAndOpacity();
        }

        private UITextureAtlas GetUiAtlas() {
            if (laneArrowButtonAtlas_ != null) {
                return laneArrowButtonAtlas_;
            }

            ButtonSkin skin = ButtonSkin.CreateDefaultButtonSkin("LaneArrow");

            // Create base atlas with backgrounds and no foregrounds
            skin.ForegroundNormal = false;
            skin.ForegroundActive = false;
            HashSet<U.AtlasSpriteDef> atlasKeysSet = skin.CreateAtlasSpriteSet(new IntVector2(64));

            // Merge names of all foreground sprites for 3 directions into atlasKeySet
            skin.ForegroundNormal = true;
            skin.ForegroundActive = true;
            foreach (string prefix in new[]
                                      { "LaneArrowLeft", "LaneArrowRight", "LaneArrowForward" }) {
                skin.Prefix = prefix;

                // Create keysets for lane arrow button icons and merge to the shared atlas
                atlasKeysSet.AddRange(skin.CreateAtlasSpriteSet(new IntVector2(64)));
            }

            // Load actual graphics into an atlas
            laneArrowButtonAtlas_ = skin.CreateAtlas(
                loadingPath: "LaneArrows",
                atlasSizeHint: new IntVector2(256), // 4x4 atlas
                atlasKeysSet);
            return laneArrowButtonAtlas_;
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
                        // The label will be repositioned to the top of the parent
                        string localizedLane = Translation.LaneRouting.Get("Format.Label:Lane");
                        string labelText = $"{localizedLane} {i + 1}";
                        buttonGroupBuilder.Label(t: labelText, stack: UStackMode.Below);

                        // Create and populate the panel with buttons
                        // 3 buttons are created [←] [↑] [→],
                        // The click event is assigned outside in LaneArrowTool.cs
                        foreach (string prefix in new[] {
                            "LaneArrowLeft",
                            "LaneArrowForward",
                            "LaneArrowRight",
                        }) {
                            using (var b = buttonGroupBuilder.Button<LaneArrowButton>())
                            {
                                b.Control.atlas = GetUiAtlas();
                                b.Control.Skin = ButtonSkin.CreateDefaultButtonSkin("LaneArrow");
                                b.Control.Skin.Prefix = prefix;
                                Buttons.Add(b.Control);

                                // First button in the group will be stacking vertical
                                // under the "Lane #" label, while 2nd and 3rd will be
                                // stacking horizontal
                                b.SetStacking(
                                    mode: prefix == "LaneArrowLeft"
                                        ? UStackMode.Below
                                        : UStackMode.ToTheRight,
                                    spacing: UConst.UIPADDING);
                                b.SetFixedSize(new Vector2(40f, 40f));
                            }
                        } // for each button
                    } // end button group panel
                } // end button loop, for each lane
            } // end button row
        }
    }
}
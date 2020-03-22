namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.U.Button;
    using TrafficManager.UI.SubTools.LaneArrows;
    using UnityEngine;

    /// <summary>
    /// Lane Arrows control window floating above the node.
    /// </summary>
    public class LaneArrowToolWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_LaneArrow_ToolPanel";
        private const float LABEL_HEIGHT = 18f; // a normal font label

        /// <summary>
        /// Contains atlas with button backgrounds and arrows.
        /// Static to prevent reloading on every window creation.
        /// </summary>
        private static UITextureAtlas laneArrowButtonAtlas_ = null;

        public List<LaneArrowButton> Buttons { get; set; }

        public override void Start() {
            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);

            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
        }

        private UITextureAtlas GetAtlas() {
            if (laneArrowButtonAtlas_ != null) {
                return laneArrowButtonAtlas_;
            }

            var skin = CreateDefaultButtonSkin();

            // Create base atlas with backgrounds and no foregrounds
            skin.ForegroundNormal = false;
            skin.ForegroundActive = false;
            HashSet<string> atlasKeysSet = skin.CreateAtlasKeyset();

            // Merge names of all foreground sprites for 3 directions into atlasKeySet
            skin.ForegroundNormal = true;
            skin.ForegroundActive = true;
            foreach (string prefix in new[]
                                      { "LaneArrowLeft", "LaneArrowRight", "LaneArrowForward" }) {
                skin.Prefix = prefix;

                // Create keysets for lane arrow button icons and merge to the shared atlas
                atlasKeysSet.AddRange(skin.CreateAtlasKeyset());
            }

            // Load actual graphics into an atlas
            laneArrowButtonAtlas_ = skin.CreateAtlas(
                "LaneArrows",
                64,
                64,
                256, // 4x4 atlas
                atlasKeysSet);
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
        /// <param name="numLanes">How many lane groups.</param>
        /// <param name="groupSize">Size in pixels for each group (vertical).</param>
        /// <param name="spacing">Spacing between groups and around window edges.</param>
        public void SetupControls(int numLanes, Vector2 groupSize, float spacing) {
            Buttons = new List<LaneArrowButton>();
            // LaneArrowsPanel [
            //     Panel "Outer" [
            //         Panel (repeated) "Group of label and 3 buttons" [
            //             Label [ Lane 1 ]
            //             Panel "3 Buttons" [
            //                 Button [ <- ]
            //                 Button [ Forward ]
            //                 Button [ -> ]
            //             ]
            //         ]
            //     ]
            //    Delete Label and button
            // ]

            // Create horizontal panel which will hold 3-button panels
            // new Vector2(
            //     (groupSize.x * numLanes) + (spacing * (numLanes + 1)),
            //     LABEL_HEIGHT),
            var outerB = new UIBuilder(this)
                         .AutoLayoutVertical((int)spacing)
                         .NestedPanel<U.Panel.UPanel>(
                             p => {
                                 p.name = "TMPE_ButtonRow";
                                 SetupControls_CreateButtonRow(p, numLanes, groupSize, spacing);
                             })
                         .Width(USizeRule.ReferenceSizeAt1080p, 40f)
                         .Height(USizeRule.MultipleOfWidth, 3f);
            // And add another line: "Delete" action
            outerB.NestedPanel<U.Panel.UPanel>(
                      p => {
                          p.name = "TMPE_DeleteLabelContainer";
                      })
                  .Width(USizeRule.FitChildren, 4f)
                  .Height(USizeRule.FitChildren, 4f)
                  .Label<UILabel>("Reset to default [Delete]");
        }

        private void SetupControls_CreateButtonRow(UIPanel rowPanel,
                                                   int numLanes,
                                                   Vector2 groupSize,
                                                   float spacing) {
            for (var i = 0; i < numLanes; i++) {
                // Create a subpanel with title and buttons subpanel
                UIBuilder groupPanelBuilder
                    = new UIBuilder(rowPanel)
                      .NestedPanel<UIPanel>(
                          p => {
                              p.name = "TMPE_LaneLabelContainer";
                              p.size = groupSize;
                          })
                      .AutoLayoutVertical()
                      .Label<UILabel>(
                          Translation.LaneRouting.Get("Format.Label:Lane") + " " + (i + 1));

                UIBuilder buttonPanelBuilder
                    = groupPanelBuilder
                      .NestedPanel<UIPanel>(
                          (p) => {
                              p.name = "TMPE_ButtonGroup";
                              p.atlas = TextureUtil.FindAtlas("Ingame");
                              p.backgroundSprite = "GenericPanel";
                              p.size = groupSize;
                          }).AutoLayoutHorizontal();

                // Create and populate the panel with buttons
                buttonPanelBuilder.Button<LaneArrowButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = CreateDefaultButtonSkin();
                        b.Skin.Prefix = "LaneArrowLeft";
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
                        Buttons.Add(b);
                    });

                buttonPanelBuilder.Button<LaneArrowButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = CreateDefaultButtonSkin();
                        b.Skin.Prefix = "LaneArrowForward";
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
                        Buttons.Add(b);
                    });

                buttonPanelBuilder.Button<LaneArrowButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = CreateDefaultButtonSkin();
                        b.Skin.Prefix = "LaneArrowRight";
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
                        Buttons.Add(b);
                    });
            }
        }

        public override void OnRescaleRequested() { }
    }
}
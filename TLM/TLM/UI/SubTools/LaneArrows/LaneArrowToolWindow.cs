namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.U;
    using TrafficManager.U.Button;
    using TrafficManager.UI.SubTools.LaneArrows;
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
            var formBuilder = new UIBuilder(this);
            formBuilder.AutoLayoutHorizontal((int)spacing);

            // float offset = spacing;
            for (var i = 0; i < numLanes; i++) {
                // Create a subpanel with title and buttons subpanel
                // LaneArrowsPanel [
                //     Nested Panel [
                //         Label [ Lane 1 ]
                //         Panel [
                //             Button [ <- ]
                //             Button [ Forward ]
                //             Button [ -> ]
                //         ]
                //     ]
                var groupPanelBuilder
                    = formBuilder
                      .NestedPanel<UIPanel>((p) => { p.size = groupSize; })
                      .AutoLayoutVertical()
                      .Label<UILabel>(
                          Translation.LaneRouting.Get("Format.Label:Lane") + " " + (i + 1));

                var buttonPanelBuilder = groupPanelBuilder
                      .NestedPanel<UIPanel>(
                          (p) => {
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

            // bool startNode = (bool)netService.IsStartNode(SelectedSegmentId, SelectedNodeId);
            // if (CanReset(SelectedSegmentId, startNode)) {
            //     height += 40;
            // }
        }

        public override void OnRescaleRequested() { }
    }
}
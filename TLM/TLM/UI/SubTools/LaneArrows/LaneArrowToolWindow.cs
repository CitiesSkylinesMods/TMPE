namespace TrafficManager.UI.SubTools {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.RedirectionFramework;
    using TrafficManager.U;
    using TrafficManager.U.Button;
    using UnityEngine;

    /// <summary>
    /// Lane Arrows control window floating above the node.
    /// </summary>
    public class LaneArrowToolWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_LaneArrow_ToolPanel";

        private static UITextureAtlas laneArrowButtonAtlas_ = null;

        /// <summary>Default skin setup copied to all Lane Arrow buttons on creation.</summary>
        private readonly ButtonSkin laneArrowButtonSkin_
            = new ButtonSkin {
                                 BackgroundPrefix = "LaneArrow", // filename prefix

                                 BackgroundHovered = true,
                                 BackgroundActive = true,
                                 BackgroundDisabled = true,

                                 ForegroundNormal = false,
                                 ForegroundHovered = false,
                                 ForegroundDisabled = false,
                             };

        public List<UButton> Buttons { get; set; }

        public override void Start() {
            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);

            this.autoSize = true;
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
        }

        private UITextureAtlas GetAtlas() {
            if (laneArrowButtonAtlas_ != null) {
                return laneArrowButtonAtlas_;
            }

            // Create base atlas with backgrounds and no foregrounds
            HashSet<string> atlasKeysSet = laneArrowButtonSkin_.CreateAtlasKeyset();

            laneArrowButtonSkin_.ForegroundNormal = true;
            foreach (string prefix in new[]
                                      { "LaneArrowLeft", "LaneArrowRight", "LaneArrowForward" }) {
                laneArrowButtonSkin_.Prefix = prefix;

                // Create keysets for lane arrow button icons and merge to the shared atlas
                atlasKeysSet.AddRange(laneArrowButtonSkin_.CreateAtlasKeyset());
            }

            laneArrowButtonAtlas_ = laneArrowButtonSkin_.CreateAtlas(
                "LaneArrows",
                64,
                64,
                256, // 4x4 atlas
                atlasKeysSet);
            return laneArrowButtonAtlas_;
        }

        public void SetupControls(int numLanes, Vector2 groupSize, float spacing) {
            Buttons = new List<UButton>();
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
                buttonPanelBuilder.Button<UButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = laneArrowButtonSkin_;
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
                    });

                buttonPanelBuilder.Button<UButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = laneArrowButtonSkin_;
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
                    });

                buttonPanelBuilder.Button<UButton>(
                    b => {
                        b.atlas = GetAtlas();
                        b.Skin = laneArrowButtonSkin_;
                        b.size = new Vector2(groupSize.x / 3f, groupSize.y);
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
namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework.UI;
    using UnityEngine;
    using CSUtil.Commons;
    using TrafficManager.UI.Textures;
    using TrafficManager.Util;
    using static UI.SubTools.PrioritySignsTool;
    using static Textures.TextureResources;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.U.Button;
    using TrafficManager.RedirectionFramework;

    public class RoadSelectionPanels : MonoBehaviour {
        private RoadSelectionUtil roadSelectionUtil_;

        public static RoadSelectionPanels Root { get; private set; } = null;
        
        public enum FunctionMode {
            Clear = 0, /// Indicates No button is in active state.
            Stop, 
            Yield,
            HighPrioirty,
            Rabout,
        }

        private ButtonFunction _function;

        public ButtonFunction Function {
            /// returns which button is in active state
            get => _function;

            /// sets which button is in active state then refreshes buttons in all panels.
            set {
                if (_function != value) {
                    _function = value;
                    Refresh();
                }
            }
        }

        private void HidePriorityRoadToggle(UIComponent component, bool value) =>
             component.isVisible = false;

        private void HideRoadAdjustPanelElements(UIPanel roadAdjustPanel) {
            UILabel roadSelectLabel = roadAdjustPanel.Find<UILabel>("Label");
            UILabel roadSelectLegend = roadAdjustPanel.Find<UILabel>("LegendLabel");
            UISprite roadSelectSprite = roadAdjustPanel.Find<UISprite>("Sprite");
            roadSelectLabel.isVisible = false;
            roadSelectLegend.isVisible = false;
            roadSelectSprite.isVisible = false;
        }

        /// <summary>
        /// Enable and refreshes overrlay for various traffic rules influenced by road selection pannel.
        /// Also enables Traffic manager tool.
        /// </summary>
        private void ShowMassEditOverlay() {
            var tmTool = ModUI.GetTrafficManagerTool(true);
            if(tmTool == null) {
                Log.Error("ModUI.GetTrafficManagerTool(true) returned null");
                return;
            }
            ModUI.EnableTool();
            MassEditOVerlay.Show = true;
            tmTool.InitializeSubTools();
        }

        private void MassEditOverlayOnEvent(UIComponent component, bool value) {
            if (value) {
                ShowMassEditOverlay();
            } else {
                MassEditOVerlay.Show = false;
                var tmTool = ModUI.GetTrafficManagerTool(false);
                if (tmTool) {
                    tmTool.InitializeSubTools();
                    ModUI.DisableTool();
                }
            }
        }

        private void ShowAdvisorOnEvent(UIComponent component, bool value) {
            if (value) {
                TrafficManagerTool.ShowAdvisor("RoadSelection");
            }
        }

        /// <summary>
        ///  list all instances of road selection panels.
        /// </summary>
        private IList<PanelExt> panels;
        private UIComponent priorityRoadToggle;

        public void OnDestroy() {
            Log._Debug("PanelExt OnDestroy() called");

            if (roadSelectionUtil_ != null) {
                roadSelectionUtil_ = null;
                RoadSelectionUtil.Release();
            }

            if (priorityRoadToggle != null) {
                priorityRoadToggle.eventVisibilityChanged -= HidePriorityRoadToggle;
            }

            if (panels != null) {
                foreach (UIPanel panel in panels) {
                    if (panel != null) {
                        //panel.eventVisibilityChanged -= ShowAdvisorOnEvent;
                        Destroy(panel.gameObject); 
                    }
                }
            }
        }

        ~RoadSelectionPanels() {
            Root = null;
            _function = null;
        }

        public void Awake() {
            Root = this;
            _function = null;
        }

        public void Start() {
            Log._Debug("PanelExt Start() called");
            panels = new List<PanelExt>();

            // attach an instance of road selection panel to RoadWorldInfoPanel.
            RoadWorldInfoPanel roadWorldInfoPanel = UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");
            if (roadWorldInfoPanel != null) {
                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(-10f, -10f);
                priorityRoadToggle = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                if (priorityRoadToggle != null) {
                    priorityRoadToggle.eventVisibilityChanged += HidePriorityRoadToggle;
                }

                panel.eventVisibilityChanged += ShowAdvisorOnEvent;
            }

            // attach another instance of road selection panel to AdjustRoad tab.
            UIPanel roadAdjustPanel = UIView.Find<UIPanel>("AdjustRoad");
            if (roadAdjustPanel != null) {
                PanelExt panel = AddPanel(roadAdjustPanel);
                panel.eventVisibilityChanged += MassEditOverlayOnEvent;
                panel.eventVisibilityChanged += ShowAdvisorOnEvent;
            }

            // every time user changes the road selection, all buttons will go back to inactive state.
            roadSelectionUtil_ = new RoadSelectionUtil();
            if (roadSelectionUtil_ != null) { 
                roadSelectionUtil_.OnChanged += RefreshOnEvent;
            }
        }

        private static void RefreshOnEvent() =>
            Root?.Refresh(reset: true);

        // Create a road selection panel. Multiple instances are allowed.
        private PanelExt AddPanel(UIComponent container) {
            UIView uiview = UIView.GetAView();
            PanelExt panel = uiview.AddUIComponent(typeof(PanelExt)) as PanelExt;
            panel.Root = this;
            panel.width = 210;
            panel.height = 50;
            panel.AlignTo(container, UIAlignAnchor.BottomLeft);
            panel.relativePosition += new Vector3(70, -10);
            panels.Add(panel);
            return panel;
        }

        /// <summary>
        /// Refreshes all butons in all panels according to state indicated by FunctionMode
        /// </summary>
        /// <param name="reset">if true, deactivates all buttons</param>
        public void Refresh(bool reset = false) {
            Log._Debug($"Refresh called Function mode is {Function}\n");
            if (reset) {
                _function = null;
            } else {
                foreach (var panel in panels ?? Enumerable.Empty<PanelExt>()) {
                    panel.Refresh();
                }
            }
        }

        /// <summary>
        /// Panel container for the Road selection UI. Multiple instances are allowed.
        /// </summary>
        public class PanelExt : UIPanel {
            public void Refresh() {
                foreach (var button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    button.Refresh();
                }
            }

            /// Container of this panel.
            public RoadSelectionPanels Root;

            /// list of buttons contained in this panel.
            public IList<ButtonExt> buttons;

            UITextureAtlas allButtonsAtlas_;

            public override void Start() {
                base.Start();
                autoLayout = true;
                autoLayoutDirection = LayoutDirection.Horizontal;
                padding = new RectOffset(1, 1, 1, 1);
                autoLayoutPadding = new RectOffset(5, 5, 5, 5);

                buttons = new List<ButtonExt>();
                buttons.Add(AddUIComponent<ClearButtton>());
                buttons.Add(AddUIComponent<StopButtton>());
                buttons.Add(AddUIComponent<YieldButton>());
                buttons.Add(AddUIComponent<HighPrioirtyButtton>());
                buttons.Add(AddUIComponent<RAboutButtton>());
                SetupAtlas();
            }

            private void SetupAtlas() {
                // Create and populate list of background atlas keys, used by all buttons
                // And also each button will have a chance to add their own atlas keys for loading.
                var tmpSkin = new ButtonSkin() {
                    Prefix = "RoadSelection",
                    BackgroundPrefix = "RoundButton",
                    ForegroundNormal = false,
                    BackgroundHovered = true,
                    BackgroundActive = true,
                    BackgroundDisabled = true,
                };

                // By default the atlas will include backgrounds: DefaultRound-bg-normal
                HashSet<string> atlasKeysSet = tmpSkin.CreateAtlasKeyset();

                foreach (var button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    button.SetupButtonSkin(atlasKeysSet);
                }

                // Create atlas and give it to all buttons
                allButtonsAtlas_ = tmpSkin.CreateAtlas(
                                       "RoadSelectionPanel",
                                       50,
                                       50,
                                       512,
                                       atlasKeysSet);

                foreach (var button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    button.atlas = allButtonsAtlas_;
                }
            }

            public class ClearButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Clear");
                protected override ButtonFunction Function => new ButtonFunction("Clear");
                public override bool IsActive() => false; // Clear funtionality can't be undone. #568
                public override void Do() => // TODO delete all rules as part of #568
                    PriorityRoad.ClearRoad(Selection);
                public override void Undo() => throw new Exception("Unreachable code");
            }
            public class StopButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Stop entry");
                protected override ButtonFunction Function => new ButtonFunction("Stop");
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainStop, Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, Selection);
            }
            public class YieldButton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Yield entry");
                protected override ButtonFunction Function => new ButtonFunction("Yield");
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainYield, Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, Selection);
            }
            public class HighPrioirtyButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:High priority");
                protected override ButtonFunction Function => new ButtonFunction("HighPrioirty");
                public override void Do() =>
                    PriorityRoad.FixRoad(Selection);
                public override void Undo() =>
                    PriorityRoad.ClearRoad(Selection);
            }
            public class RAboutButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Roundabout");
                protected override ButtonFunction Function => new ButtonFunction("Roundabout");
                public override void Do() =>
                    RoundaboutMassEdit.Instance.FixRabout(Selection);
                public override void Undo() =>
                    RoundaboutMassEdit.Instance.ClearRabout(Selection);

                public override bool ShouldDisable() {
                    if (Length <= 1) {
                        return true;
                    }
                    var segmentList = Selection;
                    bool isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: true);
                    if (!isRabout) {
                        segmentList.Reverse();
                        isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: true);
                    }
                    return !isRabout;
                }
            }

            public abstract class ButtonExt : BaseMenuButton {
                public static readonly Texture2D RoadQuickEditButtons;
                static ButtonExt() {
                    RoadQuickEditButtons = LoadDllResource("road-edit-btns.png", 22 * 50, 50);
                    RoadQuickEditButtons.name = "TMPE_RoadQuickEdit";
                }

                public override void Awake() {
                    base.Awake();
                    Skin = new U.Button.ButtonSkin() {
                        Prefix = SkinPrefix,

                        BackgroundPrefix = "RoundButton",
                        BackgroundHovered = true,
                        BackgroundActive = true,
                        BackgroundDisabled = true,

                        ForegroundNormal = true,
                        ForegroundHovered = false,
                        ForegroundActive = false,
                        ForegroundDisabled = true,
                    };
                }

                public override void Start() {
                    base.Start();
                    width = Width;
                    height = Height;
                }

                public override void SetupButtonSkin(HashSet<string> atlasKeys) {
                    atlasKeys.AddRange(this.Skin.CreateAtlasKeyset());
                }

                public override void OnClickInternal(UIMouseEventParameter p) =>
                    throw new Exception("Unreachable code");

                // Handles button click on activation. Apply traffic rules here.
                public abstract void Do();

                // Handles button click on de-activation. Reset/Undo traffic rules here.
                public abstract void Undo();

                protected override void OnClick(UIMouseEventParameter p) {
                    if (!IsActive()) {
                        Root.Function = this.Function;
                        Do();
                        Root.ShowMassEditOverlay();
                    } else {
                        Root.Function = null;
                        Undo();
                        Root.ShowMassEditOverlay();
                    }
                }

                public void Refresh() {
                    isEnabled = !ShouldDisable();
                    UpdateButtonImageAndTooltip();
                }

                public RoadSelectionPanels Root => RoadSelectionPanels.Root;

                public List<ushort> Selection => Root?.roadSelectionUtil_?.Selection;

                public int Length => Root?.roadSelectionUtil_?.Length ?? 0;

                public override bool CanActivate() => true;

                public override bool IsActive() => Root.Function == this.Function;

                public virtual bool ShouldDisable() => Length == 0;

                public override string ButtonName => "RoadQuickEdit_" + this.GetType().ToString();

                public override bool IsVisible() => true;

                public virtual string SkinPrefix => Function.Name;

                public int Width => 40;

                public int Height => 40;
            } // end class QuickEditButton
        } // end class PanelExt
    } // end AdjustRoadSelectPanelExt
} //end namesapce

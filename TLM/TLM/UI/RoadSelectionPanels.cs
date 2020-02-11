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

    public class RoadSelectionPanels : MonoBehaviour {
        public static RoadSelectionPanels Instance { get; private set; } = null;
        public RoadSelectionPanels() : base() { Instance = this; }

        public enum FunctionMode {
            Clear = 0, /// Indicates No button is in active state.
            Stop,
            Yield,
            HighPrioirty,
            Rabout,
        }

        private FunctionMode _function;

        public FunctionMode Function {
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
        /// Enable overrlay for various traffic rules influenced by road selection pannel.
        /// this enables Traffic manager tool.
        /// </summary>
        private void ShowMassEditOverlay() {
            UIBase.EnableTool();
            MassEditOVerlay.Show = true;
            UIBase.GetTrafficManagerTool()?.InitializeSubTools();
        }


        private void MassEditOverlayOnEvent(UIComponent component, bool value) {
            if (value) {
                ShowMassEditOverlay();
            } else {
                MassEditOVerlay.Show = false;
                UIBase.GetTrafficManagerTool()?.InitializeSubTools();
                UIBase.DisableTool();
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
        public void Start() {
            Function = FunctionMode.Clear;

            panels = new List<PanelExt>();

            // attach an instance of road selection panel to RoadWorldInfoPanel.
            RoadWorldInfoPanel roadWorldInfoPanel = UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");
            if (roadWorldInfoPanel != null) {
                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(-10f, -10f);
                priorityRoadToggle = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                priorityRoadToggle.eventVisibilityChanged += HidePriorityRoadToggle;

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
            RoadSelection.Instance.OnChanged += RefreshOnEvent;
        }

        private static void RefreshOnEvent() =>
            Instance.Refresh(reset: true);

        // Create a road selection panel. Multiple instances are allowed.
        private PanelExt AddPanel(UIComponent container) {
            UIView uiview = UIView.GetAView();
            PanelExt panel = uiview.AddUIComponent(typeof(PanelExt)) as PanelExt;
            panel.TopContainer = this;
            panel.width = 210;
            panel.height = 50;
            panel.AlignTo(container, UIAlignAnchor.BottomLeft);
            panel.relativePosition += new Vector3(70, -10);
            panels.Add(panel);
            return panel;
        }

        public void OnDestroy() {
            RoadSelection.Instance.OnChanged -= RefreshOnEvent;
            priorityRoadToggle.eventVisibilityChanged -= HidePriorityRoadToggle;

            foreach (UIPanel panel in panels ?? Enumerable.Empty<PanelExt>()) {
                panel.eventVisibilityChanged -= ShowAdvisorOnEvent;
                Destroy(panel);
            }
            Instance = null;
        }

        /// <summary>
        /// Refreshes all butons in all panels according to state indicated by FunctionMode
        /// </summary>
        /// <param name="reset">if true, deactivates all buttons</param>
        public void Refresh(bool reset = false) {
            Log._Debug($"Refresh called Function mode is {Function}\n");
            if (reset) {
                _function = FunctionMode.Clear;
            }
            foreach (var panel in panels ?? Enumerable.Empty<PanelExt>()) {
                panel.Refresh();
            }
        }

        /// <summary>
        /// Panel container for the Road selection UI. Multiple instances are allowed.
        /// </summary>
        public class PanelExt : UIPanel {
            public void Refresh() {
                foreach (var button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    button.UpdateProperties();
                }
            }

            /// Container of this panel.
            public RoadSelectionPanels TopContainer;

            /// list of buttons contained in this panel.
            public IList<ButtonExt> buttons;

            public void Start() {
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
            }

            public void OnDestroy() {
                foreach (UIButton button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    Destroy(button);
                }
            }

            public class ClearButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("RoadSelection.Tooltip:Clear");
                public override FunctionMode Function => FunctionMode.Clear;
                public override bool Active => false; // Clear funtionality can't be undone. #568
                public override void Do() => // TODO delete all rules as part of #568
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
                public override void Undo() => throw new Exception("Unreachable code");
            }
            public class StopButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("RoadSelection.Tooltip:Stop entry");
                public override FunctionMode Function => FunctionMode.Stop;
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainStop, RoadSelection.Instance.Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
            }
            public class YieldButton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("RoadSelection.Tooltip:Yield entry");
                public override FunctionMode Function => FunctionMode.Yield;
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainYield, RoadSelection.Instance.Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
            }
            public class HighPrioirtyButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("RoadSelection.Tooltip:High priority");
                public override FunctionMode Function => FunctionMode.HighPrioirty;
                public override void Do() =>
                    PriorityRoad.FixRoad()

                    throw new NotImplementedException("blocked by #541");
                public override void Undo() => throw new NotImplementedException("blocked by #541 #568");
            }

            public class RAboutButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("RoadSelection.Tooltip:Roundabout");
                public override FunctionMode Function => FunctionMode.Rabout;
                public override void Do() =>
                    RoundaboutMassEdit.Instance.FixRabout(RoadSelection.Instance.Selection);
                public override void Undo() =>
                    RoundaboutMassEdit.Instance.ClearRabout(RoadSelection.Instance.Selection);

                public override bool ShouldDisable {
                    get {
                        Log._Debug("RAboutButtton.IsDisabled() called" + Environment.StackTrace);
                        if (RoadSelection.Instance.Length <= 1) {
                            return true;
                        }
                        var segmentList = RoadSelection.Instance.Selection;
                        bool isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: true);
                        if (!isRabout) {
                            segmentList.Reverse();
                            isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: true);
                        }
                        return !isRabout;
                    }
                }
            }

            public abstract class ButtonExt : LinearSpriteButton {
                public static readonly Texture2D RoadQuickEditButtons;
                static ButtonExt() {
                    RoadQuickEditButtons = LoadDllResource("road-edit-btns.png", 22 * 50, 50);
                    RoadQuickEditButtons.name = "TMPE_RoadQuickEdit";
                }

                public override void Start() {
                    base.Start();
                    width = Width;
                    height = Height;
                }

                public override void HandleClick(UIMouseEventParameter p) { throw new Exception("Unreachable code"); }

                // Handles button click on activation. Apply traffic rules here.
                public abstract void Do();

                // Handles button click on de-activation. Reset/Undo traffic rules here.
                public abstract void Undo();

                protected override void OnClick(UIMouseEventParameter p) {
                    if (!Active) {
                        Root.Function = this.Function;
                        Do();
                        Root.ShowMassEditOverlay();
                    } else {
                        Root.Function = FunctionMode.Clear;
                        Undo();
                        Root.ShowMassEditOverlay();
                    }
                }

                public RoadSelectionPanels Root => RoadSelectionPanels.Instance;

                public override bool CanActivate() => true;

                public override bool Active => Root.Function == this.Function;

                public override bool CanDisable => true;

                public override bool ShouldDisable => RoadSelection.Instance.Length == 0;

                public abstract FunctionMode Function { get; }

                public override string FunctionName => Function.ToString();

                public override string[] FunctionNames => Enum.GetNames(typeof(FunctionMode));

                public override string ButtonName => "RoadQuickEdit_" + this.GetType().ToString();

                public override Texture2D AtlasTexture => RoadQuickEditButtons;

                public override bool Visible => true;

                public override int Width => 40;

                public override int Height => 40;
            } // end class QuickEditButton
        } // end class PanelExt
    } // end AdjustRoadSelectPanelExt
} //end namesapce

namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Textures;
    using UnityEngine;
    using Util;
    using static UI.SubTools.PrioritySignsTool;

    public class RoadSelectionPanel : MonoBehaviour {
        public static RoadSelectionPanel Instance { get; private set; } = null;
        public RoadSelectionPanel() : base() { Instance = this; }

        public enum FunctionMode {
            Clear = 0,
            Stop,
            Yield,
            Boulevard,
            Rabout,
        }

        private FunctionMode _function;
        public FunctionMode Function {
            get => _function;
            set {
                if (_function != value) {
                    _function = value;
                    Refresh();
                }
            }
        }

        public void HidePriorityRoadToggle(UIComponent priorityRoadToggle) {
            priorityRoadToggle.eventVisibilityChanged +=
                (component, value) => component.isVisible = false;
        }

        public void HideRoadAdjustPanelElements(UIPanel roadAdjustPanel) {
            UILabel roadSelectLabel = roadAdjustPanel.Find<UILabel>("Label");
            UILabel roadSelectLegend = roadAdjustPanel.Find<UILabel>("LegendLabel");
            UISprite roadSelectSprite = roadAdjustPanel.Find<UISprite>("Sprite");
            roadSelectLabel.isVisible = false;
            roadSelectLegend.isVisible = false;
            roadSelectSprite.isVisible = false;
        }

        ToolBase previousTool;

        public void ShowMassEditOverlay() {
            UIBase.EnableTool();
            showMassEditOverlay = true;
            UIBase.GetTrafficManagerTool()?.InitializeSubTools();
        }
        public void RegisterMassEditOverlay(UIPanel panel) {
            panel.eventVisibilityChanged +=
                (component, value) => {
                    if (value) {
                        previousTool = ToolsModifierControl.toolController.CurrentTool;
                        ShowMassEditOverlay();
                    } else {
                        showMassEditOverlay = false;
                        UIBase.GetTrafficManagerTool()?.InitializeSubTools();
                        ToolsModifierControl.toolController.CurrentTool = previousTool;
                        previousTool = null;
                    }
                };
        }

        public IList<PanelExt> panels;
        public void Start() {
            Function = FunctionMode.Clear;

            panels = new List<PanelExt>();

            RoadWorldInfoPanel roadWorldInfoPanel = UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");
            if (roadWorldInfoPanel != null) {
                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(-10f, -10f);
                UIComponent priorityRoadToggle = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                HidePriorityRoadToggle(priorityRoadToggle);
            }

            UIPanel roadAdjustPanel = UIView.Find<UIPanel>("AdjustRoad");
            if (roadAdjustPanel != null) {
                AddPanel(roadAdjustPanel);
                // HideRoadAdjustPanelElements(roadAdjustPanel);
                RegisterMassEditOverlay(roadAdjustPanel);
            }

            RoadSelection.Instance.OnChanged += () => Refresh(reset: true);
        }

        protected PanelExt AddPanel(UIComponent container) {
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
            foreach (UIPanel panel in panels ?? Enumerable.Empty<PanelExt>()) {
                Destroy(panel);
            }
            Instance = null;
        }

        public void Refresh(bool reset = false) {
            Log._Debug($"Refresh called Function mode is {Function}\n");
            if (reset) {
                _function = FunctionMode.Clear;
            }
            foreach (var panel in panels ?? Enumerable.Empty<PanelExt>()) {
                panel.Refresh();
            }
        }

        public class PanelExt : UIPanel {
            public void Refresh() {
                foreach (var button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    button.UpdateProperties();
                }
            }

            public RoadSelectionPanel TopContainer;
            //UIButton Clear, TrafficLight, Yeild, Stop , RightOnly, RoundAbout;
            public IList<ButtonExt> buttons;
            public void Start() {
                //backgroundSprite = "GenericPanel"; //TODO remove
                //opacity = 0.5f; // TODO remove
                autoLayout = true;
                autoLayoutDirection = LayoutDirection.Horizontal;
                padding = new RectOffset(5, 5, 5, 5);
                autoLayoutPadding = new RectOffset(5, 5, 5, 5);

                buttons = new List<ButtonExt>();
                buttons.Add(AddUIComponent<ClearButtton>());
                buttons.Add(AddUIComponent<StopButtton>());
                buttons.Add(AddUIComponent<YieldButton>());
                buttons.Add(AddUIComponent<BoulevardButtton>());
                buttons.Add(AddUIComponent<RAboutButtton>());
            }

            public void OnDestroy() {
                foreach (UIButton button in buttons ?? Enumerable.Empty<ButtonExt>()) {
                    Destroy(button);
                }
            }

            public class ClearButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Clear");
                public override FunctionMode Function => FunctionMode.Clear;
                public override bool Active => false;
                public override void Do() => // TODO delete everything as part of #568
                    FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
                public override void Undo() => throw new Exception("Unreachable code");
            }
            public class StopButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Stop entry");
                public override FunctionMode Function => FunctionMode.Stop;
                public override void Do() =>
                    FixPrioritySigns(PrioritySignsMassEditMode.MainStop, RoadSelection.Instance.Selection);
                public override void Undo() =>
                    FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
            }
            public class YieldButton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Yield entry");
                public override FunctionMode Function => FunctionMode.Yield;
                public override void Do() =>
                    FixPrioritySigns(PrioritySignsMassEditMode.MainYield, RoadSelection.Instance.Selection);
                public override void Undo() =>
                    FixPrioritySigns(PrioritySignsMassEditMode.Delete, RoadSelection.Instance.Selection);
            }
            public class BoulevardButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Boulevard");
                public override FunctionMode Function => FunctionMode.Boulevard;
                public override void Do() => throw new NotImplementedException("blocked by #541");
                public override void Undo() => throw new NotImplementedException("blocked by #541 #568");
                public override bool IsDisabled => true; // TODO remove after #541
            }

            public class RAboutButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Roundabout");
                public override FunctionMode Function => FunctionMode.Rabout;
                public override void Do() =>
                    RoundaboutMassEdit.Instance.FixRabout(RoadSelection.Instance.Selection);
                public override void Undo() =>
                    RoundaboutMassEdit.Instance.UndoRabout(RoadSelection.Instance.Selection);
                public override bool IsDisabled {
                    get {
                        Log._Debug("RAboutButtton.IsDisabled() called" + Environment.StackTrace);
                        // TODO why rabout is called multiple times?
                        if (RoadSelection.Instance.Length <= 1) {
                            return true;
                        }
                        var segmentList = RoadSelection.Instance.Selection;
                        bool isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: false);
                        if (!isRabout) {
                            segmentList.Reverse();
                            isRabout = RoundaboutMassEdit.IsRabout(segmentList, semi: false);
                        }
                        return !isRabout;
                    }
                }
            }

            public abstract class ButtonExt : LinearSpriteButton {
                public override void Start() {
                    base.Start();
                    width = Width;
                    height = Height;
                }

                public override void HandleClick(UIMouseEventParameter p) { throw new Exception("Unreachable code"); }

                public abstract void Do();
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

                public RoadSelectionPanel Root => RoadSelectionPanel.Instance;

                public override bool CanActivate() => true;

                public override bool Active => Root.Function == this.Function;

                public override bool CanDisable => true;

                public override bool IsDisabled => RoadSelection.Instance.Length == 0;

                public abstract FunctionMode Function { get; }

                public override string FunctionName => Function.ToString();

                public override string[] FunctionNames => Enum.GetNames(typeof(FunctionMode));

                public override string ButtonName => "RoadQuickEdit_" + this.GetType().ToString();

                public override Texture2D AtlasTexture => TextureResources.RoadQuickEditButtons;

                public override bool Visible => true;

                public override int Width => 40;

                public override int Height => 40;
            } // end class QuickEditButton
        } // end class PanelExt
    } // end AdjustRoadSelectPanelExt
} //end namesapce

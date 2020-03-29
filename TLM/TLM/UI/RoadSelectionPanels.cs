namespace TrafficManager.UI {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework;
    using ColossalFramework.UI;
    using UnityEngine;
    using CSUtil.Commons;
    using TrafficManager.Util;
    using TrafficManager.U.Button;
    using TrafficManager.RedirectionFramework;
    using static UI.SubTools.PrioritySignsTool;

    public class RoadSelectionPanels : MonoBehaviour {
        private RoadSelectionUtil roadSelectionUtil_;

        public static RoadSelectionPanels Root { get; private set; } = null;

        internal enum FunctionModes {
            None = 0,
            Clear,
            Stop,
            Yield,
            HighPriority,
            Roundabout,
        }

        private FunctionModes _function;

        internal FunctionModes Function {
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

        #region Event handling
        List<Action> actions = new List<Action>();
        const int DELAY = 1;
        int counter = DELAY;
        public void EnqueueAction(Action action) {
            if (action == null) return;
            SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(action);
            lock (actions) {
                //actions.Add(action);
            }
        }

        void PerfromAction() {
            if (actions == null) return;
            lock (actions) {
                if (actions.Count > 0) {
                    if (--counter <= 0) {
                        actions[0]?.Invoke();
                        actions.RemoveAt(0);
                        counter = DELAY;
                    }
                }
            }
        }

        void OnGUI() {
            //ShowWithDelay();
            PerfromAction();
        }

        private void HidePriorityRoadToggleEvent(UIComponent component, bool value) =>
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
            if (tmTool == null) {
                Log.Error("ModUI.GetTrafficManagerTool(true) returned null");
                return;
            }
            MassEditOVerlay.Show = true;
            tmTool.SetToolMode(ToolMode.None);
            tmTool.InitializeSubTools();
            Log._Debug("Mass edit overlay enabled");
        }

        private void HideMassEditOverlay() {
            MassEditOVerlay.Show = false;
            Log._Debug("Mass edit overlay disabled");
        }

        private void MassEditOverlayOnEvent(UIComponent component, bool value) {
            foreach (var panel in panels_)
                value |= panel.isVisible;
            if (value) {
                EnqueueAction(ShowMassEditOverlay);
            } else {
                EnqueueAction(HideMassEditOverlay);
            }
        }

        private void ShowAdvisorOnEvent(UIComponent component, bool value) {
            if (value) {
                EnqueueAction(delegate () {
                    TrafficManagerTool.ShowAdvisor("RoadSelection");
                });
            }
        }

        private void RefreshOnEvent() =>
            EnqueueAction(delegate () { Root?.Refresh(reset: true); });

        #endregion

        /// <summary>
        ///  list all instances of road selection panels.
        /// </summary>
        private IList<PanelExt> panels_;
        private UIComponent priorityRoadToggle_;

        #region Unload
        public void OnDestroy() {
            if (roadSelectionUtil_ != null) {
                roadSelectionUtil_ = null;
                RoadSelectionUtil.Release();
            }

            if (priorityRoadToggle_ != null) {
                priorityRoadToggle_.eventVisibilityChanged -= HidePriorityRoadToggleEvent;
            }

            if (panels_ != null) {
                foreach (UIPanel panel in panels_) {
                    if (panel != null) {
                        panel.eventVisibilityChanged -= ShowAdvisorOnEvent;
                        panel.eventVisibilityChanged -= MassEditOverlayOnEvent;
                        Destroy(panel.gameObject);
                    }
                }
            }
        }

        ~RoadSelectionPanels() {
            Root = null;
            _function = FunctionModes.None;
        }
        #endregion Unload

        #region Load
        public void Awake() {
            Root = this;
            _function = FunctionModes.None;
        }

        public void Start() {
            panels_ = new List<PanelExt>();

            // attach an instance of road selection panel to RoadWorldInfoPanel.
            RoadWorldInfoPanel roadWorldInfoPanel = UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");
            if (roadWorldInfoPanel != null) {
                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(-10f, -10f);
                priorityRoadToggle_ = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                if (priorityRoadToggle_ != null) {
                    priorityRoadToggle_.eventVisibilityChanged += HidePriorityRoadToggleEvent;
                }
                panel.eventVisibilityChanged += MassEditOverlayOnEvent;
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

        // Create a road selection panel. Multiple instances are allowed.
        private PanelExt AddPanel(UIComponent container) {
            UIView uiview = UIView.GetAView();
            PanelExt panel = uiview.AddUIComponent(typeof(PanelExt)) as PanelExt;
            panel.Container = this;
            panel.AlignTo(container, UIAlignAnchor.BottomLeft);
            panel.relativePosition += new Vector3(70, -10);
            panels_.Add(panel);
            return panel;
        }

        #endregion

        /// <summary>
        /// Refreshes all butons in all panels according to state indicated by FunctionMode
        /// </summary>
        /// <param name="reset">if true, deactivates all buttons</param>
        public void Refresh(bool reset = false) {
            if (reset) {
                _function = FunctionModes.None;
            }
            foreach (var panel in panels_ ?? Enumerable.Empty<PanelExt>()) {
                panel.Refresh();
            }
        }

        /// <summary>
        /// Panel container for the Road selection UI. Multiple instances are allowed.
        /// </summary>
        public class PanelExt : UIPanel {
            public void Refresh() {
                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    button.Refresh();
                }
            }

            /// Container of this panel.
            public RoadSelectionPanels Container;

            /// list of buttons contained in this panel.
            private IList<ButtonExt> buttons_;

            UITextureAtlas allButtonsAtlas_;

            public override void Awake() {
                base.Awake();
                padding = new RectOffset(1, 1, 1, 1);
                autoLayoutPadding = new RectOffset(5, 5, 5, 5);
                width = 210;
                height = 50;
            }

            public override void Start() {
                base.Start();
                autoLayout = true;
                autoLayoutDirection = LayoutDirection.Horizontal;
                buttons_ = new List<ButtonExt>();
                buttons_.Add(AddUIComponent<ClearButtton>());
                buttons_.Add(AddUIComponent<StopButtton>());
                buttons_.Add(AddUIComponent<YieldButton>());
                buttons_.Add(AddUIComponent<HighPriorityButtton>());
                buttons_.Add(AddUIComponent<RoundaboutButtton>());
                SetupAtlas();
                Show();
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

                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    atlasKeysSet.AddRange(button.GetAtlasKeys);
                }

                // Create atlas and give it to all buttons
                allButtonsAtlas_ = tmpSkin.CreateAtlas(
                                       "RoadSelectionPanel",
                                       50,
                                       50,
                                       512,
                                       atlasKeysSet);

                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    button.atlas = allButtonsAtlas_;
                }
            }

            public class ClearButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Clear");
                internal override FunctionModes Function => FunctionModes.Clear;
                public override bool IsActive() => false; // Clear funtionality can't be undone. #568
                public override void Do() => // TODO delete all rules as part of #568
                    PriorityRoad.ClearRoad(Selection);
                public override void Undo() => throw new Exception("Unreachable code");
            }
            public class StopButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Stop entry");
                internal override FunctionModes Function => FunctionModes.Stop;
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainStop, Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, Selection);
            }
            public class YieldButton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Yield entry");
                internal override FunctionModes Function => FunctionModes.Yield;
                public override void Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.MainYield, Selection);
                public override void Undo() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsMassEditMode.Delete, Selection);
            }
            public class HighPriorityButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:High priority");
                internal override FunctionModes Function => FunctionModes.HighPriority;
                public override void Do() =>
                    PriorityRoad.FixRoad(Selection);
                public override void Undo() =>
                    PriorityRoad.ClearRoad(Selection);
            }
            public class RoundaboutButtton : ButtonExt {
                public override string GetTooltip() => Translation.Menu.Get("RoadSelection.Tooltip:Roundabout");
                internal override FunctionModes Function => FunctionModes.Roundabout;
                public override void Do() =>
                    RoundaboutMassEdit.Instance.FixRoundabout(Selection);
                public override void Undo() =>
                    RoundaboutMassEdit.Instance.ClearRoundabout(Selection);

                public override bool ShouldDisable() {
                    if (Length <= 1) {
                        return true;
                    }
                    var segmentList = Selection;
                    bool isRoundabout = RoundaboutMassEdit.IsRoundabout(segmentList, semi: true);
                    if (!isRoundabout) {
                        segmentList.Reverse();
                        isRoundabout = RoundaboutMassEdit.IsRoundabout(segmentList, semi: true);
                    }
                    return !isRoundabout;
                }
            }

            public abstract class ButtonExt : BaseUButton {
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
                    width = height = REFERENCE_SIZE; //TODO move to start?
                }

                public override void Start() {
                    base.Start();
                    Refresh();
                }

                public HashSet<string> GetAtlasKeys => this.Skin.CreateAtlasKeyset();

                public override void HandleClick(UIMouseEventParameter p) =>
                    throw new Exception("Unreachable code");

                // Handles button click on activation. Apply traffic rules here.
                public abstract void Do();

                // Handles button click on de-activation. Reset/Undo traffic rules here.
                public abstract void Undo();

                protected override void OnClick(UIMouseEventParameter p) {
                    if (!IsActive()) {
                        Root.Function = this.Function;
                        Do();
                        Root.EnqueueAction(Root.ShowMassEditOverlay);
                    } else {
                        Root.Function = FunctionModes.None;
                        Undo();
                        Root.EnqueueAction(Root.ShowMassEditOverlay);
                    }
                }

                public void Refresh() {
                    isEnabled = !ShouldDisable();
                    UpdateButtonImageAndTooltip();
                    Show();
                }

                const float REFERENCE_SIZE = 40f;

                public RoadSelectionPanels Root => RoadSelectionPanels.Root;

                public List<ushort> Selection => Root?.roadSelectionUtil_?.Selection;

                public int Length => Root?.roadSelectionUtil_?.Length ?? 0;

                public override bool CanActivate() => true;

                public override bool IsActive() => Root.Function == this.Function;

                public virtual bool ShouldDisable() => Length == 0;

                public override string ButtonName => "TMPE.RoadSelectionPanel" + this.GetType().ToString();

                internal abstract FunctionModes Function { get; }

                public override bool IsVisible() => true;

                public virtual string SkinPrefix => Function.ToString();
            } // end class QuickEditButton
        } // end class PanelExt
    } // end class
} //end namesapce

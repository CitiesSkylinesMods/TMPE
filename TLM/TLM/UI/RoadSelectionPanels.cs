namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TrafficManager.U;
    using TrafficManager.UI.SubTools.PrioritySigns;
    using TrafficManager.Util;
    using TrafficManager.Util.Record;
    using UnityEngine;

    public class RoadSelectionPanels : MonoBehaviour {
        private const bool CREATE_NET_ADJUST_SUBPANEL = false;

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
            // returns which button is in active state
            get => _function;

            // sets which button is in active state then refreshes buttons in all panels.
            set {
                if (_function != value) {
                    _function = value;
                    Refresh();
                }
            }
        }

        UIPanel roadAdjustPanel_;

        UIPanel RoadAdjustPanel {
            get {
                if (roadAdjustPanel_ == null)
                    roadAdjustPanel_ = UIView.Find<UIPanel>("AdjustRoad");
                return roadAdjustPanel_;
            }
        }

        public UIPanel RoadWorldInfoPanelExt;
        public static RoadWorldInfoPanel RoadWorldInfoPanel => UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");

        /// <summary>
        ///  list all instances of road selection panels.
        /// </summary>
        private IList<PanelExt> panels_;
        private UIComponent priorityRoadToggle_;

        public IRecordable Record;

        #region Load
        public void Awake() {
            _function = FunctionModes.None;
        }

        public void Start() {
            Root = this;

            panels_ = new List<PanelExt>();

            // attach an instance of road selection panel to RoadWorldInfoPanel.
            RoadWorldInfoPanel roadWorldInfoPanel = RoadWorldInfoPanel;
            if (roadWorldInfoPanel != null) {
                // TODO [issue #710] add panel when able to get road by name.
                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(-10f, -10f);
                priorityRoadToggle_ = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                if (priorityRoadToggle_ != null) {
                    priorityRoadToggle_.eventVisibilityChanged += HidePriorityRoadToggleEvent;
                }
                panel.eventVisibilityChanged += ShowAdvisorOnEvent;
                RoadWorldInfoPanelExt = panel;

                UISprite icon = roadWorldInfoPanel.Find<UISlicedSprite>("Caption")?.Find<UISprite>("Sprite");
                if (icon != null) {
                    icon.spriteName = "ToolbarIconRoads";
                    icon.Invalidate();
                }
            }

            // attach another instance of road selection panel to AdjustRoad tab.
            UIPanel roadAdjustPanel = RoadAdjustPanel;
            if (roadAdjustPanel != null) {
                if (CREATE_NET_ADJUST_SUBPANEL) {
                    AddPanel(roadAdjustPanel);
                }
                roadAdjustPanel.eventVisibilityChanged += ShowAdvisorOnEvent;
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

        #region Unload
        public void OnDestroy() {
            if (roadSelectionUtil_ != null) {
                roadSelectionUtil_ = null;
                RoadSelectionUtil.Release();
            }

            if (priorityRoadToggle_ != null) {
                priorityRoadToggle_.eventVisibilityChanged -= HidePriorityRoadToggleEvent;
            }

            UIPanel roadAdjustPanel = RoadAdjustPanel;
            if (roadAdjustPanel != null) {
                roadAdjustPanel.eventVisibilityChanged -= ShowAdvisorOnEvent;
            }

            if (panels_ != null) {
                foreach (UIPanel panel in panels_) {
                    if (panel != null) {
                        panel.eventVisibilityChanged -= ShowAdvisorOnEvent;
                        Destroy(panel.gameObject);
                    }
                }
            }

            _function = FunctionModes.None;

            // OnDestroy is called with a delay. during reload a new instance of
            // RoadSelectionPanels could have been created. In that case there is
            // no need to set it to null
            if (Root == this)
                Root = null;
        }
        #endregion Unload

        #region Event handling

        /// <summary>
        /// Refreshes all buttons in all panels according to state indicated by FunctionMode.
        /// this is activated in response to user button click or roadSelectionUtil_.OnChanged
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

        private void RefreshOnEvent() =>
            EnqueueAction(() => { Root?.Refresh(reset: true); });

        internal void RenderOverlay() {
            //Log._Debug("Render over lay called st:\n" + Environment.StackTrace);
            NetManager.instance.NetAdjust.PathVisible = ShouldPathBeVisible();
            UpdateMassEditOverlay();
        }

        internal void UpdateMassEditOverlay() {
            var tool = ModUI.GetTrafficManagerTool();
            if (tool && tool.GetToolMode() == ToolMode.None) {
                if (!UI.SubTools.PrioritySigns.MassEditOverlay.IsActive) {
                    if (ShouldShowMassEditOverlay()) {
                        RoadSelectionUtil.ShowMassEditOverlay();
                    }
                } else {
                    if (!ShouldShowMassEditOverlay()) {
                        HideMassEditOverlay();
                    }
                }
            }
        }

        internal bool HasHoveringButton() {
            foreach (var panel in panels_ ?? Enumerable.Empty<PanelExt>()) {
                if (panel.HasHoveringButton()) {
                    return true;
                }
            }
            return false;
        }

        internal bool ShouldPathBeVisible() =>
            RoadSelectionUtil.IsNetAdjustMode() || HasHoveringButton();

        internal bool ShouldShowMassEditOverlay() {
            foreach (var panel in panels_ ?? Enumerable.Empty<PanelExt>()) {
                if (panel.isVisible)
                    return true;
            }
            return NetManager.instance.NetAdjust.PathVisible;
        }

        // even though we enqueue actions from main thread, we still need to enqueue them to
        // the main thread in order to introduce some delay. this delay is necessary to prevent
        // the CO.UI from crashing.
        // this wrapper function exists to make it easier to customize the delaying mechanism.
        private void EnqueueAction(Action action) {
            if (action == null) return;
            SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(action);
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

        private void HideMassEditOverlay() {
            UI.SubTools.PrioritySigns.MassEditOverlay.Show = false;
            Log._Debug("Mass edit overlay disabled");
        }

        private void ShowAdvisorOnEvent(UIComponent component, bool value) {
            if (value) {
                EnqueueAction(() => {
                    TrafficManagerTool.ShowAdvisor("RoadSelection");
                });
            }
        }
        #endregion

        /// <summary>
        /// Panel container for the Road selection UI. Multiple instances are allowed.
        /// </summary>
        public class PanelExt : UIPanel {
            // Container of this panel.
            public RoadSelectionPanels Container;

            // list of buttons contained in this panel.
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
                var tmpSkin = ButtonSkin.CreateSimple(
                                            foregroundPrefix: "RoadSelection",
                                            backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                        .CanHover(foreground: false)
                                        .CanActivate(foreground: false)
                                        .CanDisable(foreground: false)
                                        .NormalForeground(false);

                // By default the atlas will include backgrounds: DefaultRound-bg-normal
                var atlasBuilder = new AtlasBuilder(
                    atlasName: "RoadSelectionPanelAtlas",
                    loadingPath: "RoadSelectionPanel",
                    sizeHint: new IntVector2(512));
                tmpSkin.UpdateAtlasBuilder(
                    atlasBuilder: atlasBuilder,
                    spriteSize: new IntVector2(50));

                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    button.Skin.UpdateAtlasBuilder(
                        atlasBuilder: atlasBuilder,
                        spriteSize: new IntVector2(50));
                }

                // Create atlas and give it to all buttons
                allButtonsAtlas_ = atlasBuilder.CreateAtlas();

                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    button.atlas = allButtonsAtlas_;
                }
            }

            internal bool HasHoveringButton() {
                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    if (button.IsHovered && button.isEnabled)
                        return true;
                }
                return false;
            }

            public void Refresh() {
                foreach (var button in buttons_ ?? Enumerable.Empty<ButtonExt>()) {
                    button.Refresh();
                }
            }

            public class ClearButtton : ButtonExt {
                protected override string U_OverrideTooltipText() => Translation.Menu.Get("RoadSelection.Tooltip:Clear");
                public override string SkinPrefix => Function.ToString(); // remove _RHT/_LHT postFix.
                internal override FunctionModes Function => FunctionModes.Clear;
                public override IRecordable Do() => PriorityRoad.ClearRoad(Selection);
            }
            public class StopButtton : ButtonExt {
                protected override string U_OverrideTooltipText() => Translation.Menu.Get("RoadSelection.Tooltip:Stop entry");
                internal override FunctionModes Function => FunctionModes.Stop;
                public override IRecordable Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsTool.PrioritySignsMassEditMode.MainStop, Selection);
            }
            public class YieldButton : ButtonExt {
                protected override string U_OverrideTooltipText() => Translation.Menu.Get("RoadSelection.Tooltip:Yield entry");
                internal override FunctionModes Function => FunctionModes.Yield;
                public override IRecordable Do() =>
                    PriorityRoad.FixPrioritySigns(PrioritySignsTool.PrioritySignsMassEditMode.MainYield, Selection);
            }
            public class HighPriorityButtton : ButtonExt {
                protected override string U_OverrideTooltipText() => Translation.Menu.Get("RoadSelection.Tooltip:High priority");
                internal override FunctionModes Function => FunctionModes.HighPriority;
                public override IRecordable Do() => PriorityRoad.FixRoad(Selection);
            }
            public class RoundaboutButtton : ButtonExt {
                protected override string U_OverrideTooltipText() => Translation.Menu.Get("RoadSelection.Tooltip:Roundabout");
                internal override FunctionModes Function => FunctionModes.Roundabout;
                public override IRecordable Do() => RoundaboutMassEdit.Instance.FixRoundabout(Selection);
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
                const float REFERENCE_SIZE = 40f;

                public RoadSelectionPanels Root => RoadSelectionPanels.Root;

                public virtual string ButtonName => "TMPE.RoadSelectionPanel" + this.GetType().ToString();

                private static string TrafficSidePostFix => Shortcuts.RHT ? "_RHT" : "_LHT";

                public virtual string SkinPrefix => Function.ToString() + TrafficSidePostFix;

                public bool IsHovered => m_IsMouseHovering; // exposing the protected member

                public List<ushort> Selection => Root?.roadSelectionUtil_?.Selection;

                public int Length => Root?.roadSelectionUtil_?.Length ?? 0;

                public override bool CanActivate() => true;

                protected override bool IsActive() => Root.Function == this.Function;

                public virtual bool ShouldDisable() => Length == 0;

                internal abstract FunctionModes Function { get; }

                protected override bool IsVisible() => true;

                public override void Start() {
                    base.Start();
                    Refresh();
                }

                public override void Awake() {
                    base.Awake();
                    name = ButtonName;
                    Skin = ButtonSkin.CreateSimple(
                                         foregroundPrefix: SkinPrefix,
                                         backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                     .CanDisable()
                                     .CanHover(foreground: false)
                                     .CanActivate(foreground: false);
                    width = height = REFERENCE_SIZE; //TODO move to start?
                }

                public void Refresh() {
                    isEnabled = !ShouldDisable();
                    UpdateButtonSkinAndTooltip();
                    Show();
                }

                #region related to click
                public override void HandleClick(UIMouseEventParameter p) =>
                    throw new Exception("Unreachable code");

                /// <summary>Handles button click on activation. Apply traffic rules here.</summary>
                public abstract IRecordable Do();

                /// <summary>Handles button click on de-activation. Reset/Undo traffic rules here.</summary>
                public virtual void Undo() => Root.Record?.Restore();

                protected override void OnClick(UIMouseEventParameter p) {
                    if (!IsActive()) {
                        Root.Function = this.Function;
                        Root.Record = Do();
                        Root.EnqueueAction(RoadSelectionUtil.ShowMassEditOverlay);
                    } else {
                        Root.Function = FunctionModes.None;
                        Undo();
                        Root.EnqueueAction(RoadSelectionUtil.ShowMassEditOverlay);
                    }
                }
                #endregion
            } // end class QuickEditButton
        } // end class PanelExt
    } // end class
} //end namesapce

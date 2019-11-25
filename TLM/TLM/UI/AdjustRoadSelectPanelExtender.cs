namespace TrafficManager.UI {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using Textures;
    using UnityEngine;
    using System;
    using ColossalFramework;
    using ICities;
    using System.Reflection;

    public class AdjustRoadSelectPanelExtender : MonoBehaviour {
        public enum FunctionMode {
            Clear=0,
            Yeild,
            Stop,
            MiddleBarrier,
            Rabout,
        }
        private FunctionMode _function = FunctionMode.Clear;
        public FunctionMode Function {
            get => _function;
            set {
                _function = value;
                Refresh();
            }
        }

        public IList<PanelExt> panels;
        public void Start() {
            Debug.Log("POINT A1");
            panels = new List<PanelExt>();

            RoadWorldInfoPanel roadWorldInfoPanel = UIView.library.Get<RoadWorldInfoPanel>("RoadWorldInfoPanel");
            if (roadWorldInfoPanel != null) {
                UICheckBox priorityRoadToggle = roadWorldInfoPanel.component.Find<UICheckBox>("PriorityRoadCheckbox");
                priorityRoadToggle.isVisible = false;
                priorityRoadToggle.Hide(); //TODO actually hide
                Debug.Log($"priority toggle => {priorityRoadToggle.name}");

                PanelExt panel = AddPanel(roadWorldInfoPanel.component);
                panel.relativePosition += new Vector3(0, -15f);
                panels.Add(panel);
            }

            UIPanel roadAdjustPanel = UIView.Find<UIPanel>("AdjustRoad");
            if (roadAdjustPanel != null) {
                UILabel roadSelectLabel = roadAdjustPanel.Find<UILabel>("Label");
                UILabel roadSelectLegend = roadAdjustPanel.Find<UILabel>("LegendLabel");
                UISprite roadSelectSprite = roadAdjustPanel.Find<UISprite>("Sprite");
                roadSelectLabel.isVisible = false;
                roadSelectLegend.isVisible = false;
                //roadSelectSprite.isVisible = false;
                PanelExt panel = AddPanel(roadAdjustPanel);
                panels.Add(panel);
            }
            Debug.Log("POINT A2");
        }

        protected PanelExt AddPanel(UIComponent container) {
            UIView uiview = UIView.GetAView();
            PanelExt panel = uiview.AddUIComponent(typeof(PanelExt)) as PanelExt;
            panel.adjustRoadSelectPanelExtender = this;
            panel.width = 210;
            panel.height = 50;
            panel.AlignTo(container, UIAlignAnchor.BottomLeft);
            panels.Add(panel);
            return panel;
        }


        public void OnDestroy() {
            if (panels == null) {
                return;
            }

            foreach (UIPanel panel in panels) {
                Destroy(panel.gameObject);
            }
        }

        public void Refresh() {
            foreach(var panel in panels) {
                panel.Refresh();
            }
        }


        public class PanelExt : UIPanel {
            public void Refresh() {
                foreach(var button in buttons) {
                    button.UpdateProperties();
                }
            }

            public AdjustRoadSelectPanelExtender adjustRoadSelectPanelExtender;
            //UIButton Clear, TrafficLight, Stop, Yeild, RightOnly, RoundAbout;
            public IList<ButtonExt> buttons;
            public void Start() {
                Debug.Log("POINT B1");
                //backgroundSprite = "GenericPanel"; //TODO remove
                //opacity = 0.5f; // TODO remove
                autoLayout = true;
                autoLayoutDirection = LayoutDirection.Horizontal;
                padding = new RectOffset(5, 5, 5, 5);
                autoLayoutPadding = new RectOffset(5, 5, 5, 5);

                buttons = new List<ButtonExt>();
                buttons.Add(AddUIComponent<ClearButtton>());
                buttons.Add(AddUIComponent<YeildButtton>());
                buttons.Add(AddUIComponent<StopButtton>());
                buttons.Add(AddUIComponent<AvneueButtton>());
                buttons.Add(AddUIComponent<RboutButtton>());

                Debug.Log("POINT B2");
            }

            public void OnDestroy() {
                if (buttons == null) {
                    return;
                }

                foreach (UIButton button in buttons) {
                    Destroy(button.gameObject);
                }
            }

            public class ClearButtton : ButtonExt {
                public override void OnActivate() { }
                public override void OnDeactivate() { throw new Exception("Unreachable code"); }
                public override bool Active => false;
                public override string Tooltip => Translation.Menu.Get("Tooltip:Clear");
                public override FunctionMode Function => FunctionMode.Clear;
                protected override void OnClick(UIMouseEventParameter p) {
                    base.OnClick(p);
                }
            }
            public class YeildButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Yeil entry");
                public override FunctionMode Function => FunctionMode.Yeild;
                public override void OnActivate() { }
                public override void OnDeactivate() { }
            }
            public class StopButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Stop entry");
                public override FunctionMode Function => FunctionMode.Stop;
                public override void OnActivate() { }
                public override void OnDeactivate() { }
            }
            public class AvneueButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Avenue");
                public override FunctionMode Function => FunctionMode.MiddleBarrier;
                public override void OnActivate() { }
                public override void OnDeactivate() { }
            }
            public class RboutButtton : ButtonExt {
                public override string Tooltip => Translation.Menu.Get("Tooltip:Roundabout");
                public override FunctionMode Function => FunctionMode.Rabout;
                public override void OnActivate() { }
                public override void OnDeactivate() { }
            }

            public abstract class ButtonExt : LinearSpriteButton {
                public override void Start() {
                    base.Start();
                    width = Width;
                    height = Height;
                    Debug.Log("POINT C");
                }

                public abstract void OnActivate();
                public abstract void OnDeactivate();

                protected override void OnClick(UIMouseEventParameter p) {
                    if (!Active) {
                        MainExt.Function = this.Function;
                        OnActivate();
                    } else {
                        MainExt.Function = FunctionMode.Clear;
                        OnDeactivate();
                    }
                }

                public override void HandleClick(UIMouseEventParameter p) { }

                public AdjustRoadSelectPanelExtender MainExt => Singleton<AdjustRoadSelectPanelExtender>.instance;

                public override bool Active => MainExt.Function == this.Function;

                public abstract FunctionMode Function { get; }

                public override string FunctionName => Function.ToString();

                public override string[] FunctionNames => Enum.GetNames(typeof(FunctionMode));

                public override string ButtonName => "RoadQuickEdit_" + this.GetType().ToString();

                public override Texture2D AtlasTexture => TextureResources.RoadQuickEditButtons;

                public override bool Visible => true;

                public override int Width => 30;

                public override int Height => 30;

                public override bool CanActivate() => true;
            } // end class QuickEditButton
        } // end class PanelExt
    } // end AdjustRoadSelectPanelExt
} //end namesapce

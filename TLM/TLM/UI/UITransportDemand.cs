namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using TrafficManager.State;
    using UnityEngine;

    public class UITransportDemand : UIPanel {
        private UIButton switchViewModeButton_;
        private UILabel viewModeLabel_;

        public override void Start() {
            base.Start();

            var transportInfoViewPanel = GameObject
                                         .Find("(Library) PublicTransportInfoViewPanel")
                                         .GetComponent<PublicTransportInfoViewPanel>();

            if (transportInfoViewPanel != null) {
                Log._Debug($"Public transport info view panel found.");
                transportInfoViewPanel.component.eventVisibilityChanged += this.ParentVisibilityChanged;
            } else {
                Log.Warning($"Public transport info view panel NOT found.");
            }

            isInteractive = true;
            isVisible = false;

            backgroundSprite = "GenericPanel";
            color = new Color32(75, 75, 135, 255);
            width = 156;
            height = 48;

            relativePosition = new Vector3(540f, 10f);

            viewModeLabel_ = AddUIComponent<UILabel>();
            viewModeLabel_.text = Translation.Menu.Get("Label:Outgoing demand");
            viewModeLabel_.relativePosition = new Vector3(3f, 33f);
            viewModeLabel_.textScale = 0.75f;

            switchViewModeButton_ = _createButton(
                Translation.Menu.Get("Button:Switch view"),
                3,
                3,
                clickSwitchViewMode);
        }

        private UIButton _createButton(string text, int x, int y, MouseEventHandler eventClick) {
            var button = AddUIComponent<UIButton>();
            button.textScale = 0.8f;
            button.width = 150f;
            button.height = 30;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = new Vector3(x, y);
            button.eventClick += eventClick;

            return button;
        }

        private void clickSwitchViewMode(UIComponent component, UIMouseEventParameter eventParam) {
            if (TrafficManagerTool.CurrentTransportDemandViewMode == TransportDemandViewMode.Outgoing) {
                viewModeLabel_.text = Translation.Menu.Get("Label:Incoming demand");
                TrafficManagerTool.CurrentTransportDemandViewMode = TransportDemandViewMode.Incoming;
            } else {
                viewModeLabel_.text = Translation.Menu.Get("Label:Outgoing demand");
                TrafficManagerTool.CurrentTransportDemandViewMode = TransportDemandViewMode.Outgoing;
            }
        }

        private void ParentVisibilityChanged(UIComponent component, bool value) {
            Log._Debug($"Public transport info view panel changed visibility: {value}");

            if (value && SavedGameOptions.Instance.parkingAI) {
                TrafficManagerTool.CurrentTransportDemandViewMode =
                    TransportDemandViewMode.Outgoing;
                if (viewModeLabel_ != null) {
                    viewModeLabel_.text = Translation.Menu.Get("Label:Outgoing demand");
                }

                if (switchViewModeButton_ != null) {
                    switchViewModeButton_.text = Translation.Menu.Get("Button:Switch view");
                }

                Show();
            } else {
                Hide();
            }
        }
    }
}

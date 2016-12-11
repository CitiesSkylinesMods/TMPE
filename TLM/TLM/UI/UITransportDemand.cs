#define EXTRAPFx

using System;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using TrafficManager.Geometry;
using TrafficManager.TrafficLight;
using UnityEngine;
using TrafficManager.State;
using TrafficManager.Custom.PathFinding;
using System.Collections.Generic;
using TrafficManager.Manager;

namespace TrafficManager.UI {
	public class UITransportDemand : UIPanel {
		private UIButton switchViewModeButton;
		private UILabel viewModeLabel;

		public override void Start() {
			base.Start();

            var transportInfoViewPanel = GameObject.Find("(Library) PublicTransportInfoViewPanel").GetComponent<PublicTransportInfoViewPanel>();
            if (transportInfoViewPanel != null) {
                Log._Debug($"Public transport info view panel found.");
                transportInfoViewPanel.component.eventVisibilityChanged += new PropertyChangedEventHandler<bool>(this.ParentVisibilityChanged);
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

            viewModeLabel = AddUIComponent<UILabel>();
            viewModeLabel.text = Translation.GetString("Outgoing_demand");
            viewModeLabel.relativePosition = new Vector3(3f, 33f);
            viewModeLabel.textScale = 0.75f;

            switchViewModeButton = _createButton(Translation.GetString("Switch_view"), 3, 3, clickSwitchViewMode);
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
                viewModeLabel.text = Translation.GetString("Incoming_demand");
                TrafficManagerTool.CurrentTransportDemandViewMode = TransportDemandViewMode.Incoming;
			} else {
                viewModeLabel.text = Translation.GetString("Outgoing_demand");
                TrafficManagerTool.CurrentTransportDemandViewMode = TransportDemandViewMode.Outgoing;
            }
		}

        private void ParentVisibilityChanged(UIComponent component, bool value) {
            Log._Debug($"Public transport info view panel changed visibility: {value}");
            if (value && Options.prohibitPocketCars) {
				TrafficManagerTool.CurrentTransportDemandViewMode = TransportDemandViewMode.Outgoing;
				if (viewModeLabel != null)
					viewModeLabel.text = Translation.GetString("Outgoing_demand");
				if (switchViewModeButton != null)
					switchViewModeButton.text = Translation.GetString("Switch_view");
				this.Show();
			} else
                this.Hide();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;

namespace TrafficManager.UI.MainMenu {
	public class ToggleTrafficLightsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.SwitchTrafficLight;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.ToggleTrafficLights;
			}
		}

		public override string Tooltip {
			get {
				return "Switch_traffic_lights";
			}
		}

		public override bool Visible {
			get {
				return true;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class ManualTrafficLightsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.ManualSwitch;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.ManualTrafficLights;
			}
		}

		public override string Tooltip {
			get {
				return "Manual_traffic_lights";
			}
		}

		public override bool Visible {
			get {
				return Options.timedLightsEnabled;
			}
		}
	}
}

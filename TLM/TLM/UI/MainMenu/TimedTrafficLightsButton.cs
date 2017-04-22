using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class TimedTrafficLightsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.TimedLightsSelectNode;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.TimedTrafficLights;
			}
		}

		public override string Tooltip {
			get {
				return "Timed_traffic_lights";
			}
		}

		public override bool Visible {
			get {
				return Options.timedLightsEnabled;
			}
		}
	}
}

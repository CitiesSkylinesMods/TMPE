using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;

namespace TrafficManager.UI.MainMenu {
	public class LaneConnectorButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.LaneConnector;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.LaneConnector;
			}
		}

		public override string Tooltip {
			get {
				return "Lane_connector";
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class LaneArrowsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.LaneChange;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.LaneArrows;
			}
		}

		public override string Tooltip {
			get {
				return "Change_lane_arrows";
			}
		}

		public override bool Visible {
			get {
				return true;
			}
		}
	}
}

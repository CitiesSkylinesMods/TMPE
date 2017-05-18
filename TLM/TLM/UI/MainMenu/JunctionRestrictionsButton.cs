using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class JunctionRestrictionsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.JunctionRestrictions;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.JunctionRestrictions;
			}
		}

		public override string Tooltip {
			get {
				return "Junction_restrictions";
			}
		}

		public override bool Visible {
			get {
				return Options.junctionRestrictionsEnabled;
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class PrioritySignsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.AddPrioritySigns;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.PrioritySigns;
			}
		}

		public override string Tooltip {
			get {
				return "Add_priority_signs";
			}
		}

		public override bool Visible {
			get {
				return Options.prioritySignsEnabled;
			}
		}
	}
}

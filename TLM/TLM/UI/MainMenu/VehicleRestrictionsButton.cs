using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class VehicleRestrictionsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.VehicleRestrictions;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.VehicleRestrictions;
			}
		}

		public override string Tooltip {
			get {
				return "Vehicle_restrictions";
			}
		}

		public override bool Visible {
			get {
				return Options.vehicleRestrictionsEnabled;
			}
		}
	}
}

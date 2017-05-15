using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class ParkingRestrictionsButton : MenuToolModeButton {
		public override ToolMode ToolMode {
			get {
				return ToolMode.ParkingRestrictions;
			}
		}
		
		public override ButtonFunction Function {
			get {
				return ButtonFunction.ParkingRestrictions;
			}
		}

		public override string Tooltip {
			get {
				return "Parking_restrictions";
			}
		}

		public override bool Visible {
			get {
				return Options.parkingRestrictionsEnabled;
			}
		}
	}
}

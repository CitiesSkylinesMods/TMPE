using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class ParkingRestrictionsButton : MenuToolModeButton {
		public override ToolMode ToolMode => ToolMode.ParkingRestrictions;
		public override ButtonFunction Function => ButtonFunction.ParkingRestrictions;
		public override string Tooltip => "Parking_restrictions";
		public override bool Visible => Options.parkingRestrictionsEnabled;
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;

namespace TrafficManager.UI.MainMenu {
	public class ClearTrafficButton : MenuButton {
		public override bool Active {
			get {
				return false;
			}
		}

		public override ButtonFunction Function {
			get {
				return ButtonFunction.ClearTraffic;
			}
		}

		public override string Tooltip {
			get {
				return "Clear_Traffic";
			}
		}

		public override bool Visible {
			get {
				return true;
			}
		}

		public override void OnClickInternal(UIMouseEventParameter p) {
			UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			VehicleStateManager.Instance.RequestClearTraffic();
		}
	}
}

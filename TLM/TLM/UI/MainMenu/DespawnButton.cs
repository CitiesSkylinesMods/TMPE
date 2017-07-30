using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.State;

namespace TrafficManager.UI.MainMenu {
	public class DespawnButton : MenuButton {
		public override bool Active {
			get {
				return false;
			}
		}

		public override ButtonFunction Function {
			get {
				return Options.disableDespawning ? ButtonFunction.DespawnDisabled : ButtonFunction.DespawnEnabled;
			}
		}

		public override string Tooltip {
			get {
				return Options.disableDespawning ? "Enable_despawning" : "Disable_despawning";
			}
		}

		public override bool Visible {
			get {
				return true;
			}
		}

		public override void OnClickInternal(UIMouseEventParameter p) {
			UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			Options.setDisableDespawning(!Options.disableDespawning);
		}
	}
}

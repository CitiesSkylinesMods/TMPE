using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;

namespace TrafficManager.UI.MainMenu {
	public abstract class MenuToolModeButton : MenuButton {
		public abstract ToolMode ToolMode { get; }

		public override bool Active {
			get {
				return this.ToolMode.Equals(UIBase.GetTrafficManagerTool(false)?.GetToolMode());
			}
		}

		public override void OnClickInternal(UIMouseEventParameter p) {
			if (Active) {
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			} else {
				UIBase.GetTrafficManagerTool(true).SetToolMode(this.ToolMode);
			}
		}
	}
}

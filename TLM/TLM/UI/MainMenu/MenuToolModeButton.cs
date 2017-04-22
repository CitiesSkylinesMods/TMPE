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
				return this.ToolMode.Equals(LoadingExtension.TrafficManagerTool.GetToolMode());
			}
		}

		public override void OnClickInternal(UIMouseEventParameter p) {
			if (Active) {
				LoadingExtension.TrafficManagerTool.SetToolMode(ToolMode.None);
			} else {
				LoadingExtension.TrafficManagerTool.SetToolMode(this.ToolMode);
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.UI;
using TrafficManager.Manager;
using TrafficManager.Manager.Impl;

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
			ConfirmPanel.ShowModal(Translation.GetString("Clear_Traffic"), Translation.GetString("Clear_Traffic") + "?", delegate (UIComponent comp, int ret) {
				if (ret == 1) {
					Constants.ServiceFactory.SimulationService.AddAction(() => {
						UtilityManager.Instance.ClearTraffic();
					});
				}
				UIBase.GetTrafficManagerTool(true).SetToolMode(ToolMode.None);
			});
		}
	}
}

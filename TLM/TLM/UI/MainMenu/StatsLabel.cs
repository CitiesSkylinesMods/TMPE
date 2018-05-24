using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Custom.PathFinding;
using TrafficManager.State;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public class StatsLabel : UILabel {
		public override void Start() {
			size = new Vector2(MainMenuPanel.SIZE_PROFILES[0].MENU_WIDTH / 2, MainMenuPanel.SIZE_PROFILES[0].TOP_BORDER); // TODO use current size profile
			text = "";
			relativePosition = new Vector3(5f, -20f);
			textAlignment = UIHorizontalAlignment.Left;
			anchor = UIAnchorStyle.Top | UIAnchorStyle.Left;
		}

#if QUEUEDSTATS
		public override void Update() {
			if (Options.showPathFindStats) {
				uint queued = CustomPathManager.TotalQueuedPathFinds;
				if (queued < 1000) {
					textColor = Color.Lerp(Color.green, Color.yellow, (float)queued / 1000f);
				} else if (queued < 2500) {
					textColor = Color.Lerp(Color.yellow, Color.red, (float)(queued - 1000f) / 1500f);
				} else {
					textColor = Color.red;
				}

				text = CustomPathManager.TotalQueuedPathFinds.ToString() + " PFs";
			} else {
				text = "";
				m_TextColor = Color.white;
			}
		}
#endif
	}
}

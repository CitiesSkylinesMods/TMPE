using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public class VersionLabel : UILabel {
		public override void Start() {
			size = new Vector2(MainMenuPanel.MENU_WIDTH, MainMenuPanel.TOP_BORDER);
			text = "TM:PE " + TrafficManagerMod.Version;
			relativePosition = new Vector3(5f, 5f);
			textAlignment = UIHorizontalAlignment.Left;
		}
	}
}

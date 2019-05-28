using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public class VersionLabel : UILabel {
		public override void Start() {

            size = new Vector2(MainMenuPanel.SIZE_PROFILES[0].MENU_WIDTH, MainMenuPanel.SIZE_PROFILES[0].TOP_BORDER); // TODO use current size profile

#if LABS
            text = "TM:PE " + TrafficManagerMod.Version + "-L";
#elif DEBUG
            text = "TM:PE " + TrafficManagerMod.Version + "-D";
#else // STABLE
            text = "TM:PE " + TrafficManagerMod.Version + "-S";
#endif
            relativePosition = new Vector3(5f, 5f);

            textAlignment = UIHorizontalAlignment.Left;
		}
	}
}

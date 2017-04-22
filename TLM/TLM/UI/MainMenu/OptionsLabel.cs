using ColossalFramework.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
	public class OptionsLabel : UILabel {
		public override void Awake() {
			base.Awake();
			size = new Vector2(85f, 20f);
			font.size = 9;
			text = Translation.GetString("Options"); // TODO use game translation
			relativePosition = new Vector3(90f, 5f);
			textAlignment = UIHorizontalAlignment.Right;
		}
	}
}

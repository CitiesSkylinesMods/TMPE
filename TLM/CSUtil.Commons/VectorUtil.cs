using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CSUtil.Commons {
	public static class VectorUtil {
		public static void ClampPosToScreen(ref Vector3 pos) {
			if (pos.x < 0)
				pos.x = 0;
			if (pos.y < 0)
				pos.y = 0;
			if (pos.x >= Screen.currentResolution.width)
				pos.x = Screen.currentResolution.width - 1;
			if (pos.y >= Screen.currentResolution.height)
				pos.y = Screen.currentResolution.height - 1;
		}
	}
}

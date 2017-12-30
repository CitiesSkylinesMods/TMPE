using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CSUtil.Commons {
	public static class VectorUtil {
		public static void ClampPosToScreen(ref Vector3 pos, Vector2 resolution) {
			if (pos.x < 0)
				pos.x = 0;
			if (pos.y < 0)
				pos.y = 0;
			if (pos.x >= Screen.width)
				pos.x = Screen.width - 1;
			if (pos.y >= Screen.height)
				pos.y = Screen.height - 1;
		}
	}
}

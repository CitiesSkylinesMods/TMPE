using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Util {
	public static class LoopUtil {
		public delegate bool TwoDimLoopHandler(int x, int y);

		public static void SpiralLoop(int xCenter, int yCenter, int width, int height, TwoDimLoopHandler handler) {
			SpiralLoop(width, height, delegate(int x, int y) {
				return handler(xCenter + x, yCenter + y);
			});
		}

		public static void SpiralLoop(int width, int height, TwoDimLoopHandler handler) {
			int x, y, dx, dy;
			x = y = dx = 0;
			dy = -1;
			int t = width > height ? width : height;
			int maxI = t * t;
			for (int i = 0; i < maxI; i++) {
				if ((-width / 2 <= x) && (x <= width / 2) && (-height / 2 <= y) && (y <= height / 2)) {
					if (! handler(x, y))
						break;
				}
				if ((x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1 - y))) {
					t = dx;
					dx = -dy;
					dy = t;
				}
				x += dx;
				y += dy;
			}
		}
	}
}

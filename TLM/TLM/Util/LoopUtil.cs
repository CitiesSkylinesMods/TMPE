using System.Collections.Generic;
using UnityEngine;

namespace TrafficManager.Util {
    public static class LoopUtil {
        public delegate bool TwoDimLoopHandler(int x, int y);

        public static void SpiralLoop(int xCenter,
                                      int yCenter,
                                      int width,
                                      int height,
                                      TwoDimLoopHandler handler) {
            SpiralLoop(
                width,
                height,
                (x, y) => handler(xCenter + x, yCenter + y));
        }

        public static void SpiralLoop(int width, int height, TwoDimLoopHandler handler) {
            int x, y, dx, dy;
            x = y = dx = 0;
            dy = -1;
            int t = width > height ? width : height;
            int maxI = t * t;
            for (int i = 0; i < maxI; i++) {
                if ((-width / 2 <= x) && (x <= width / 2) && (-height / 2 <= y)
                    && (y <= height / 2))
                {
                    if (!handler(x, y)) {
                        break;
                    }
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

        public static IEnumerable<Vector2> GenerateLoopCoords(int sizeX, int sizeY) {
            var x = 0;
            var y = 0;
            var dx = 0;
            var dy = -1;
            var largerDimension = sizeX > sizeY ? sizeX : sizeY;
            var maxI = largerDimension * largerDimension;
            for (int i = 0; i < maxI; i++) {
                if ((x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1 - y))) {
                    largerDimension = dx;
                    dx = -dy;
                    dy = largerDimension;
                }

                yield return new Vector2(x, y);

                x += dx;
                y += dy;
            }
        }
    }
}
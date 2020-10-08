using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficManager.Util {
    public static class LoopUtil {
        public delegate bool TwoDimLoopHandler(int x, int y);

        [Obsolete("Use 'GenerateSpiralGridCoordsClockwise' instead.")]
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

        [Obsolete("Use 'GenerateSpiralGridCoordsClockwise' instead.")]
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

        /// <summary>
        /// Generates a stream of spiral grid coordinates clockwise.
        /// </summary>
        /// <returns>Stream of spiral grid coordinates until int.MaxValue + 1 elements were returned.</returns>
        public static IEnumerable<Vector2> GenerateSpiralGridCoordsClockwise() {
            var cursorX = 0;
            var cursorY = 0;

            var directionX = 1;
            var directionY = 0;

            var segmentLength = 1;
            var segmentsPassed = 0;

            for (var n = 0; n <= int.MaxValue; n++) {
                yield return new Vector2(cursorX, cursorY);

                cursorX += directionX;
                cursorY += directionY;

                segmentsPassed++;
                if (segmentsPassed != segmentLength) {
                    continue; //if segment is not full yet
                }
                segmentsPassed = 0; //start of new segment

                //rotate clockwise
                var buffer = directionX;
                directionX = -directionY;
                directionY = buffer;

                //increase segmentLength every second time
                if (directionY == 0) {
                    segmentLength++;
                }
            }
        }
    }
}
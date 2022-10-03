using System.Collections.Generic;
using UnityEngine;

namespace TrafficManager.Util {
    public static class LoopUtil {
        /// <summary>
        /// Generates a stream of spiral grid coordinates clockwise.
        /// </summary>
        /// <returns>Stream of spiral grid coordinates until int.MaxValue + 1 elements were returned.</returns>
        public static IEnumerable<Vector2> GenerateSpiralGridCoordsClockwise() {
            var cursorX = 0;
            var cursorY = 0;

            var directionX = -1;
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
                var buffer = -directionX;
                directionX = directionY;
                directionY = buffer;

                //increase segmentLength every second time
                if (directionY == 0) {
                    segmentLength++;
                }
            }
        }
        
        /// <summary>
        /// Generates a stream of spiral grid coordinates counterclockwise.
        /// </summary>
        /// <returns>Stream of spiral grid coordinates until int.MaxValue + 1 elements were returned.</returns>
        public static IEnumerable<Vector2> GenerateSpiralGridCoordsCounterclockwise() {
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

                //rotate counterClockwise
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TrafficManager.Util;
using UnityEngine;

namespace TMUnitTest.Util {
    [TestClass]
    public class LoopUtilTests {
        [TestMethod]
        public void CompareSpiralLoopToGenerateLoopCoords_RadiusZero() {
            var radius = 0;

            var expected = new List<Vector2>();
            LoopUtil.SpiralLoop(radius, radius, (x, y) => {
                expected.Add(new Vector2(x, y));
                return true;
            });

            var actual = LoopUtil.GenerateSpiralGridCoordsClockwise(radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CompareSpiralLoopToGenerateLoopCoords_Radius10() {
            var radius = 10;

            var expected = new List<Vector2>();
            LoopUtil.SpiralLoop(radius, radius, (x, y) => {
                expected.Add(new Vector2(x, y));
                return true;
            });

            var actual = LoopUtil.GenerateSpiralGridCoordsClockwise(radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CompareSpiralLoopToGenerateLoopCoords_Radius33() {
            var radius = 33;

            var expected = new List<Vector2>();
            LoopUtil.SpiralLoop(radius, radius, (x, y) => {
                expected.Add(new Vector2(x, y));
                return true;
            });

            var actual = LoopUtil.GenerateSpiralGridCoordsClockwise(radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CompareSpiralLoopToGenerateLoopCoords_Radius33_WithOffset() {
            var radius = 33;
            var offset = new Vector2(121.3f, 324.7f);

            var expected = new List<Vector2>();
            LoopUtil.SpiralLoop((int)offset.x, (int)offset.y, radius, radius, (x, y) => {
                expected.Add(new Vector2(x, y));
                return true;
            });

            var actual = LoopUtil.GenerateSpiralGridCoordsClockwise(radius)
                .Select(p => p + new Vector2((int)offset.x, (int)offset.y))
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void CompareSpiralLoopToGenerateLoopCoords_BreakCondition() {
            var radius = 50;

            var expected = new List<Vector2>();
            LoopUtil.SpiralLoop(radius, radius, (x, y) => {
                if (x == 4) {
                    return false;
                }

                expected.Add(new Vector2(x, y));
                return true;
            });

            var actual = new List<Vector2>();
            foreach (var item in LoopUtil.GenerateSpiralGridCoordsClockwise(radius)) {
                if (item.x == 4) {
                    break;
                }

                actual.Add(item);
            }

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

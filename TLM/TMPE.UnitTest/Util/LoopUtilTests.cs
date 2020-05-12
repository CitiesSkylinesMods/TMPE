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
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TrafficManager.Util;
using UnityEngine;

namespace TMUnitTest.Util {
    [TestClass]
    public class LoopUtilTests {
        [TestMethod]
        public void GenerateSpiralGridCoordsClockwise_Radius1() {
            var radius = 1;
            var expected = new List<Vector2>()
            {
                new Vector2() { x = 0f, y = 0f }
            };

            var actual = LoopUtil.GenerateSpiralGridCoordsCounterclockwise()
                .Take(radius * radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GenerateSpiralGridCoordsClockwise_Radius5() {
            var radius = 5;
            var expected = new List<Vector2>()
            {
                new Vector2() { x = 0f, y = 0f },
                new Vector2() { x = -1f, y = 0f },
                new Vector2() { x = -1f, y = 1f },
                new Vector2() { x = 0f, y = 1f },
                new Vector2() { x = 1f, y = 1f },
                new Vector2() { x = 1f, y = 0f },
                new Vector2() { x = 1f, y = -1f },
                new Vector2() { x = 0f, y = -1f },
                new Vector2() { x = -1f, y = -1f },
                new Vector2() { x = -2f, y = -1f },
                new Vector2() { x = -2f, y = 0f },
                new Vector2() { x = -2f, y = 1f },
                new Vector2() { x = -2f, y = 2f },
                new Vector2() { x = -1f, y = 2f },
                new Vector2() { x = 0f, y = 2f },
                new Vector2() { x = 1f, y = 2f },
                new Vector2() { x = 2f, y = 2f },
                new Vector2() { x = 2f, y = 1f },
                new Vector2() { x = 2f, y = 0f },
                new Vector2() { x = 2f, y = -1f },
                new Vector2() { x = 2f, y = -2f },
                new Vector2() { x = 1f, y = -2f },
                new Vector2() { x = 0f, y = -2f },
                new Vector2() { x = -1f, y = -2f },
                new Vector2() { x = -2f, y = -2f },
            };

            var actual = LoopUtil.GenerateSpiralGridCoordsClockwise()
                .Take(radius * radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void GenerateSpiralGridCoordsCounterclockwise_Radius5() {
            var radius = 5;
            var expected = new List<Vector2>()
            {
                new Vector2() { x = 0f, y = 0f },
                new Vector2() { x = 1f, y = 0f },
                new Vector2() { x = 1f, y = 1f },
                new Vector2() { x = 0f, y = 1f },
                new Vector2() { x = -1f, y = 1f },
                new Vector2() { x = -1f, y = 0f },
                new Vector2() { x = -1f, y = -1f },
                new Vector2() { x = 0f, y = -1f },
                new Vector2() { x = 1f, y = -1f },
                new Vector2() { x = 2f, y = -1f },
                new Vector2() { x = 2f, y = 0f },
                new Vector2() { x = 2f, y = 1f },
                new Vector2() { x = 2f, y = 2f },
                new Vector2() { x = 1f, y = 2f },
                new Vector2() { x = 0f, y = 2f },
                new Vector2() { x = -1f, y = 2f },
                new Vector2() { x = -2f, y = 2f },
                new Vector2() { x = -2f, y = 1f },
                new Vector2() { x = -2f, y = 0f },
                new Vector2() { x = -2f, y = -1f },
                new Vector2() { x = -2f, y = -2f },
                new Vector2() { x = -1f, y = -2f },
                new Vector2() { x = 0f, y = -2f },
                new Vector2() { x = 1f, y = -2f },
                new Vector2() { x = 2f, y = -2f },
            };

            var actual = LoopUtil.GenerateSpiralGridCoordsCounterclockwise()
                .Take(radius * radius)
                .ToList();

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

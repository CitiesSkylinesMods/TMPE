using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TrafficManager.Util;
using UnityEngine;

namespace TMUnitTest.Util {
    [TestClass]
    public class LoopUtilTests {
        [TestMethod]
        public void TestSizeXAndSizeYIsZero() {
            var sizeX = 0;
            var sizeY = 0;

            var resultSpiralLoop = new List<Vector2>();
            LoopUtil.SpiralLoop(sizeX, sizeY, (x, y) => {
                resultSpiralLoop.Add(new Vector2(x, y));
                return true;
            });

            var resultGenerateLoopCoords = LoopUtil.GenerateLoopCoords(sizeX, sizeY)
                .ToList();

            CollectionAssert.AreEqual(resultSpiralLoop, resultGenerateLoopCoords);
        }

        [TestMethod]
        public void TestSizeXSmallerSizeY() {
            var sizeX = 3;
            var sizeY = 4;

            var resultSpiralLoop = new List<Vector2>();
            LoopUtil.SpiralLoop(sizeX, sizeY, (x, y) => {
                resultSpiralLoop.Add(new Vector2(x, y));
                return true;
            });

            var resultGenerateLoopCoords = LoopUtil.GenerateLoopCoords(sizeX, sizeY)
                .ToList();

            CollectionAssert.AreEqual(resultSpiralLoop, resultGenerateLoopCoords);
        }

        [TestMethod]
        public void TestSizeXLargerSizeY() {
            var sizeX = 4;
            var sizeY = 3;

            var resultSpiralLoop = new List<Vector2>();
            LoopUtil.SpiralLoop(sizeX, sizeY, (x, y) => {
                resultSpiralLoop.Add(new Vector2(x, y));
                return true;
            });

            var resultGenerateLoopCoords = LoopUtil.GenerateLoopCoords(sizeX, sizeY)
                .ToList();

            CollectionAssert.AreEqual(resultSpiralLoop, resultGenerateLoopCoords);
        }

        [TestMethod]
        public void TestSizeXEqualSizeY() {
            var sizeX = 4;
            var sizeY = 4;

            var resultSpiralLoop = new List<Vector2>();
            LoopUtil.SpiralLoop(sizeX, sizeY, (x, y) => {
                resultSpiralLoop.Add(new Vector2(x, y));
                return true;
            });

            var resultGenerateLoopCoords = LoopUtil.GenerateLoopCoords(sizeX, sizeY)
                .ToList();

            CollectionAssert.AreEqual(resultSpiralLoop, resultGenerateLoopCoords);
        }
    }
}

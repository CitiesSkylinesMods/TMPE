using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TrafficManager.Util;
using UnityEngine;

namespace TMUnitTest.Util {
    [TestClass]
    public class SpiralTests {
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_RadiusOutOfRange_Should_ThrowException() {
            _ = new Spiral(0);
        }

        [TestMethod]
        public void Constructor_Radius1_Should_Generate1SpiralCoords() {
            var initialRadius = 1;
            var expectedSpiralCoordsCount = initialRadius * initialRadius;

            var cachedSpiral = new Spiral(initialRadius);

            Assert.AreEqual(expectedSpiralCoordsCount, cachedSpiral.GetCoords(initialRadius).Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void GetSpiralCoords_RadiusOutOfRange_Should_ThrowException() {
            var spiral = new Spiral(1);
            spiral.GetCoords(-1);
        }

        [TestMethod]
        public void GetSpiralCoords_Should_IncreaseIfRadiusLargerInitialRadius() {
            var initialRadius = 1;
            var newRadius = 5;
            var expectedSpiralCoordsCount = newRadius * newRadius;

            var cachedSpiral = new Spiral(initialRadius);

            Assert.AreEqual(expectedSpiralCoordsCount, cachedSpiral.GetCoords(newRadius).Count);
        }

        [TestMethod]
        public void GetSpiralCoords_Should_BeCorrectAfterEachTime() {
            var initialRadius = 1;
            var initialRadiusExpectedResult = new ReadOnlyCollection<Vector2>(
                new List<Vector2>() { new Vector2(0, 0) }
            );

            var firstRadiusIncrease = 2;
            var firstRadiusIncreaseExpectedResult = new ReadOnlyCollection<Vector2>(
                new List<Vector2>() {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                }
            );

            var secondRadiusIncrease = 3;
            var secondRadiusIncreaseExpectedResult = new ReadOnlyCollection<Vector2>(
                new List<Vector2>() {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                    new Vector2(-1, 1),
                    new Vector2(-1, 0),
                    new Vector2(-1, -1),
                    new Vector2(0, -1),
                    new Vector2(1, -1),
                }
            );

            var cachedSpiral = new Spiral(initialRadius);
            var initialRadiusActualResult = cachedSpiral.GetCoords(initialRadius);
            CollectionAssert.AreEqual(initialRadiusExpectedResult, initialRadiusActualResult);

            var firstRadiusIncreaseActualResult = cachedSpiral.GetCoords(firstRadiusIncrease);
            CollectionAssert.AreEqual(firstRadiusIncreaseExpectedResult, firstRadiusIncreaseActualResult);

            var secondRadiusIncreaseActualResult = cachedSpiral.GetCoords(secondRadiusIncrease);
            CollectionAssert.AreEqual(secondRadiusIncreaseExpectedResult, secondRadiusIncreaseActualResult);
        }

        [TestMethod]
        public void GetSpiralCoords_Should_ShareTheResult() {
            var initialRadius = 1;
            var firstRadiusIncrease = 2;
            var secondRadiusIncrease = 3;
            var sharedExpectedResult = new ReadOnlyCollection<Vector2>(
                new List<Vector2>() {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                    new Vector2(-1, 1),
                    new Vector2(-1, 0),
                    new Vector2(-1, -1),
                    new Vector2(0, -1),
                    new Vector2(1, -1),
                }
            );

            var cachedSpiral = new Spiral(initialRadius);
            var initialRadiusActualResult = cachedSpiral.GetCoords(initialRadius);
            var firstRadiusIncreaseActualResult = cachedSpiral.GetCoords(firstRadiusIncrease);
            var secondRadiusIncreaseActualResult = cachedSpiral.GetCoords(secondRadiusIncrease);

            CollectionAssert.AreEqual(sharedExpectedResult, initialRadiusActualResult);
            CollectionAssert.AreEqual(sharedExpectedResult, firstRadiusIncreaseActualResult);
            CollectionAssert.AreEqual(sharedExpectedResult, secondRadiusIncreaseActualResult);
        }
    }
}

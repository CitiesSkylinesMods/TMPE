using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using CSModLib.GameObjects;
using static CSModLib.GameObjects.ExtNetInfo;

namespace CSModLib.Test.GameObjects {
    [TestClass]
    public class ExtNetInfoTest {

        [TestMethod]
        public void TestTwoLane() {

            var lanes = new[] {
                PedestrianLane(-6.5f, 3f),
                PedestrianLane(6.5f, 3f),
                ParkingLane(-4f, true),
                ParkingLane(4f, false),
                CarLane(-1.5f, true),
                CarLane(1.5f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.TwoWay, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 5, 3, 1);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 4);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterForward, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestTwoLaneOneWay() {

            var lanes = new[] {
                PedestrianLane(-6.5f, 3f),
                PedestrianLane(6.5f, 3f),
                ParkingLane(-4f, false),
                ParkingLane(4f, false),
                CarLane(-1.5f, false),
                CarLane(1.5f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.OneWay, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 5, 3, 1);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(1, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterForward, 4, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestFourLane() {

            var lanes = new[] {
                PedestrianLane(-13.5f, 5f),
                PedestrianLane(13.5f, 5f),
                ParkingLane(-10f, true),
                ParkingLane(10f, false),
                CarLane(-7.5f, true),
                CarLane(7.5f, false),
                CarLane(-4.5f, true),
                CarLane(4.5f, false),
                RaisedMedianLane(0f),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.TwoWay, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 6, 8, 7, 5, 3, 1);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 4, 6);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterForward, 7, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.None);
        }

        [TestMethod]
        public void TestFourLaneInverted() {

            var lanes = new[] {
                PedestrianLane(-13.5f, 5f),
                PedestrianLane(13.5f, 5f),
                ParkingLane(-10f, false),
                ParkingLane(10f, true),
                CarLane(-7.5f, false),
                CarLane(7.5f, true),
                CarLane(-4.5f, false),
                CarLane(4.5f, true),
                RaisedMedianLane(0f),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Inverted, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 6, 8, 7, 5, 3, 1);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterForward, 4, 6);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterBackward, 7, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.None);
        }

        [TestMethod]
        public void TestCFI() {

            var lanes = new[] {
                CarLane(0f, true),
                ThruCarLane(3f, false),
                ThruCarLane(6f, false),
                ThruCarLane(9f, true),
                ThruCarLane(12f, true),
                CarLane(15f, false),
                CarLane(18f, false),
                CarLane(21f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.AllowCFI, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.AllowCFI, 3, 4);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.OuterForward, 5, 6, 7);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.AllowCFI);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.AllowCFI);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.AllowCFI);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.AllowCFI);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestUncontrolledDisplaced() {

            var lanes = new[] {
                CarLane(0f, true),
                CarLane(3f, false),
                CarLane(6f, true),
                CarLane(9f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.ForbidControlledLanes, 1);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.ForbidControlledLanes, 2);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.OuterForward, 3);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.ForbidControlledLanes);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.ForbidControlledLanes);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestForwardTurnaround() {

            var lanes = new[] {
                CarLane(0f, false),
                CarLane(3f, true),
                CarLane(6f, true),
                CarLane(9f, false),
                CarLane(12f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(3, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterBackward, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.OuterForward, 3, 4);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestBackwardTurnaround() {

            var lanes = new[] {
                CarLane(0f, true),
                CarLane(3f, true),
                CarLane(6f, false),
                CarLane(9f, false),
                CarLane(12f, true),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(3, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterForward, 2, 3);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedOuterBackward, 4);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.DisplacedOuterBackward);
        }

        [TestMethod]
        public void TestDualTurnaround() {

            var lanes = new[] {
                CarLane(0f, false),
                CarLane(3f, true),
                CarLane(6f, true),
                CarLane(9f, false),
                CarLane(12f, false),
                CarLane(15f, true),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterBackward, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.OuterForward, 3, 4);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.DisplacedOuterBackward, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.DisplacedOuterBackward);
        }


        [TestMethod]
        public void TestDoubleDualTurnaround() {

            var lanes = new[] {
                CarLane(0f, false),
                CarLane(3f, false),
                CarLane(6f, true),
                CarLane(9f, true),
                CarLane(12f, false),
                CarLane(15f, false),
                CarLane(18f, true),
                CarLane(21f, true),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.OuterBackward, 2, 3);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.OuterForward, 4, 5);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.DisplacedOuterBackward, 6, 7);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.DisplacedOuterForward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.DisplacedOuterForward);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.DisplacedOuterBackward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.DisplacedOuterBackward);
        }

        [TestMethod]
        public void TestInnerDisplacedBusLanes() {

            var lanes = new[] {
                CarLane(0f, true),
                CarLane(3f, true),
                ThruBusLane(6f, false),
                ThruBusLane(9f, false),
                PedestrianLane(13.5f, 6f),
                ThruBusLane(18f, true),
                ThruBusLane(21f, true),
                CarLane(24f, false),
                CarLane(27f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7, 8);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInnerForward, 2, 3);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInnerBackward, 5, 6);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.OuterForward, 7, 8);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.OuterBackward);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInnerForward);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.DisplacedInnerForward);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.DisplacedInnerBackward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.DisplacedInnerBackward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.OuterForward);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.OuterForward);
        }

        [TestMethod]
        public void TestFourCarriageway() {

            var lanes = new[] {
                PedestrianLane(0f, 3f),
                ParkingLane(3f, true, 3f),
                CarLane(6f, true),
                PedestrianLane(9f, 3f),
                ThruCarLane(12f, true),
                ThruCarLane(15f, true),
                RaisedMedianLane(18f),
                ThruCarLane(21f, false),
                ThruCarLane(24f, false),
                PedestrianLane(27f, 3f),
                CarLane(30f, false),
                ParkingLane(33f, false, 3f),
                PedestrianLane(36f, 3f),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.TwoWay, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane, 2);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane, 4, 5);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane, 7, 8);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane, 10);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 9, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 10, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 11, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 12, ExtLaneFlags.None);
        }

        [TestMethod]
        public void TestThreeCarriageway() {

            var lanes = new[] {
                PedestrianLane(0f, 3f),
                ParkingLane(3f, true, 3f),
                CarLane(6f, true),
                PedestrianLane(9f, 3f),
                ThruCarLane(12f, true),
                ThruCarLane(15f, true),
                ThruCarLane(18f, false),
                ThruCarLane(21f, false),
                PedestrianLane(24f, 3f),
                CarLane(27f, false),
                ParkingLane(30f, false, 3f),
                PedestrianLane(33f, 3f),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.TwoWay, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane, 2);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane, 4, 5);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane, 6, 7);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane, 9);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 9, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 10, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 11, ExtLaneFlags.None);
        }

        [TestMethod]
        public void TestSixCarriageway() {

            var lanes = new[] {
                CarLane(0f, true),
                CarLane(3f, true),
                RaisedMedianLane(6f),
                ThruCarLane(9f, true),
                ThruCarLane(12f, true),
                ThruBusLane(15f, false),
                ThruBusLane(18f, false),
                PedestrianLane(22.5f, 6f),
                ThruBusLane(27f, true),
                ThruBusLane(30f, true),
                ThruCarLane(33f, false),
                ThruCarLane(36f, false),
                RaisedMedianLane(39f),
                CarLane(42f, false),
                CarLane(45f, false),
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(6, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane, 3, 4);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInnerForward, 5, 6);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.DisplacedInnerBackward, 8, 9);
            VerifyGroup(extNetInfo, 4, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane, 10, 11);
            VerifyGroup(extNetInfo, 5, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane, 13, 14);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.InnerBackward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.DisplacedInnerForward);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.DisplacedInnerForward);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.DisplacedInnerBackward);
            VerifyLane(extNetInfo, 9, ExtLaneFlags.DisplacedInnerBackward);
            VerifyLane(extNetInfo, 11, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 10, ExtLaneFlags.InnerForward | ExtLaneFlags.AllowExpressLane);
            VerifyLane(extNetInfo, 12, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 13, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 14, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane);
        }

        [TestMethod]
        public void TestLargeCargoRoad() {

            var lanes = new[] {
                FlushMedianLane(-16.4f),  // 0
                FlushMedianLane(16.4f),   // 1
                FlushMedianLane(5.5f),    // 2
                PedestrianLane(-27f, 2f), // 3
                FlushMedianLane(-5.5f),   // 4
                PedestrianLane(27f, 2f),  // 5
                CarLane(-11.1f, true),    // 6
                CarLane(11.1f, false),    // 7
                CarLane(-22.5f, true),    // 8
                CarLane(22.5f, false),    // 9
                CarLane(8.1f, true),      // 10
                CarLane(-8.1f, false),    // 11
            };

            var extNetInfo = new ExtNetInfo(lanes);

            Assert.AreEqual(LaneConfiguration.Complex, extNetInfo.m_roadLaneConfiguration);
            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 3, 8, 0, 6, 11, 4, 2, 10, 7, 1, 9, 5);

            VerifyAggregations(extNetInfo);

            Assert.AreEqual(6, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane, 8);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.InnerBackward | ExtLaneFlags.ForbidControlledLanes, 6);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.ForbidControlledLanes, 11);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.ForbidControlledLanes, 10);
            VerifyGroup(extNetInfo, 4, ExtLaneFlags.InnerForward | ExtLaneFlags.ForbidControlledLanes, 7);
            VerifyGroup(extNetInfo, 5, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane, 9);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.InnerBackward | ExtLaneFlags.ForbidControlledLanes);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.InnerForward | ExtLaneFlags.ForbidControlledLanes);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.OuterBackward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 9, ExtLaneFlags.OuterForward | ExtLaneFlags.AllowServiceLane);
            VerifyLane(extNetInfo, 10, ExtLaneFlags.DisplacedInnerBackward | ExtLaneFlags.ForbidControlledLanes);
            VerifyLane(extNetInfo, 11, ExtLaneFlags.DisplacedInnerForward | ExtLaneFlags.ForbidControlledLanes);
        }

        private void VerifyAggregations(ExtNetInfo extNetInfo) {

            var expectedForwardFlags = extNetInfo.m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.ForwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);
            var expectedBackwardFlags = extNetInfo.m_extLanes.Select(l => l.m_extFlags).Where(f => (f & ExtLaneFlags.BackwardGroup) != 0).DefaultIfEmpty().Aggregate((x, y) => x | y);
            var expectedFlags = extNetInfo.m_extLanes.Select(l => l.m_extFlags).DefaultIfEmpty().Aggregate((x, y) => x | y);

            Assert.AreEqual(expectedForwardFlags, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(expectedBackwardFlags, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(expectedFlags, extNetInfo.m_extLaneFlags);
        }

        private void VerifyGroup(ExtNetInfo extNetInfo, int groupIndex, ExtLaneFlags laneFlags, params int[] sortedLanes) {
            Assert.AreEqual(laneFlags, extNetInfo.m_laneGroups[groupIndex].m_extLaneFlags);
            VerifySequence(extNetInfo.m_laneGroups[groupIndex].m_sortedLanes, sortedLanes);
        }

        private void VerifyLane(ExtNetInfo extNetInfo, int laneIndex, ExtLaneFlags flags) {
            Assert.AreEqual(flags, extNetInfo.m_extLanes[laneIndex].m_extFlags);
        }

        private void VerifySequence(int[] actual, params int[] expected) {
            CollectionAssert.AreEqual(expected, actual);
        }

        private NetInfo.Lane PedestrianLane(float position, float width) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = width,
                m_verticalOffset = 0f,
                m_laneType = NetInfo.LaneType.Pedestrian,
                m_direction = NetInfo.Direction.Both,
            };

        private NetInfo.Lane ParkingLane(float position, bool backward, float width = 2f) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = width,
                m_verticalOffset = -.3f,
                m_laneType = NetInfo.LaneType.Parking,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = backward ? NetInfo.Direction.Backward : NetInfo.Direction.Forward,
            };

        private NetInfo.Lane CarLane(float position, bool backward) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = 3f,
                m_verticalOffset = -.3f,
                m_laneType = NetInfo.LaneType.Vehicle,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = backward ? NetInfo.Direction.Backward : NetInfo.Direction.Forward,
                m_allowConnect = true,
            };

        private NetInfo.Lane ThruCarLane(float position, bool backward) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = 3f,
                m_verticalOffset = -.3f,
                m_laneType = NetInfo.LaneType.Vehicle,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = backward ? NetInfo.Direction.Backward : NetInfo.Direction.Forward,
                m_allowConnect = false,
            };

        private NetInfo.Lane BusLane(float position, bool backward) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = 3f,
                m_verticalOffset = -.3f,
                m_laneType = NetInfo.LaneType.TransportVehicle,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = backward ? NetInfo.Direction.Backward : NetInfo.Direction.Forward,
                m_allowConnect = true,
            };

        private NetInfo.Lane ThruBusLane(float position, bool backward) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = 3f,
                m_verticalOffset = -.3f,
                m_laneType = NetInfo.LaneType.TransportVehicle,
                m_vehicleType = VehicleInfo.VehicleType.Car,
                m_direction = backward ? NetInfo.Direction.Backward : NetInfo.Direction.Forward,
                m_allowConnect = false,
            };

        private NetInfo.Lane RaisedMedianLane(float position, float width = 0f) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = width,
                m_verticalOffset = 0f,
                m_direction = NetInfo.Direction.Both,
            };

        private NetInfo.Lane FlushMedianLane(float position, float width = 0f) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = width,
                m_verticalOffset = -.3f,
                m_direction = NetInfo.Direction.Both,
            };
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TrafficManager.ExtPrefabs;
using static TrafficManager.ExtPrefabs.ExtNetInfo;

namespace TMUnitTest.ExtPrefabs {
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 5, 3, 1);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 4);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 5, 3, 1);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(1, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 4, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 6, 8, 7, 5, 3, 1);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 4, 6);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 7, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.ForwardGroup, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.BackwardGroup, 3, 4);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 5, 6, 7);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.DisplacedInner | ExtLaneFlags.AllowCFI | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.ForwardGroup, 1);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.BackwardGroup, 2);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 3);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForbidControlledLanes | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(3, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 3, 4);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(3, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 2, 3);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup, 4);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup, 0);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 1, 2);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 3, 4);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.DisplacedOuter | ExtLaneFlags.BackwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 1, 2, 3, 4, 5, 6, 7, 8);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(4, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 0, 1);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForwardGroup, 2, 3);
            VerifyGroup(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.BackwardGroup, 5, 6);
            VerifyGroup(extNetInfo, 3, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 7, 8);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.DisplacedInner | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.DisplacedInner | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.DisplacedInner | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
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

            Assert.AreEqual(lanes.Length, extNetInfo.m_extLanes.Length);
            VerifySequence(extNetInfo.m_sortedLanes, 0, 2, 4, 6, 8, 7, 5, 3, 1);

            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, extNetInfo.m_forwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, extNetInfo.m_backwardExtLaneFlags);
            Assert.AreEqual(ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup | ExtLaneFlags.BackwardGroup, extNetInfo.m_extLaneFlags);

            Assert.AreEqual(2, extNetInfo.m_laneGroups.Length);
            VerifyGroup(extNetInfo, 0, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup, 4, 6);
            VerifyGroup(extNetInfo, 1, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup, 7, 5);

            VerifyLane(extNetInfo, 0, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 1, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 2, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 3, ExtLaneFlags.None);
            VerifyLane(extNetInfo, 4, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 5, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 6, ExtLaneFlags.Outer | ExtLaneFlags.ForwardGroup);
            VerifyLane(extNetInfo, 7, ExtLaneFlags.Outer | ExtLaneFlags.BackwardGroup);
            VerifyLane(extNetInfo, 8, ExtLaneFlags.None);
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

        private NetInfo.Lane ParkingLane(float position, bool backward) =>
            new NetInfo.Lane {
                m_position = position,
                m_width = 2f,
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
            };
    }
}

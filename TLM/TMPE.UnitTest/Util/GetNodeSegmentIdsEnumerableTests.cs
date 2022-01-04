using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using TrafficManager.Manager.Impl;
using TrafficManager.Util.Iterators;

namespace TMUnitTest.Util {
    [TestClass]
    public class GetNodeSegmentIdsEnumerableTests {
        [TestMethod]
        public void ToArray_InitialSegmentId1_Clockwise() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new ushort[] { 1, 2, 3, 4 };

            var actual = new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ToArray_InitialSegmentId3_Clockwise() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new ushort[] { 3, 4, 1, 2 };

            var actual = new GetNodeSegmentIdsEnumerable(1, 3, ClockDirection.Clockwise, testSegments).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void ToArray_InitialSegmentId1_CounterClockwise() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new ushort[] { 1, 4, 3, 2 };

            var actual = new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.CounterClockwise, testSegments).ToArray();

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Foreach_InitialSegmentId1_Clockwise() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new List<ushort> { 1, 2, 3, 4 };
            var actual = new List<ushort>();

            foreach (var item in new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments)) {
                actual.Add(item);
            }

            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Foreach_StartAt1_Clockwise_TwoTimes() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new List<ushort> { 1, 2, 3, 4 };
            var actual1 = new List<ushort>();
            var actual2 = new List<ushort>();

            foreach (var item in new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments)) {
                actual1.Add(item);
            }

            foreach (var item in new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments)) {
                actual2.Add(item);
            }

            CollectionAssert.AreEqual(expected, actual1);
            CollectionAssert.AreEqual(expected, actual2);
        }

        [TestMethod]
        public void Foreach_ViaInterface_InitialSegmentId1_Clockwise() {
            var testSegments = new NetSegment[] {
                default,
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 1,
                    m_startLeftSegment = 2,
                    m_startRightSegment = 4,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 2,
                    m_startLeftSegment = 3,
                    m_startRightSegment = 1,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 3,
                    m_startLeftSegment = 4,
                    m_startRightSegment = 2,
                },
                new NetSegment() {
                    m_startNode = 1,
                    m_endNode = 4,
                    m_startLeftSegment = 1,
                    m_startRightSegment = 3,
                },
            };

            var expected = new List<ushort> { 1, 2, 3, 4 };
            var actual = new List<ushort>();

            IEnumerable<ushort> nodeSegmentIds = new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments);
            foreach (var item in nodeSegmentIds) {
                actual.Add(item);
            }

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}

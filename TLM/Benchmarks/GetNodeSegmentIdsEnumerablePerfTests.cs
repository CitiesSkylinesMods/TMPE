using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using TrafficManager.Manager.Impl;
using TrafficManager.Util.Iterators;

namespace Benchmarks {
    public class GetNodeSegmentIdsEnumerablePerfTests {
        private readonly static NetSegment[] testSegments = new NetSegment[] {
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
                m_startRightSegment = 3,
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
                m_startRightSegment = 1,
            },
        };

        [Benchmark]
        public void Foreach_Direct() {
            foreach (ushort item in new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments)) {
            }
        }

        [Benchmark]
        public void Foreach_ViaInterface() {
            IEnumerable<ushort> nodeSegmentIds = new GetNodeSegmentIdsEnumerable(1, 1, ClockDirection.Clockwise, testSegments);
            foreach (ushort item in nodeSegmentIds) {
            }
        }
    }
}

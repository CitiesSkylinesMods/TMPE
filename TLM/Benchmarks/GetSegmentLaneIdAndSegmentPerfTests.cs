using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using TrafficManager.Manager.Impl;
using TrafficManager.Util.Iterators;

namespace Benchmarks {
    public class GetSegmentLaneIdAndSegmentPerfTests {
        private readonly static NetLane[] testLanes = new NetLane[] {
            new NetLane() {
                m_nextLane = 1
            },
            new NetLane() {
                m_nextLane = 2
            },
            new NetLane() {
                m_nextLane = 0
            },
        };

        [Benchmark]
        public void Foreach_Direct() {
            foreach (var item in new GetSegmentLaneIdsEnumerable(0, testLanes.Length, testLanes)) {
            }
        }

        [Benchmark]
        public void Foreach_ViaInterface() {
            IEnumerable<LaneIdAndIndex> nodeSegmentIds = new GetSegmentLaneIdsEnumerable(0, testLanes.Length, testLanes);
            foreach (var item in nodeSegmentIds) {
            }
        }
    }
}

using BenchmarkDotNet.Attributes;
using System.Linq;
using TrafficManager.Util;

namespace Benchmarks {
    public class LoopUtilPerfTests {
        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_Once_Radius17() {
            var radius = 17;
            var coords = LoopUtil.GenerateSpiralGridCoordsCounterclockwise().Take(radius * radius);
            foreach (var coord in coords) {
            }
        }

        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_10Times_Radius17() {
            var radius = 17;
            for (int i = 0; i < 10; i++) {
                var coords = LoopUtil.GenerateSpiralGridCoordsCounterclockwise().Take(radius * radius);
                foreach (var coord in coords) {
                }
            }
        }
    }
}

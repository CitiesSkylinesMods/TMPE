using BenchmarkDotNet.Attributes;
using TrafficManager.Util;

namespace Benchmarks {
    public class SpiralPerfTests {
        private readonly Spiral _cachedSpiral17 = new Spiral(17);

        [Benchmark]
        public void SpiralGetCoords_Once_Radius17() {
            var radius = 17;
            var spiral = new Spiral(radius);
            var coords = spiral.GetCoordsCounterclockwise(radius);
            for (int i = 0; i < coords.Count; i++) {
                var coord = coords[i];
            }
        }

        [Benchmark]
        public void SpiralGetCoords_10Times_Radius17() {
            var radius = 17;
            var spiral = new Spiral(radius);
            for (int i = 0; i < 10; i++) {
                var coords = spiral.GetCoordsCounterclockwise(radius);
                for (int j = 0; j < coords.Count; j++) {
                    var coord = coords[j];
                }
            }
        }

        [Benchmark]
        public void SpiralGetCoords_Once_Radius17_Precached() {
            var radius = 17;
            var coords = _cachedSpiral17.GetCoordsCounterclockwise(radius);
            for (int i = 0; i < coords.Count; i++) {
                var coord = coords[i];
            }
        }

        [Benchmark]
        public void SpiralGetCoords_10Times_Radius17_Precached() {
            var radius = 17;
            for (int i = 0; i < 10; i++) {
                var coords = _cachedSpiral17.GetCoordsCounterclockwise(radius);
                for (int j = 0; j < coords.Count; j++) {
                    var coord = coords[j];
                }
            }
        }
    }
}

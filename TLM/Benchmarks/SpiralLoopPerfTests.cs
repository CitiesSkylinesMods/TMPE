using BenchmarkDotNet.Attributes;
using System.Linq;
using TrafficManager.Util;
using UnityEngine;

namespace Benchmarks {
    public class SpiralLoopPerfTests {
        private readonly Vector2[] spiralCacheRadius17 = LoopUtil.GenerateSpiralGridCoordsClockwise(17).ToArray();

        [Benchmark]
        public void SpiralLoop_Once_Radius17() {
            var posX = 3621;
            var posY = 2342;
            var BUILDINGGRID_CELL_SIZE = 64;
            var BUILDINGGRID_RESOLUTION = 270;

            var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));

            int radius = 17;

            ushort ignoreParked = 1;
            Vector3 refPos = Vector3.back;
            float width = 5f;
            float length = 5f;
            bool randomize = true;
            ushort foundSegmentId = 0;
            Vector3 myParkPos = Vector3.back;
            Quaternion myParkRot = Quaternion.identity;
            float myParkOffset = 10f;

            bool LoopHandler(int x, int y) {
                if (randomize) {
                    width = ignoreParked
                        + refPos.x
                        + length
                        + length
                        + foundSegmentId
                        + myParkPos.x
                        + myParkPos.x
                        + myParkRot.x
                        + myParkOffset;
                }

                return true;
            }

            LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, LoopHandler);
        }

        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_Once_Radius17() {
            var posX = 3621;
            var posY = 2342;
            var BUILDINGGRID_CELL_SIZE = 64;
            var BUILDINGGRID_RESOLUTION = 270;

            var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));

            int radius = 17;

            ushort ignoreParked = 1;
            Vector3 refPos = Vector3.back;
            float width = 5f;
            float length = 5f;
            bool randomize = true;
            ushort foundSegmentId = 0;
            Vector3 myParkPos = Vector3.back;
            Quaternion myParkRot = Quaternion.identity;
            float myParkOffset = 10f;

            bool LoopHandler(int x, int y) {
                if (randomize) {
                    width = ignoreParked
                        + refPos.x
                        + length
                        + length
                        + foundSegmentId
                        + myParkPos.x
                        + myParkPos.x
                        + myParkRot.x
                        + myParkOffset;
                }

                return true;
            }

            foreach (var position in LoopUtil.GenerateSpiralGridCoordsClockwise(radius)) {
                var positionWithOffset = position + new Vector2(centerI, centerJ);
                if (LoopHandler((int)positionWithOffset.x, (int)positionWithOffset.y)) {
                    break;
                }
            }
        }

        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_Once_Radius17_Precached() {
            var posX = 3621;
            var posY = 2342;
            var BUILDINGGRID_CELL_SIZE = 64;
            var BUILDINGGRID_RESOLUTION = 270;

            var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));
            var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                (BUILDINGGRID_RESOLUTION / 2f));

            int radius = 17;

            ushort ignoreParked = 1;
            Vector3 refPos = Vector3.back;
            float width = 5f;
            float length = 5f;
            bool randomize = true;
            ushort foundSegmentId = 0;
            Vector3 myParkPos = Vector3.back;
            Quaternion myParkRot = Quaternion.identity;
            float myParkOffset = 10f;

            bool LoopHandler(int x, int y) {
                if (randomize) {
                    width = ignoreParked
                        + refPos.x
                        + length
                        + length
                        + foundSegmentId
                        + myParkPos.x
                        + myParkPos.x
                        + myParkRot.x
                        + myParkOffset;
                }

                return true;
            }

            foreach (var position in spiralCacheRadius17) {
                var positionWithOffset = position + new Vector2(centerI, centerJ);
                if (LoopHandler((int)positionWithOffset.x, (int)positionWithOffset.y)) {
                    break;
                }
            }
        }

        [Benchmark]
        public void SpiralLoop_10Times_Radius17() {
            for (int i = 0; i < 10; i++) {
                var posX = 3621;
                var posY = 2342;
                var BUILDINGGRID_CELL_SIZE = 64;
                var BUILDINGGRID_RESOLUTION = 270;

                var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));
                var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));

                int radius = 17;

                ushort ignoreParked = 1;
                Vector3 refPos = Vector3.back;
                float width = 5f;
                float length = 5f;
                bool randomize = true;
                ushort foundSegmentId = 0;
                Vector3 myParkPos = Vector3.back;
                Quaternion myParkRot = Quaternion.identity;
                float myParkOffset = 10f;

                bool LoopHandler(int x, int y) {
                    if (randomize) {
                        width = ignoreParked
                            + refPos.x
                            + length
                            + length
                            + foundSegmentId
                            + myParkPos.x
                            + myParkPos.x
                            + myParkRot.x
                            + myParkOffset;
                    }

                    return true;
                }

                LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, LoopHandler);
            }
        }

        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_10Times_Radius17() {
            for (int i = 0; i < 10; i++) {
                var posX = 3621;
                var posY = 2342;
                var BUILDINGGRID_CELL_SIZE = 64;
                var BUILDINGGRID_RESOLUTION = 270;

                var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));
                var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));

                int radius = 17;

                ushort ignoreParked = 1;
                Vector3 refPos = Vector3.back;
                float width = 5f;
                float length = 5f;
                bool randomize = true;
                ushort foundSegmentId = 0;
                Vector3 myParkPos = Vector3.back;
                Quaternion myParkRot = Quaternion.identity;
                float myParkOffset = 10f;

                bool LoopHandler(int x, int y) {
                    if (randomize) {
                        width = ignoreParked
                            + refPos.x
                            + length
                            + length
                            + foundSegmentId
                            + myParkPos.x
                            + myParkPos.x
                            + myParkRot.x
                            + myParkOffset;
                    }

                    return true;
                }

                foreach (var position in LoopUtil.GenerateSpiralGridCoordsClockwise(radius)) {
                    var positionWithOffset = position + new Vector2(centerI, centerJ);
                    if (LoopHandler((int)positionWithOffset.x, (int)positionWithOffset.y)) {
                        break;
                    }
                }
            }
        }

        [Benchmark]
        public void GenerateSpiralGridCoordsClockwise_10Times_Radius17_Precached() {
            for (int i = 0; i < 10; i++) {
                var posX = 3621;
                var posY = 2342;
                var BUILDINGGRID_CELL_SIZE = 64;
                var BUILDINGGRID_RESOLUTION = 270;

                var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));
                var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));

                int radius = 17;

                ushort ignoreParked = 1;
                Vector3 refPos = Vector3.back;
                float width = 5f;
                float length = 5f;
                bool randomize = true;
                ushort foundSegmentId = 0;
                Vector3 myParkPos = Vector3.back;
                Quaternion myParkRot = Quaternion.identity;
                float myParkOffset = 10f;

                bool LoopHandler(int x, int y) {
                    if (randomize) {
                        width = ignoreParked
                            + refPos.x
                            + length
                            + length
                            + foundSegmentId
                            + myParkPos.x
                            + myParkPos.x
                            + myParkRot.x
                            + myParkOffset;
                    }

                    return true;
                }

                foreach (var position in spiralCacheRadius17) {
                    var positionWithOffset = position + new Vector2(centerI, centerJ);
                    if (LoopHandler((int)positionWithOffset.x, (int)positionWithOffset.y)) {
                        break;
                    }
                }
            }
        }
    }
}

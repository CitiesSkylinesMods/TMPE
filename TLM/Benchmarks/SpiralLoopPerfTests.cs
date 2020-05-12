using BenchmarkDotNet.Attributes;
using System;
using TrafficManager.Util;
using UnityEngine;

namespace Benchmarks {
    public class SpiralLoopPerfTests {
        [Benchmark]
        public void SpiralLoopPerfTest1() {
            for (int i = 0; i < 10; i++) {
                bool logParkingAi = false;

                var parkPos = Vector3.zero;
                var parkRot = Quaternion.identity;
                var parkOffset = 0f;

                var posX = 3621;
                var posY = 2342;
                var BUILDINGGRID_CELL_SIZE = 64;
                var BUILDINGGRID_RESOLUTION = 270;
                var maxDistance = 500;

                var centerI = (int)((posX / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));
                var centerJ = (int)((posY / BUILDINGGRID_CELL_SIZE) +
                                    (BUILDINGGRID_RESOLUTION / 2f));

                int radius = Math.Max(1, (int)(maxDistance / (BUILDINGGRID_CELL_SIZE / 2f)) + 1);

                ushort foundSegmentId = 0;
                Vector3 myParkPos = parkPos;
                Quaternion myParkRot = parkRot;
                float myParkOffset = parkOffset;

                Vector3 refPos = Vector3.zero;

                var width = 1000f;
                var length = 1000f;

                bool FindParkingSpaceAtRoadSideLoopHandler(int x, int y) {
                    return true;
                }

                LoopUtil.SpiralLoop(centerI, centerJ, radius, radius, FindParkingSpaceAtRoadSideLoopHandler);
            }
        }
    }
}

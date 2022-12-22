using TrafficManager.Util.Extensions;

namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using TrafficManager.Util;
    using UnityEngine;

    public class TrafficMeasurementManager : AbstractCustomManager, ITrafficMeasurementManager {
        private const VehicleInfo.VehicleType VEHICLE_TYPES = VehicleInfo.VehicleType.Car;

        private const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const ushort REF_REL_SPEED_PERCENT_DENOMINATOR = 100;

        public const ushort REF_REL_SPEED = 10000;

        public static readonly TrafficMeasurementManager Instance = new TrafficMeasurementManager();

        private readonly uint[] meanSpeeds = { 0, 0 };

        private readonly int[] meanSpeedLanes = { 0, 0 };

        private TrafficMeasurementManager() {
            LaneTrafficData = new LaneTrafficData[NetManager.MAX_SEGMENT_COUNT][];
            SegmentDirTrafficData = new SegmentDirTrafficData[NetManager.MAX_SEGMENT_COUNT * 2];

            for (int i = 0; i < SegmentDirTrafficData.Length; ++i) {
                SegmentDirTrafficData[i].meanSpeed = REF_REL_SPEED;
            }

            ResetTrafficStats();
        }

        public LaneTrafficData[][] LaneTrafficData { get; }

        public SegmentDirTrafficData[] SegmentDirTrafficData { get; }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Lane traffic data:");

            if (LaneTrafficData == null) {
                Log._Debug("\t<null>");
            } else {
                for (var i = 0; i < LaneTrafficData.Length; ++i) {
                    if (LaneTrafficData[i] == null) {
                        continue;
                    }

                    Log._Debug($"\tSegment {i}:");
                    for (var k = 0; k < LaneTrafficData[i].Length; ++k) {
                        Log._Debug($"\t\tLane {k}: {LaneTrafficData[i][k]}");
                    }
                }
            }

            Log._Debug("Segment direction traffic data:");

            if (SegmentDirTrafficData == null) {
                Log._Debug("\t<null>");
            } else {
                for (var i = 0; i < SegmentDirTrafficData.Length; ++i) {
                    Log._Debug($"\tIndex {i}: {SegmentDirTrafficData[i]}");
                }
            }
        }

        public ushort CalcLaneRelativeMeanSpeed(ushort segmentId,
                                                byte laneIndex,
                                                uint laneId,
                                                NetInfo.Lane laneInfo) {
            if (LaneTrafficData[segmentId] == null
                || laneIndex >= LaneTrafficData[segmentId].Length)
            {
                return REF_REL_SPEED;
            }

            ushort currentBuf = LaneTrafficData[segmentId][laneIndex].trafficBuffer;
            ushort curRelSpeed = REF_REL_SPEED;

            // we use integer division here because it's faster
            if (currentBuf > 0) {
                uint laneVehicleSpeedLimit = Math.Min(
                    3u * 8u,
                    (uint)((SavedGameOptions.Instance.customSpeedLimitsEnabled
                                ? SpeedLimitManager.Instance.GetGameSpeedLimit(
                                    segmentId,
                                    laneIndex,
                                    laneId,
                                    laneInfo)
                                : laneInfo.m_speedLimit) * 8f));

                if (laneVehicleSpeedLimit <= 0) {
                    // fallback: custom lanes may not have valid values set for speed limit
                    laneVehicleSpeedLimit = 1;
                }

                // 0 .. 10000, m_speedLimit of highway is 2 (100 km/h), actual max. vehicle speed
                // on highway is 16, that's why we use x*8 == x<<3 (don't ask why CO uses different
                // units for velocity)
                curRelSpeed = (ushort)Math.Min(
                    REF_REL_SPEED,
                    (LaneTrafficData[segmentId][laneIndex].accumulatedSpeeds * REF_REL_SPEED)
                        / currentBuf
                        / laneVehicleSpeedLimit);

                if (curRelSpeed >=
                    GlobalConfig.Instance.DynamicLaneSelection.VolumeMeasurementRelSpeedThreshold *
                    (uint)REF_REL_SPEED_PERCENT_DENOMINATOR)
                {
                    ushort lastBuf = LaneTrafficData[segmentId][laneIndex].lastTrafficBuffer;
                    ushort maxBuf = LaneTrafficData[segmentId][laneIndex].maxTrafficBuffer;

                    float factor = Mathf.Clamp01(1f - (lastBuf / (float)maxBuf));
                    curRelSpeed =
                        (ushort)(curRelSpeed + (uint)(factor * (REF_REL_SPEED - (uint)curRelSpeed)));
                }
            }

            return curRelSpeed;
        }

        public void OnBeforeSimulationStep(ushort segmentId, ref NetSegment segment) {
            GlobalConfig conf = GlobalConfig.Instance;

            // calculate traffic density
            NetInfo segmentInfo = segment.Info;
            int numLanes = segmentInfo.m_lanes.Length;

            if (LaneTrafficData[segmentId] == null || LaneTrafficData[segmentId].Length < numLanes) {
                LaneTrafficData[segmentId] = new LaneTrafficData[numLanes];
                for (int i = 0; i < numLanes; ++i) {
                    // laneTrafficData[segmentId][i] = new LaneTrafficData();
                    LaneTrafficData[segmentId][i].meanSpeed = REF_REL_SPEED;
                }
            }

            // calculate max./min. lane speed
            for (var i = 0; i < 2; ++i) {
                meanSpeeds[i] = 0;
                meanSpeedLanes[i] = 0;
            }

            uint curLaneId = segment.m_lanes;

            byte laneIndex = 0;
            while (laneIndex < numLanes && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if ((laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None
                    && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None)
                {
                    int dirIndex = GetDirIndex(laneInfo.m_finalDirection);

                    // calculate reported mean speed
                    ushort newRelSpeed = CalcLaneRelativeMeanSpeed(
                        segmentId,
                        laneIndex,
                        curLaneId,
                        segment.Info.m_lanes[laneIndex]);

                    meanSpeeds[dirIndex] += newRelSpeed;
                    ++meanSpeedLanes[dirIndex];

                    LaneTrafficData[segmentId][laneIndex].meanSpeed = newRelSpeed;

                    ushort trafficBuffer = LaneTrafficData[segmentId][laneIndex].trafficBuffer;

                    // remember historic data
                    LaneTrafficData[segmentId][laneIndex].lastTrafficBuffer = trafficBuffer;

                    if (trafficBuffer > LaneTrafficData[segmentId][laneIndex].maxTrafficBuffer) {
                        LaneTrafficData[segmentId][laneIndex].maxTrafficBuffer = trafficBuffer;
                    }

                    // reset buffers
                    if (conf.AdvancedVehicleAI.MaxTrafficBuffer > 0) {
                        if (LaneTrafficData[segmentId][laneIndex].trafficBuffer >
                            conf.AdvancedVehicleAI.MaxTrafficBuffer) {
                            LaneTrafficData[segmentId][laneIndex].accumulatedSpeeds /=
                                LaneTrafficData[segmentId][laneIndex].trafficBuffer
                                / conf.AdvancedVehicleAI.MaxTrafficBuffer;
                            LaneTrafficData[segmentId][laneIndex].trafficBuffer =
                                (ushort)conf.AdvancedVehicleAI.MaxTrafficBuffer;
                        } else if (LaneTrafficData[segmentId][laneIndex].trafficBuffer ==
                                   conf.AdvancedVehicleAI.MaxTrafficBuffer) {
                            LaneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
                            LaneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
                        }
                    } else {
                        LaneTrafficData[segmentId][laneIndex].accumulatedSpeeds = 0;
                        LaneTrafficData[segmentId][laneIndex].trafficBuffer = 0;
                    }
                }

                laneIndex++;
                curLaneId = curLaneId.ToLane().m_nextLane;
            }

            for (int i = 0; i < 2; ++i) {
                int segDirIndex = i == 0
                                      ? GetDirIndex(segmentId, NetInfo.Direction.Forward)
                                      : GetDirIndex(segmentId, NetInfo.Direction.Backward);

                if (meanSpeedLanes[i] > 0) {
                    SegmentDirTrafficData[segDirIndex].meanSpeed = (ushort)Math.Min(
                        REF_REL_SPEED,
                        meanSpeeds[i] / meanSpeedLanes[i]);
                } else {
                    SegmentDirTrafficData[segDirIndex].meanSpeed = REF_REL_SPEED;
                }
            }
        }

        public bool GetLaneTrafficData(ushort segmentId,
                                       byte laneIndex,
                                       out LaneTrafficData trafficData) {
            if (LaneTrafficData[segmentId] == null
                || laneIndex >= LaneTrafficData[segmentId].Length)
            {
                trafficData = default;
                return false;
            }

            trafficData = LaneTrafficData[segmentId][laneIndex];
            return true;
        }

        public void DestroySegmentStats(ushort segmentId) {
            LaneTrafficData[segmentId] = null;

            int fwdIndex = GetDirIndex(segmentId, NetInfo.Direction.Forward);
            int backIndex = GetDirIndex(segmentId, NetInfo.Direction.Backward);

            SegmentDirTrafficData[fwdIndex] = default;
            SegmentDirTrafficData[fwdIndex].meanSpeed = REF_REL_SPEED;

            SegmentDirTrafficData[backIndex] = default;
            SegmentDirTrafficData[backIndex].meanSpeed = REF_REL_SPEED;
        }

        public void ResetTrafficStats() {
            for (int i = 0; i < NetManager.MAX_SEGMENT_COUNT; ++i) {
                DestroySegmentStats((ushort)i);
            }
        }

        public void AddTraffic(ushort segmentId, byte laneIndex, ushort speed) {
            if (LaneTrafficData[segmentId] == null ||
                laneIndex >= LaneTrafficData[segmentId].Length)
                return;

            LaneTrafficData[segmentId][laneIndex].trafficBuffer = (ushort)Math.Min(
                ushort.MaxValue,
                LaneTrafficData[segmentId][laneIndex].trafficBuffer + 1u);
            LaneTrafficData[segmentId][laneIndex].accumulatedSpeeds += speed;
        }

        public int GetDirIndex(ushort segmentId, NetInfo.Direction dir) {
            return segmentId + (dir == NetInfo.Direction.Backward
                                    ? NetManager.MAX_SEGMENT_COUNT
                                    : 0);
        }

        public int GetDirIndex(NetInfo.Direction dir) {
            return dir == NetInfo.Direction.Backward ? 1 : 0;
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            ResetTrafficStats();
        }
    }
}
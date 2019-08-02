﻿namespace TrafficManager.Manager.Impl {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using API.Manager;
    using API.Traffic.Data;
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using State;
    using UI.SubTools.SpeedLimits;
    using UnityEngine;
    using Util;

    public class SpeedLimitManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneSpeedLimit>>,
          ICustomDataManager<Dictionary<string, float>>,
          ISpeedLimitManager {
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Monorail;

        /// <summary>Ingame speed units, max possible speed</summary>
        public const float MAX_SPEED = 10f * 2f; // 1000 km/h

        /// <summary>Ingame speed units, minimal speed</summary>
        private const float MIN_SPEED = 0.1f; // 5 km/h

        // For each NetInfo (by name) and lane index: game default speed limit
        private Dictionary<string, float[]> vanillaLaneSpeedLimitsByNetInfoName;

        // For each NetInfo (by name): Parent NetInfo (name)
        private Dictionary<string, List<string>> childNetInfoNamesByCustomizableNetInfoName;

        private List<NetInfo> customizableNetInfos;

        // For each NetInfo (by name) and lane index: custom speed limit
        internal Dictionary<string, float> CustomLaneSpeedLimitByNetInfoName;

        // For each name: NetInfo
        internal Dictionary<string, NetInfo> NetInfoByName;

        public static readonly SpeedLimitManager Instance = new SpeedLimitManager();

        private SpeedLimitManager() {
            vanillaLaneSpeedLimitsByNetInfoName = new Dictionary<string, float[]>();
            CustomLaneSpeedLimitByNetInfoName = new Dictionary<string, float>();
            customizableNetInfos = new List<NetInfo>();
            childNetInfoNamesByCustomizableNetInfoName = new Dictionary<string, List<string>>();
            NetInfoByName = new Dictionary<string, NetInfo>();
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for SpeedLimitManager");
        }

        /// <summary>
        /// Determines if custom speed limits may be assigned to the given segment.
        /// </summary>
        /// <param name="segmentId">Affected segment id</param>
        /// <param name="segment">Reference to affected segment</param>
        /// <returns>Success</returns>
        public bool MayHaveCustomSpeedLimits(ushort segmentId, ref NetSegment segment) {
            if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None)
                return false;
            ItemClass connectionClass = segment.Info.GetConnectionClass();
            return connectionClass.m_service == ItemClass.Service.Road
                   || (connectionClass.m_service == ItemClass.Service.PublicTransport
                       && (connectionClass.m_subService == ItemClass.SubService.PublicTransportTrain
                           || connectionClass.m_subService ==
                           ItemClass.SubService.PublicTransportTram
                           || connectionClass.m_subService ==
                           ItemClass.SubService.PublicTransportMetro
                           || connectionClass.m_subService ==
                           ItemClass.SubService.PublicTransportMonorail));
        }

        /// <summary>
        /// Determines if custom speed limits may be assigned to the given lane info
        /// </summary>
        /// <param name="laneInfo"></param>
        /// <returns></returns>
        public bool MayHaveCustomSpeedLimits(NetInfo.Lane laneInfo) {
            return (laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None
                   && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None;
        }

        /// <summary>
        /// Determines the currently set speed limit for the given segment and lane direction in
        /// terms of discrete speed limit levels. An in-game speed limit of 2.0 (e.g. on highway) is
        /// hereby translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="segmentId">Interested in this segment</param>
        /// <param name="finalDir">Direction</param>
        /// <returns>Mean speed limit, average for custom and default lane speeds</returns>
        public float GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction finalDir) {
            // calculate the currently set mean speed limit
            if (segmentId == 0 ||
                (Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_flags &
                 NetSegment.Flags.Created) == NetSegment.Flags.None) {
                return 0f;
            }

            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;
            uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
            var laneIndex = 0;
            var meanSpeedLimit = 0f;
            uint validLanes = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (d != finalDir) {
                    goto nextIter;
                }

                if (!MayHaveCustomSpeedLimits(laneInfo)) {
                    goto nextIter;
                }

                float? setSpeedLimit = Flags.GetLaneSpeedLimit(curLaneId);

                if (setSpeedLimit != null) {
                    meanSpeedLimit += ToGameSpeedLimit(setSpeedLimit.Value); // custom speed limit
                } else {
                    meanSpeedLimit += laneInfo.m_speedLimit; // game default
                }

                ++validLanes;

                nextIter:
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            if (validLanes > 0) {
                meanSpeedLimit /= validLanes;
            }

            return meanSpeedLimit;
        }

        /// <summary>
        /// Determines the average default speed limit for a given NetInfo object in terms of
        /// discrete speed limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby
        /// translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="segmentInfo">Interested in this segment</param>
        /// <param name="finalDir">Direction</param>
        /// <returns>Result</returns>
        [UsedImplicitly]
        public float GetAverageDefaultCustomSpeedLimit(NetInfo segmentInfo,
                                                       NetInfo.Direction? finalDir = null) {
            var meanSpeedLimit = 0f;
            uint validLanes = 0;

            foreach (NetInfo.Lane laneInfo in segmentInfo.m_lanes) {
                NetInfo.Direction d = laneInfo.m_finalDirection;
                if (finalDir != null && d != finalDir) {
                    continue;
                }

                if (!MayHaveCustomSpeedLimits(laneInfo)) {
                    continue;
                }

                meanSpeedLimit += laneInfo.m_speedLimit;
                ++validLanes;
            }

            if (validLanes > 0) {
                meanSpeedLimit /= validLanes;
            }

            return meanSpeedLimit;
        }

        /// <summary>
        /// Determines the average custom speed limit for a given NetInfo object in terms of
        /// discrete speed limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby
        /// translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="segmentInfo">Interested in this segment</param>
        /// <param name="finalDir">Directoin</param>
        /// <returns>Result</returns>
        [UsedImplicitly]
        public ushort GetAverageCustomSpeedLimit(ushort segmentId,
                                                 ref NetSegment segment,
                                                 NetInfo segmentInfo,
                                                 NetInfo.Direction? finalDir = null) {
            // calculate the currently set mean speed limit
            float meanSpeedLimit = 0f;
            uint validLanes = 0;
            uint curLaneId = segment.m_lanes;

            for (byte laneIndex = 0; laneIndex < segmentInfo.m_lanes.Length; ++laneIndex) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (finalDir != null && d != finalDir) {
                    continue;
                }

                if (!MayHaveCustomSpeedLimits(laneInfo)) {
                    continue;
                }

                meanSpeedLimit += GetLockFreeGameSpeedLimit(
                    segmentId,
                    laneIndex,
                    curLaneId,
                    laneInfo);

                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                ++validLanes;
            }

            if (validLanes > 0) {
                meanSpeedLimit /= validLanes;
            }

            return (ushort)Mathf.Round(meanSpeedLimit);
        }

        /// <summary>
        /// Determines the currently set speed limit for the given lane in terms of discrete speed
        /// limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into
        /// a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="laneId">Interested in this lane</param>
        /// <returns>Speed limit if set for lane, otherwise 0</returns>
        public float GetCustomSpeedLimit(uint laneId) {
            // check custom speed limit
            float? setSpeedLimit = Flags.GetLaneSpeedLimit(laneId);
            if (setSpeedLimit != null) {
                return setSpeedLimit.Value;
            }

            // check default speed limit
            ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
            if (!MayHaveCustomSpeedLimits(
                    segmentId,
                    ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId])) {
                return 0;
            }

            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

            uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    return !MayHaveCustomSpeedLimits(laneInfo) ? 0 : laneInfo.m_speedLimit;
                }

                laneIndex++;
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
            }

            Log.Warning($"Speed limit for lane {laneId} could not be determined.");
            return 0; // no speed limit found
        }

        /// <summary>
        /// Determines the currently set speed limit for the given lane in terms of game (floating
        /// point) speed limit levels.
        /// </summary>
        /// <param name="laneId">Interested in this lane</param>
        /// <returns>Km/h in lane converted to game speed float</returns>
        [UsedImplicitly]
        public float GetGameSpeedLimit(uint laneId) {
            return ToGameSpeedLimit(GetCustomSpeedLimit(laneId));
        }

        public float GetLockFreeGameSpeedLimit(ushort segmentId,
                                               byte laneIndex,
                                               uint laneId,
                                               NetInfo.Lane laneInfo) {
            if (!Options.customSpeedLimitsEnabled || !MayHaveCustomSpeedLimits(laneInfo)) {
                return laneInfo.m_speedLimit;
            }

            float speedLimit;
            float?[] fastArray = Flags.laneSpeedLimitArray[segmentId];

            if (fastArray != null
                && fastArray.Length > laneIndex
                && fastArray[laneIndex] != null) {
                speedLimit = ToGameSpeedLimit((float)fastArray[laneIndex]);
            } else {
                speedLimit = laneInfo.m_speedLimit;
            }

            return speedLimit;
        }

        /// <summary>
        /// Converts a possibly zero (no limit) custom speed limit to a game speed limit.
        /// </summary>
        /// <param name="customSpeedLimit">Custom speed limit which can be zero</param>
        /// <returns>Speed limit in game speed units</returns>
        public float ToGameSpeedLimit(float customSpeedLimit) {
            return FloatUtil.IsZero(customSpeedLimit)
                       ? MAX_SPEED
                       : customSpeedLimit;
        }

        /// <summary>
        /// Explicitly stores currently set speed limits for all segments of the specified NetInfo
        /// </summary>
        /// <param name="info"></param>
        public void FixCurrentSpeedLimits(NetInfo info) {
            if (info == null) {
#if DEBUG
                Log.Warning($"SpeedLimitManager.FixCurrentSpeedLimits: info is null!");
#endif
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.FixCurrentSpeedLimits: info.name is null!");
            //    return;
            // }

            if (!customizableNetInfos.Contains(info)) {
                return;
            }

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneValid(laneId)) {
                    continue;
                }

                ushort segmentId =
                    Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
                NetInfo laneInfo =
                    Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

                if (laneInfo.name != info.name
                    && (!childNetInfoNamesByCustomizableNetInfoName.ContainsKey(info.name)
                        || !childNetInfoNamesByCustomizableNetInfoName[info.name]
                            .Contains(laneInfo.name))) {
                    continue;
                }

                Flags.SetLaneSpeedLimit(laneId, GetCustomSpeedLimit(laneId));
            }
        }

        /// <summary>
        /// Explicitly clear currently set speed limits for all segments of the specified NetInfo
        /// </summary>
        /// <param name="info"></param>
        public void ClearCurrentSpeedLimits(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.ClearCurrentSpeedLimits: info is null!");
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.ClearCurrentSpeedLimits: info.name is null!");
            //    return;
            // }

            if (!customizableNetInfos.Contains(info)) {
                return;
            }

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneValid(laneId)) {
                    continue;
                }

                NetInfo laneInfo = Singleton<NetManager>
                                   .instance.m_segments
                                   .m_buffer[Singleton<NetManager>
                                             .instance.m_lanes.m_buffer[laneId].m_segment]
                                   .Info;

                if (laneInfo.name != info.name &&
                    (!childNetInfoNamesByCustomizableNetInfoName.ContainsKey(info.name) ||
                     !childNetInfoNamesByCustomizableNetInfoName[info.name]
                         .Contains(laneInfo.name))) {
                    continue;
                }

                Flags.removeLaneSpeedLimit(laneId);
            }
        }

        /// <summary>
        /// Determines the game default speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the game default speed limit should be determined</param>
        /// <param name="roundToSignLimits">if true, custom speed limit are rounded to speed limits
        /// available as speed limit sign</param>
        /// <returns></returns>
        public float GetVanillaNetInfoSpeedLimit(NetInfo info, bool roundToSignLimits = true) {
            if (info == null) {
                Log._DebugOnlyWarning(
                    $"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info is null!");
                return 0;
            }

            if (info.m_netAI == null) {
                Log._DebugOnlyWarning(
                    $"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.m_netAI is null!");
                return 0;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.name is null!");
            //    return 0;
            // }

            string infoName = info.name;
            if (!vanillaLaneSpeedLimitsByNetInfoName.TryGetValue(
                    infoName,
                    out float[] vanillaSpeedLimits)) {
                return 0;
            }

            float? maxSpeedLimit = null;

            foreach (float speedLimit in vanillaSpeedLimits) {
                if (maxSpeedLimit == null || speedLimit > maxSpeedLimit) {
                    maxSpeedLimit = speedLimit;
                }
            }

            return maxSpeedLimit ?? 0;
        }

        /// <summary>
        /// Determines the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the custom speed limit should be determined</param>
        /// <returns>-1 if no custom speed limit was set</returns>
        public float GetCustomNetInfoSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning(
                    $"SpeedLimitManager.SetCustomNetInfoSpeedLimitIndex: info is null!");
                return -1;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.SetCustomNetInfoSpeedLimitIndex: info.name is null!");
            //    return -1;
            // }

            string infoName = info.name;
            return !CustomLaneSpeedLimitByNetInfoName.TryGetValue(infoName, out float speedLimit)
                       ? GetVanillaNetInfoSpeedLimit(info, true)
                       : speedLimit;
        }

        /// <summary>
        /// Sets the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo for which the custom speed limit should be set</param>
        /// <param name="customSpeedLimit">The speed value to set in game speed units</param>
        public void SetCustomNetInfoSpeedLimit(NetInfo info, float customSpeedLimit) {
            if (info == null) {
                Log._DebugOnlyWarning($"SetCustomNetInfoSpeedLimitIndex: info is null!");
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SetCustomNetInfoSpeedLimitIndex: info.name is null!");
            //    return;
            // }

            string infoName = info.name;
            CustomLaneSpeedLimitByNetInfoName[infoName] = customSpeedLimit;
            float gameSpeedLimit = ToGameSpeedLimit(customSpeedLimit);

            // save speed limit in all NetInfos
#if DEBUGLOAD
            Log._Debug($"Updating parent NetInfo {infoName}: Setting speed limit to {gameSpeedLimit}");
#endif
            UpdateNetInfoGameSpeedLimit(info, gameSpeedLimit);

            if (childNetInfoNamesByCustomizableNetInfoName.TryGetValue(
                infoName,
                out List<string> childNetInfoNames)) {
                foreach (string childNetInfoName in childNetInfoNames) {
                    if (NetInfoByName.TryGetValue(childNetInfoName, out NetInfo childNetInfo)) {
#if DEBUGLOAD
                        Log._Debug($"Updating child NetInfo {childNetInfoName}: Setting speed limit to {gameSpeedLimit}");
#endif
                        CustomLaneSpeedLimitByNetInfoName[childNetInfoName] = customSpeedLimit;
                        UpdateNetInfoGameSpeedLimit(childNetInfo, gameSpeedLimit);
                    }
                }
            }
        }

        private void UpdateNetInfoGameSpeedLimit(NetInfo info, float gameSpeedLimit) {
            if (info == null) {
                Log._DebugOnlyWarning(
                    $"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info is null!");
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info.name is null!");
            //    return;
            // }

            if (info.m_lanes == null) {
                Log._DebugOnlyWarning(
                    $"SpeedLimitManager.UpdateNetInfoGameSpeedLimit: info.name is null!");
                return;
            }

            Log._Trace($"Updating speed limit of NetInfo {info.name} to {gameSpeedLimit}");

            foreach (NetInfo.Lane lane in info.m_lanes) {
                // TODO refactor check
                if ((lane.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None) {
                    lane.m_speedLimit = gameSpeedLimit;
                }
            }
        }

        /// <summary>Sets the speed limit of a given lane.</summary>
        /// <param name="segmentId"></param>
        /// <param name="laneIndex"></param>
        /// <param name="laneInfo"></param>
        /// <param name="laneId"></param>
        /// <param name="speedLimit">Game speed units, 0=unlimited</param>
        /// <returns></returns>
        public bool SetSpeedLimit(ushort segmentId,
                                  uint laneIndex,
                                  NetInfo.Lane laneInfo,
                                  uint laneId,
                                  float speedLimit) {
            if (!MayHaveCustomSpeedLimits(laneInfo)) {
                return false;
            }

            if (!IsValidRange(speedLimit)) {
                return false;
            }

            if (!Services.NetService.IsLaneValid(laneId)) {
                return false;
            }

            Flags.SetLaneSpeedLimit(segmentId, laneIndex, laneId, speedLimit);
            return true;
        }

        /// <summary>
        /// Sets the speed limit of a given segment and lane direction.
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="finalDir"></param>
        /// <param name="speedLimit"></param>
        /// <returns></returns>
        public bool SetSpeedLimit(ushort segmentId, NetInfo.Direction finalDir, float speedLimit) {
            if (!MayHaveCustomSpeedLimits(
                    segmentId,
                    ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId])) {
                return false;
            }

            if (!IsValidRange(speedLimit)) {
                return false;
            }

            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].Info;

            if (segmentInfo == null) {
#if DEBUG
                Log.Warning($"SpeedLimitManager.SetSpeedLimit: info is null!");
#endif
                return false;
            }

            if (segmentInfo.m_lanes == null) {
#if DEBUG
                Log.Warning($"SpeedLimitManager.SetSpeedLimit: info.name is null!");
#endif
                return false;
            }

            uint curLaneId = Singleton<NetManager>.instance.m_segments.m_buffer[segmentId].m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;
                if (d != finalDir) {
                    goto nextIter;
                }

                if (!MayHaveCustomSpeedLimits(laneInfo)) {
                    goto nextIter;
                }
#if DEBUG
                Log._Debug(
                    $"SpeedLimitManager: Setting speed limit of lane {curLaneId} " +
                    $"to {speedLimit * Constants.SPEED_TO_KMPH}");
#endif
                Flags.SetLaneSpeedLimit(curLaneId, speedLimit);

                nextIter:
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            return true;
        }

        public List<NetInfo> GetCustomizableNetInfos() {
            return customizableNetInfos;
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();

            // determine vanilla speed limits and customizable NetInfos
            SteamHelper.DLC_BitMask dlcMask = SteamHelper.GetOwnedDLCMask();

            int numLoaded = PrefabCollection<NetInfo>.LoadedCount();

            vanillaLaneSpeedLimitsByNetInfoName.Clear();
            customizableNetInfos.Clear();
            CustomLaneSpeedLimitByNetInfoName.Clear();
            childNetInfoNamesByCustomizableNetInfoName.Clear();
            NetInfoByName.Clear();

            List<NetInfo> mainNetInfos = new List<NetInfo>();

            Log.Info($"SpeedLimitManager.OnBeforeLoadData: {numLoaded} NetInfos loaded.");
            for (uint i = 0; i < numLoaded; ++i) {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);

                if (info == null
                    || info.m_netAI == null
                    || !(info.m_netAI is RoadBaseAI || info.m_netAI is MetroTrackAI ||
                         info.m_netAI is TrainTrackBaseAI)
                    || !(info.m_dlcRequired == 0 || (uint)(info.m_dlcRequired & dlcMask) != 0u)) {
                    if (info == null) {
                        Log.Warning($"SpeedLimitManager.OnBeforeLoadData: NetInfo @ {i} is null!");
                    }

                    continue;
                }

                string infoName = info.name;

                // Resharper warning: condition always false
                // if (infoName == null) {
                //    Log.Warning($"SpeedLimitManager.OnBeforeLoadData: NetInfo name @ {i} is null!");
                //    continue;
                // }

                if (!vanillaLaneSpeedLimitsByNetInfoName.ContainsKey(infoName)) {
                    if (info.m_lanes == null) {
                        Log.Warning(
                            $"SpeedLimitManager.OnBeforeLoadData: NetInfo lanes @ {i} is null!");
                        continue;
                    }

                    Log._Trace($"Loaded road NetInfo: {infoName}");
                    NetInfoByName[infoName] = info;
                    mainNetInfos.Add(info);

                    var vanillaLaneSpeedLimits = new float[info.m_lanes.Length];

                    for (var k = 0; k < info.m_lanes.Length; ++k) {
                        vanillaLaneSpeedLimits[k] = info.m_lanes[k].m_speedLimit;
                    }

                    vanillaLaneSpeedLimitsByNetInfoName[infoName] = vanillaLaneSpeedLimits;
                }
            }

            mainNetInfos.Sort(
                delegate(NetInfo a, NetInfo b) {
                    bool aRoad = a.m_netAI is RoadBaseAI;
                    bool bRoad = b.m_netAI is RoadBaseAI;

                    if (aRoad != bRoad) {
                        return aRoad ? -1 : 1;
                    }

                    bool aTrain = a.m_netAI is TrainTrackBaseAI;
                    bool bTrain = b.m_netAI is TrainTrackBaseAI;

                    if (aTrain != bTrain) {
                        return aTrain ? 1 : -1;
                    }

                    bool aMetro = a.m_netAI is MetroTrackAI;
                    bool bMetro = b.m_netAI is MetroTrackAI;

                    if (aMetro != bMetro) {
                        return aMetro ? 1 : -1;
                    }

                    if (aRoad && bRoad) {
                        bool aHighway = ((RoadBaseAI)a.m_netAI).m_highwayRules;
                        bool bHighway = ((RoadBaseAI)b.m_netAI).m_highwayRules;

                        if (aHighway != bHighway) {
                            return aHighway ? 1 : -1;
                        }
                    }

                    int aNumVehicleLanes = 0;
                    foreach (NetInfo.Lane lane in a.m_lanes) {
                        if ((lane.m_laneType & LANE_TYPES) != NetInfo.LaneType.None) {
                            ++aNumVehicleLanes;
                        }
                    }

                    int bNumVehicleLanes = 0;
                    foreach (NetInfo.Lane lane in b.m_lanes) {
                        if ((lane.m_laneType & LANE_TYPES) != NetInfo.LaneType.None)
                            ++bNumVehicleLanes;
                    }

                    int res = aNumVehicleLanes.CompareTo(bNumVehicleLanes);
                    if (res == 0) {
                        return a.name.CompareTo(b.name);
                    } else {
                        return res;
                    }
                });

            // identify parent NetInfos
            int x = 0;
            while (x < mainNetInfos.Count) {
                NetInfo info = mainNetInfos[x];
                string infoName = info.name;

                // find parent with prefix name
                bool foundParent = false;
                foreach (NetInfo parentInfo in mainNetInfos) {
                    if (info.m_placementStyle == ItemClass.Placement.Procedural
                        && !infoName.Equals(parentInfo.name)
                        && infoName.StartsWith(parentInfo.name)) {
                        Log._Trace(
                            $"Identified child NetInfo {infoName} of parent {parentInfo.name}");

                        if (!childNetInfoNamesByCustomizableNetInfoName.TryGetValue(
                                parentInfo.name,
                                out List<string> childNetInfoNames)) {
                            childNetInfoNamesByCustomizableNetInfoName[parentInfo.name] =
                                childNetInfoNames = new List<string>();
                        }

                        childNetInfoNames.Add(info.name);
                        NetInfoByName[infoName] = info;
                        foundParent = true;
                        break;
                    }
                }

                if (foundParent) {
                    mainNetInfos.RemoveAt(x);
                } else {
                    ++x;
                }
            }

            customizableNetInfos = mainNetInfos;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[seg.segmentId].Info;
            uint curLaneId = Singleton<NetManager>
                             .instance.m_segments.m_buffer[seg.segmentId].m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                // NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                // float? setSpeedLimit = Flags.getLaneSpeedLimit(curLaneId);
                Flags.SetLaneSpeedLimit(curLaneId, null);

                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        public bool LoadData(List<Configuration.LaneSpeedLimit> data) {
            bool success = true;
            Log.Info($"Loading lane speed limit data. {data.Count} elements");
            foreach (Configuration.LaneSpeedLimit laneSpeedLimit in data) {
                try {
                    if (!Services.NetService.IsLaneValid(laneSpeedLimit.laneId)) {
#if DEBUGLOAD
                        Log._Debug($"SpeedLimitManager.LoadData: Skipping lane {laneSpeedLimit.laneId}: Lane is invalid");
#endif
                        continue;
                    }

                    ushort segmentId = Singleton<NetManager>.instance
                                                            .m_lanes
                                                            .m_buffer[laneSpeedLimit.laneId]
                                                            .m_segment;
                    NetInfo info = Singleton<NetManager>
                                   .instance.m_segments.m_buffer[segmentId].Info;
                    float customSpeedLimit = GetCustomNetInfoSpeedLimit(info);
#if DEBUGLOAD
                    Log._Debug($"SpeedLimitManager.LoadData: Handling lane {laneSpeedLimit.laneId}: " +
                        $"Custom speed limit of segment {segmentId} info ({info}, name={info?.name}, " +
                        $"lanes={info?.m_lanes} is {customSpeedLimit}");
#endif

                    if (IsValidRange(customSpeedLimit)) {
                        // lane speed limit differs from default speed limit
#if DEBUGLOAD
                        Log._Debug($"SpeedLimitManager.LoadData: Loading lane speed limit: lane "+
                            $"{laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit}");
#endif
                        Flags.SetLaneSpeedLimit(laneSpeedLimit.laneId, laneSpeedLimit.speedLimit);
                        Log._Debug(
                            $"SpeedLimitManager.LoadData: Loading lane speed limit: " +
                            $"lane {laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit} km/h");
                        float kmph = laneSpeedLimit.speedLimit /
                                     Constants.SPEED_TO_KMPH; // convert to game units
                        Flags.SetLaneSpeedLimit(laneSpeedLimit.laneId, kmph);
                    } else {
#if DEBUGLOAD
                    Log._Debug($"SpeedLimitManager.LoadData: " +
                        $"Skipping lane speed limit of lane {laneSpeedLimit.laneId} " +
                        $"({laneSpeedLimit.speedLimit} km/h)");
#endif
                    }
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning($"SpeedLimitManager.LoadData: Error loading speed limits: {e}");
                    success = false;
                }
            }

            return success;
        }

        List<Configuration.LaneSpeedLimit> ICustomDataManager<List<Configuration.LaneSpeedLimit>>.
            SaveData(ref bool success) {
            var ret = new List<Configuration.LaneSpeedLimit>();
            foreach (KeyValuePair<uint, float> e in Flags.GetAllLaneSpeedLimits()) {
                try {
                    var laneSpeedLimit = new Configuration.LaneSpeedLimit(e.Key, new SpeedValue(e.Value));
#if DEBUGSAVE
                    Log._Debug($"Saving speed limit of lane {laneSpeedLimit.laneId}: " +
                        $"{laneSpeedLimit.speedLimit*SpeedLimit.SPEED_TO_KMPH} km/h");
#endif
                    ret.Add(laneSpeedLimit);
                }
                catch (Exception ex) {
                    Log.Error($"Exception occurred while saving lane speed limit @ {e.Key}: {ex}");
                    success = false;
                }
            }

            return ret;
        }

        public bool LoadData(Dictionary<string, float> data) {
            Log.Info($"Loading custom default speed limit data. {data.Count} elements");
            foreach (KeyValuePair<string, float> e in data) {
                if (!NetInfoByName.TryGetValue(e.Key, out NetInfo netInfo)) {
                    continue;
                }

                if (e.Value >= 0f) {
                    SetCustomNetInfoSpeedLimit(netInfo, e.Value);
                }
            }

            return true; // true = success
        }

        Dictionary<string, float> ICustomDataManager<Dictionary<string, float>>.SaveData(
            ref bool success) {
            var ret = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> e in CustomLaneSpeedLimitByNetInfoName) {
                try {
                    float gameSpeedLimit = ToGameSpeedLimit(e.Value);

                    ret.Add(e.Key, gameSpeedLimit);
                }
                catch (Exception ex) {
                    Log.Error(
                        $"Exception occurred while saving custom default speed limits @ {e.Key}: {ex}");
                    success = false;
                }
            }

            return ret;
        }

#if DEBUG
//        public Dictionary<NetInfo, ushort> GetDefaultSpeedLimits() {
//            Dictionary<NetInfo, ushort> ret = new Dictionary<NetInfo, ushort>();
//            int numLoaded = PrefabCollection<NetInfo>.LoadedCount();
//            for (uint i = 0; i < numLoaded; ++i) {
//                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);
//                var defaultSpeedLimit =
//                    (ushort)GetAverageDefaultCustomSpeedLimit(info, NetInfo.Direction.Forward);
//                ret.Add(info, defaultSpeedLimit);
//                Log._DebugFormat(
//                    "Loaded NetInfo: {0}, placementStyle={1}, availableIn={2}, thumbnail={3} " +
//                    "connectionClass.service: {4}, connectionClass.subService: {5}, " +
//                    "avg. default speed limit: {6}",
//                    info.name,
//                    info.m_placementStyle,
//                    info.m_availableIn,
//                    info.m_Thumbnail,
//                    info.GetConnectionClass().m_service,
//                    info.GetConnectionClass().m_subService,
//                    defaultSpeedLimit);
//            }
//
//            return ret;
//        }
#endif

        /// <summary>
        /// Used for loading and saving lane speed limits
        /// </summary>
        /// <returns>ICustomDataManager with custom lane speed limits</returns>
        public static ICustomDataManager<List<Configuration.LaneSpeedLimit>> AsLaneSpeedLimitsDM() {
            return Instance;
        }

        /// <summary>
        /// Used for loading and saving custom default speed limits
        /// </summary>
        /// <returns>ICustomDataManager with custom default speed limits</returns>
        public static ICustomDataManager<Dictionary<string, float>> AsCustomDefaultSpeedLimitsDM() {
            return Instance;
        }

        public static bool IsValidRange(float speed) {
            return FloatUtil.IsZero(speed) || (speed >= MIN_SPEED && speed <= MAX_SPEED);
        }
    } // end class
}
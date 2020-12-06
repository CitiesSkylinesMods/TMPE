namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
#if DEBUG
    using TrafficManager.State.ConfigData;
#endif
    using TrafficManager.Util;
    using UnityEngine;
    using System.Text;
    using TrafficManager.API.Traffic;

    /// <summary>
    /// Handles mod-global storage for speed limit overrides/default speed overrides.
    /// </summary>
    public class SpeedLimitManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneSpeedLimit>>,
          ICustomDataManager<Dictionary<string, float>>,
          ISpeedLimitManager {
        /// <summary>Lane types which can have speed limits override.</summary>
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        /// <summary>
        /// Vehicle types to filter the roads/lanes which can have speed limit override.
        /// </summary>
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus;

        /// <summary>Ingame speed units, max possible speed.</summary>
        public const float MAX_SPEED = SpeedValue.SPECIAL_UNLIMITED_VALUE; // 1000 km/h

        /// <summary>Singleton for this class.</summary>
        public static readonly SpeedLimitManager Instance = new();

        /// <summary>For each NetInfo (by name) and lane index: custom speed limit.</summary>
        internal readonly Dictionary<string, float> CustomLaneSpeedLimitByNetInfoName;

        /// <summary>For each name: NetInfo.</summary>
        private readonly Dictionary<string, NetInfo> NetInfoByName;

        /// <summary>Ingame speed units, minimal speed.</summary>
        private const float MIN_SPEED = 0.1f; // 5 km/h

        /// <summary>For each NetInfo (by name) and lane index: game default speed limit.</summary>
        private readonly Dictionary<string, float[]> vanillaLaneSpeedLimitsByNetInfoName_;

        /// <summary>For each NetInfo (by name): Parent NetInfo (name).</summary>
        private readonly Dictionary<string, List<string>>
            childNetInfoNamesByCustomizableNetInfoName_;

        private List<NetInfo> customizableNetInfos_;

        private SpeedLimitManager() {
            vanillaLaneSpeedLimitsByNetInfoName_ = new Dictionary<string, float[]>();
            CustomLaneSpeedLimitByNetInfoName = new Dictionary<string, float>();
            customizableNetInfos_ = new List<NetInfo>();
            childNetInfoNamesByCustomizableNetInfoName_ = new Dictionary<string, List<string>>();
            NetInfoByName = new Dictionary<string, NetInfo>();
        }

        /// <summary>Determines if custom speed limits may be assigned to the given segment.</summary>
        /// <param name="segment">Reference to affected segment.</param>
        /// <returns>Success.</returns>
        public bool MayHaveCustomSpeedLimits(ref NetSegment segment) {
            if ((segment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
                return false;
            }

            ItemClass connectionClass = segment.Info.GetConnectionClass();
            ItemClass.SubService subService = connectionClass.m_subService;
            ItemClass.Service service = connectionClass.m_service;

            return service == ItemClass.Service.Road
                   || (service == ItemClass.Service.PublicTransport
                       && (subService == ItemClass.SubService.PublicTransportTrain
                           || subService == ItemClass.SubService.PublicTransportTram
                           || subService == ItemClass.SubService.PublicTransportMetro
                           || subService == ItemClass.SubService.PublicTransportMonorail));
        }

        /// <summary>
        /// Determines if custom speed limits may be assigned to the given lane info.
        /// </summary>
        /// <param name="laneInfo">The <see cref="NetInfo.Lane"/> that you wish to check.</param>
        /// <returns>Whether lane can have custom speed limits.</returns>
        public bool MayHaveCustomSpeedLimits(NetInfo.Lane laneInfo) {
            return (laneInfo.m_laneType & LANE_TYPES) != NetInfo.LaneType.None
                   && (laneInfo.m_vehicleType & VEHICLE_TYPES) != VehicleInfo.VehicleType.None;
        }

        /// <summary>
        /// Determines the average default speed limit for a given NetInfo object in terms of
        /// discrete speed limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby
        /// translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="segmentInfo">Interested in this segment.</param>
        /// <param name="finalDir">Direction.</param>
        /// <returns>Result.</returns>
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
        /// <param name="segmentInfo">Interested in this segment.</param>
        /// <param name="finalDir">Direction.</param>
        /// <returns>Result.</returns>
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
        /// Determines the currently set speed limit for the given segment and lane direction in
        /// terms of discrete speed limit levels. An in-game speed limit of 2.0 (e.g. on highway) is
        /// hereby translated into a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="segmentId">Interested in this segment.</param>
        /// <param name="finalDir">Direction.</param>
        /// <returns>
        /// Mean speed limit, average for custom and default lane speeds or null
        /// if cannot be determined.
        /// </returns>
        public SpeedValue? GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction finalDir) {
            // calculate the currently set mean speed limit
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            if (segmentId == 0 ||
                (segmentsBuffer[segmentId].m_flags & NetSegment.Flags.Created) ==
                NetSegment.Flags.None) {
                return null;
            }

            NetInfo segmentInfo = segmentsBuffer[segmentId].Info;
            uint curLaneId = segmentsBuffer[segmentId].m_lanes;
            var laneIndex = 0;
            uint validLanes = 0;
            SpeedValue meanSpeedLimit = default;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (d != finalDir) {
                    goto nextIter;
                }

                if (!MayHaveCustomSpeedLimits(laneInfo)) {
                    goto nextIter;
                }

                SpeedValue? setSpeedLimit = Flags.GetLaneSpeedLimit(curLaneId);

                if (setSpeedLimit.HasValue) {
                    // custom speed limit
                    meanSpeedLimit += setSpeedLimit.Value;
                } else {
                    // game default (in game units where 1.0f = 50kmph)
                    meanSpeedLimit += new SpeedValue(laneInfo.m_speedLimit);
                }

                ++validLanes;

                nextIter:
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            if (validLanes > 0) {
                meanSpeedLimit = meanSpeedLimit.Scale(1.0f / validLanes);
            }

            return meanSpeedLimit;
        }

        /// <summary>
        /// Determines the currently set speed limit for the given lane in terms of discrete speed
        /// limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into
        /// a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="laneId">Interested in this lane.</param>
        /// <returns>Speed limit if set for lane, otherwise 0.</returns>
        public GetSpeedLimitResult GetCustomSpeedLimit(uint laneId) {
            //----------------------------------------
            // check custom speed limit for the lane
            //----------------------------------------
            SpeedValue? overrideValue = Flags.GetLaneSpeedLimit(laneId);

            //----------------------------
            // check default speed limit
            //----------------------------
            NetLane[] laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
            ushort segmentId = laneBuffer[laneId].m_segment;

            if (!MayHaveCustomSpeedLimits(ref segmentsBuffer[segmentId])) {
                // Don't have override, and default is not known
                return new GetSpeedLimitResult(
                    overrideValue: null,
                    defaultValue: null);
            }

            NetInfo segmentInfo = segmentsBuffer[segmentId].Info;
            uint curLaneId = segmentsBuffer[segmentId].m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    SpeedValue knownDefault = new SpeedValue(laneInfo.m_speedLimit);

                    if (MayHaveCustomSpeedLimits(laneInfo)) {
                        // May possibly have override, also the default is known
                        return new GetSpeedLimitResult(
                            overrideValue: overrideValue,
                            defaultValue: knownDefault);
                    }

                    // No override, but the default is known
                    return new GetSpeedLimitResult(
                        overrideValue: null,
                        defaultValue: knownDefault);
                }

                laneIndex++;
                curLaneId = laneBuffer[curLaneId].m_nextLane;
            }

            Log.Warning($"Speed limit for lane {laneId} could not be determined.");
            return new GetSpeedLimitResult(
                overrideValue: null,
                defaultValue: null);
        }

        // /// <summary>
        // /// Determines the currently set speed limit for the given lane in terms of game (floating
        // /// point) speed limit levels.
        // /// </summary>
        // /// <param name="laneId">Interested in this lane.</param>
        // /// <returns>Km/h in lane converted to game speed float.</returns>
        // [UsedImplicitly]
        // public float GetGameSpeedLimit(uint laneId) {
        //     return ToGameSpeedLimit(GetCustomSpeedLimit(laneId));
        // }

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

#if FGSFDS_DISABLED_CODE
        /// <summary>
        /// Explicitly stores currently set speed limits for all segments of the specified NetInfo
        /// </summary>
        /// <param name="info">The <see cref="NetInfo"/> for which speed limits should be stored.</param>
        [Obsolete("Not used, delete this")]
        public void FixCurrentSpeedLimits(NetInfo info) {
            if (info == null) {
#if DEBUG
                Log.Warning("SpeedLimitManager.FixCurrentSpeedLimits: info is null!");
#endif
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.FixCurrentSpeedLimits: info.name is null!");
            //    return;
            // }

            if (!customizableNetInfos_.Contains(info)) {
                return;
            }

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneAndItsSegmentValid(laneId)) {
                    continue;
                }

                NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
                NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;
                ushort segmentId = lanesBuffer[laneId].m_segment;
                NetInfo laneInfo = segmentsBuffer[segmentId].Info;

                if (laneInfo.name != info.name
                    && (!childNetInfoNamesByCustomizableNetInfoName_.ContainsKey(info.name)
                        || !childNetInfoNamesByCustomizableNetInfoName_[info.name].Contains(laneInfo.name))) {
                    continue;
                }

                GetSpeedLimitResult laneCustomSpeedLimit = GetCustomSpeedLimit(laneId);
                if (laneCustomSpeedLimit.OverrideValue.HasValue) {
                    Flags.SetLaneSpeedLimit(
                        laneId: laneId,
                        action: SetSpeedLimitAction.SetSpeed(laneCustomSpeedLimit.OverrideValue.Value));
                }
            }
        }
#endif // disabled code

        /// <summary>
        /// Explicitly clear currently set speed limits for all segments of the specified NetInfo
        /// </summary>
        /// <param name="info">The <see cref="NetInfo"/> for which speed limits should be cleared.</param>
        public void ClearCurrentSpeedLimits(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning("SpeedLimitManager.ClearCurrentSpeedLimits: info is null!");
                return;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.ClearCurrentSpeedLimits: info.name is null!");
            //    return;
            // }

            if (!customizableNetInfos_.Contains(info)) {
                return;
            }

            for (uint laneId = 1; laneId < NetManager.MAX_LANE_COUNT; ++laneId) {
                if (!Services.NetService.IsLaneAndItsSegmentValid(laneId)) {
                    continue;
                }

                NetInfo laneInfo = Singleton<NetManager>
                                   .instance.m_segments
                                   .m_buffer[Singleton<NetManager>
                                             .instance.m_lanes.m_buffer[laneId].m_segment]
                                   .Info;

                if (laneInfo.name != info.name &&
                    (!childNetInfoNamesByCustomizableNetInfoName_.ContainsKey(info.name) ||
                     !childNetInfoNamesByCustomizableNetInfoName_[info.name]
                         .Contains(laneInfo.name))) {
                    continue;
                }

                Flags.RemoveLaneSpeedLimit(laneId);
            }
        }

        /// <summary>
        /// Determines the game default speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the game default speed limit should be determined</param>
        /// <param name="roundToSignLimits">if true, custom speed limit are rounded to speed limits
        /// available as speed limit sign</param>
        /// <returns>The vanilla speed limit, in game units.</returns>
        public float GetVanillaNetInfoSpeedLimit(NetInfo info, bool roundToSignLimits = true) {
            if (info == null) {
                Log._DebugOnlyWarning(
                    "SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info is null!");
                return 0;
            }

            if (info.m_netAI == null) {
                Log._DebugOnlyWarning(
                    "SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.m_netAI is null!");
                return 0;
            }

            // Resharper warning: condition always false
            // if (info.name == null) {
            //    Log._DebugOnlyWarning($"SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.name is null!");
            //    return 0;
            // }

            string infoName = info.name;
            if (!vanillaLaneSpeedLimitsByNetInfoName_.TryGetValue(
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
        /// <returns>-1 if no custom speed limit was set.</returns>
        public float GetCustomNetInfoSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning(
                    "SpeedLimitManager.GetCustomNetInfoSpeedLimit: info is null!");
                return -1;
            }

            string infoName = info.name;
            return !CustomLaneSpeedLimitByNetInfoName.TryGetValue(infoName, out float speedLimit)
                       ? GetVanillaNetInfoSpeedLimit(info: info, roundToSignLimits: true)
                       : speedLimit;
        }

        /// <summary>
        /// Sets the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo for which the custom speed limit should be set</param>
        /// <param name="customSpeedLimit">The speed value to set in game speed units</param>
        // TODO: The speed limit manager only supports default speed limit overrides per road type, not per lane
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

            if (childNetInfoNamesByCustomizableNetInfoName_.TryGetValue(
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

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for SpeedLimitManager");
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
        /// <param name="action">Game speed units, unlimited, or default.</param>
        /// <returns>Success.</returns>
        public bool SetLaneSpeedLimit(ushort segmentId,
                                      uint laneIndex,
                                      NetInfo.Lane laneInfo,
                                      uint laneId,
                                      SetSpeedLimitAction action) {
            if (!MayHaveCustomSpeedLimits(laneInfo)) {
                return false;
            }

            if (action.Type != SetSpeedLimitAction.ActionType.ResetToDefault
                && !IsValidRange(action.Override.Value.GameUnits)) {
                return false;
            }

            if (!Services.NetService.IsLaneAndItsSegmentValid(laneId)) {
                return false;
            }

            if (action.Type != SetSpeedLimitAction.ActionType.ResetToDefault) {
                Flags.SetLaneSpeedLimit(segmentId, laneIndex, laneId, action);
            } else {
                Flags.RemoveLaneSpeedLimit(laneId);
            }

            return true;
        }

        /// <summary>Sets speed limit for all configurable lanes.</summary>
        /// <param name="action">Speed limit in game units, or null to restore defaults.</param>
        /// <returns>
        /// <c>false</c>if there are no configurable lanes.
        /// <c>true</c> if any speed limits were applied.
        /// </returns>
        public bool SetSegmentSpeedLimit(ushort segmentId, SetSpeedLimitAction action) {
            bool ret = false;

            foreach (NetInfo.Direction finaldir in Enum.GetValues(typeof(NetInfo.Direction))) {
                ret |= SetSegmentSpeedLimit(segmentId, finaldir, action);
            }

            return ret;
        }

        /// <summary>Sets the speed limit of a given segment and lane direction.</summary>
        /// <param name="segmentId">Segment id.</param>
        /// <param name="finalDir">Direction.</param>
        /// <param name="action">Game speed units, unlimited, or reset to default.</param>
        /// <returns>Success.</returns>
        public bool SetSegmentSpeedLimit(ushort segmentId,
                                         NetInfo.Direction finalDir,
                                         SetSpeedLimitAction action) {
            NetSegment[] segmentsBuffer = Singleton<NetManager>.instance.m_segments.m_buffer;

            if (!MayHaveCustomSpeedLimits(ref segmentsBuffer[segmentId])) {
                return false;
            }

            if (action.Type == SetSpeedLimitAction.ActionType.SetOverride
                && !IsValidRange(action.Override.Value.GameUnits)) {
                return false;
            }

            NetInfo segmentInfo = segmentsBuffer[segmentId].Info;

            if (segmentInfo == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.SetSpeedLimit: info is null!");
                return false;
            }

            if (segmentInfo.m_lanes == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.SetSpeedLimit: info.m_lanes is null!");
                return false;
            }

            uint curLaneId = segmentsBuffer[segmentId].m_lanes;
            int laneIndex = 0;
            NetLane[] laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            //-------------------------
            // For each affected lane
            //-------------------------
            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (d == finalDir && MayHaveCustomSpeedLimits(laneInfo)) {
                    if (action.Type == SetSpeedLimitAction.ActionType.ResetToDefault) {
                        // Setting to 'Default' will instead remove the override
                        Log._Debug(
                            $"SpeedLimitManager: Setting speed limit of lane {curLaneId} " +
                            $"to default");
                        Flags.RemoveLaneSpeedLimit(curLaneId);
                    } else {
                        Log._Debug(
                            $"SpeedLimitManager: Setting speed limit of lane {curLaneId} " +
                            $"to {action.Override}");
                        Flags.SetLaneSpeedLimit(curLaneId, action);
                    }
                }

                curLaneId = laneBuffer[curLaneId].m_nextLane;
                laneIndex++;
            }

            return true;
        }

        public List<NetInfo> GetCustomizableNetInfos() {
            return customizableNetInfos_;
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();

#if DEBUG
            bool debugSpeedLimits = GlobalConfig.Instance.Debug.SpeedLimits;
#endif

            // determine vanilla speed limits and customizable NetInfos
            SteamHelper.DLC_BitMask dlcMask = SteamHelper.GetOwnedDLCMask();

            int numLoaded = PrefabCollection<NetInfo>.LoadedCount();

            // todo: move this to a Reset() or Clear() method?
            vanillaLaneSpeedLimitsByNetInfoName_.Clear();
            customizableNetInfos_.Clear();
            CustomLaneSpeedLimitByNetInfoName.Clear();
            childNetInfoNamesByCustomizableNetInfoName_.Clear();
            NetInfoByName.Clear();

            List<NetInfo> mainNetInfos = new List<NetInfo>();

            // Basic logging to help road/track asset creators see if their netinfo is wrong
            // 6000 is rougly 120 lines; should be more than enough for most users
            StringBuilder log = new StringBuilder(6000);

            log.AppendFormat(
                "SpeedLimitManager.OnBeforeLoadData: {0} NetInfos loaded. Verifying...\n",
                numLoaded);

            for (uint i = 0; i < numLoaded; ++i) {
                NetInfo info = PrefabCollection<NetInfo>.GetLoaded(i);

                // Basic validity checks to see if this NetInfo is something speed limits can be applied to...

                // Something in the workshop has null NetInfos in it...
                if (info == null) {
                    Log.InfoFormat(
                        "SpeedLimitManager.OnBeforeLoadData: NetInfo #{0} is null!",
                        i);
                    continue;
                }

                string infoName = info.name;

                // We need a valid name
                if (string.IsNullOrEmpty(infoName)) {
                    log.AppendFormat(
                        "- Skipped: NetInfo #{0} - name is empty!\n",
                        i);
                    continue;
                }

                // Make sure it's valid AI
                if (info.m_netAI == null) {
                    log.AppendFormat(
                        "- Skipped: NetInfo #{0} ({1}) - m_netAI is null.\n",
                        i,
                        infoName);
                    continue;
                }

                // Must be road or track based
                if (!(info.m_netAI is RoadBaseAI || info.m_netAI is TrainTrackBaseAI ||
                      info.m_netAI is MetroTrackAI)) {
#if DEBUG
                    // Only outputting these in debug as there are loads of them
                    Log._DebugIf(
                        debugSpeedLimits,
                        () =>
                            $"- Skipped: NetInfo #{i} ({infoName}) - m_netAI is not applicable: {info.m_netAI}.");
#endif
                    continue;
                }

                // If it requires DLC, check the DLC is active
                if ((info.m_dlcRequired & dlcMask) != info.m_dlcRequired) {
                    log.AppendFormat(
                        "- Skipped: NetInfo #{0} ({1}) - required DLC not active.\n",
                        i,
                        infoName);
                    continue;
                }

                // #510: Filter out decorative networks (`None`) and bike paths (`Bicycle`)
                if (info.m_vehicleTypes == VehicleInfo.VehicleType.None ||
                    info.m_vehicleTypes == VehicleInfo.VehicleType.Bicycle) {
                    log.AppendFormat(
                        "- Skipped: NetInfo #{0} ({1}) - no vehicle support (decorative or bike path?)\n",
                        i,
                        infoName);
                    continue;
                }

                if (!vanillaLaneSpeedLimitsByNetInfoName_.ContainsKey(infoName)) {
                    if (info.m_lanes == null) {
                        log.AppendFormat(
                            "- Skipped: NetInfo #{0} ({1}) - m_lanes is null!\n",
                            i,
                            infoName);

                        Log.Warning(
                            $"SpeedLimitManager.OnBeforeLoadData: NetInfo @ {i} ({infoName}) lanes is null!");
                        continue;
                    }

                    Log._Trace($"- Loaded road NetInfo: {infoName}");

                    NetInfoByName[infoName] = info;
                    mainNetInfos.Add(info);

                    float[] vanillaLaneSpeedLimits = new float[info.m_lanes.Length];

                    for (var k = 0; k < info.m_lanes.Length; ++k) {
                        vanillaLaneSpeedLimits[k] = info.m_lanes[k].m_speedLimit;
                    }

                    vanillaLaneSpeedLimitsByNetInfoName_[infoName] = vanillaLaneSpeedLimits;
                }
            }

            log.Append("SpeedLimitManager.OnBeforeLoadData: Scan complete.\n");
            Log.Info(log.ToString());

            mainNetInfos.Sort(
                (NetInfo a, NetInfo b) => {
                    // todo: move arrow function somewhere else?
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

                        if (!childNetInfoNamesByCustomizableNetInfoName_.TryGetValue(
                                parentInfo.name,
                                out List<string> childNetInfoNames)) {
                            childNetInfoNamesByCustomizableNetInfoName_[parentInfo.name] =
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

            customizableNetInfos_ = mainNetInfos;
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            NetInfo segmentInfo =
                Singleton<NetManager>.instance.m_segments.m_buffer[seg.segmentId].Info;
            uint curLaneId = Singleton<NetManager>
                             .instance.m_segments.m_buffer[seg.segmentId].m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                Flags.SetLaneSpeedLimit(curLaneId, SetSpeedLimitAction.ResetToDefault());

                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

        public bool LoadData(List<Configuration.LaneSpeedLimit> data) {
            bool success = true;
            Log.Info($"Loading lane speed limit data. {data.Count} elements");
#if DEBUG
            bool debugSpeedLimits = GlobalConfig.Instance.Debug.SpeedLimits;
#endif
            foreach (Configuration.LaneSpeedLimit laneSpeedLimit in data) {
                try {
                    if (!Services.NetService.IsLaneAndItsSegmentValid(laneSpeedLimit.laneId)) {
#if DEBUG
                        Log._DebugIf(
                            debugSpeedLimits,
                            () =>
                                $"SpeedLimitManager.LoadData: Skipping lane {laneSpeedLimit.laneId}: Lane is invalid");
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
#if DEBUG
                    Log._DebugIf(
                        debugSpeedLimits,
                        () =>
                            $"SpeedLimitManager.LoadData: Handling lane {laneSpeedLimit.laneId}: " +
                            $"Custom speed limit of segment {segmentId} info ({info}, name={info?.name}, " +
                            $"lanes={info?.m_lanes} is {customSpeedLimit}");
#endif

                    if (IsValidRange(customSpeedLimit)) {
                        // lane speed limit differs from default speed limit
#if DEBUG
                        Log._DebugIf(
                            debugSpeedLimits,
                            () =>
                                "SpeedLimitManager.LoadData: Loading lane speed limit: " +
                                $"lane {laneSpeedLimit.laneId} = {laneSpeedLimit.speedLimit} km/h");
#endif
                        // convert to game units
                        float units = laneSpeedLimit.speedLimit / ApiConstants.SPEED_TO_KMPH;

                        Flags.SetLaneSpeedLimit(
                            laneSpeedLimit.laneId,
                            SetSpeedLimitAction.SetOverride(new SpeedValue(units)));
                    } else {
#if DEBUG
                        Log._DebugIf(
                            debugSpeedLimits,
                            () =>
                                "SpeedLimitManager.LoadData: " +
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
                    var laneSpeedLimit = new Configuration.LaneSpeedLimit(
                        e.Key,
                        new SpeedValue(e.Value));
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

        public static bool IsValidRange(float gameUnits) {
            return FloatUtil.IsZero(gameUnits)
                   || (gameUnits >= MIN_SPEED && gameUnits <= MAX_SPEED);
        }
    } // end class
}
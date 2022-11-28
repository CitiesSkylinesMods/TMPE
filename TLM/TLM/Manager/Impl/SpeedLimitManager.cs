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
    using System.Text;
    using TrafficManager.API.Traffic;
    using TrafficManager.Util.Extensions;
    using UnityEngine;

    public class SpeedLimitManager
        : AbstractGeometryObservingManager,
          ICustomDataManager<List<Configuration.LaneSpeedLimit>>,
          ICustomDataManager<Dictionary<string, float>>,
          ISpeedLimitManager {
        /// <summary>Interested only in these lane types.</summary>
        public const NetInfo.LaneType LANE_TYPES =
            NetInfo.LaneType.Vehicle | NetInfo.LaneType.TransportVehicle;

        /// <summary>Support speed limits only for these vehicle types.</summary>
        public const VehicleInfo.VehicleType VEHICLE_TYPES =
            VehicleInfo.VehicleType.Car | VehicleInfo.VehicleType.Tram |
            VehicleInfo.VehicleType.Metro | VehicleInfo.VehicleType.Train |
            VehicleInfo.VehicleType.Monorail | VehicleInfo.VehicleType.Trolleybus;

        private readonly object laneSpeedLimitLock_ = new();

        /// <summary>For each lane: Defines the currently set speed limit. Units: Game speed units (1.0 = 50 km/h).</summary>
        private readonly Dictionary<uint, float> laneOverrideSpeedLimit_ = new();

        /// <summary>
        /// caches the final game speed limit.
        /// array index is laneId.
        /// Units: Game speed units (1.0 = 50 km/h).
        /// </summary>
        private readonly float[] cachedLaneSpeedLimits_ = new float[NetManager.instance.m_lanes.m_size];

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        /// <summary>Ingame speed units, minimal speed.</summary>
        private const float MIN_SPEED = 0.1f; // 5 km/h

        public static readonly SpeedLimitManager Instance = new();

        /// <summary>For each NetInfo name: custom speed limit.</summary>
        private readonly Dictionary<NetInfo, float> customNetinfoSpeedLimits_ = new();

        /// <summary>determine if speed limits on this netinfo can be customised</summary>
        public bool IsCustomisable(NetInfo netinfo) {
            if (!netinfo) {
                Log.Warning("Skipped NetINfo with null info");
                return false;
            }

            if (string.IsNullOrEmpty(netinfo.name)) {
                Log.Warning("Skipped NetINfo with empty name");
                return false;
            }

            if (netinfo.m_netAI == null) {
                Log.Warning($"Skipped NetInfo '{netinfo.name}' with null AI");
                return false;
            }

#if DEBUG
            bool debugSpeedLimits = DebugSwitch.SpeedLimits.Get();
#endif

            // Must be road or track based:
            if (!(netinfo.m_netAI is RoadBaseAI or TrainTrackBaseAI or MetroTrackBaseAI)) {
#if DEBUG
                if (debugSpeedLimits)
                    Log._Debug($"Skipped NetInfo '{netinfo.name}' because m_netAI is not applicable: {netinfo.m_netAI}");
#endif
                return false;
            }

            if (!netinfo.m_vehicleTypes.IsFlagSet(VEHICLE_TYPES) || !netinfo.m_laneTypes.IsFlagSet(LANE_TYPES)) {
#if DEBUG
                if (debugSpeedLimits)
                    Log._Debug($"Skipped decorative NetInfo '{netinfo.name}' with m_vehicleType={netinfo.m_vehicleTypes} and m_laneTypes={netinfo.m_laneTypes}");
#endif

                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the speed limit value for the NetInfo and the lane.
        /// If customised returns the custom speed limit.
        /// Otherwise returns the lane speed limit.
        /// </summary>
        public SpeedValue GetDefaultSpeedLimit(NetInfo netinfo, NetInfo.Lane laneInfo) {
            if (customNetinfoSpeedLimits_.TryGetValue(netinfo, out float speedLimit)) {
                return new SpeedValue(speedLimit);
            } else {
                return new SpeedValue(laneInfo.m_speedLimit);
            }
        }

        /// <summary>Determines the currently set speed limit for the given segment and lane
        ///     vehicle type in terms of discrete speed limit levels.</summary>
        /// <param name="segmentId">Interested in this segment.</param>
        /// <param name="vehicleType">Vehicle type</param>
        /// <returns>Mean speed limit, average for custom and default lane speeds or null
        ///     if cannot be determined.</returns>
        public SpeedValue? CalculateCustomSpeedLimit(ushort segmentId, VehicleInfo.VehicleType vehicleType) {
            // calculate the currently set mean speed limit
            if (segmentId == 0) {
                return null;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
                return null;
            }

            NetInfo netinfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            var laneIndex = 0;
            uint validLanes = 0;
            SpeedValue meanSpeedLimit = default;

            while (laneIndex < netinfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = netinfo.m_lanes[laneIndex];

                if (!laneInfo.m_vehicleType.IsFlagSet(vehicleType)) {
                    goto nextIter;
                }

                if (!laneInfo.MayHaveCustomSpeedLimits()) {
                    goto nextIter;
                }

                SpeedValue? setSpeedLimit = this.CalculateLaneSpeedLimit(curLaneId);

                if (setSpeedLimit.HasValue) {
                    // custom speed limit
                    meanSpeedLimit += setSpeedLimit.Value;
                } else {
                    meanSpeedLimit += GetDefaultSpeedLimit(netinfo, laneInfo);
                }

                ++validLanes;

                nextIter:
                curLaneId = curLaneId.ToLane().m_nextLane;
                laneIndex++;
            }

            return validLanes == 0
                       ? null
                       : meanSpeedLimit.Scale(1.0f / validLanes);
        }

        /// <summary>
        /// Determines the currently set speed limit for the given lane in terms of discrete speed
        /// limit levels. An in-game speed limit of 2.0 (e.g. on highway) is hereby translated into
        /// a discrete speed limit value of 100 (km/h).
        /// </summary>
        /// <param name="laneId">Interested in this lane</param>
        /// <returns>Speed limit if set for lane, otherwise 0</returns>
        public GetSpeedLimitResult CalculateCustomSpeedLimit(uint laneId) {
            //----------------------------------------
            // check custom speed limit for the lane
            //----------------------------------------
            SpeedValue? overrideValue = this.CalculateLaneSpeedLimit(laneId);

            //----------------------------
            // check default speed limit
            //----------------------------
            ref NetLane netLane = ref laneId.ToLane();
            ushort segmentId = netLane.m_segment;
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.MayHaveCustomSpeedLimits()) {
                // Don't have override, and default is not known
                return new GetSpeedLimitResult(
                    overrideValue: null,
                    defaultValue: null);
            }

            NetInfo netinfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            int laneIndex = 0;

            while (laneIndex < netinfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    NetInfo.Lane laneInfo = netinfo.m_lanes[laneIndex];
                    SpeedValue knownDefault = GetDefaultSpeedLimit(netinfo, laneInfo);

                    if (laneInfo.MayHaveCustomSpeedLimits()) {
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
                curLaneId = curLaneId.ToLane().m_nextLane;
            }

            Log.Warning($"Speed limit for lane {laneId} could not be determined.");
            return new GetSpeedLimitResult(
                overrideValue: null,
                defaultValue: null);
        }

        /// <summary>Determines the currently set speed limit for the given lane.</summary>
        /// <param name="laneId">The lane id.</param>
        /// <returns>Game units.</returns>
        public float CalculateGameSpeedLimit(uint laneId) {
            GetSpeedLimitResult overrideSpeedLimit = this.CalculateCustomSpeedLimit(laneId);
            if (overrideSpeedLimit.DefaultValue != null) {
                SpeedValue activeLimit = overrideSpeedLimit.OverrideValue ?? overrideSpeedLimit.DefaultValue.Value;
                return ToGameSpeedLimit(activeLimit.GameUnits);
            }

            return 0f;
        }

        public float GetGameSpeedLimit(uint laneId) {
            return GetGameSpeedLimit(
                laneId: laneId,
                laneInfo: ExtLaneManager.Instance.GetLaneInfo(laneId));
        }

        [Obsolete]
        public float GetGameSpeedLimit(ushort segmentId, byte laneIndex, uint laneId, NetInfo.Lane laneInfo) =>
            GetGameSpeedLimit(laneId, laneInfo);

        /// <summary>
        /// fast access to lane speed limit.
        /// Units: Game speed units (1.0 = 50 km/h).
        /// </summary>
        /// <returns>
        /// final game speed limit of the lane.
        /// </returns>
        public float GetGameSpeedLimit(uint laneId, NetInfo.Lane laneInfo) {
            try {
#if !SPEEDLIMIT
                if (!SavedGameOptions.Instance.customSpeedLimitsEnabled || !laneInfo.MayHaveCustomSpeedLimits())
#endif
                {
                    return laneInfo.m_speedLimit;
                }

                return cachedLaneSpeedLimits_[laneId];
            } catch (Exception ex) {
                new Exception($"GetGameSpeedLimit({laneId}, {laneInfo}", ex).LogException();
                return laneInfo?.m_speedLimit ?? 0;
            }
        }

        /// <summary>
        /// Converts a possibly zero (no limit) custom speed limit to a game speed limit.
        /// </summary>
        /// <param name="customSpeedLimit">Custom speed limit which can be zero</param>
        /// <returns>Speed limit in game speed units</returns>
        private static float ToGameSpeedLimit(float customSpeedLimit) {
            return FloatUtil.IsZero(customSpeedLimit)
                       ? SpeedValue.UNLIMITED
                       : customSpeedLimit;
        }

        /// <summary>
        /// Scans lanes which may have customizable speed limit and returns the fastest <c>m_speedLimit</c> encountered.
        /// </summary>
        /// <param name="info">The <see cref="NetInfo"/> to inspect.</param>
        /// <returns>The vanilla speed limit, in game units.</returns>
        private float FindFastestCustomisableVanillaLaneSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning("SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info is null!");
                return 0f;
            }

            if (info.m_netAI == null) {
                Log._DebugOnlyWarning("SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.m_netAI is null!");
                return 0f;
            }

            if (info.m_lanes == null) {
                return 0f;
            }

            float maxSpeedLimit = 0f;

            foreach (var laneInfo in info.m_lanes) {
                if (laneInfo.MayHaveCustomSpeedLimits()) {
                    float speedLimit = laneInfo.m_speedLimit;
                    if (speedLimit > maxSpeedLimit) {
                        maxSpeedLimit = speedLimit;
                    }
                }
            }

            return maxSpeedLimit;
        }

        /// <summary>
        /// Determines the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the custom speed limit should be determined</param>
        /// <returns>-1 if no custom speed limit was set</returns>
        public float CalculateCustomNetinfoSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.GetCustomNetinfoSpeedLimit: info is null!");
                return -1f;
            }

            return !customNetinfoSpeedLimits_.TryGetValue(info, out float speedLimit)
                       ? FindFastestCustomisableVanillaLaneSpeedLimit(info)
                       : speedLimit;
        }

        internal IEnumerable<NetInfo> GetCustomisableRelatives(NetInfo netinfo) {
            foreach(var netinfo2 in netinfo.GetRelatives()) {
                if (IsCustomisable(netinfo2))
                    yield return netinfo2;
            }
        }

        /// <summary>
        /// Sets the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="netinfo">the NetInfo for which the custom speed limit should be set</param>
        /// <param name="customSpeedLimit">The speed value to set in game speed units</param>
        public void SetCustomNetinfoSpeedLimit(NetInfo netinfo, float customSpeedLimit) {
            if (netinfo == null) {
                Log._DebugOnlyWarning($"SetCustomNetInfoSpeedLimitIndex: info is null!");
                return;
            }

            float gameSpeedLimit = ToGameSpeedLimit(customSpeedLimit);


            foreach (var relatedNetinfo in GetCustomisableRelatives(netinfo)) {
                customNetinfoSpeedLimits_[relatedNetinfo] = customSpeedLimit;

#if DEBUGLOAD
                Log._Debug($"Updating NetInfo {relatedNetinfo.name}: Setting speed limit to {gameSpeedLimit}");
#endif
                // save speed limit in all NetInfos
                UpdateNetinfo(relatedNetinfo);
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for SpeedLimitManager");
        }

        private void UpdateNetinfo(NetInfo netinfo) {
            if (netinfo == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.UpdateNetinfoSpeedLimit: info is null!");
                return;
            }

            if (netinfo.m_lanes == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.UpdateNetinfoSpeedLimit: info.lanes is null!");
                return;
            }

            Log._Debug($"caching speed limits of segments of NetInfo {netinfo.name}");

            for(ushort segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref var netSegment = ref segmentId.ToSegment();
                if (netSegment.IsValid() && netSegment.Info == netinfo) {
                    CacheSegmentSpeeds(segmentId);
                    Notifier.Instance.OnSegmentModified(segmentId, this);
                }
            }
        }

        private void CacheSegmentSpeeds(ushort segmentId) {
            ref var netSegment = ref segmentId.ToSegment();
            NetInfo netinfo = netSegment.Info;
            var lanes = netinfo?.m_lanes;
            if (lanes != null) {
                uint laneId = netSegment.m_lanes;
                for (int laneIndex = 0; laneId != 0 && laneIndex < lanes.Length; ++laneIndex) {
                    cachedLaneSpeedLimits_[laneId] = CalculateGameSpeedLimit(laneId);
                    laneId = laneId.ToLane().m_nextLane;
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
            if (!laneInfo.MayHaveCustomSpeedLimits()) {
                return false;
            }

            if (action.Type == SetSpeedLimitAction.ActionType.ResetToDefault) {
                RemoveLaneSpeedLimit(laneId);
                Notifier.Instance.OnSegmentModified(segmentId, this);
                return true;
            }

            if (action.Type != SetSpeedLimitAction.ActionType.ResetToDefault
                && !IsValidRange(action.GuardedValue.Override.GameUnits)) {
                return false;
            }

            ref NetLane netLane = ref laneId.ToLane();
            if (!netLane.IsValidWithSegment()) {
                return false;
            }

            SetLaneSpeedLimit(segmentId, laneIndex, laneId, action);

            Notifier.Instance.OnSegmentModified(segmentId, this);
            return true;
        }

        /// <summary>
        /// Resets default speed limit for netinfo and all child netinfos.
        /// Mostly repeats the code of <see cref="SetCustomNetinfoSpeedLimit"/>.
        /// </summary>
        public void ResetCustomNetinfoSpeedLimit([NotNull] NetInfo netinfo) {
            if (netinfo == null) {
                Log._DebugOnlyWarning($"SetCustomNetInfoSpeedLimitIndex: info is null!");
                return;
            }

            var vanillaSpeedLimit = FindFastestCustomisableVanillaLaneSpeedLimit(netinfo);

            foreach (var relatedNetinfo in GetCustomisableRelatives(netinfo)) {
                if (this.customNetinfoSpeedLimits_.ContainsKey(relatedNetinfo)) {
                    this.customNetinfoSpeedLimits_.Remove(relatedNetinfo);
                }
                this.UpdateNetinfo(relatedNetinfo);
            }
        }

        /// <summary>Sets the speed limit for the specified <paramref name="segmentId"/>.</summary>
        /// <param name="segmentId">Segment id.</param>
        /// <param name="action">Game speed units, unlimited, or reset to default.</param>
        /// <returns>
        /// Returns <c>true</c> if speed limits were applied to at least one lane, otherwise <c>false</c>.
        /// </returns>
        public bool SetSegmentSpeedLimit(
            ushort segmentId,
            SetSpeedLimitAction action) {

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.MayHaveCustomSpeedLimits()) {
                return false;
            }

            if (action.Type == SetSpeedLimitAction.ActionType.SetOverride
                && !IsValidRange(action.GuardedValue.Override.GameUnits)) {
                return false;
            }

            NetInfo segmentInfo = netSegment.Info;

            if (segmentInfo == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.SetSpeedLimit: info is null!");
                return false;
            }

            if (segmentInfo.m_lanes == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.SetSpeedLimit: info.m_lanes is null!");
                return false;
            }

            uint curLaneId = netSegment.m_lanes;
            int laneIndex = 0;

            while (curLaneId != 0u && laneIndex < segmentInfo.m_lanes.Length) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];

                if (laneInfo.MayHaveCustomSpeedLimits()) {
                    if (action.Type == SetSpeedLimitAction.ActionType.ResetToDefault) {
                        // Setting to 'Default' will instead remove the override
                        Log._Debug($"SpeedLimitManager: Setting speed limit of lane {curLaneId} to default");
                        RemoveLaneSpeedLimit(curLaneId);
                    } else {
#if DEBUG
                        bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                        string overrideStr = action.GuardedValue.Override.FormatStr(showMph);

                        Log._Debug($"SpeedLimitManager: Setting lane {curLaneId} to {overrideStr}");
#endif
                        SetLaneSpeedLimit(curLaneId, action);
                    }
                }

                curLaneId = curLaneId.ToLane().m_nextLane;
                laneIndex++;
            }

            Notifier.Instance.OnSegmentModified(segmentId, this);
            return true;
        }

        public static bool IsInSlowDrivingDistrict(ref NetSegment segment) {
            Vector3 pos = segment.m_middlePosition;
            DistrictManager districtManager = DistrictManager.instance;
            byte parkId = districtManager.GetPark(pos);
            return parkId != 0 && (districtManager.m_parks.m_buffer[parkId].m_parkPolicies & DistrictPolicies.Park.SlowDriving) != 0;
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();
            ResetSpeedLimits();
        }

        protected override void HandleInvalidSegment(ref ExtSegment extSegment) {
            ref NetSegment netSegment = ref extSegment.segmentId.ToSegment();
            foreach (var lane in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                SetLaneSpeedLimit(lane.laneId, SetSpeedLimitAction.ResetToDefault());
            }
        }

        protected override void HandleValidSegment(ref ExtSegment extSegment) {
            ref NetSegment netSegment = ref extSegment.segmentId.ToSegment();
            foreach(var lane in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                cachedLaneSpeedLimits_[lane.laneId] = CalculateGameSpeedLimit(lane.laneId);
            }
        }

        public bool LoadData(List<Configuration.LaneSpeedLimit> data) {
            bool success = true;
            Log.Info($"Loading lane speed limit data. {data.Count} elements");
#if DEBUG
            bool debugSpeedLimits = DebugSwitch.SpeedLimits.Get();
#endif
            foreach (Configuration.LaneSpeedLimit laneSpeedLimit in data) {
                try {
                    ref NetLane netLane = ref laneSpeedLimit.laneId.ToLane();

                    if (!netLane.IsValidWithSegment()) {
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
                    NetInfo info = segmentId.ToSegment().Info;
                    float customSpeedLimit = CalculateCustomNetinfoSpeedLimit(info);
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

                        SetLaneSpeedLimit(
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

        /// <summary>Impl. for Lane speed limits data manager.</summary>
        List<Configuration.LaneSpeedLimit> ICustomDataManager<List<Configuration.LaneSpeedLimit>>.SaveData(ref bool success)
        {
            return this.SaveLanes(ref success);
        }

        private List<Configuration.LaneSpeedLimit> SaveLanes(ref bool success) {
            var result = new List<Configuration.LaneSpeedLimit>();

            foreach (KeyValuePair<uint, float> e in this.GetAllLaneSpeedLimits()) {
                try {
                    var laneSpeedLimit = new Configuration.LaneSpeedLimit(
                        e.Key,
                        new SpeedValue(e.Value));
#if DEBUGSAVE
                    Log._Debug($"Saving speed limit of lane {laneSpeedLimit.laneId}: " +
                        $"{laneSpeedLimit.speedLimit*SpeedLimit.SPEED_TO_KMPH} km/h");
#endif
                    result.Add(laneSpeedLimit);
                }
                catch (Exception ex) {
                    Log.Error($"Exception occurred while saving lane speed limit @ {e.Key}: {ex}");
                    success = false;
                }
            }

            return result;
        }

        /// <summary>Called to load our data from the savegame or config options.</summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool LoadData([NotNull] Dictionary<string, float> data) {
            Log.Info($"Loading custom default speed limit data. {data.Count} elements");
            foreach (KeyValuePair<string, float> e in data) {
                if (PrefabCollection<NetInfo>.FindLoaded(e.Key) is not NetInfo netInfo) {
                    continue;
                }

                if (e.Value >= 0f) {
                    SetCustomNetinfoSpeedLimit(netInfo, e.Value);
                }
            }

            return true; // true = success
        }

        /// <summary>Impl. for Custom default speed limits data manager.</summary>
        Dictionary<string, float> ICustomDataManager<Dictionary<string, float>>.SaveData(ref bool success) {
            return this.SaveCustomDefaultLimits(ref success);
        }

        private Dictionary<string, float> SaveCustomDefaultLimits(ref bool success) {
            var result = new Dictionary<string, float>();

            foreach (var pair in customNetinfoSpeedLimits_) {
                try {
                    float gameSpeedLimit = ToGameSpeedLimit(pair.Value);
                    result.Add(pair.Key?.name, gameSpeedLimit);
                }
                catch (Exception ex) {
                    Log.Error(
                        $"Exception occurred while saving custom default speed limits @ {pair.Key?.name}: {ex}");
                    success = false;
                }
            }

            return result;
        }

        /// <summary>Used for loading and saving lane speed limits.</summary>
        /// <returns>ICustomDataManager with custom lane speed limits</returns>
        public static ICustomDataManager<List<Configuration.LaneSpeedLimit>> AsLaneSpeedLimitsDM() {
            return Instance;
        }

        /// <summary>Used for loading and saving custom default speed limits.</summary>
        /// <returns>ICustomDataManager with custom default speed limits</returns>
        public static ICustomDataManager<Dictionary<string, float>> AsCustomDefaultSpeedLimitsDM() {
            return Instance;
        }

        public static bool IsValidRange(float speed) {
            return FloatUtil.IsZero(speed) || (speed >= MIN_SPEED && speed <= SpeedValue.UNLIMITED);
        }

        /// <summary>Private: Do not call from the outside.</summary>
        private void SetLaneSpeedLimit(uint laneId, SetSpeedLimitAction action) {
            if (!Flags.CheckLane(laneId)) {
                return;
            }

            ushort segmentId = laneId.ToLane().m_segment;
            ref NetSegment netSegment = ref segmentId.ToSegment();
            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            uint laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    SetLaneSpeedLimit(segmentId, laneIndex, laneId, action);
                    return;
                }

                laneIndex++;
                curLaneId = curLaneId.ToLane().m_nextLane;
            }
        }

        /// <summary>Private: Do not call from the outside.</summary>
        public void RemoveLaneSpeedLimit(uint laneId) {
            SetLaneSpeedLimit(laneId, SetSpeedLimitAction.ResetToDefault());
        }

        /// <summary>Private: Do not call from the outside.</summary>
        public void SetLaneSpeedLimit(ushort segmentId,
                                      uint laneIndex,
                                      uint laneId,
                                      SetSpeedLimitAction action) {
            if (segmentId <= 0 || laneId <= 0) {
                return;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & (NetSegment.Flags.Created | NetSegment.Flags.Deleted)) != NetSegment.Flags.Created) {
                return;
            }

            if (((NetLane.Flags)laneId.ToLane().m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;
            if (laneIndex >= segmentInfo.m_lanes.Length) {
                return;
            }

            lock (laneSpeedLimitLock_) {
#if DEBUGFLAGS
                Log._Debug(
                    $"Flags.setLaneSpeedLimit: setting speed limit of lane index {laneIndex} @ seg. " +
                    $"{segmentId} to {speedLimit}");
#endif
                switch (action.Type) {
                    case SetSpeedLimitAction.ActionType.ResetToDefault:
                        laneOverrideSpeedLimit_.Remove(laneId);
                        break;
                    case SetSpeedLimitAction.ActionType.Unlimited:
                    case SetSpeedLimitAction.ActionType.SetOverride:
                        float gameUnits = action.GuardedValue.Override.GameUnits;
                        laneOverrideSpeedLimit_[laneId] = gameUnits;
                        break;
                }
                cachedLaneSpeedLimits_[laneId] = CalculateGameSpeedLimit(laneId);
            }
        }

        public SpeedValue? CalculateLaneSpeedLimit(uint laneId) {
            lock(laneSpeedLimitLock_) {
                if (laneId <= 0 || !laneOverrideSpeedLimit_.TryGetValue(laneId, out float gameUnitsOverride)) {
                    return null;
                }

                // assumption: speed limit is stored in km/h
                return new SpeedValue(gameUnitsOverride);
            }
        }

        internal IDictionary<uint, float> GetAllLaneSpeedLimits() {
            IDictionary<uint, float> ret;

            lock(laneSpeedLimitLock_) {
                ret = new Dictionary<uint, float>(laneOverrideSpeedLimit_);
            }

            return ret;
        }

        public void ResetSpeedLimits() {
            lock (laneSpeedLimitLock_) {
                laneOverrideSpeedLimit_.Clear();
                customNetinfoSpeedLimits_.Clear();
                for (ushort segmentId = 1; segmentId < NetManager.instance.m_segments.m_size; ++segmentId) {
                    ref NetSegment netSegment = ref segmentId.ToSegment();
                    if (netSegment.IsValid()) {
                        foreach (var laneIdAndIndex in netSegment.GetSegmentLaneIdsAndLaneIndexes()) {
                            var laneInfo = netSegment.GetLaneInfo(laneIdAndIndex.laneIndex);
                            cachedLaneSpeedLimits_[laneIdAndIndex.laneId] = laneInfo?.m_speedLimit ?? 0;
                        }
                    }
                }
            }
        }

        /// <summary>Called from Debug Panel via IAbstractCustomManager.</summary>
        internal new void PrintDebugInfo() {
            Log.Info("-------------------------");
            Log.Info("--- LANE SPEED LIMITS ---");
            Log.Info("-------------------------");
            for (ushort segmentId = 0; segmentId < NetManager.instance.m_segments.m_size; ++segmentId) {
                ref NetSegment netSegment = ref segmentId.ToSegment();
                NetInfo netinfo = netSegment.Info;
                var lanes = netinfo?.m_lanes;
                if (lanes != null) {
                    uint curLaneId = netSegment.m_lanes;
                    for (int laneIndex = 0; curLaneId != 0 && laneIndex < lanes.Length; ++laneIndex) {
                        ref NetLane curNetLane = ref curLaneId.ToLane();
                        Log.Info(
                            $"lane={curLaneId}, idx={laneIndex}, segment:{segmentId}, valid={curNetLane.IsValidWithSegment()}, " +
                            $"speedLimit={cachedLaneSpeedLimits_[curLaneId]}");
                        curLaneId = curNetLane.m_nextLane;
                    }
                }
            }
        }

        /// <summary>Called by the Lifecycle.</summary>
        public override void OnLevelUnloading() {
            for (uint i = 0; i < cachedLaneSpeedLimits_.Length; ++i) {
                cachedLaneSpeedLimits_[i] = 0;
            }

            lock (laneSpeedLimitLock_) {
                laneOverrideSpeedLimit_.Clear();
            }
        }
    } // end class
}
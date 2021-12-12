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
        private readonly Dictionary<uint, float> laneSpeedLimit_ = new();

        /// <summary>For faster, lock-free access, 1st index: segment id, 2nd index: lane index.</summary>
        private readonly float?[][] laneSpeedLimitArray_;

        public NetInfo.LaneType LaneTypes => LANE_TYPES;

        public VehicleInfo.VehicleType VehicleTypes => VEHICLE_TYPES;

        /// <summary>Ingame speed units, minimal speed.</summary>
        private const float MIN_SPEED = 0.1f; // 5 km/h

        public static readonly SpeedLimitManager Instance = new();

        /// <summary>For each NetInfo name: custom speed limit.</summary>
        private readonly Dictionary<string, float> customLaneSpeedLimit_ = new();

        /// <summary>NetInfo lookup by name.</summary>
        private readonly Dictionary<string, NetInfo> netInfoByName_ = new();

        /// <summary>For each NetInfo name: game default speed limit.</summary>
        private readonly Dictionary<string, float[]> vanillaLaneSpeedLimits_ = new();

        /// <summary>
        /// For each NetInfo name: list of child NetInfo names.
        /// Netinfos are grouped together, roads of same type but different builds (slope, bridge
        /// etc) have different netinfo names, but belong to the same asset.
        /// </summary>
        private readonly Dictionary<string, List<string>> childNetinfoNames_ = new();

        /// <summary>For each <see cref="NetInfo"/> name store its parent name.</summary>
        private readonly Dictionary<string, string> parentNetinfoNames_ = new();

        private List<NetInfo> customizableNetInfos_ = new();

        private SpeedLimitManager() {
            laneSpeedLimitArray_ = new float?[NetManager.MAX_SEGMENT_COUNT][];
        }

        /// <summary>Determines the currently set speed limit for the given segment and lane
        ///     direction in terms of discrete speed limit levels.</summary>
        /// <param name="segmentId">Interested in this segment.</param>
        /// <param name="finalDir">Direction.</param>
        /// <returns>Mean speed limit, average for custom and default lane speeds or null
        ///     if cannot be determined.</returns>
        public SpeedValue? GetCustomSpeedLimit(ushort segmentId, NetInfo.Direction finalDir) {
            // calculate the currently set mean speed limit
            if (segmentId == 0) {
                return null;
            }

            ref NetSegment netSegment = ref segmentId.ToSegment();

            if ((netSegment.m_flags & NetSegment.Flags.Created) == NetSegment.Flags.None) {
                return null;
            }

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            var laneIndex = 0;
            uint validLanes = 0;
            SpeedValue meanSpeedLimit = default;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (d != finalDir) {
                    goto nextIter;
                }

                if (!laneInfo.MayHaveCustomSpeedLimits()) {
                    goto nextIter;
                }

                SpeedValue? setSpeedLimit = this.GetLaneSpeedLimit(curLaneId);

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
        public GetSpeedLimitResult GetCustomSpeedLimit(uint laneId) {
            //----------------------------------------
            // check custom speed limit for the lane
            //----------------------------------------
            SpeedValue? overrideValue = this.GetLaneSpeedLimit(laneId);

            //----------------------------
            // check default speed limit
            //----------------------------
            NetLane[] laneBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;
            ushort segmentId = laneBuffer[laneId].m_segment;
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.MayHaveCustomSpeedLimits()) {
                // Don't have override, and default is not known
                return new GetSpeedLimitResult(
                    overrideValue: null,
                    defaultValue: null);
            }

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                if (curLaneId == laneId) {
                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    SpeedValue knownDefault = new SpeedValue(laneInfo.m_speedLimit);

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
                curLaneId = laneBuffer[curLaneId].m_nextLane;
            }

            Log.Warning($"Speed limit for lane {laneId} could not be determined.");
            return new GetSpeedLimitResult(
                overrideValue: null,
                defaultValue: null);
        }

        /// <summary>Determines the currently set speed limit for the given lane.</summary>
        /// <param name="laneId">The lane id.</param>
        /// <returns>Game units.</returns>
        public float GetGameSpeedLimit(uint laneId) {
            GetSpeedLimitResult overrideSpeedLimit = this.GetCustomSpeedLimit(laneId);
            if (overrideSpeedLimit.DefaultValue != null) {
                SpeedValue activeLimit = overrideSpeedLimit.OverrideValue ?? overrideSpeedLimit.DefaultValue.Value;
                return ToGameSpeedLimit(activeLimit.GameUnits);
            }

            return 0f;
        }

        public float GetLockFreeGameSpeedLimit(ushort segmentId,
                                               byte laneIndex,
                                               uint laneId,
                                               NetInfo.Lane laneInfo) {
            if (!Options.customSpeedLimitsEnabled || !laneInfo.MayHaveCustomSpeedLimits()) {
                return laneInfo.m_speedLimit;
            }

            float speedLimit;
            float?[] fastArray = this.laneSpeedLimitArray_[segmentId];

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
        private static float ToGameSpeedLimit(float customSpeedLimit) {
            return FloatUtil.IsZero(customSpeedLimit)
                       ? SpeedValue.UNLIMITED
                       : customSpeedLimit;
        }

        /// <summary>
        /// Determines the game default speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the game default speed limit should be determined</param>
        /// <returns>The vanilla speed limit, in game units.</returns>
        private float GetVanillaNetInfoSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning("SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info is null!");
                return 0f;
            }

            if (info.m_netAI == null) {
                Log._DebugOnlyWarning("SpeedLimitManager.GetVanillaNetInfoSpeedLimit: info.m_netAI is null!");
                return 0f;
            }

            string infoName = info.name;
            if (!vanillaLaneSpeedLimits_.TryGetValue(
                    infoName,
                    out float[] vanillaSpeedLimits)) {
                return 0f;
            }

            float? maxSpeedLimit = null;

            foreach (float speedLimit in vanillaSpeedLimits) {
                if (maxSpeedLimit == null || speedLimit > maxSpeedLimit) {
                    maxSpeedLimit = speedLimit;
                }
            }

            return maxSpeedLimit ?? 0f;
        }

        /// <summary>
        /// Determines the custom speed limit of the given NetInfo.
        /// </summary>
        /// <param name="info">the NetInfo of which the custom speed limit should be determined</param>
        /// <returns>-1 if no custom speed limit was set</returns>
        public float GetCustomNetinfoSpeedLimit(NetInfo info) {
            if (info == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.GetCustomNetinfoSpeedLimit: info is null!");
                return -1f;
            }

            return !customLaneSpeedLimit_.TryGetValue(info.name, out float speedLimit)
                       ? GetVanillaNetInfoSpeedLimit(info)
                       : speedLimit;
        }

        /// <summary>
        /// Go into parent netinfo names and child netinfo names, and restore the collection of all
        /// netinfo names belonging to the same asset.
        /// </summary>
        /// <param name="netinfoName">The name to begin from.</param>
        /// <param name="result">The resulting name set, also used to avoid infinite loops.</param>
        private void GetRelatedNetinfos(string netinfoName, [NotNull] HashSet<string> result) {
            if (result.Contains(netinfoName)) {
                return; // avoid infinite loops
            }

            result.Add(netinfoName);

            if (this.parentNetinfoNames_.TryGetValue(netinfoName, out string parentNetinfoName)) {
                GetRelatedNetinfos(parentNetinfoName, result);
            }

            if (this.childNetinfoNames_.TryGetValue(netinfoName, out var childNetinfoNames)) {
                foreach (var childName in childNetinfoNames) {
                    GetRelatedNetinfos(childName, result);
                }
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

            HashSet<string> relatedNetinfoNames = new();
            this.GetRelatedNetinfos(netinfo.name, relatedNetinfoNames);

            foreach (var netinfoName in relatedNetinfoNames) {
                customLaneSpeedLimit_[netinfoName] = customSpeedLimit;

#if DEBUGLOAD
                Log._Debug($"Updating parent NetInfo {infoName}: Setting speed limit to {gameSpeedLimit}");
#endif
                // save speed limit in all NetInfos
                if (this.netInfoByName_.TryGetValue(netinfoName, out var relatedNetinfo)) {
                    UpdateNetinfoSpeedLimit(relatedNetinfo, gameSpeedLimit);
                }
            }
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log.NotImpl("InternalPrintDebugInfo for SpeedLimitManager");
        }

        private void UpdateNetinfoSpeedLimit(NetInfo info, float gameSpeedLimit) {
            if (info == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.UpdateNetinfoSpeedLimit: info is null!");
                return;
            }

            if (info.m_lanes == null) {
                Log._DebugOnlyWarning($"SpeedLimitManager.UpdateNetinfoSpeedLimit: info.lanes is null!");
                return;
            }

            Log._Trace($"Updating speed limit of NetInfo {info.name} to {gameSpeedLimit}");

            var mask = this.VehicleTypes;

            foreach (NetInfo.Lane lane in info.m_lanes) {
                if ((lane.m_vehicleType & mask) != VehicleInfo.VehicleType.None) {
                    lane.m_speedLimit = gameSpeedLimit;
                }
            }

            for(ushort segmentId = 1; segmentId < NetManager.MAX_SEGMENT_COUNT; ++segmentId) {
                ref var segment = ref segmentId.ToSegment();

                if (segment.IsValid() && segment.Info == info) {
                    Notifier.Instance.OnSegmentModified(segmentId, this);
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

            Log.Info($"Resetting custom default speed for netinfo '{netinfo.name}'");

            var vanillaSpeedLimit = GetVanillaNetInfoSpeedLimit(netinfo);

            HashSet<string> relatedNetinfoNames = new();
            this.GetRelatedNetinfos(netinfo.name, relatedNetinfoNames);

            foreach (var netinfoName in relatedNetinfoNames) {
                if (this.customLaneSpeedLimit_.ContainsKey(netinfoName)) {
                    this.customLaneSpeedLimit_.Remove(netinfoName);
                }

                if (this.netInfoByName_.TryGetValue(netinfoName, out var relatedNetinfo)) {
                    this.UpdateNetinfoSpeedLimit(relatedNetinfo, vanillaSpeedLimit);
                }
            }
        }

        /// <summary>Sets speed limit for all configurable lanes.</summary>
        /// <param name="action">Speed limit in game units, or null to restore defaults.</param>
        /// <returns><c>true</c> if speed limits were applied to at least one lane.</returns>
        public bool SetSegmentSpeedLimit(ushort segmentId, SetSpeedLimitAction action) {
            bool ret = false;

            foreach (NetInfo.Direction finaldir in Enum.GetValues(typeof(NetInfo.Direction))) {
                ret |= this.SetSegmentSpeedLimit(segmentId, finaldir, action);
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

            //-------------------------
            // For each affected lane
            //-------------------------
            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                NetInfo.Direction d = laneInfo.m_finalDirection;

                if (d == finalDir && laneInfo.MayHaveCustomSpeedLimits()) {
                    if (action.Type == SetSpeedLimitAction.ActionType.ResetToDefault) {
                        // Setting to 'Default' will instead remove the override
                        Log._Debug(
                            $"SpeedLimitManager: Setting speed limit of lane {curLaneId} " +
                            $"to default");
                        RemoveLaneSpeedLimit(curLaneId);
                    } else {
                        bool showMph = GlobalConfig.Instance.Main.DisplaySpeedLimitsMph;
                        string overrideStr = action.GuardedValue.Override.FormatStr(showMph);

                        Log._Debug(
                            $"SpeedLimitManager: Setting lane {curLaneId} to {overrideStr}");
                        SetLaneSpeedLimit(curLaneId, action);
                    }
                }

                curLaneId = curLaneId.ToLane().m_nextLane;
                laneIndex++;
            }

            Notifier.Instance.OnSegmentModified(segmentId, this);
            return true;
        }

        public override void OnBeforeLoadData() {
            base.OnBeforeLoadData();

#if DEBUG
            bool debugSpeedLimits = DebugSwitch.SpeedLimits.Get();
#endif

            // determine vanilla speed limits and customizable NetInfos
            SteamHelper.DLC_BitMask dlcMask =
                SteamHelper.GetOwnedDLCMask().IncludingMissingGameDlcBitmasks();

            int numLoaded = PrefabCollection<NetInfo>.LoadedCount();

            // todo: move this to a Reset() or Clear() method?
            this.vanillaLaneSpeedLimits_.Clear();
            this.customizableNetInfos_.Clear();
            this.customLaneSpeedLimit_.Clear();
            this.childNetinfoNames_.Clear();
            this.parentNetinfoNames_.Clear();
            this.netInfoByName_.Clear();

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

                if (!vanillaLaneSpeedLimits_.ContainsKey(infoName)) {
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

                    netInfoByName_[infoName] = info;
                    mainNetInfos.Add(info);

                    float[] vanillaLaneSpeedLimits = new float[info.m_lanes.Length];

                    for (var k = 0; k < info.m_lanes.Length; ++k) {
                        vanillaLaneSpeedLimits[k] = info.m_lanes[k].m_speedLimit;
                    }

                    vanillaLaneSpeedLimits_[infoName] = vanillaLaneSpeedLimits;
                }
            }

            log.Append("SpeedLimitManager.OnBeforeLoadData: Scan complete.\n");
            Log.Info(log.ToString());

            int CompareNetinfos(NetInfo a, NetInfo b) {
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
                    if ((lane.m_laneType & LANE_TYPES) != NetInfo.LaneType.None) ++bNumVehicleLanes;
                }

                int res = aNumVehicleLanes.CompareTo(bNumVehicleLanes);
                if (res == 0) {
                    return a.name.CompareTo(b.name);
                }

                return res;
            }

            mainNetInfos.Sort(CompareNetinfos);

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
                        Log._Trace($"Identified child NetInfo {infoName} of parent {parentInfo.name}");

                        if (!childNetinfoNames_.TryGetValue(
                                parentInfo.name,
                                out List<string> childNetInfoNames)) {
                            childNetinfoNames_[parentInfo.name] = childNetInfoNames = new();
                        }

                        childNetInfoNames.Add(info.name);
                        parentNetinfoNames_.Add(info.name, parentInfo.name);

                        netInfoByName_[infoName] = info;
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

        protected override void HandleInvalidSegment(ref ExtSegment extSegment) {
            ref NetSegment netSegment = ref extSegment.segmentId.ToSegment();

            NetInfo segmentInfo = netSegment.Info;
            uint curLaneId = netSegment.m_lanes;
            int laneIndex = 0;

            while (laneIndex < segmentInfo.m_lanes.Length && curLaneId != 0u) {
                // NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                // float? setSpeedLimit = Flags.getLaneSpeedLimit(curLaneId);
                SetLaneSpeedLimit(curLaneId, SetSpeedLimitAction.ResetToDefault());

                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
                laneIndex++;
            }
        }

        protected override void HandleValidSegment(ref ExtSegment seg) { }

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
                    float customSpeedLimit = GetCustomNetinfoSpeedLimit(info);
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
                if (!netInfoByName_.TryGetValue(e.Key, out NetInfo netInfo)) {
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

            foreach (KeyValuePair<string, float> e in customLaneSpeedLimit_) {
                try {
                    float gameSpeedLimit = ToGameSpeedLimit(e.Value);

                    result.Add(e.Key, gameSpeedLimit);
                }
                catch (Exception ex) {
                    Log.Error(
                        $"Exception occurred while saving custom default speed limits @ {e.Key}: {ex}");
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

        /// <summary>
        /// Used to check roads if they're a known and valid asset.
        /// This will filter out helper roads which are created during public transport route setup.
        /// </summary>
        // ReSharper restore Unity.ExpensiveCode
        public bool IsKnownNetinfoName(string infoName) {
            return this.vanillaLaneSpeedLimits_.ContainsKey(infoName);
        }

        /// <summary>Private: Do not call from the outside.</summary>
        private void SetLaneSpeedLimit(uint laneId, SetSpeedLimitAction action) {
            if (!Flags.CheckLane(laneId)) {
                return;
            }

            ushort segmentId = Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_segment;
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
                curLaneId = Singleton<NetManager>.instance.m_lanes.m_buffer[curLaneId].m_nextLane;
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

            NetLane[] lanesBuffer = Singleton<NetManager>.instance.m_lanes.m_buffer;

            if (((NetLane.Flags)lanesBuffer[laneId].m_flags &
                 (NetLane.Flags.Created | NetLane.Flags.Deleted)) != NetLane.Flags.Created) {
                return;
            }

            NetInfo segmentInfo = netSegment.Info;
            if (laneIndex >= segmentInfo.m_lanes.Length) {
                return;
            }

            lock(laneSpeedLimitLock_) {
#if DEBUGFLAGS
                Log._Debug(
                    $"Flags.setLaneSpeedLimit: setting speed limit of lane index {laneIndex} @ seg. " +
                    $"{segmentId} to {speedLimit}");
#endif
                switch (action.Type) {
                    case SetSpeedLimitAction.ActionType.ResetToDefault: {
                        laneSpeedLimit_.Remove(laneId);

                        if (laneSpeedLimitArray_[segmentId] == null) {
                            return;
                        }

                        if (laneIndex >= laneSpeedLimitArray_[segmentId].Length) {
                            return;
                        }

                        laneSpeedLimitArray_[segmentId][laneIndex] = null;
                        break;
                    }
                    case SetSpeedLimitAction.ActionType.Unlimited:
                    case SetSpeedLimitAction.ActionType.SetOverride: {
                        float gameUnits = action.GuardedValue.Override.GameUnits;
                        laneSpeedLimit_[laneId] = gameUnits;

                        // save speed limit into the fast-access array.
                        // (1) ensure that the array is defined and large enough
                        //-----------------------------------------------------
                        if (laneSpeedLimitArray_[segmentId] == null) {
                            laneSpeedLimitArray_[segmentId] = new float?[segmentInfo.m_lanes.Length];
                        } else if (laneSpeedLimitArray_[segmentId].Length < segmentInfo.m_lanes.Length) {
                            float?[] oldArray = laneSpeedLimitArray_[segmentId];
                            laneSpeedLimitArray_[segmentId] = new float?[segmentInfo.m_lanes.Length];
                            Array.Copy(sourceArray: oldArray,
                                       destinationArray: laneSpeedLimitArray_[segmentId],
                                       length: oldArray.Length);
                        }

                        // (2) insert the custom speed limit
                        //-----------------------------------------------------
                        laneSpeedLimitArray_[segmentId][laneIndex] = gameUnits;
                        break;
                    }
                }
            }
        }

        public SpeedValue? GetLaneSpeedLimit(uint laneId) {
            lock(laneSpeedLimitLock_) {
                if (laneId <= 0 || !laneSpeedLimit_.TryGetValue(laneId, out float gameUnitsOverride)) {
                    return null;
                }

                // assumption: speed limit is stored in km/h
                return new SpeedValue(gameUnitsOverride);
            }
        }

        internal IDictionary<uint, float> GetAllLaneSpeedLimits() {
            IDictionary<uint, float> ret;

            lock(laneSpeedLimitLock_) {
                ret = new Dictionary<uint, float>(laneSpeedLimit_);
            }

            return ret;
        }

        public void ResetSpeedLimits() {
            lock(laneSpeedLimitLock_) {
                laneSpeedLimit_.Clear();

                uint segmentsCount = Singleton<NetManager>.instance.m_segments.m_size;

                for (int i = 0; i < segmentsCount; ++i) {
                    laneSpeedLimitArray_[i] = null;
                }
            }
        }

        /// <summary>Called from Debug Panel via IAbstractCustomManager.</summary>
        internal new void PrintDebugInfo() {
            Log.Info("-------------------------");
            Log.Info("--- LANE SPEED LIMITS ---");
            Log.Info("-------------------------");
            for (uint i = 0; i < laneSpeedLimitArray_.Length; ++i) {
                if (laneSpeedLimitArray_[i] == null) {
                    continue;
                }

                ref NetSegment netSegment = ref ((ushort)i).ToSegment();

                Log.Info($"Segment {i}: valid? {netSegment.IsValid()}");
                for (int x = 0; x < laneSpeedLimitArray_[i].Length; ++x) {
                    if (laneSpeedLimitArray_[i][x] == null)
                        continue;
                    Log.Info($"\tLane idx {x}: {laneSpeedLimitArray_[i][x]}");
                }
            }
        }

        /// <summary>Called by the Lifecycle.</summary>
        public override void OnLevelUnloading() {
            for (uint i = 0; i < laneSpeedLimitArray_.Length; ++i) {
                laneSpeedLimitArray_[i] = null;
            }

            lock (laneSpeedLimitLock_) {
                laneSpeedLimit_.Clear();
            }
        }
    } // end class
}
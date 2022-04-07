// #define DEBUGGET

namespace TrafficManager.TrafficLight.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Geometry.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Manager.Impl;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Represents the set of custom traffic lights located at a node
    /// </summary>
    public class CustomSegmentLights
        : SegmentEndId
    {
        // private static readonly ExtVehicleType[] SINGLE_LANE_VEHICLETYPES
        // = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service,
        // ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport
        // | ExtVehicleType.Service, ExtVehicleType.RailVehicle };
        public const ExtVehicleType DEFAULT_MAIN_VEHICLETYPE = ExtVehicleType.None;

        [Obsolete]
        protected CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                      ushort nodeId,
                                      ushort segmentId,
                                      bool calculateAutoPedLight)
            : this(
                lightsContainer,
                segmentId,
                nodeId == segmentId.ToSegment().m_startNode,
                calculateAutoPedLight) { }

        public CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                   ushort segmentId,
                                   bool startNode,
                                   bool calculateAutoPedLight)
            : this(
                lightsContainer,
                segmentId,
                startNode,
                calculateAutoPedLight,
                true) { }

        public CustomSegmentLights(ITrafficLightContainer lightsContainer,
                                   ushort segmentId,
                                   bool startNode,
                                   bool calculateAutoPedLight,
                                   bool performHousekeeping)
            : base(segmentId, startNode) {
            this.lightsContainer = lightsContainer;
            if (performHousekeeping) {
                Housekeeping(false, calculateAutoPedLight);
            }
        }

        [Obsolete]
        public ushort NodeId => StartNode
            ? SegmentId.ToSegment().m_startNode
            : SegmentId.ToSegment().m_endNode;

        private uint LastChangeFrame;

        // TODO improve & remove
        public bool InvalidPedestrianLight { get; set; } = false;

        public IDictionary<ExtVehicleType, CustomSegmentLight> CustomLights {
            get;
        } = new Dictionary<ExtVehicleType, CustomSegmentLight>();

        // TODO replace collection
        public LinkedList<ExtVehicleType> VehicleTypes {
            get; private set;
        } = new LinkedList<ExtVehicleType>();

        public ExtVehicleType?[] VehicleTypeByLaneIndex {
            get; private set;
        } = new ExtVehicleType?[0];

        /// <summary>
        /// Vehicles types that have their own traffic light
        /// </summary>
        private ExtVehicleType SeparateVehicleTypes {
            get;
            set;
        } = ExtVehicleType.None;

        // TODO set should be private
        public RoadBaseAI.TrafficLightState AutoPedestrianLightState { get; set; } =
            RoadBaseAI.TrafficLightState.Green;

        public RoadBaseAI.TrafficLightState? PedestrianLightState {
            get {
                if (InvalidPedestrianLight || InternalPedestrianLightState == null) {
                    // no pedestrian crossing at this point
                    return RoadBaseAI.TrafficLightState.Green;
                }

                return ManualPedestrianMode && InternalPedestrianLightState != null
                           ? (RoadBaseAI.TrafficLightState)InternalPedestrianLightState
                           : AutoPedestrianLightState;
            }

            set {
                if (InternalPedestrianLightState == null) {
#if DEBUGHK
                    Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {SegmentId}");
#endif
                    return;
                }

                // Log._Debug($"CustomSegmentLights: Setting pedestrian light at segment {segmentId}");
                InternalPedestrianLightState = value;
            }
        }

        public bool ManualPedestrianMode {
            get => manualPedestrianMode;
            set {
                if (!manualPedestrianMode && value) {
                    PedestrianLightState = AutoPedestrianLightState;
                }

                manualPedestrianMode = value;
            }
        }

        private bool manualPedestrianMode;

        public RoadBaseAI.TrafficLightState? InternalPedestrianLightState { get; private set; }

        private ExtVehicleType mainVehicleType = ExtVehicleType.None;

        protected CustomSegmentLight MainSegmentLight {
            get {
                CustomLights.TryGetValue(mainVehicleType, out CustomSegmentLight res);
                return res;
            }
        }

        public ITrafficLightContainer LightsContainer {
            get => lightsContainer;

            [UsedImplicitly]
            set {
                lightsContainer = value;
                OnChange();
            }
        }

        private ITrafficLightContainer lightsContainer;

        public override string ToString() {
            return string.Format(
                "[CustomSegmentLights {0} @ node {1}\n\tLastChangeFrame: {2}\n\tInvalidPedestrianLight: " +
                "{3}\n\tCustomLights: {4}\n\tVehicleTypes: {5}\n\tVehicleTypeByLaneIndex: {6}\n" +
                "\tSeparateVehicleTypes: {7}\n\tAutoPedestrianLightState: {8}\n\tPedestrianLightState: " +
                "{9}\n\tManualPedestrianMode: {10}\n\tmanualPedestrianMode: {11}\n\t" +
                "InternalPedestrianLightState: {12}\n\tMainSegmentLight: {13}\nCustomSegmentLights]",
                base.ToString(),
                NodeId,
                LastChangeFrame,
                InvalidPedestrianLight,
                CustomLights,
                VehicleTypes.CollectionToString(),
                VehicleTypeByLaneIndex.ArrayToString(),
                SeparateVehicleTypes,
                AutoPedestrianLightState,
                PedestrianLightState,
                ManualPedestrianMode,
                manualPedestrianMode,
                InternalPedestrianLightState,
                MainSegmentLight);
        }

        public bool Relocate(ushort segmentId,
                             bool startNode,
                             ITrafficLightContainer lightsContainer) {
            if (Relocate(segmentId, startNode)) {
                this.lightsContainer = lightsContainer;
                Housekeeping(true, true);
                return true;
            }

            return false;
        }

        public bool IsAnyGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsAnyGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyInTransition() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsAnyInTransition()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyLeftGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsLeftGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyMainGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsMainGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAnyRightGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (v.IsRightGreen()) {
                    return true;
                }
            }

            return false;
        }

        public bool IsAllLeftRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsLeftRed()) {
                    return false;
                }
            }

            return true;
        }

        public bool IsAllMainRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsMainRed()) {
                    return false;
                }
            }

            return true;
        }

        public bool IsAllRightRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                if (!v.IsRightRed()) {
                    return false;
                }
            }

            return true;
        }

        public void UpdateVisuals() {
            MainSegmentLight?.UpdateVisuals();
        }

        public object Clone() {
            return Clone(LightsContainer, true);
        }

        public CustomSegmentLights Clone(ITrafficLightContainer newLightsManager,
                                          bool performHousekeeping = true) {
            var clone = new CustomSegmentLights(
                newLightsManager ?? LightsContainer,
                SegmentId,
                StartNode,
                false,
                false);

            foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in CustomLights) {
                clone.CustomLights.Add(e.Key, (CustomSegmentLight)e.Value.Clone());
            }

            clone.InternalPedestrianLightState = InternalPedestrianLightState;
            clone.manualPedestrianMode = manualPedestrianMode;
            clone.VehicleTypes = new LinkedList<ExtVehicleType>(VehicleTypes);
            clone.LastChangeFrame = LastChangeFrame;
            clone.mainVehicleType = mainVehicleType;
            clone.AutoPedestrianLightState = AutoPedestrianLightState;

            if (performHousekeeping) {
                clone.Housekeeping(false, false);
            }

            return clone;
        }

        public CustomSegmentLight GetCustomLight(byte laneIndex) {
            if (laneIndex >= VehicleTypeByLaneIndex.Length) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type "+
                $"found for lane index");
#endif
                return MainSegmentLight;
            }

            ExtVehicleType? vehicleType = VehicleTypeByLaneIndex[laneIndex];

            if (vehicleType == null) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found "+
                $"for lane index: lane is invalid");
#endif
                return MainSegmentLight;
            }

#if DEBUGGET
            Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Vehicle type is {vehicleType}");
#endif

            if (!CustomLights.TryGetValue((ExtVehicleType)vehicleType, out CustomSegmentLight light)) {
#if DEBUGGET
                Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No custom light "+
                $"found for vehicle type {vehicleType}");
#endif
                return MainSegmentLight;
            }
#if DEBUGGET
            Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light "+
            $"for vehicle type {vehicleType}");
#endif
            return light;
        }

        public CustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
            if (!CustomLights.TryGetValue(vehicleType, out CustomSegmentLight ret)) {
                ret = MainSegmentLight;
            }

            return ret;

            // if (vehicleType != ExtVehicleType.None)
            //  Log._Debug($"No traffic light for vehicle type {vehicleType} defined at
            //     segment {segmentId}, node {nodeId}.");
        }

        public void MakeRed() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.MakeRed();
            }
        }

        public void MakeRedOrGreen() {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.MakeRedOrGreen();
            }
        }

        public void SetLights(RoadBaseAI.TrafficLightState lightState) {
            foreach (CustomSegmentLight v in CustomLights.Values) {
                v.SetStates(lightState, lightState, lightState, false);
            }

            CalculateAutoPedestrianLightState(ref NodeId.ToNode());
        }

        public void SetLights(CustomSegmentLights otherLights) {
            foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in otherLights.CustomLights) {
                if (!CustomLights.TryGetValue(e.Key, out CustomSegmentLight ourLight)) {
                    continue;
                }

                ourLight.SetStates(e.Value.LightMain, e.Value.LightLeft, e.Value.LightRight, false);
                // ourLight.LightPedestrian = e.Value.LightPedestrian;
            }

            InternalPedestrianLightState = otherLights.InternalPedestrianLightState;
            manualPedestrianMode = otherLights.ManualPedestrianMode;
            AutoPedestrianLightState = otherLights.AutoPedestrianLightState;
        }

        public void ChangeLightPedestrian() {
            if (PedestrianLightState == null) {
                return;
            }

            var invertedLight = PedestrianLightState == RoadBaseAI.TrafficLightState.Green
                                    ? RoadBaseAI.TrafficLightState.Red
                                    : RoadBaseAI.TrafficLightState.Green;

            PedestrianLightState = invertedLight;
            UpdateVisuals();
        }

        private static uint GetCurrentFrame() {
            return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
        }

        public uint LastChange() {
            return GetCurrentFrame() - LastChangeFrame;
        }

        public void OnChange(bool calculateAutoPedLight = true) {
            LastChangeFrame = GetCurrentFrame();

            if (calculateAutoPedLight) {
                CalculateAutoPedestrianLightState(ref NodeId.ToNode());
            }
        }

        public void CalculateAutoPedestrianLightState(ref NetNode node, bool propagate = true) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif

            if (logTrafficLights) {
                Log._Debug("CustomSegmentLights.CalculateAutoPedestrianLightState: Calculating " +
                           $"pedestrian light state of seg. {SegmentId} @ node {NodeId}");
            }

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment seg = segMan.ExtSegments[SegmentId];
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)];
            ushort nodeId = segEnd.nodeId;

            if (nodeId != NodeId) {
                Log.Warning("CustomSegmentLights.CalculateAutoPedestrianLightState: Node id " +
                            $"mismatch! segment end node is {nodeId} but we are node {NodeId}. " +
                            $"segEnd={segEnd} this={this}");
                return;
            }

            if (propagate) {
                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = node.GetSegment(i);

                    if (otherSegmentId == 0 || otherSegmentId == SegmentId) {
                        continue;
                    }

                    CustomSegmentLights otherLights = LightsContainer.GetSegmentLights(nodeId, otherSegmentId);
                    if (otherLights == null) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Expected other (propagate) CustomSegmentLights at segment {otherSegmentId} " +
                            $"@ {NodeId} but there was none. Original segment id: {SegmentId}");

                        continue;
                    }

                    otherLights.CalculateAutoPedestrianLightState(ref node, false);
                }
            }

            if (IsAnyGreen()) {
                if (logTrafficLights) {
                    Log._Debug("CustomSegmentLights.CalculateAutoPedestrianLightState: Any green " +
                               $"at seg. {SegmentId} @ {NodeId}");
                }

                AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                return;
            }

            Log._DebugIf(
                logTrafficLights,
                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Querying incoming " +
                $"segments at seg. {SegmentId} @ {NodeId}");

            ItemClass prevConnectionClass = SegmentId.ToSegment().Info.GetConnectionClass();
            var autoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            if (!(segEnd.incoming && seg.oneWay)) {
                for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                    ushort otherSegmentId = node.GetSegment(segmentIndex);

                    if (otherSegmentId == 0 || otherSegmentId == SegmentId) {
                        continue;
                    }

                    ref NetSegment otherSegment = ref otherSegmentId.ToSegment();

                    // ExtSegment otherSeg = segMan.ExtSegments[otherSegmentId];
                    int index0 = segEndMan.GetIndex(
                        otherSegmentId,
                        otherSegment.IsStartNode(NodeId));

                    if (!segEndMan.ExtSegmentEnds[index0].incoming) {
                        continue;
                    }

                    Log._DebugIf(
                        logTrafficLights,
                        () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Checking " +
                        $"incoming straight segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                    CustomSegmentLights otherLights = LightsContainer.GetSegmentLights(nodeId, otherSegmentId);

                    if (otherLights == null) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Expected other (straight) CustomSegmentLights at segment {otherSegmentId} " +
                            $"@ {NodeId} but there was none. Original segment id: {SegmentId}");
                        continue;
                    }

                    ItemClass nextConnectionClass = otherSegment.Info.GetConnectionClass();
                    if (nextConnectionClass.m_service != prevConnectionClass.m_service) {
                        if (logTrafficLights) {
                            Log._DebugFormat(
                                "CustomSegmentLights.CalculateAutoPedestrianLightState: Other (straight) " +
                                "segment {0} @ {1} has different connection service than segment {2} " +
                                "({3} vs. {4}). Ignoring traffic light state.",
                                otherSegmentId,
                                NodeId,
                                SegmentId,
                                nextConnectionClass.m_service,
                                prevConnectionClass.m_service);
                        }

                        continue;
                    }

                    ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);
                    bool lht = Shortcuts.LHT;
                    if (dir == ArrowDirection.Forward) {
                        if (!otherLights.IsAllMainRed()) {
                            Log._DebugIf(
                                logTrafficLights,
                                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Not " +
                                $"all main red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                            autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                            break;
                        }
                    } else if (((dir == ArrowDirection.Left && lht)
                                || (dir == ArrowDirection.Right && !lht))
                               && ((lht && !otherLights.IsAllRightRed())
                                   || (!lht && !otherLights.IsAllLeftRed())))
                    {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "CustomSegmentLights.CalculateAutoPedestrianLightState: " +
                            $"Not all left red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");

                        autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                        break;
                    }
                }
            }

            AutoPedestrianLightState = autoPedestrianLightState;

            Log._DebugIf(
                logTrafficLights,
                () => "CustomSegmentLights.CalculateAutoPedestrianLightState: Calculated " +
                $"AutoPedestrianLightState for segment {SegmentId} @ {NodeId}: {AutoPedestrianLightState}");
        }

        // TODO improve & remove
        public void Housekeeping(bool mayDelete, bool calculateAutoPedLight) {
#if DEBUGHK
            bool logHouseKeeping = DebugSwitch.TimedTrafficLights.Get()
                                   && DebugSettings.NodeId == NodeId;
#else
            const bool logHouseKeeping = false;
#endif

            // we intentionally never delete vehicle types (because we may want to retain traffic
            // light states if a segment is upgraded or replaced)
            CustomSegmentLight mainLight = MainSegmentLight;
            ushort nodeId = NodeId;
            var setupLights = new HashSet<ExtVehicleType>();

            // TODO improve
            IDictionary<byte, ExtVehicleType> allAllowedTypes =
                Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypesAsDict(
                    SegmentId,
                    nodeId,
                    VehicleRestrictionsMode.Restricted);

            ExtVehicleType allAllowedMask =
                Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypes(
                    SegmentId,
                    nodeId,
                    VehicleRestrictionsMode.Restricted);
            SeparateVehicleTypes = ExtVehicleType.None;

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping started @ seg. {2}, " +
                    "node {3}, allAllowedTypes={4}, allAllowedMask={5}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    allAllowedTypes.DictionaryToString(),
                    allAllowedMask);
            }

            // bool addPedestrianLight = false;
            uint separateLanes = 0;
            int defaultLanes = 0;
            NetInfo segmentInfo = SegmentId.ToSegment().Info;
            VehicleTypeByLaneIndex = new ExtVehicleType?[segmentInfo.m_lanes.Length];

            // TODO improve
            var laneIndicesWithoutSeparateLights = new HashSet<byte>(allAllowedTypes.Keys);

            // check if separate traffic lights are required
            bool separateLightsRequired = false;

            foreach (KeyValuePair<byte, ExtVehicleType> e in allAllowedTypes) {
                if (e.Value != allAllowedMask) {
                    separateLightsRequired = true;
                    break;
                }
            }

            // set up vehicle-separated traffic lights
            if (separateLightsRequired) {
                foreach (KeyValuePair<byte, ExtVehicleType> e in allAllowedTypes) {
                    byte laneIndex = e.Key;
                    NetInfo.Lane laneInfo = segmentInfo.m_lanes[laneIndex];
                    ExtVehicleType allowedTypes = e.Value;
                    ExtVehicleType defaultMask =
                        Constants.ManagerFactory.VehicleRestrictionsManager
                                 .GetDefaultAllowedVehicleTypes(
                                     SegmentId,
                                     segmentInfo,
                                     laneIndex,
                                     laneInfo,
                                     VehicleRestrictionsMode.Unrestricted);

                    Log._DebugIf(
                        logHouseKeeping,
                        () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                        $"housekeeping @ seg. {SegmentId}, node {nodeId}: Processing lane {laneIndex} " +
                        $"with allowedTypes={allowedTypes}, defaultMask={defaultMask}");

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.Car && allowedTypes == defaultMask) {
                        Log._DebugIf(
                            logHouseKeeping,
                            () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                            $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                            "Allowed types equal default mask. Ignoring lane.");

                        // no vehicle restrictions applied, generic lights are handled further below
                        ++defaultLanes;
                        continue;
                    }

                    ExtVehicleType mask = allowedTypes & ~ExtVehicleType.Emergency;

                    Log._DebugIf(
                        logHouseKeeping,
                        () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                        $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                        $"Trying to add {mask} light");

                    if (!CustomLights.TryGetValue(mask, out CustomSegmentLight segmentLight)) {
                        // add a new light
                        segmentLight = new CustomSegmentLight(
                            this,
                            RoadBaseAI.TrafficLightState.Red);

                        if (mainLight != null) {
                            segmentLight.CurrentMode = mainLight.CurrentMode;
                            segmentLight.SetStates(
                                mainLight.LightMain,
                                mainLight.LightLeft,
                                mainLight.LightRight,
                                false);
                        }

                        Log._DebugIf(
                            logHouseKeeping,
                            () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                            $"housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: " +
                            $"Light for mask {mask} does not exist. Created new light: {segmentLight} " +
                            $"(mainLight: {mainLight})");

                        CustomLights.Add(mask, segmentLight);
                        VehicleTypes.AddFirst(mask);
                    }

                    mainVehicleType = mask;
                    VehicleTypeByLaneIndex[laneIndex] = mask;
                    laneIndicesWithoutSeparateLights.Remove(laneIndex);
                    ++separateLanes;

                    // addPedestrianLight = true;
                    setupLights.Add(mask);
                    SeparateVehicleTypes |= mask;

                    if (logHouseKeeping) {
                        Log._DebugFormat(
                            "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, " +
                            "node {3}: Finished processing lane {4}: mainVehicleType={5}, " +
                            "VehicleTypeByLaneIndex={6}, laneIndicesWithoutSeparateLights={7}, " +
                            "numLights={8}, SeparateVehicleTypes={9}",
                            mayDelete,
                            calculateAutoPedLight,
                            SegmentId,
                            nodeId,
                            laneIndex,
                            mainVehicleType,
                            VehicleTypeByLaneIndex.ArrayToString(),
                            laneIndicesWithoutSeparateLights.CollectionToString(),
                            separateLanes,
                            SeparateVehicleTypes);
                    }
                }
            }

            if (separateLanes == 0 || defaultLanes > 0) {
                Log._DebugIf(
                    logHouseKeeping,
                    () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                    $"housekeeping @ seg. {SegmentId}, node {nodeId}: Adding default main vehicle " +
                    $"light: {DEFAULT_MAIN_VEHICLETYPE}");

                // generic traffic lights
                if (!CustomLights.TryGetValue(
                        DEFAULT_MAIN_VEHICLETYPE,
                        out CustomSegmentLight defaultSegmentLight)) {
                    defaultSegmentLight = new CustomSegmentLight(
                        this,
                        RoadBaseAI.TrafficLightState.Red);

                    if (mainLight != null) {
                        defaultSegmentLight.CurrentMode = mainLight.CurrentMode;
                        defaultSegmentLight.SetStates(
                            mainLight.LightMain,
                            mainLight.LightLeft,
                            mainLight.LightRight,
                            false);
                    }

                    CustomLights.Add(DEFAULT_MAIN_VEHICLETYPE, defaultSegmentLight);
                    VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
                }

                mainVehicleType = DEFAULT_MAIN_VEHICLETYPE;
                setupLights.Add(DEFAULT_MAIN_VEHICLETYPE);

                foreach (byte laneIndex in laneIndicesWithoutSeparateLights) {
                    VehicleTypeByLaneIndex[laneIndex] = ExtVehicleType.None;
                }

                Log._DebugIf(
                    logHouseKeeping,
                    () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                    $"housekeeping @ seg. {SegmentId}, node {nodeId}: Added default main vehicle " +
                    $"light: {defaultSegmentLight}");

                    // addPedestrianLight = true;
            } else {
                // addPedestrianLight = allAllowedMask == ExtVehicleType.None
                //     || (allAllowedMask & ~ExtVehicleType.RailVehicle) != ExtVehicleType.None;
            }

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, node {3}: " +
                    "Created all necessary lights. VehicleTypeByLaneIndex={4}, CustomLights={5}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    VehicleTypeByLaneIndex.ArrayToString(),
                    CustomLights.DictionaryToString());
            }

            if (mayDelete) {
                // delete traffic lights for non-existing vehicle-separated configurations
                var vehicleTypesToDelete = new HashSet<ExtVehicleType>();

                foreach (ExtVehicleType key in CustomLights.Keys) {
                    // if (e.Key == DEFAULT_MAIN_VEHICLETYPE) {
                    //        continue;
                    // }
                    if (!setupLights.Contains(key)) {
                        vehicleTypesToDelete.Add(key);
                    }
                }

                if (logHouseKeeping) {
                    Log._DebugFormat(
                        "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, " +
                        "node {3}: Going to delete unnecessary lights now: vehicleTypesToDelete={4}",
                        mayDelete,
                        calculateAutoPedLight,
                        SegmentId,
                        nodeId,
                        vehicleTypesToDelete.CollectionToString());
                }

                foreach (ExtVehicleType vehicleType in vehicleTypesToDelete) {
                    CustomLights.Remove(vehicleType);
                    VehicleTypes.Remove(vehicleType);
                }
            }

            if (CustomLights.ContainsKey(DEFAULT_MAIN_VEHICLETYPE)
                && VehicleTypes.First.Value != DEFAULT_MAIN_VEHICLETYPE)
            {
                VehicleTypes.Remove(DEFAULT_MAIN_VEHICLETYPE);
                VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
            }

            // if (addPedestrianLight) {
            Log._DebugIf(
                logHouseKeeping,
                () => $"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): " +
                $"housekeeping @ seg. {SegmentId}, node {nodeId}: adding pedestrian light");

            if (InternalPedestrianLightState == null) {
                InternalPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
            }

            // } else {
            //        InternalPedestrianLightState = null;
            // }
            OnChange(calculateAutoPedLight);

            if (logHouseKeeping) {
                Log._DebugFormat(
                    "CustomSegmentLights.Housekeeping({0}, {1}): housekeeping @ seg. {2}, node {3}: " +
                    "Housekeeping complete. VehicleTypeByLaneIndex={4} CustomLights={5}",
                    mayDelete,
                    calculateAutoPedLight,
                    SegmentId,
                    nodeId,
                    VehicleTypeByLaneIndex.ArrayToString(),
                    CustomLights.DictionaryToString());
            }
        } // end Housekeeping()
    } // end class
}
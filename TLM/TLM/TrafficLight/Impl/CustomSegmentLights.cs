#define DEBUGGETx

namespace TrafficManager.TrafficLight.Impl {
    using System;
    using System.Collections.Generic;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using ColossalFramework;
    using CSUtil.Commons;
    using Geometry.Impl;
    using Manager;
    using State;
    using Traffic.Data;
    using Util;

    /// <summary>
    /// Represents the set of custom traffic lights located at a node
    /// </summary>
    public class CustomSegmentLights : SegmentEndId, ICustomSegmentLights {
        // private static readonly ExtVehicleType[] SINGLE_LANE_VEHICLETYPES
        // = new ExtVehicleType[] { ExtVehicleType.Tram, ExtVehicleType.Service,
        // ExtVehicleType.CargoTruck, ExtVehicleType.RoadPublicTransport
        // | ExtVehicleType.Service, ExtVehicleType.RailVehicle };
        public const ExtVehicleType DEFAULT_MAIN_VEHICLETYPE = ExtVehicleType.None;

        [Obsolete]
        public ushort NodeId {
            get {
                return Constants.ServiceFactory.NetService.GetSegmentNodeId(SegmentId, StartNode);
            }
        }

        public uint LastChangeFrame;

        public bool InvalidPedestrianLight { get; set; } = false; // TODO improve & remove

        public IDictionary<ExtVehicleType, ICustomSegmentLight> CustomLights {
            get; private set;
        } = new TinyDictionary<ExtVehicleType, ICustomSegmentLight>();

        public LinkedList<ExtVehicleType> VehicleTypes { // TODO replace collection
            get; private set;
        } = new LinkedList<ExtVehicleType>();

        public ExtVehicleType?[] VehicleTypeByLaneIndex {
            get; private set;
        } = new ExtVehicleType?[0];

        /// <summary>
        /// Vehicles types that have their own traffic light
        /// </summary>
        public ExtVehicleType SeparateVehicleTypes {
            get; private set;
        } = ExtVehicleType.None;

        public RoadBaseAI.TrafficLightState AutoPedestrianLightState { get; set; } = RoadBaseAI.TrafficLightState.Green; // TODO set should be private

        public RoadBaseAI.TrafficLightState? PedestrianLightState {
            get {
                if (InvalidPedestrianLight || InternalPedestrianLightState == null)
                    return RoadBaseAI.TrafficLightState.Green; // no pedestrian crossing at this point

                if (ManualPedestrianMode && InternalPedestrianLightState != null)
                    return (RoadBaseAI.TrafficLightState)InternalPedestrianLightState;
                else {
                    return AutoPedestrianLightState;
                }
            }
            set {
                if (InternalPedestrianLightState == null) {
#if DEBUGHK
                    Log._Debug($"CustomSegmentLights: Refusing to change pedestrian light at segment {SegmentId}");
#endif
                    return;
                }
                //Log._Debug($"CustomSegmentLights: Setting pedestrian light at segment {segmentId}");
                InternalPedestrianLightState = value;
            }
        }

        public bool ManualPedestrianMode {
            get { return manualPedestrianMode; }
            set {
                if (!manualPedestrianMode && value) {
                    PedestrianLightState = AutoPedestrianLightState;
                }
                manualPedestrianMode = value;
            }
        }

        private bool manualPedestrianMode = false;

        public RoadBaseAI.TrafficLightState? InternalPedestrianLightState { get; private set; } = null;
        private ExtVehicleType mainVehicleType = ExtVehicleType.None;
        protected ICustomSegmentLight MainSegmentLight {
            get {
                ICustomSegmentLight res = null;
                CustomLights.TryGetValue(mainVehicleType, out res);
                return res;
            }
        }

        public ICustomSegmentLightsManager LightsManager {
            get {
                return lightsManager;
            }
            set {
                lightsManager = value;
                OnChange();
            }
        }
        private ICustomSegmentLightsManager lightsManager;

        public override string ToString() {
            return $"[CustomSegmentLights {base.ToString()} @ node {NodeId}\n" +
                   "\t" + $"LastChangeFrame: {LastChangeFrame}\n" +
                   "\t" + $"InvalidPedestrianLight: {InvalidPedestrianLight}\n" +
                   "\t" + $"CustomLights: {CustomLights}\n" +
                   "\t" + $"VehicleTypes: {VehicleTypes.CollectionToString()}\n" +
                   "\t" + $"VehicleTypeByLaneIndex: {VehicleTypeByLaneIndex.ArrayToString()}\n" +
                   "\t" + $"SeparateVehicleTypes: {SeparateVehicleTypes}\n" +
                   "\t" + $"AutoPedestrianLightState: {AutoPedestrianLightState}\n" +
                   "\t" + $"PedestrianLightState: {PedestrianLightState}\n" +
                   "\t" + $"ManualPedestrianMode: {ManualPedestrianMode}\n" +
                   "\t" + $"manualPedestrianMode: {manualPedestrianMode}\n" +
                   "\t" + $"InternalPedestrianLightState: {InternalPedestrianLightState}\n" +
                   "\t" + $"MainSegmentLight: {MainSegmentLight}\n" +
                   "CustomSegmentLights]";
        }

        public bool Relocate(ushort segmentId, bool startNode, ICustomSegmentLightsManager lightsManager) {
            if (Relocate(segmentId, startNode)) {
                this.lightsManager = lightsManager;
                Housekeeping(true, true);
                return true;
            }
            return false;
        }

        [Obsolete]
        protected CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort nodeId, ushort segmentId, bool calculateAutoPedLight)
            : this(lightsManager, segmentId, nodeId == Constants.ServiceFactory.NetService.GetSegmentNodeId(segmentId, true), calculateAutoPedLight) {

        }

        public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight) : this(lightsManager, segmentId, startNode, calculateAutoPedLight, true) {

        }

        public CustomSegmentLights(ICustomSegmentLightsManager lightsManager, ushort segmentId, bool startNode, bool calculateAutoPedLight, bool performHousekeeping) : base(segmentId, startNode) {
            this.lightsManager = lightsManager;
            if (performHousekeeping) {
                Housekeeping(false, calculateAutoPedLight);
            }
        }

        public bool IsAnyGreen() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (e.Value.IsAnyGreen())
                    return true;
            }
            return false;
        }

        public bool IsAnyInTransition() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (e.Value.IsAnyInTransition())
                    return true;
            }
            return false;
        }

        public bool IsAnyLeftGreen() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (e.Value.IsLeftGreen())
                    return true;
            }
            return false;
        }

        public bool IsAnyMainGreen() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (e.Value.IsMainGreen())
                    return true;
            }
            return false;
        }

        public bool IsAnyRightGreen() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (e.Value.IsRightGreen())
                    return true;
            }
            return false;
        }

        public bool IsAllLeftRed() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (!e.Value.IsLeftRed())
                    return false;
            }
            return true;
        }

        public bool IsAllMainRed() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (!e.Value.IsMainRed())
                    return false;
            }
            return true;
        }

        public bool IsAllRightRed() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                if (!e.Value.IsRightRed())
                    return false;
            }
            return true;
        }

        public void UpdateVisuals() {
            if (MainSegmentLight == null)
                return;

            MainSegmentLight.UpdateVisuals();
        }

        public object Clone() {
            return Clone(LightsManager, true);
        }

        public ICustomSegmentLights Clone(ICustomSegmentLightsManager newLightsManager, bool performHousekeeping=true) {
            CustomSegmentLights clone = new CustomSegmentLights(newLightsManager != null ? newLightsManager : LightsManager, SegmentId, StartNode, false, false);
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                clone.CustomLights.Add(e.Key, (ICustomSegmentLight)e.Value.Clone());
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

        public ICustomSegmentLight GetCustomLight(byte laneIndex) {
            if (laneIndex >= VehicleTypeByLaneIndex.Length) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index");
#endif
                return MainSegmentLight;
            }

            ExtVehicleType? vehicleType = VehicleTypeByLaneIndex[laneIndex];

            if (vehicleType == null) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No vehicle type found for lane index: lane is invalid");
#endif
                return MainSegmentLight;
            }

#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Vehicle type is {vehicleType}");
#endif
            ICustomSegmentLight light;
            if (!CustomLights.TryGetValue((ExtVehicleType)vehicleType, out light)) {
#if DEBUGGET
				Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): No custom light found for vehicle type {vehicleType}");
#endif
                return MainSegmentLight;
            }
#if DEBUGGET
			Log._Debug($"CustomSegmentLights.GetCustomLight({laneIndex}): Returning custom light for vehicle type {vehicleType}");
#endif
            return light;
        }

        public ICustomSegmentLight GetCustomLight(ExtVehicleType vehicleType) {
            ICustomSegmentLight ret = null;
            if (!CustomLights.TryGetValue(vehicleType, out ret)) {
                ret = MainSegmentLight;
            }

            return ret;

            /*if (vehicleType != ExtVehicleType.None)
                    Log._Debug($"No traffic light for vehicle type {vehicleType} defined at segment {segmentId}, node {nodeId}.");*/
        }

        public void MakeRed() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                e.Value.MakeRed();
            }
        }

        public void MakeRedOrGreen() {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                e.Value.MakeRedOrGreen();
            }
        }

        public void SetLights(RoadBaseAI.TrafficLightState lightState) {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                e.Value.SetStates(lightState, lightState, lightState, false);
            }

            Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                CalculateAutoPedestrianLightState(ref node);
                return true;
            });
        }

        public void SetLights(ICustomSegmentLights otherLights) {
            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in otherLights.CustomLights) {
                ICustomSegmentLight ourLight = null;
                if (!CustomLights.TryGetValue(e.Key, out ourLight)) {
                    continue;
                }

                ourLight.SetStates(e.Value.LightMain, e.Value.LightLeft, e.Value.LightRight, false);
                //ourLight.LightPedestrian = e.Value.LightPedestrian;
            }
            InternalPedestrianLightState = otherLights.InternalPedestrianLightState;
            manualPedestrianMode = otherLights.ManualPedestrianMode;
            AutoPedestrianLightState = otherLights.AutoPedestrianLightState;
        }

        public void ChangeLightPedestrian() {
            if (PedestrianLightState != null) {
                var invertedLight = PedestrianLightState == RoadBaseAI.TrafficLightState.Green
                                        ? RoadBaseAI.TrafficLightState.Red
                                        : RoadBaseAI.TrafficLightState.Green;

                PedestrianLightState = invertedLight;
                UpdateVisuals();
            }
        }

        private static uint getCurrentFrame() {
            return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
        }

        public uint LastChange() {
            return getCurrentFrame() - LastChangeFrame;
        }

        public void OnChange(bool calculateAutoPedLight=true) {
            LastChangeFrame = getCurrentFrame();

            if (calculateAutoPedLight) {
                Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                    CalculateAutoPedestrianLightState(ref node);
                    return true;
                });
            }
        }

        public void CalculateAutoPedestrianLightState(ref NetNode node, bool propagate=true) {
#if DEBUGTTL
            bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == NodeId;
#endif

#if DEBUGTTL
            if (debug)
                Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Calculating pedestrian light state of seg. {SegmentId} @ node {NodeId}");
#endif

            IExtSegmentManager segMan = Constants.ManagerFactory.ExtSegmentManager;
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ExtSegment seg = segMan.ExtSegments[SegmentId];
            ExtSegmentEnd segEnd = segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)];

            ushort nodeId = segEnd.nodeId;
            if (nodeId != NodeId) {
                Log.Warning($"CustomSegmentLights.CalculateAutoPedestrianLightState: Node id mismatch! segment end node is {nodeId} but we are node {NodeId}. segEnd={segEnd} this={this}");
                return;
            }

            if (propagate) {
                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = node.GetSegment(i);
                    if (otherSegmentId == 0 || otherSegmentId == SegmentId)
                        continue;

                    ICustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
                    if (otherLights == null) {
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Expected other (propagate) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
#endif
                        continue;
                    }

                    otherLights.CalculateAutoPedestrianLightState(ref node, false);
                }
            }

            if (IsAnyGreen()) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Any green at seg. {SegmentId} @ {NodeId}");
#endif
                AutoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                return;
            }

#if DEBUGTTL
            if (debug)
                Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Querying incoming segments at seg. {SegmentId} @ {NodeId}");
#endif

            ItemClass prevConnectionClass = null;
            Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort prevSegId, ref NetSegment segment) {
                prevConnectionClass = segment.Info.GetConnectionClass();
                return true;
            });

            RoadBaseAI.TrafficLightState autoPedestrianLightState = RoadBaseAI.TrafficLightState.Green;
            bool lhd = Constants.ServiceFactory.SimulationService.LeftHandDrive;
            if (!(segEnd.incoming && seg.oneWay)) {
                for (int i = 0; i < 8; ++i) {
                    ushort otherSegmentId = node.GetSegment(i);
                    if (otherSegmentId == 0 || otherSegmentId == SegmentId)
                        continue;

                    //ExtSegment otherSeg = segMan.ExtSegments[otherSegmentId];

                    if (!segEndMan.ExtSegmentEnds[segEndMan.GetIndex(otherSegmentId, (bool)Constants.ServiceFactory.NetService.IsStartNode(otherSegmentId, NodeId))].incoming) {
                        continue;
                    }

#if DEBUGTTL
                    if (debug)
                        Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Checking incoming straight segment {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif

                    ICustomSegmentLights otherLights = LightsManager.GetSegmentLights(nodeId, otherSegmentId);
                    if (otherLights == null) {
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Expected other (straight) CustomSegmentLights at segment {otherSegmentId} @ {NodeId} but there was none. Original segment id: {SegmentId}");
#endif
                        continue;
                    }

                    ItemClass nextConnectionClass = null;
                    Constants.ServiceFactory.NetService.ProcessSegment(otherSegmentId, delegate (ushort otherSegId, ref NetSegment segment) {
                        nextConnectionClass = segment.Info.GetConnectionClass();
                        return true;
                    });

                    if (nextConnectionClass.m_service != prevConnectionClass.m_service) {
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Other (straight) segment {otherSegmentId} @ {NodeId} has different connection service than segment {SegmentId} ({nextConnectionClass.m_service} vs. {prevConnectionClass.m_service}). Ignoring traffic light state.");
#endif
                        continue;
                    }

                    ArrowDirection dir = segEndMan.GetDirection(ref segEnd, otherSegmentId);
                    if (dir == ArrowDirection.Forward) {
                        if (!otherLights.IsAllMainRed()) {
#if DEBUGTTL
                            if (debug)
                                Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Not all main red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif
                            autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                            break;
                        }
                    } else if ((dir == ArrowDirection.Left && lhd) || (dir == ArrowDirection.Right && !lhd)) {
                        if ((lhd && !otherLights.IsAllRightRed()) || (!lhd && !otherLights.IsAllLeftRed())) {
#if DEBUGTTL
                            if (debug)
                                Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Not all left red at {otherSegmentId} at seg. {SegmentId} @ {NodeId}");
#endif
                            autoPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
                            break;
                        }
                    }
                }
            }

            AutoPedestrianLightState = autoPedestrianLightState;
#if DEBUGTTL
            if (debug)
                Log._Debug($"CustomSegmentLights.CalculateAutoPedestrianLightState: Calculated AutoPedestrianLightState for segment {SegmentId} @ {NodeId}: {AutoPedestrianLightState}");
#endif
        }

        // TODO improve & remove
        public void Housekeeping(bool mayDelete, bool calculateAutoPedLight) {
#if DEBUGHK
            bool debug = GlobalConfig.Instance.Debug.Switches[7] && GlobalConfig.Instance.Debug.NodeId == NodeId;
#endif

            // we intentionally never delete vehicle types (because we may want to retain traffic light states if a segment is upgraded or replaced)

            ICustomSegmentLight mainLight = MainSegmentLight;
            ushort nodeId = NodeId;
            HashSet<ExtVehicleType> setupLights = new HashSet<ExtVehicleType>();
            IDictionary<byte, ExtVehicleType> allAllowedTypes = Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypesAsDict(SegmentId, nodeId, VehicleRestrictionsMode.Restricted); // TODO improve
            ExtVehicleType allAllowedMask = Constants.ManagerFactory.VehicleRestrictionsManager.GetAllowedVehicleTypes(SegmentId, nodeId, VehicleRestrictionsMode.Restricted);
            SeparateVehicleTypes = ExtVehicleType.None;
#if DEBUGHK
            if (debug)
                Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping started @ seg. {SegmentId}, node {nodeId}, allAllowedTypes={allAllowedTypes.DictionaryToString()}, allAllowedMask={allAllowedMask}");
#endif
            //bool addPedestrianLight = false;
            uint separateLanes = 0;
            int defaultLanes = 0;
            NetInfo segmentInfo = null;
            Constants.ServiceFactory.NetService.ProcessSegment(SegmentId, delegate (ushort segId, ref NetSegment segment) {
                VehicleTypeByLaneIndex = new ExtVehicleType?[segment.Info.m_lanes.Length];
                segmentInfo = segment.Info;
                return true;
            });
            HashSet<byte> laneIndicesWithoutSeparateLights = new HashSet<byte>(allAllowedTypes.Keys); // TODO improve

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
                    ExtVehicleType defaultMask = Constants.ManagerFactory.VehicleRestrictionsManager.GetDefaultAllowedVehicleTypes(SegmentId, segmentInfo, laneIndex, laneInfo, VehicleRestrictionsMode.Unrestricted);

#if DEBUGHK
                    if (debug)
                        Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Processing lane {laneIndex} with allowedTypes={allowedTypes}, defaultMask={defaultMask}");
#endif

                    if (laneInfo.m_vehicleType == VehicleInfo.VehicleType.Car && allowedTypes == defaultMask) {
#if DEBUGHK
                        if (debug)
                            Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: Allowed types equal default mask. Ignoring lane.");
#endif
                        // no vehicle restrictions applied, generic lights are handled further below
                        ++defaultLanes;
                        continue;
                    }

                    ExtVehicleType mask = allowedTypes & ~ExtVehicleType.Emergency;

#if DEBUGHK
                    if (debug)
                        Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: Trying to add {mask} light");
#endif

                    ICustomSegmentLight segmentLight;
                    if (!CustomLights.TryGetValue(mask, out segmentLight)) {
                        // add a new light
                        segmentLight = new CustomSegmentLight(this, RoadBaseAI.TrafficLightState.Red);
                        if (mainLight != null) {
                            segmentLight.CurrentMode = mainLight.CurrentMode;
                            segmentLight.SetStates(mainLight.LightMain, mainLight.LightLeft, mainLight.LightRight, false);
                        }

#if DEBUGHK
                        if (debug)
                            Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}, lane {laneIndex}: Light for mask {mask} does not exist. Created new light: {segmentLight} (mainLight: {mainLight})");
#endif

                        CustomLights.Add(mask, segmentLight);
                        VehicleTypes.AddFirst(mask);
                    }

                    mainVehicleType = mask;
                    VehicleTypeByLaneIndex[laneIndex] = mask;
                    laneIndicesWithoutSeparateLights.Remove(laneIndex);
                    ++separateLanes;
                    //addPedestrianLight = true;
                    setupLights.Add(mask);
                    SeparateVehicleTypes |= mask;

#if DEBUGHK
                    if (debug)
                        Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Finished processing lane {laneIndex}: mainVehicleType={mainVehicleType}, VehicleTypeByLaneIndex={VehicleTypeByLaneIndex.ArrayToString()}, laneIndicesWithoutSeparateLights={laneIndicesWithoutSeparateLights.CollectionToString()}, numLights={separateLanes}, SeparateVehicleTypes={SeparateVehicleTypes}");
#endif
                }
            }

            if (separateLanes == 0 || defaultLanes > 0) {
#if DEBUGHK
                if (debug)
                    Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Adding default main vehicle light: {DEFAULT_MAIN_VEHICLETYPE}");
#endif

                // generic traffic lights
                ICustomSegmentLight defaultSegmentLight;
                if (!CustomLights.TryGetValue(DEFAULT_MAIN_VEHICLETYPE, out defaultSegmentLight)) {
                    defaultSegmentLight = new CustomSegmentLight(this, RoadBaseAI.TrafficLightState.Red);
                    if (mainLight != null) {
                        defaultSegmentLight.CurrentMode = mainLight.CurrentMode;
                        defaultSegmentLight.SetStates(mainLight.LightMain, mainLight.LightLeft, mainLight.LightRight, false);
                    }
                    CustomLights.Add(DEFAULT_MAIN_VEHICLETYPE, defaultSegmentLight);
                    VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
                }
                mainVehicleType = DEFAULT_MAIN_VEHICLETYPE;
                setupLights.Add(DEFAULT_MAIN_VEHICLETYPE);

                foreach (byte laneIndex in laneIndicesWithoutSeparateLights) {
                    VehicleTypeByLaneIndex[laneIndex] = ExtVehicleType.None;
                }

#if DEBUGHK
                if (debug)
                    Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Added default main vehicle light: {defaultSegmentLight}");
#endif
                //addPedestrianLight = true;
            } else {
                //addPedestrianLight = allAllowedMask == ExtVehicleType.None || (allAllowedMask & ~ExtVehicleType.RailVehicle) != ExtVehicleType.None;
            }

#if DEBUGHK
            if (debug)
                Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Created all necessary lights. VehicleTypeByLaneIndex={VehicleTypeByLaneIndex.ArrayToString()}, CustomLights={CustomLights.DictionaryToString()}");
#endif

            if (mayDelete) {
                // delete traffic lights for non-existing vehicle-separated configurations
                HashSet<ExtVehicleType> vehicleTypesToDelete = new HashSet<ExtVehicleType>();
                foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in CustomLights) {
                    /*if (e.Key == DEFAULT_MAIN_VEHICLETYPE) {
                            continue;
                    }*/
                    if (!setupLights.Contains(e.Key)) {
                        vehicleTypesToDelete.Add(e.Key);
                    }
                }

#if DEBUGHK
                if (debug)
                    Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Going to delete unnecessary lights now: vehicleTypesToDelete={vehicleTypesToDelete.CollectionToString()}");
#endif

                foreach (ExtVehicleType vehicleType in vehicleTypesToDelete) {
                    CustomLights.Remove(vehicleType);
                    VehicleTypes.Remove(vehicleType);
                }
            }

            if (CustomLights.ContainsKey(DEFAULT_MAIN_VEHICLETYPE) && VehicleTypes.First.Value != DEFAULT_MAIN_VEHICLETYPE) {
                VehicleTypes.Remove(DEFAULT_MAIN_VEHICLETYPE);
                VehicleTypes.AddFirst(DEFAULT_MAIN_VEHICLETYPE);
            }

            //if (addPedestrianLight) {
#if DEBUGHK
            if (debug)
                Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: adding pedestrian light");
#endif
            if (InternalPedestrianLightState == null) {
                InternalPedestrianLightState = RoadBaseAI.TrafficLightState.Red;
            }
            /*} else {
                    InternalPedestrianLightState = null;
            }*/

            OnChange(calculateAutoPedLight);
#if DEBUGHK
            if (debug)
                Log._Debug($"CustomSegmentLights.Housekeeping({mayDelete}, {calculateAutoPedLight}): housekeeping @ seg. {SegmentId}, node {nodeId}: Housekeeping complete. VehicleTypeByLaneIndex={VehicleTypeByLaneIndex.ArrayToString()} CustomLights={CustomLights.DictionaryToString()}");
#endif
        }
    }
}
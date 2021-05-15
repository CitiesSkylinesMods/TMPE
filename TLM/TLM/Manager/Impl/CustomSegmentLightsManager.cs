namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.TrafficLight;
    using TrafficManager.TrafficLight.Impl;
    using TrafficManager.TrafficLight;
    using TrafficManager.Util;

    /// <summary>
    /// Manages the states of all custom traffic lights on the map
    /// </summary>
    public class CustomSegmentLightsManager
        : AbstractGeometryObservingManager,
          ICustomSegmentLightsManager
    {
        static CustomSegmentLightsManager() {
            Instance = new CustomSegmentLightsManager();
        }

        public static CustomSegmentLightsManager Instance { get; }

        /// <summary>
        /// custom traffic lights by segment id
        /// </summary>
        private CustomSegment[] customSegments_ = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Custom segments:");
            for (int i = 0; i < customSegments_.Length; ++i) {
                if (customSegments_[i] == null) {
                    continue;
                }

                Log._Debug($"Segment {i}: {customSegments_[i]}");
            }
        }

        /// <summary>
        /// Adds custom traffic lights at the specified node and segment.
        ///     Light states (red, yellow, green) are taken from the "live" state, that is the
        ///     traffic light's light state right before the custom light takes control.
        /// </summary>
        /// <param name="segmentId">SegmentId affected</param>
        /// <param name="startNode">NodeId affected</param>
        private ICustomSegmentLights AddLiveSegmentLights(ushort segmentId, bool startNode) {
            if (!Services.NetService.IsSegmentValid(segmentId)) {
                return null;
            }

            ushort nodeId = Services.NetService.GetSegmentNodeId(segmentId, startNode);
            uint currentFrameIndex = Services.SimulationService.CurrentFrameIndex;

            RoadBaseAI.GetTrafficLightState(
                nodeId,
                ref Singleton<NetManager>.instance.m_segments.m_buffer[segmentId],
                currentFrameIndex - 256u,
                out RoadBaseAI.TrafficLightState vehicleLightState,
                out RoadBaseAI.TrafficLightState _,
                out bool _,
                out bool _);

            return AddSegmentLights(
                segmentId,
                startNode,
                vehicleLightState == RoadBaseAI.TrafficLightState.Green
                    ? RoadBaseAI.TrafficLightState.Green
                    : RoadBaseAI.TrafficLightState.Red);
        }

        /// <summary>
        /// Adds custom traffic lights at the specified node and segment.
        /// Light stats are set to the given light state, or to "Red" if no light state is given.
        /// </summary>
        /// <param name="segmentId">SegmentId affected</param>
        /// <param name="startNode">NodeId affected</param>
        /// <param name="lightState">(optional) light state to set</param>
        private ICustomSegmentLights
            AddSegmentLights(ushort segmentId,
                             bool startNode,
                             RoadBaseAI.TrafficLightState lightState =
                                 RoadBaseAI.TrafficLightState.Red)
        {
#if DEBUG
            Log._Trace($"CustomTrafficLights.AddSegmentLights: Adding segment light: {segmentId} @ startNode={startNode}");
#endif
            if (!Services.NetService.IsSegmentValid(segmentId)) {
                return null;
            }

            CustomSegment customSegment = customSegments_[segmentId];

            if (customSegment == null) {
                customSegment = new CustomSegment();
                customSegments_[segmentId] = customSegment;
            } else {
                ICustomSegmentLights existingLights =
                    startNode ? customSegment.StartNodeLights : customSegment.EndNodeLights;

                if (existingLights != null) {
                    existingLights.SetLights(lightState);
                    return existingLights;
                }
            }

            if (startNode) {
                customSegment.StartNodeLights = new CustomSegmentLights(
                    this,
                    segmentId,
                    startNode,
                    false);

                customSegment.StartNodeLights.SetLights(lightState);
                return customSegment.StartNodeLights;
            } else {
                customSegment.EndNodeLights = new CustomSegmentLights(
                    this,
                    segmentId,
                    startNode,
                    false);

                customSegment.EndNodeLights.SetLights(lightState);
                return customSegment.EndNodeLights;
            }
        }

        public bool SetSegmentLights(ushort nodeId,
                                     ushort segmentId,
                                     ICustomSegmentLights lights) {
            bool? startNode = Services.NetService.IsStartNode(segmentId, nodeId);
            if (startNode == null) {
                return false;
            }

            CustomSegment customSegment = customSegments_[segmentId];
            if (customSegment == null) {
                customSegment = new CustomSegment();
                customSegments_[segmentId] = customSegment;
            }

            if ((bool)startNode) {
                customSegment.StartNodeLights = lights;
            } else {
                customSegment.EndNodeLights = lights;
            }

            return true;
        }

        /// <summary>
        /// Add custom traffic lights at the given node
        /// </summary>
        /// <param name="nodeId">NodeId affected</param>
        public void AddNodeLights(ushort nodeId) {
            if (!Services.NetService.IsNodeValid(nodeId)) {
                return;
            }

            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    AddSegmentLights(segmentId, segmentId.ToSegment().m_startNode == nodeId);
                }
            }
        }

        /// <summary>
        /// Removes custom traffic lights at the given node
        /// </summary>
        /// <param name="nodeId">NodeId affected</param>
        public void RemoveNodeLights(ushort nodeId) {
            ref NetNode node = ref nodeId.ToNode();
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);
                if (segmentId != 0) {
                    RemoveSegmentLight(segmentId, segmentId.ToSegment().m_startNode == nodeId);
                }
            }
        }

        /// <summary>
        /// Removes all custom traffic lights at both ends of the given segment.
        /// </summary>
        /// <param name="segmentId">Segment id affected</param>
        public void RemoveSegmentLights(ushort segmentId) {
            customSegments_[segmentId] = null;
        }

        /// <summary>
        /// Removes the custom traffic light at the given segment end.
        /// </summary>
        /// <param name="segmentId">SegmentId affected</param>
        /// <param name="startNode">The nodeId affected</param>
        public void RemoveSegmentLight(ushort segmentId, bool startNode) {
#if DEBUG
            Log._Trace($"Removing segment light: {segmentId} @ startNode={startNode}");
#endif

            CustomSegment customSegment = customSegments_[segmentId];
            if (customSegment == null) {
                return;
            }

            if (startNode) {
                customSegment.StartNodeLights = null;
            } else {
                customSegment.EndNodeLights = null;
            }

            if (customSegment.StartNodeLights == null && customSegment.EndNodeLights == null) {
                customSegments_[segmentId] = null;
            }
        }

        /// <summary>
        /// Checks if a custom traffic light is present at the given segment end.
        /// </summary>
        /// <param name="segmentId">SegmentId affected</param>
        /// <param name="startNode">NodeId affected</param>
        /// <returns>Whether segment has a light on that end</returns>
        public bool IsSegmentLight(ushort segmentId, bool startNode) {
            CustomSegment customSegment = customSegments_[segmentId];
            if (customSegment == null) {
                return false;
            }

            return (startNode && customSegment.StartNodeLights != null) ||
                   (!startNode && customSegment.EndNodeLights != null);
        }

        /// <summary>
        /// Retrieves the custom traffic light at the given segment end. If none exists, a new
        ///     custom traffic light is created and returned.
        /// </summary>
        /// <param name="segmentId">SegmentId affected</param>
        /// <param name="startNode">NodeId affected</param>
        /// <returns>existing or new custom traffic light at segment end</returns>
        public ICustomSegmentLights GetOrLiveSegmentLights(ushort segmentId, bool startNode) {
            return !IsSegmentLight(segmentId, startNode)
                       ? AddLiveSegmentLights(segmentId, startNode)
                       : GetSegmentLights(segmentId, startNode);
        }

        /// <summary>
        /// Retrieves the custom traffic light at the given segment end.
        /// </summary>
        /// <param name="nodeId">NodeId affected</param>
        /// <param name="segmentId">SegmentId affected</param>
        /// <returns>existing custom traffic light at segment end, <code>null</code> if none exists</returns>
        public ICustomSegmentLights
            GetSegmentLights(ushort segmentId,
                             bool startNode,
                             bool add = true,
                             RoadBaseAI.TrafficLightState lightState = RoadBaseAI.TrafficLightState.Red)
        {
            if (!IsSegmentLight(segmentId, startNode)) {
                return add ? AddSegmentLights(segmentId, startNode, lightState) : null;
            }

            CustomSegment customSegment = customSegments_[segmentId];
            return startNode ? customSegment.StartNodeLights : customSegment.EndNodeLights;
        }

        public void SetLightMode(ushort segmentId,
                                 bool startNode,
                                 ExtVehicleType vehicleType,
                                 LightMode mode) {
            ICustomSegmentLights liveLights = GetSegmentLights(segmentId, startNode);
            if (liveLights == null) {
                Log.Warning(
                    $"CustomSegmentLightsManager.SetLightMode({segmentId}, {startNode}, " +
                    $"{vehicleType}, {mode}): Could not retrieve segment lights.");
                return;
            }

            ICustomSegmentLight liveLight = liveLights.GetCustomLight(vehicleType);
            if (liveLight == null) {
                Log.Error(
                    $"CustomSegmentLightsManager.SetLightMode: Cannot change light mode on seg. " +
                    $"{segmentId} @ {startNode} for vehicle type {vehicleType} to {mode}: Vehicle light not found");
                return;
            }

            liveLight.CurrentMode = mode;
        }

        public bool ApplyLightModes(ushort segmentId,
                                    bool startNode,
                                    ICustomSegmentLights otherLights) {
            ICustomSegmentLights sourceLights = GetSegmentLights(segmentId, startNode);
            if (sourceLights == null) {
                Log.Warning(
                    $"CustomSegmentLightsManager.ApplyLightModes({segmentId}, {startNode}, " +
                    $"{otherLights}): Could not retrieve segment lights.");
                return false;
            }

            foreach (KeyValuePair<ExtVehicleType, ICustomSegmentLight> e in sourceLights.CustomLights) {
                ExtVehicleType vehicleType = e.Key;
                ICustomSegmentLight targetLight = e.Value;

                if (otherLights.CustomLights.TryGetValue(vehicleType, out ICustomSegmentLight sourceLight)) {
                    targetLight.CurrentMode = sourceLight.CurrentMode;
                }
            }

            return true;
        }

        public ICustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
            bool? startNode = Services.NetService.IsStartNode(segmentId, nodeId);
            return startNode == null ? null : GetSegmentLights(segmentId, (bool)startNode, false);
        }

        protected override void HandleInvalidSegment(ref ExtSegment seg) {
            RemoveSegmentLights(seg.segmentId);
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            customSegments_ = new CustomSegment[NetManager.MAX_SEGMENT_COUNT];
        }
    }
}
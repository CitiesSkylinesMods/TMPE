// #define DEBUGVISUALS

namespace TrafficManager.TrafficLight.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;

    /// <summary>
    /// Represents the traffic light (left, forward, right) at a specific segment end
    /// </summary>
    public class CustomSegmentLight
    {
        public CustomSegmentLight(CustomSegmentLights lights,
                                  RoadBaseAI.TrafficLightState mainLight) {
            this.lights_ = lights;

            SetStates(mainLight, LightLeft, LightRight);
            UpdateVisuals();
        }

        [UsedImplicitly]
        public CustomSegmentLight(CustomSegmentLights lights,
                                  RoadBaseAI.TrafficLightState mainLight,
                                  RoadBaseAI.TrafficLightState leftLight,
                                  RoadBaseAI.TrafficLightState rightLight) {
            this.lights_ = lights;

            SetStates(mainLight, leftLight, rightLight);

            UpdateVisuals();
        }

        [Obsolete]
        public ushort NodeId => lights_.NodeId;

        public ushort SegmentId => lights_.SegmentId;

        public bool StartNode => lights_.StartNode;

        public LightMode CurrentMode {
            get => InternalCurrentMode;
            set {
                if (InternalCurrentMode == value) {
                    return;
                }

                InternalCurrentMode = value;
                EnsureModeLights();
            }
        }

        // TODO should be private
        public LightMode InternalCurrentMode { get; set; } = LightMode.Simple;

        public RoadBaseAI.TrafficLightState LightLeft { get; private set; }

        public RoadBaseAI.TrafficLightState LightMain { get; private set; }

        public RoadBaseAI.TrafficLightState LightRight { get; private set; }

        private CustomSegmentLights lights_;

        public override string ToString() {
            return string.Format(
                "[CustomSegmentLight seg. {0} @ node {1}\n\tCurrentMode: {2}\n\tLightLeft: {3}\n" +
                "\tLightMain: {4}\n\tLightRight: {5}\nCustomSegmentLight]",
                SegmentId,
                NodeId,
                CurrentMode,
                LightLeft,
                LightMain,
                LightRight);
        }

        private void EnsureModeLights() {
            bool changed = false;

            switch (InternalCurrentMode) {
                case LightMode.Simple: {
                    if (LightLeft != LightMain) {
                        LightLeft = LightMain;
                        changed = true;
                    }

                    if (LightRight != LightMain) {
                        LightRight = LightMain;
                        changed = true;
                    }

                    break;
                }

                case LightMode.SingleLeft: {
                    if (LightRight != LightMain) {
                        LightRight = LightMain;
                        changed = true;
                    }

                    break;
                }

                case LightMode.SingleRight: {
                    if (LightLeft != LightMain) {
                        LightLeft = LightMain;
                        changed = true;
                    }

                    break;
                }
            }

            if (changed) {
                lights_.OnChange();
            }
        }

        public void ToggleMode() {
            ref NetSegment netSegment = ref SegmentId.ToSegment();

            if (!netSegment.IsValid()) {
                Log.Error($"CustomSegmentLight.ToggleMode: Segment {SegmentId} is invalid.");
                return;
            }

            IExtSegmentEndManager extSegMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ToggleMode(
                ref extSegMan.ExtSegmentEnds[extSegMan.GetIndex(SegmentId, StartNode)],
                ref NodeId.ToNode());
        }

        private void ToggleMode(ref ExtSegmentEnd segEnd, ref NetNode node) {
            IExtSegmentEndManager extSegEndMan = Constants.ManagerFactory.ExtSegmentEndManager;

            extSegEndMan.CalculateOutgoingLeftStraightRightSegments(
                ref segEnd,
                ref node,
                out bool hasLeftSegment,
                out bool hasForwardSegment,
                out bool hasRightSegment);

            Log._Debug(
                $"ChangeMode. segment {SegmentId} @ node {NodeId}, hasLeftSegment={hasLeftSegment}, " +
                $"hasForwardSegment={hasForwardSegment}, hasRightSegment={hasRightSegment}");

            LightMode newMode;

            switch (CurrentMode) {
                case LightMode.Simple when !hasLeftSegment: {
                    newMode = LightMode.SingleRight;
                    break;
                }

                case LightMode.Simple: {
                    newMode = LightMode.SingleLeft;
                    break;
                }

                case LightMode.SingleLeft when !hasForwardSegment || !hasRightSegment: {
                    newMode = LightMode.Simple;
                    break;
                }

                case LightMode.SingleLeft: {
                    newMode = LightMode.SingleRight;
                    break;
                }

                case LightMode.SingleRight when !hasLeftSegment: {
                    newMode = LightMode.Simple;
                    break;
                }

                case LightMode.SingleRight: {
                    newMode = LightMode.All;
                    break;
                }

                default: {
                    newMode = LightMode.Simple;
                    break;
                }
            }

            CurrentMode = newMode;
        }

        public void ChangeMainLight() {
            var invertedLight = LightMain == RoadBaseAI.TrafficLightState.Green
                                    ? RoadBaseAI.TrafficLightState.Red
                                    : RoadBaseAI.TrafficLightState.Green;

            switch (CurrentMode) {
                case LightMode.Simple: {
                    SetStates(invertedLight, invertedLight, invertedLight);
                    break;
                }

                case LightMode.SingleLeft: {
                    SetStates(invertedLight, null, invertedLight);
                    break;
                }

                case LightMode.SingleRight: {
                    SetStates(invertedLight, invertedLight, null);
                    break;
                }

                default: {
                    // LightMain = invertedLight;
                    SetStates(invertedLight, null, null);
                    break;
                }
            }

            UpdateVisuals();
        }

        public void ChangeLeftLight() {
            var invertedLight = LightLeft == RoadBaseAI.TrafficLightState.Green
                                    ? RoadBaseAI.TrafficLightState.Red
                                    : RoadBaseAI.TrafficLightState.Green;

            // LightLeft = invertedLight;
            SetStates(null, invertedLight, null);
            UpdateVisuals();
        }

        public void ChangeRightLight() {
            var invertedLight = LightRight == RoadBaseAI.TrafficLightState.Green
                                    ? RoadBaseAI.TrafficLightState.Red
                                    : RoadBaseAI.TrafficLightState.Green;

            // LightRight = invertedLight;
            SetStates(null, null, invertedLight);
            UpdateVisuals();
        }

        public RoadBaseAI.TrafficLightState GetLightState(ushort toSegmentId) {
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            ArrowDirection dir = segEndMan.GetDirection(
                ref segEndMan.ExtSegmentEnds[segEndMan.GetIndex(SegmentId, StartNode)],
                toSegmentId);
            return GetLightState(dir);
        }

        public RoadBaseAI.TrafficLightState GetLightState(ArrowDirection dir) {
            switch (dir) {
                case ArrowDirection.Left: {
                    return LightLeft;
                }

                case ArrowDirection.Right: {
                    return LightRight;
                }

                case ArrowDirection.Turn: {
                    return Shortcuts.LHT
                        ? LightRight
                        : LightLeft;
                }

                // also: case ArrowDirection.Forward:
                default: {
                    return LightMain;
                }
            }
        }

        public bool IsGreen(ArrowDirection dir) {
            return GetLightState(dir) == RoadBaseAI.TrafficLightState.Green;
        }

        public bool IsInTransition(ArrowDirection dir) {
            RoadBaseAI.TrafficLightState state = GetLightState(dir);
            return state == RoadBaseAI.TrafficLightState.GreenToRed
                   || state == RoadBaseAI.TrafficLightState.RedToGreen;
        }

        public bool IsRed(ArrowDirection dir) {
            return GetLightState(dir) == RoadBaseAI.TrafficLightState.Red;
        }

        public bool IsAnyGreen() {
            return LightMain == RoadBaseAI.TrafficLightState.Green
                   || LightLeft == RoadBaseAI.TrafficLightState.Green
                   || LightRight == RoadBaseAI.TrafficLightState.Green;
        }

        public bool IsAnyInTransition() {
            return LightMain == RoadBaseAI.TrafficLightState.RedToGreen
                   || LightLeft == RoadBaseAI.TrafficLightState.RedToGreen
                   || LightRight == RoadBaseAI.TrafficLightState.RedToGreen
                   || LightMain == RoadBaseAI.TrafficLightState.GreenToRed
                   || LightLeft == RoadBaseAI.TrafficLightState.GreenToRed
                   || LightRight == RoadBaseAI.TrafficLightState.GreenToRed;
        }

        public bool IsLeftGreen() {
            return LightLeft == RoadBaseAI.TrafficLightState.Green;
        }

        public bool IsMainGreen() {
            return LightMain == RoadBaseAI.TrafficLightState.Green;
        }

        public bool IsRightGreen() {
            return LightRight == RoadBaseAI.TrafficLightState.Green;
        }

        public bool IsLeftRed() {
            return LightLeft == RoadBaseAI.TrafficLightState.Red;
        }

        public bool IsMainRed() {
            return LightMain == RoadBaseAI.TrafficLightState.Red;
        }

        public bool IsRightRed() {
            return LightRight == RoadBaseAI.TrafficLightState.Red;
        }

        public void UpdateVisuals() {
            var instance = Singleton<NetManager>.instance;

            ushort nodeId = lights_.NodeId;
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;
            uint simGroup = (uint)nodeId >> 7;

            RoadBaseAI.TrafficLightState mainCopy = LightMain;
            RoadBaseAI.TrafficLightState leftCopy = LightLeft;
            RoadBaseAI.TrafficLightState rightCopy = LightRight;

            switch (CurrentMode) {
                case LightMode.Simple: {
                    LightLeft = LightRight = mainCopy;
                    break;
                }

                case LightMode.SingleLeft: {
                    LightRight = mainCopy;
                    break;
                }

                case LightMode.SingleRight: {
                    LightLeft = mainCopy;
                    break;
                }
            }

            RoadBaseAI.TrafficLightState vehicleLightState = GetVisualLightState();
            RoadBaseAI.TrafficLightState pedestrianLightCopy =
                lights_.PedestrianLightState ?? RoadBaseAI.TrafficLightState.Red;

#if DEBUGVISUALS
            Log._Debug($"Setting visual traffic light state of node {NodeId}, seg. {SegmentId} to "+
            $"vehicleState={vehicleLightState} pedState={pedestrianLightState}");
#endif

            uint now = ((currentFrameIndex - simGroup) >> 8) & 1;

            ref NetSegment netSegment = ref SegmentId.ToSegment();

            TrafficLightSimulationManager.Instance.SetVisualState(
                nodeId,
                ref netSegment,
                now << 8,
                vehicleLightState,
                pedestrianLightCopy,
                false,
                false);

            TrafficLightSimulationManager.Instance.SetVisualState(
                nodeId,
                ref netSegment,
                (1u - now) << 8,
                vehicleLightState,
                pedestrianLightCopy,
                false,
                false);
            bool isPedZoneRoad = netSegment.Info.IsPedestrianZoneRoad();
            bool isRegularRoadEnd = (nodeId.ToNode().flags & NetNode.FlagsLong.RegularRoadEnd) != 0;
            if (isPedZoneRoad != isRegularRoadEnd) {
                RoadBaseAI.GetBollardState(
                    nodeId,
                    ref netSegment,
                    now << 8,
                    out RoadBaseAI.TrafficLightState enterState,
                    out _);

                TrafficLightSimulationManager.SetBollardVisualState(
                    nodeId,
                    ref netSegment,
                    now << 8,
                    enterState,
                    vehicleLightState,
                    false,
                    false,
                    skipEnterUpdate: true);

                RoadBaseAI.GetBollardState(
                    nodeId,
                    ref netSegment,
                    (1u - now) << 8,
                    out RoadBaseAI.TrafficLightState enterState2,
                    out _);
                TrafficLightSimulationManager.SetBollardVisualState(
                    nodeId,
                    ref netSegment,
                    (1u - now) << 8,
                    enterState2,
                    vehicleLightState,
                    false,
                    false,
                    skipEnterUpdate: true);
            }
        }

        public RoadBaseAI.TrafficLightState GetVisualLightState() {
            RoadBaseAI.TrafficLightState vehicleLightState;

            // any green?
            if (LightMain == RoadBaseAI.TrafficLightState.Green
                || LightLeft == RoadBaseAI.TrafficLightState.Green
                || LightRight == RoadBaseAI.TrafficLightState.Green) {
                vehicleLightState = RoadBaseAI.TrafficLightState.Green;
            } else // all red?
            if (LightMain == RoadBaseAI.TrafficLightState.Red
                && LightLeft == RoadBaseAI.TrafficLightState.Red
                && LightRight == RoadBaseAI.TrafficLightState.Red) {
                vehicleLightState = RoadBaseAI.TrafficLightState.Red;
            } else // any red+yellow?
            if (LightMain == RoadBaseAI.TrafficLightState.RedToGreen
                || LightLeft == RoadBaseAI.TrafficLightState.RedToGreen
                || LightRight == RoadBaseAI.TrafficLightState.RedToGreen) {
                vehicleLightState = RoadBaseAI.TrafficLightState.RedToGreen;
            } else {
                vehicleLightState = RoadBaseAI.TrafficLightState.GreenToRed;
            }

            return vehicleLightState;
        }

        [UsedImplicitly]
        private RoadBaseAI.TrafficLightState CheckPedestrianLight() {
            return LightLeft == RoadBaseAI.TrafficLightState.Red
                   && LightMain == RoadBaseAI.TrafficLightState.Red
                   && LightRight == RoadBaseAI.TrafficLightState.Red
                       ? RoadBaseAI.TrafficLightState.Green
                       : RoadBaseAI.TrafficLightState.Red;
        }

        public object Clone() {
            return MemberwiseClone();
        }

        public void MakeRedOrGreen() {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId)
                Log._Debug($"CustomSegmentLight.MakeRedOrGreen: called for segment {SegmentId} @ {NodeId}");
#endif

            RoadBaseAI.TrafficLightState mainState = RoadBaseAI.TrafficLightState.Green;
            RoadBaseAI.TrafficLightState leftState = RoadBaseAI.TrafficLightState.Green;
            RoadBaseAI.TrafficLightState rightState = RoadBaseAI.TrafficLightState.Green;

            if (LightLeft != RoadBaseAI.TrafficLightState.Green) {
                leftState = RoadBaseAI.TrafficLightState.Red;
            }

            if (LightMain != RoadBaseAI.TrafficLightState.Green) {
                mainState = RoadBaseAI.TrafficLightState.Red;
            }

            if (LightRight != RoadBaseAI.TrafficLightState.Green) {
                rightState = RoadBaseAI.TrafficLightState.Red;
            }

            SetStates(mainState, leftState, rightState);
        }

        public void MakeRed() {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId)
                Log._Debug($"CustomSegmentLight.MakeRed: called for segment {SegmentId} @ {NodeId}");
#endif

            SetStates(
                RoadBaseAI.TrafficLightState.Red,
                RoadBaseAI.TrafficLightState.Red,
                RoadBaseAI.TrafficLightState.Red);
        }

        public void SetStates(RoadBaseAI.TrafficLightState? mainLight,
                              RoadBaseAI.TrafficLightState? leftLight,
                              RoadBaseAI.TrafficLightState? rightLight,
                              bool calcAutoPedLight = true)
        {
            if ((mainLight == null || LightMain == mainLight.Value) &&
                (leftLight == null || LightLeft == leftLight.Value) &&
                (rightLight == null || LightRight == rightLight.Value)) {
                return;
            }

            if (mainLight != null) {
                LightMain = mainLight.Value;
            }

            if (leftLight != null) {
                LightLeft = leftLight.Value;
            }

            if (rightLight != null) {
                LightRight = rightLight.Value;
            }

#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId) {
                Log._Debug(
                    $"CustomSegmentLight.SetStates({mainLight}, {leftLight}, {rightLight}, {calcAutoPedLight}) for segment {SegmentId} @ {NodeId}: Main={LightMain} L={LightLeft} R={LightRight}");
            }
#endif

            lights_.OnChange(calcAutoPedLight);
        }
    }
}
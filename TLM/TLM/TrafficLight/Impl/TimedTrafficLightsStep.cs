namespace TrafficManager.TrafficLight.Impl {
    using CSUtil.Commons;
    using ExtVehicleType = global::TrafficManager.API.Traffic.Enums.ExtVehicleType;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Traffic;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.State;
    using TrafficManager.Traffic;
    using TrafficManager.Util;
    using ColossalFramework;
    using TrafficManager.Util.Extensions;

    // TODO class should be completely reworked, approx. in version 1.10
    public class TimedTrafficLightsStep : ITrafficLightContainer
    {
        public TimedTrafficLightsStep(TimedTrafficLights timedNode,
                                      int minTime,
                                      int maxTime,
                                      StepChangeMetric stepChangeMode,
                                      float waitFlowBalance,
                                      bool makeRed = false) {
            MinTime = minTime;
            MaxTime = maxTime;
            ChangeMetric = stepChangeMode;
            WaitFlowBalance = waitFlowBalance;
            this.timedNode = timedNode;

            CurrentFlow = Single.NaN;
            CurrentWait = Single.NaN;

            endTransitionStart = null;
            stepDone = false;

            ref NetNode node = ref timedNode.NodeId.ToNode();

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }

                bool startNode = segmentId.ToSegment().IsStartNode(timedNode.NodeId);

                if (!AddSegment(segmentId, startNode, makeRed)) {
                    Log.Warning(
                        $"TimedTrafficLightsStep.ctor: Failed to add segment {segmentId} " +
                        $"@ start {startNode} to node {timedNode.NodeId}");
                }
            }
        }

        /// <summary>
        /// The number of time units this traffic light remains in the current state at least
        /// </summary>
        public int MinTime { get; set; }

        /// <summary>
        /// The number of time units this traffic light remains in the current state at most
        /// </summary>
        public int MaxTime { get; set; }

        /// <summary>
        /// Indicates if waiting vehicles should be measured
        /// </summary>
        public StepChangeMetric ChangeMetric { get; set; }

        private uint startFrame;

        /// <summary>
        /// Indicates if the step is done (internal use only)
        /// </summary>
        private bool stepDone;

        /// <summary>
        /// Frame when the GreenToRed phase started
        /// </summary>
        private uint? endTransitionStart;

        /// <summary>
        /// minimum mean "number of cars passing through" / "average segment length"
        /// </summary>
        public float CurrentFlow { get; private set; }

        /// <summary>
        ///	maximum mean "number of cars waiting for green" / "average segment length"
        /// </summary>
        public float CurrentWait { get; private set; }

        public int PreviousStepRefIndex { get; set; } = -1;

        public int NextStepRefIndex { get; set; } = -1;

        private uint lastFlowWaitCalc;

        private TimedTrafficLights timedNode;

        public IDictionary<ushort, CustomSegmentLights> CustomSegmentLights { get; }
            = new Dictionary<ushort, CustomSegmentLights>();

        public LinkedList<CustomSegmentLights> InvalidSegmentLights { get; }
            = new LinkedList<CustomSegmentLights>();

        public float WaitFlowBalance { get; set; } = 1f;

        public override string ToString() {
            return string.Format(
                "[TimedTrafficLightsStep\n\tminTime = {0}\n\tmaxTime = {1}\n\tstepChangeMode = {2}\n" +
                "\tstartFrame = {3}\n\tstepDone = {4}\n\tendTransitionStart = {5}\n\tminFlow = {6}\n" +
                "\tmaxWait = {7}\n\tPreviousStepRefIndex = {8}\n\tNextStepRefIndex = {9}\n" +
                "\tlastFlowWaitCalc = {10}\n\tCustomSegmentLights = {11}\n" +
                "\tInvalidSegmentLights = {12}\n\twaitFlowBalance = {13}\nTimedTrafficLightsStep]",
                MinTime,
                MaxTime,
                ChangeMetric,
                startFrame,
                stepDone,
                endTransitionStart,
                CurrentFlow,
                CurrentWait,
                PreviousStepRefIndex,
                NextStepRefIndex,
                lastFlowWaitCalc,
                CustomSegmentLights,
                InvalidSegmentLights.CollectionToString(),
                WaitFlowBalance);
        }

        /// <summary>
        /// Checks if the green-to-red (=yellow) phase is finished
        /// </summary>
        /// <returns></returns>
        public bool IsEndTransitionDone() {
            if (!timedNode.IsMasterNode()) {
                TimedTrafficLights masterLights = timedNode.MasterLights();
                return masterLights.GetStep(masterLights.CurrentStep).IsEndTransitionDone();
            }

            bool ret = endTransitionStart != null && GetCurrentFrame() > endTransitionStart &&
                       stepDone;
            // StepDone(false);
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._DebugFormat(
                    "TimedTrafficLightsStep.isEndTransitionDone() called for master NodeId={0}. " +
                    "CurrentStep={1} getCurrentFrame()={2} endTransitionStart={3} stepDone={4} ret={5}",
                    timedNode.NodeId,
                    timedNode.CurrentStep,
                    GetCurrentFrame(),
                    endTransitionStart,
                    stepDone,
                    ret);
            }
#endif
            return ret;
        }

        /// <summary>
        /// Checks if the green-to-red (=yellow) phase is currently active
        /// </summary>
        /// <returns></returns>
        public bool IsInEndTransition() {
            if (!timedNode.IsMasterNode()) {
                TimedTrafficLights masterLights = timedNode.MasterLights();
                return masterLights.GetStep(masterLights.CurrentStep).IsInEndTransition();
            }

            bool ret = endTransitionStart != null && GetCurrentFrame() <= endTransitionStart &&
                       stepDone;
            // StepDone(false);
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._DebugFormat(
                    "TimedTrafficLightsStep.isInEndTransition() called for master NodeId={0}. " +
                    "CurrentStep={1} getCurrentFrame()={2} endTransitionStart={3} stepDone={4} ret={5}",
                    timedNode.NodeId,
                    timedNode.CurrentStep,
                    GetCurrentFrame(),
                    endTransitionStart,
                    stepDone,
                    ret);
            }
#endif
            return ret;
        }

        public bool IsInStartTransition() {
            if (!timedNode.IsMasterNode()) {
                TimedTrafficLights masterLights = timedNode.MasterLights();
                return masterLights.GetStep(masterLights.CurrentStep).IsInStartTransition();
            }

            bool ret = GetCurrentFrame() == startFrame && !stepDone; //!StepDone(false);
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._DebugFormat(
                    "TimedTrafficLightsStep.isInStartTransition() called for master NodeId={0}. " +
                    "CurrentStep={1} getCurrentFrame()={2} startFrame={3} stepDone={4} ret={5}",
                    timedNode.NodeId,
                    timedNode.CurrentStep,
                    GetCurrentFrame(),
                    startFrame,
                    stepDone,
                    ret);
            }
#endif

            return ret;
        }

        public RoadBaseAI.TrafficLightState GetLightState(ushort segmentId,
                                                          ExtVehicleType vehicleType,
                                                          int lightType) {
            CustomSegmentLight segLight = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);

            if (segLight != null) {
                switch (lightType) {
                    case 0: {
                        return segLight.LightMain;
                    }

                    case 1: {
                        return segLight.LightLeft;
                    }

                    case 2: {
                        return segLight.LightRight;
                    }

                    case 3: {
                        RoadBaseAI.TrafficLightState? pedState = CustomSegmentLights[segmentId].PedestrianLightState;
                        return pedState ?? RoadBaseAI.TrafficLightState.Red;
                    }
                }
            }

            return RoadBaseAI.TrafficLightState.Green;
        }

        /// <summary>
        /// Starts the step.
        /// </summary>
        public void Start(int previousStepRefIndex = -1) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug(
                    $"TimedTrafficLightsStep.Start: Starting step {timedNode.CurrentStep} @ {timedNode.NodeId}");
            }
#endif

            startFrame = GetCurrentFrame();
            Reset();
            PreviousStepRefIndex = previousStepRefIndex;

#if DEBUG
            // if (DebugSwitch.BasicParkingAILog.Get()) {
            //    if (timedNode.NodeId == 31605) {
            //        Log._Debug(
            //            $"===== Step {timedNode.CurrentStep} @ node {timedNode.NodeId} =====");
            //        Log._Debug($"minTime: {minTime} maxTime: {maxTime}");
            //        foreach (KeyValuePair<ushort, CustomSegmentLights> e in segmentLights) {
            //            Log._Debug($"\tSegment {e.Key}:");
            //            Log._Debug($"\t{e.Value.ToString()}");
            //        }
            //    }
            // }
#endif
        }

        internal void Reset() {
            endTransitionStart = null;
            CurrentFlow = float.NaN;
            CurrentWait = float.NaN;
            lastFlowWaitCalc = 0;
            PreviousStepRefIndex = -1;
            NextStepRefIndex = -1;
            stepDone = false;
        }

        internal static uint GetCurrentFrame() {
            return Singleton<SimulationManager>.instance.m_currentFrameIndex >> 6;
        }

        /// <summary>
        /// Updates "real-world" traffic light states according to the timed scripts
        /// </summary>
        public void UpdateLiveLights() {
            UpdateLiveLights(false);
        }

        public void UpdateLiveLights(bool noTransition) {
            try {
                CustomSegmentLightsManager customTrafficLightsManager =
                    CustomSegmentLightsManager.Instance;

                bool atEndTransition =
                    !noTransition && (IsInEndTransition() || IsEndTransitionDone()); // = yellow
                bool atStartTransition =
                    !noTransition && !atEndTransition && IsInStartTransition(); // = red + yellow

#if DEBUG
                bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get() &&
                                        DebugSettings.NodeId == timedNode.NodeId;

                if (timedNode == null) {
                    Log.Error("TimedTrafficLightsStep: timedNode is null!");
                    return;
                }
#else
                const bool logTrafficLights = false;
#endif

                if (PreviousStepRefIndex >= timedNode.NumSteps()) {
                    PreviousStepRefIndex = -1;
                }

                if (NextStepRefIndex >= timedNode.NumSteps()) {
                    NextStepRefIndex = -1;
                }

                TimedTrafficLightsStep previousStep = timedNode.GetStep(
                    PreviousStepRefIndex >= 0
                        ? PreviousStepRefIndex
                        : ((timedNode.CurrentStep + timedNode.NumSteps() - 1) %
                           timedNode.NumSteps()));
                TimedTrafficLightsStep nextStep = timedNode.GetStep(
                    NextStepRefIndex >= 0
                        ? NextStepRefIndex
                        : ((timedNode.CurrentStep + 1) % timedNode.NumSteps()));

#if DEBUG
                if (logTrafficLights) {
                    if (previousStep == null) {
                        Log.Error("TimedTrafficLightsStep: previousStep is null!");
                        // return;
                    }

                    if (nextStep == null) {
                        Log.Error("TimedTrafficLightsStep: nextStep is null!");
                        // return;
                    }

                    if (previousStep.CustomSegmentLights == null) {
                        Log.Error("TimedTrafficLightsStep: previousStep.segmentLights is null!");
                        // return;
                    }

                    if (nextStep.CustomSegmentLights == null) {
                        Log.Error("TimedTrafficLightsStep: nextStep.segmentLights is null!");
                        // return;
                    }

                    if (CustomSegmentLights == null) {
                        Log.Error("TimedTrafficLightsStep: segmentLights is null!");
                        // return;
                    }
                }
#endif

#if DEBUG
                // Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition}) called for
                //     NodeId={timedNode.NodeId}. atStartTransition={atStartTransition}
                //     atEndTransition={atEndTransition}");
#endif

                foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights) {
                    ushort segmentId = e.Key;
                    CustomSegmentLights curStepSegmentLights = e.Value;

#if DEBUG
                    // Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})   ->
                    //     segmentId={segmentId} @ NodeId={timedNode.NodeId}");
#endif

                    if (!previousStep.CustomSegmentLights.TryGetValue(
                            segmentId,
                            out CustomSegmentLights prevStepSegmentLights))
                    {
                        if (logTrafficLights) {
                            Log.Warning("TimedTrafficLightsStep: previousStep does not contain " +
                                        $"lights for segment {segmentId}!");
                        }

                        continue;
                    }

                    if (!nextStep.CustomSegmentLights.TryGetValue(
                            segmentId,
                            out CustomSegmentLights nextStepSegmentLights)) {
                        if (logTrafficLights) {
                            Log.Warning("TimedTrafficLightsStep: nextStep does not contain lights for " +
                                    $"segment {segmentId}!");
                        }
                        continue;
                    }

                    // segLightState.makeRedOrGreen(); // TODO temporary fix

                    CustomSegmentLights liveSegmentLights = customTrafficLightsManager.GetSegmentLights(
                            segmentId,
                            curStepSegmentLights.StartNode,
                            false);

                    if (liveSegmentLights == null) {
                        Log.Warning(
                            $"TimedTrafficLightsStep.UpdateLights() @ node {timedNode.NodeId}: " +
                            $"Could not retrieve live segment lights for segment {segmentId} " +
                            $"@ start {curStepSegmentLights.StartNode}.");
                        continue;
                    }

                    RoadBaseAI.TrafficLightState pedLightState = calcLightState(
                        (RoadBaseAI.TrafficLightState)prevStepSegmentLights.PedestrianLightState,
                        (RoadBaseAI.TrafficLightState)curStepSegmentLights.PedestrianLightState,
                        (RoadBaseAI.TrafficLightState)nextStepSegmentLights.PedestrianLightState,
                        atStartTransition,
                        atEndTransition);

                    // Log._Debug($"TimedStep.SetLights: Setting pedestrian light state @ seg.
                    //      {segmentId} to {pedLightState} {curStepSegmentLights.ManualPedestrianMode}");
                    liveSegmentLights.ManualPedestrianMode =
                        curStepSegmentLights.ManualPedestrianMode;
                    liveSegmentLights.PedestrianLightState =
                        liveSegmentLights.AutoPedestrianLightState = pedLightState;

                    // Log.Warning($"Step @ {timedNode.NodeId}: Segment {segmentId}: Ped.:
                    //     {liveSegmentLights.PedestrianLightState.ToString()} /
                    //     {liveSegmentLights.AutoPedestrianLightState.ToString()}");

                    if (logTrafficLights && curStepSegmentLights.VehicleTypes == null) {
                        Log.Error("TimedTrafficLightsStep: curStepSegmentLights.VehicleTypes is null!");
                        return;
                    }

                    foreach (ExtVehicleType vehicleType in curStepSegmentLights.VehicleTypes) {
                        // Log._Debug($"TimedTrafficLightsStep.SetLights({noTransition})     ->
                        //    segmentId={segmentId} @ NodeId={timedNode.NodeId} for vehicle {vehicleType}");
                        CustomSegmentLight liveSegmentLight = liveSegmentLights.GetCustomLight(vehicleType);

                        if (liveSegmentLight == null) {
                            Log._DebugIf(
                                logTrafficLights,
                                () => $"Timed step @ seg. {segmentId}, node {timedNode.NodeId} has " +
                                $"a traffic light for {vehicleType} but the live segment does not have one.");
                            continue;
                        }

                        CustomSegmentLight curStepSegmentLight = curStepSegmentLights.GetCustomLight(vehicleType);
                        CustomSegmentLight prevStepSegmentLight = prevStepSegmentLights.GetCustomLight(vehicleType);
                        CustomSegmentLight nextStepSegmentLight = nextStepSegmentLights.GetCustomLight(vehicleType);

#if DEBUG
                        if (logTrafficLights) {
                            if (curStepSegmentLight == null) {
                                Log.Error("TimedTrafficLightsStep: curStepSegmentLight is null!");
                                //return;
                            }

                            if (prevStepSegmentLight == null) {
                                Log.Error("TimedTrafficLightsStep: prevStepSegmentLight is null!");
                                //return;
                            }

                            if (nextStepSegmentLight == null) {
                                Log.Error("TimedTrafficLightsStep: nextStepSegmentLight is null!");
                                //return;
                            }
                        }
#endif

                        // TODO improve & remove
                        liveSegmentLight.InternalCurrentMode = curStepSegmentLight.CurrentMode;

                        // curStepSegmentLight.EnsureModeLights();
                        // prevStepSegmentLight.EnsureModeLights();
                        // nextStepSegmentLight.EnsureModeLights();

                        RoadBaseAI.TrafficLightState mainLight = calcLightState(
                            prevStepSegmentLight.LightMain,
                            curStepSegmentLight.LightMain,
                            nextStepSegmentLight.LightMain,
                            atStartTransition,
                            atEndTransition);

                        RoadBaseAI.TrafficLightState leftLight = calcLightState(
                            prevStepSegmentLight.LightLeft,
                            curStepSegmentLight.LightLeft,
                            nextStepSegmentLight.LightLeft,
                            atStartTransition,
                            atEndTransition);

                        RoadBaseAI.TrafficLightState rightLight = calcLightState(
                            prevStepSegmentLight.LightRight,
                            curStepSegmentLight.LightRight,
                            nextStepSegmentLight.LightRight,
                            atStartTransition,
                            atEndTransition);

                        liveSegmentLight.SetStates(mainLight, leftLight, rightLight, false);

#if DEBUG
                        if (logTrafficLights) {
                            Log._DebugFormat(
                                "TimedTrafficLightsStep.SetLights({0})     -> *SETTING* LightLeft={1} " +
                                "LightMain={2} LightRight={3} for segmentId={4} @ NodeId={5} for vehicle {6}",
                                noTransition,
                                liveSegmentLight.LightLeft,
                                liveSegmentLight.LightMain,
                                liveSegmentLight.LightRight,
                                segmentId,
                                timedNode.NodeId,
                                vehicleType);
                        }
#endif

                        // Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId} for vehicle
                        //     type {vehicleType}: L: {liveSegmentLight.LightLeft} F:
                        //     {liveSegmentLight.LightMain} R: {liveSegmentLight.LightRight}");
                    }

                    // if (timedNode.NodeId == 20164) {
                    //     Log._Debug($"Step @ {timedNode.NodeId}: Segment {segmentId}:
                    //     {segmentLight.LightLeft} {segmentLight.LightMain)} {segmentLight.LightRight}
                    //     {segmentLight.LightPedestrian}");
                    // }

                    liveSegmentLights.UpdateVisuals();
                }
            }
            catch (Exception e) {
                Log.Error($"Exception in TimedTrafficStep.UpdateLiveLights for node {timedNode.NodeId}: {e}");
                // invalid = true;
            }
        }

        private RoadBaseAI.TrafficLightState calcLightState(
            RoadBaseAI.TrafficLightState previousState,
            RoadBaseAI.TrafficLightState currentState,
            RoadBaseAI.TrafficLightState nextState,
            bool atStartTransition,
            bool atEndTransition)
        {
            if (atStartTransition && currentState == RoadBaseAI.TrafficLightState.Green
                                  && previousState == RoadBaseAI.TrafficLightState.Red) {
                return RoadBaseAI.TrafficLightState.RedToGreen;
            }

            if (atEndTransition && currentState == RoadBaseAI.TrafficLightState.Green
                                && nextState == RoadBaseAI.TrafficLightState.Red) {
                return RoadBaseAI.TrafficLightState.GreenToRed;
            }

            return currentState;
        }

        /// <summary>
        /// Updates timed segment lights according to "real-world" traffic light states
        /// </summary>
        public void UpdateLights() {
            Log._Debug("TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic " +
                       $"light step @ {timedNode.NodeId}");

            foreach (KeyValuePair<ushort, CustomSegmentLights> e in CustomSegmentLights) {
                ushort segmentId = e.Key;
                CustomSegmentLights segLights = e.Value;

                Log._Debug("TimedTrafficLightsStep.UpdateLights: Updating lights of timed traffic " +
                           $"light step at seg. {e.Key} @ {timedNode.NodeId}");

                // if (segment == 0) continue;

                CustomSegmentLights liveSegLights =
                    CustomSegmentLightsManager.Instance.GetSegmentLights(
                        segmentId,
                        segLights.StartNode,
                        false);

                if (liveSegLights == null) {
                    Log.Warning($"TimedTrafficLightsStep.UpdateLights() @ node {timedNode.NodeId}: " +
                                $"Could not retrieve live segment lights for segment {segmentId} " +
                                $"@ start {segLights.StartNode}.");
                    continue;
                }

                segLights.SetLights(liveSegLights);
                Log._Debug(
                    $"TimedTrafficLightsStep.UpdateLights: Segment {segmentId} " +
                    $"AutoPedState={segLights.AutoPedestrianLightState} live={liveSegLights.AutoPedestrianLightState}");
            }
        }

        /// <summary>
        /// Countdown value for min. time
        /// </summary>
        /// <returns></returns>
        public long MinTimeRemaining() {
            return Math.Max(0, startFrame + MinTime - GetCurrentFrame());
        }

        /// <summary>
        /// Countdown value for max. time
        /// </summary>
        /// <returns></returns>
        public long MaxTimeRemaining() {
            return Math.Max(0, startFrame + MaxTime - GetCurrentFrame());
        }

        public void SetStepDone() {
            stepDone = true;
        }

        public bool StepDone(bool updateValues) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == timedNode.NodeId;
#else
            const bool logTrafficLights = false;
#endif
            Log._DebugIf(
                logTrafficLights,
                () => $"StepDone: called for node {timedNode.NodeId} @ step {timedNode.CurrentStep}");

            if (!timedNode.IsMasterNode()) {
                TimedTrafficLights masterLights = timedNode.MasterLights();
                return masterLights.GetStep(masterLights.CurrentStep).StepDone(updateValues);
            }

            // we are the master node
            if (timedNode.IsInTestMode()) {
                return false;
            }

            if (stepDone) {
                return true;
            }

            if (GetCurrentFrame() >= startFrame + MaxTime) {
                // maximum time reached. switch!
                Log._DebugIf(
                    logTrafficLights,
                    () => $"StepDone: step finished @ {timedNode.NodeId}");

                if (!stepDone && updateValues) {
                    stepDone = true;
                    endTransitionStart = GetCurrentFrame();
                }

                return stepDone;
            }

            if (GetCurrentFrame() < startFrame + MinTime) {
                return false;
            }

            float wait, flow;
            uint curFrame = GetCurrentFrame();

            // Log._Debug($"TTL @ {timedNode.NodeId}: curFrame={curFrame} lastFlowWaitCalc={lastFlowWaitCalc}");
            if (lastFlowWaitCalc < curFrame) {
                // Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc<curFrame");
                CalcWaitFlow(true, timedNode.CurrentStep, out wait, out flow);

                if (updateValues) {
                    lastFlowWaitCalc = curFrame;
                    // Log._Debug($"TTL @ {timedNode.NodeId}: updated lastFlowWaitCalc=curFrame={curFrame}");
                }
            } else {
                flow = CurrentFlow;
                wait = CurrentWait;
                // Log._Debug($"TTL @ {timedNode.NodeId}: lastFlowWaitCalc>=curFrame wait={maxWait} flow={minFlow}");
            }

            float newFlow = CurrentFlow;
            float newWait = CurrentWait;

            if (ChangeMetric != StepChangeMetric.Default || Single.IsNaN(newFlow)) {
                newFlow = flow;
            } else {
                newFlow = GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor * newFlow +
                          (1f - GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor) *
                          flow; // some smoothing
            }

            if (Single.IsNaN(newWait) && ChangeMetric != StepChangeMetric.NoWait) {
                newWait = 0;
            } else if (ChangeMetric != StepChangeMetric.Default) {
                newWait = wait;
            } else {
                newWait = GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor * newWait +
                          (1f - GlobalConfig.Instance.TimedTrafficLights.SmoothingFactor) *
                          wait; // some smoothing
            }

            // if more cars are waiting than flowing, we change the step
            bool done = ShouldGoToNextStep(newFlow, newWait, out float _);
            // newWait > 0 && newFlow < newWait;

            // Log._Debug($"TTL @ {timedNode.NodeId}: newWait={newWait} newFlow={newFlow}
            //     updateValues={updateValues} stepDone={stepDone} done={done}");
            if (updateValues) {
                CurrentFlow = newFlow;
                CurrentWait = newWait;
                // Log._Debug($"TTL @ {timedNode.NodeId}: updated minFlow=newFlow={minFlow}
                //     maxWait=newWait={maxWait}");
            }
#if DEBUG
            // Log.Message("step finished (2) @ " + nodeId);
#endif
            if (updateValues && !stepDone && done) {
                stepDone = done;
                endTransitionStart = GetCurrentFrame();
            }

            return done;
        }

        public float GetMetric(float flow, float wait) {
            switch (ChangeMetric) {
                case StepChangeMetric.FirstFlow: {
                    return flow <= 0 ? 1f : 0f;
                }

                case StepChangeMetric.FirstWait: {
                    return wait <= 0 ? 1f : 0f;
                }

                case StepChangeMetric.NoFlow: {
                    return flow > 0 ? 1f : 0f;
                }

                case StepChangeMetric.NoWait: {
                    return wait > 0 ? 1f : 0f;
                }

                // also: case StepChangeMetric.Default:
                default: {
                    return flow - wait;
                }
            }
        }

        public bool ShouldGoToNextStep(float flow, float wait, out float metric) {
            metric = GetMetric(flow, wait);
            return ChangeMetric == StepChangeMetric.Default
                       ? metric < 0
                       : Math.Abs(metric) < FloatUtil.VERY_SMALL_FLOAT;
        }

        /// <summary>
        /// Calculates the current metrics for flowing and waiting vehicles
        /// </summary>
        /// <param name="wait"></param>
        /// <param name="flow"></param>
        public void CalcWaitFlow(bool countOnlyMovingIfGreen,
                                 int stepRefIndex,
                                 out float wait,
                                 out float flow) {
            uint numFlows = 0;
            uint numWaits = 0;
            float curTotalFlow = 0;
            float curTotalWait = 0;

#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == timedNode.NodeId;
#else
            const bool logTrafficLights = false;
#endif
            if (logTrafficLights) {
                Log.Warning($"calcWaitFlow: called for node {timedNode.NodeId} @ step {stepRefIndex}");
            }

            // TODO checking against getCurrentFrame() is only valid if this is the current step
            // during start phase all vehicles on "green" segments are counted as flowing
            if (countOnlyMovingIfGreen
                && GetCurrentFrame() <= startFrame + MinTime + 1)
            {
                countOnlyMovingIfGreen = false;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            ISegmentEndManager endMan = Constants.ManagerFactory.SegmentEndManager;
            IVehicleRestrictionsManager restrMan = Constants.ManagerFactory.VehicleRestrictionsManager;

            // loop over all timed traffic lights within the node group
            foreach (ushort timedNodeId in timedNode.NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[timedNodeId].timedLight;
                TimedTrafficLightsStep slaveStep = slaveTTL.GetStep(stepRefIndex);

                // minimum time reached. check traffic! loop over source segments
                uint numNodeFlows = 0;
                uint numNodeWaits = 0;
                float curTotalNodeFlow = 0;
                float curTotalNodeWait = 0;

                foreach (KeyValuePair<ushort, CustomSegmentLights> e in slaveStep.CustomSegmentLights) {
                    ushort sourceSegmentId = e.Key;
                    CustomSegmentLights segLights = e.Value;

                    if (!slaveTTL.Directions.TryGetValue(
                            sourceSegmentId,
                            out IDictionary<ushort, ArrowDirection> directions))
                    {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "calcWaitFlow: No arrow directions defined for segment " +
                            $"{sourceSegmentId} @ {timedNodeId}");
                        continue;
                    }

                    // one of the traffic lights at this segment is green: count minimum traffic flowing through
                    ISegmentEnd sourceSegmentEnd = endMan.GetSegmentEnd(sourceSegmentId, segLights.StartNode);

                    if (sourceSegmentEnd == null) {
                        Log.Error("TimedTrafficLightsStep.calcWaitFlow: No segment end @ seg. " +
                                  $"{sourceSegmentId} found!");
                        continue; // skip invalid segment
                    }

                    IDictionary<ushort, uint>[] movingVehiclesMetrics = null;
                    bool countOnlyMovingIfGreenOnSegment = false;

                    if (ChangeMetric == StepChangeMetric.Default)
                    {
                        countOnlyMovingIfGreenOnSegment = countOnlyMovingIfGreen;

                        if (countOnlyMovingIfGreenOnSegment) {
                            if (restrMan.IsRailSegment(sourceSegmentId.ToSegment().Info)) {
                                countOnlyMovingIfGreenOnSegment = false;
                            }
                        }

                        movingVehiclesMetrics =
                            countOnlyMovingIfGreenOnSegment
                                ? sourceSegmentEnd.MeasureOutgoingVehicles(false, logTrafficLights)
                                : null;
                    }

                    IDictionary<ushort, uint>[] allVehiclesMetrics
                        = sourceSegmentEnd.MeasureOutgoingVehicles(true, logTrafficLights);
                    ExtVehicleType?[] vehTypeByLaneIndex = segLights.VehicleTypeByLaneIndex;

                    if (logTrafficLights) {
                        Log._DebugFormat(
                            "calcWaitFlow: Seg. {0} @ {1}, vehTypeByLaneIndex={2}",
                            sourceSegmentId,
                            timedNodeId,
                            string.Join(
                                ", ",
                                vehTypeByLaneIndex.Select(x => x == null ? "null" : x.ToString())
                                    .ToArray()));
                    }

                    uint numSegFlows = 0;
                    uint numSegWaits = 0;
                    float curTotalSegFlow = 0;
                    float curTotalSegWait = 0;

                    // loop over source lanes
                    for (byte laneIndex = 0; laneIndex < vehTypeByLaneIndex.Length; ++laneIndex) {
                        ExtVehicleType? vehicleType = vehTypeByLaneIndex[laneIndex];
                        if (vehicleType == null) {
                            continue;
                        }

                        CustomSegmentLight segLight = segLights.GetCustomLight(laneIndex);
                        if (segLight == null) {
                            Log._DebugOnlyWarningIf(
                                logTrafficLights,
                                () => "Timed traffic light step: Failed to get custom light for vehicleType " +
                                $"{vehicleType} @ seg. {sourceSegmentId}, node {timedNode.NodeId}!");

                            continue;
                        }

                        IDictionary<ushort, uint> movingVehiclesMetric
                            = countOnlyMovingIfGreenOnSegment ? movingVehiclesMetrics[laneIndex] : null;
                        IDictionary<ushort, uint> allVehiclesMetric = allVehiclesMetrics[laneIndex];

                        if (allVehiclesMetrics == null) {
                            Log._DebugIf(
                                logTrafficLights,
                                () => "TimedTrafficLightsStep.calcWaitFlow: No cars on lane " +
                                $"{laneIndex} @ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");
                            continue;
                        }

                        Log._DebugIf(
                            logTrafficLights,
                            () => $"TimedTrafficLightsStep.calcWaitFlow: Checking lane {laneIndex} " +
                            $"@ seg. {sourceSegmentId}. Vehicle types: {vehicleType}");

                        // loop over target segment: calculate waiting/moving traffic
                        uint numLaneFlows = 0;
                        uint numLaneWaits = 0;
                        uint curTotalLaneFlow = 0;
                        uint curTotalLaneWait = 0;

                        foreach (KeyValuePair<ushort, uint> f in allVehiclesMetric) {
                            ushort targetSegmentId = f.Key;
                            uint numVehicles = f.Value;

                            if (!directions.TryGetValue(targetSegmentId, out ArrowDirection dir)) {
                                Log._Debug("TimedTrafficLightsStep.calcWaitFlow: Direction undefined " +
                                           $"for target segment {targetSegmentId} @ {timedNodeId}");
                                continue;
                            }

                            uint numMovingVehicles =
                                countOnlyMovingIfGreenOnSegment
                                    ? movingVehiclesMetric[f.Key]
                                    : numVehicles;

                            Log._DebugIf(
                                logTrafficLights,
                                () => "TimedTrafficLightsStep.calcWaitFlow: Total num of cars on seg. " +
                                $"{sourceSegmentId}, lane {laneIndex} going to seg. {targetSegmentId}: " +
                                $"{numMovingVehicles} (all: {numVehicles})");

                            bool addToFlow = false;

                            switch (dir) {
                                case ArrowDirection.Turn: {
                                    addToFlow = Shortcuts.LHT
                                        ? segLight.IsRightGreen()
                                        : segLight.IsLeftGreen();
                                    break;
                                }

                                case ArrowDirection.Left: {
                                    addToFlow = segLight.IsLeftGreen();
                                    break;
                                }

                                case ArrowDirection.Right: {
                                    addToFlow = segLight.IsRightGreen();
                                    break;
                                }

                                // also: case ArrowDirection.Forward:
                                default: {
                                    addToFlow = segLight.IsMainGreen();
                                    break;
                                }
                            }

                            if (addToFlow) {
                                curTotalLaneFlow += numMovingVehicles;
                                ++numLaneFlows;
                                Log._DebugIf(
                                    logTrafficLights,
                                    () => "TimedTrafficLightsStep.calcWaitFlow: ## Vehicles @ " +
                                    $"lane {laneIndex}, seg. {sourceSegmentId} going to seg. " +
                                    $"{targetSegmentId}: COUTING as FLOWING -- numMovingVehicles={numMovingVehicles}");
                            } else {
                                curTotalLaneWait += numVehicles;
                                ++numLaneWaits;
                                Log._DebugIf(
                                    logTrafficLights,
                                    () => "TimedTrafficLightsStep.calcWaitFlow: ## Vehicles @ " +
                                    $"lane {laneIndex}, seg. {sourceSegmentId} going to seg. " +
                                    $"{targetSegmentId}: COUTING as WAITING -- numVehicles={numVehicles}");
                            }

                            Log._DebugIf(
                                logTrafficLights,
                                () => "TimedTrafficLightsStep.calcWaitFlow: >>>>> Vehicles @ " +
                                $"lane {laneIndex}, seg. {sourceSegmentId} going to seg. {targetSegmentId}: " +
                                $"curTotalLaneFlow={curTotalLaneFlow}, curTotalLaneWait={curTotalLaneWait}, " +
                                $"numLaneFlows={numLaneFlows}, numLaneWaits={numLaneWaits}");
                        } // foreach target segment

                        float meanLaneFlow = 0;

                        if (numLaneFlows > 0) {
                            switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                                case FlowWaitCalcMode.Total: {
                                    numSegFlows += numLaneFlows;
                                    curTotalSegFlow += curTotalLaneFlow;
                                    break;
                                }

                                // also: case FlowWaitCalcMode.Mean:
                                default: {
                                    ++numSegFlows;
                                    meanLaneFlow = curTotalLaneFlow / (float)numLaneFlows;
                                    curTotalSegFlow += meanLaneFlow;
                                    break;
                                }
                            }
                        }

                        float meanLaneWait = 0;

                        if (numLaneWaits > 0) {
                            switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                                case FlowWaitCalcMode.Total: {
                                    numSegWaits += numLaneWaits;
                                    curTotalSegWait += curTotalLaneWait;
                                    break;
                                }

                                // also: case FlowWaitCalcMode.Mean:
                                default: {
                                    ++numSegWaits;
                                    meanLaneWait = curTotalLaneWait / (float)numLaneWaits;
                                    curTotalSegWait += meanLaneWait;
                                    break;
                                }
                            }
                        }

                        Log._DebugIf(
                            logTrafficLights,
                            () => "TimedTrafficLightsStep.calcWaitFlow: >>>> Vehicles @ lane " +
                            $"{laneIndex}, seg. {sourceSegmentId}: meanLaneFlow={meanLaneFlow}, " +
                            $"meanLaneWait={meanLaneWait} // curTotalSegFlow={curTotalSegFlow}, " +
                            $"curTotalSegWait={curTotalSegWait}, numSegFlows={numSegFlows}, " +
                            $"numSegWaits={numSegWaits}");
                    } // foreach source lane

                    float meanSegFlow = 0;

                    if (numSegFlows > 0) {
                        switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                            case FlowWaitCalcMode.Total: {
                                numNodeFlows += numSegFlows;
                                curTotalNodeFlow += curTotalSegFlow;
                                break;
                            }

                            // also: case FlowWaitCalcMode.Mean:
                            default: {
                                ++numNodeFlows;
                                meanSegFlow = curTotalSegFlow / numSegFlows;
                                curTotalNodeFlow += meanSegFlow;
                                break;
                            }
                        }
                    }

                    float meanSegWait = 0;

                    if (numSegWaits > 0) {
                        switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                            case FlowWaitCalcMode.Total: {
                                numNodeWaits += numSegWaits;
                                curTotalNodeWait += curTotalSegWait;
                                break;
                            }

                            // also: case FlowWaitCalcMode.Mean:
                            default: {
                                ++numNodeWaits;
                                meanSegWait = curTotalSegWait / numSegWaits;
                                curTotalNodeWait += meanSegWait;
                                break;
                            }
                        }
                    }

                    Log._DebugIf(
                        logTrafficLights,
                        () => $"TimedTrafficLightsStep.calcWaitFlow: >>> Vehicles @ seg. {sourceSegmentId}: " +
                        $"meanSegFlow={meanSegFlow}, meanSegWait={meanSegWait} // " +
                        $"curTotalNodeFlow={curTotalNodeFlow}, curTotalNodeWait={curTotalNodeWait}, " +
                        $"numNodeFlows={numNodeFlows}, numNodeWaits={numNodeWaits}");
                } // foreach source segment

                float meanNodeFlow = 0;

                if (numNodeFlows > 0) {
                    switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                        case FlowWaitCalcMode.Total: {
                            numFlows += numNodeFlows;
                            curTotalFlow += curTotalNodeFlow;
                            break;
                        }

                        // also: case FlowWaitCalcMode.Mean:
                        default: {
                            ++numFlows;
                            meanNodeFlow = curTotalNodeFlow / numNodeFlows;
                            curTotalFlow += meanNodeFlow;
                            break;
                        }
                    }
                }

                float meanNodeWait = 0;

                if (numNodeWaits > 0) {
                    switch (GlobalConfig.Instance.TimedTrafficLights.FlowWaitCalcMode) {
                        case FlowWaitCalcMode.Total: {
                            numWaits += numNodeWaits;
                            curTotalWait += curTotalNodeWait;
                            break;
                        }

                        // also: case FlowWaitCalcMode.Mean:
                        default: {
                            ++numWaits;
                            meanNodeWait = curTotalNodeWait / numNodeWaits;
                            curTotalWait += meanNodeWait;
                            break;
                        }
                    }
                }

                Log._DebugIf(
                    logTrafficLights,
                    () => "TimedTrafficLightsStep.calcWaitFlow: Calculated flow for source node " +
                    $"{timedNodeId}: meanNodeFlow={meanNodeFlow} meanNodeWait={meanNodeWait} // " +
                    $"curTotalFlow={curTotalFlow}, curTotalWait={curTotalWait}, numFlows={numFlows}, " +
                    $"numWaits={numWaits}");
            } // foreach timed node

            float meanFlow = numFlows > 0 ? curTotalFlow / numFlows : 0;
            float meanWait = numWaits > 0 ? curTotalWait / numWaits : 0;
            meanFlow /= WaitFlowBalance; // a value smaller than 1 rewards steady traffic currents
            wait = meanWait;
            flow = meanFlow;

            if (logTrafficLights) {
                Log._Debug(
                    "TimedTrafficLightsStep.calcWaitFlow: ***CALCULATION FINISHED*** for master " +
                    $"node {timedNode.NodeId}: flow={flow} wait={wait}");
            }
        }

        internal void ChangeLightMode(ushort segmentId,
                                      ExtVehicleType vehicleType,
                                      LightMode mode)
        {
            CustomSegmentLight light = CustomSegmentLights[segmentId].GetCustomLight(vehicleType);
            if (light != null) {
                light.CurrentMode = mode;
            }
        }

        public CustomSegmentLights RemoveSegmentLights(ushort segmentId) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.RemoveSegmentLights({segmentId}) called.");
            }
#endif

            if (CustomSegmentLights.TryGetValue(segmentId, out CustomSegmentLights ret)) {
                CustomSegmentLights.Remove(segmentId);
            }

            return ret;
        }

        public CustomSegmentLights GetSegmentLights(ushort segmentId) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.GetSegmentLights({segmentId}) called.");
            }
#endif

            return GetSegmentLights(timedNode.NodeId, segmentId);
        }

        public CustomSegmentLights GetSegmentLights(ushort nodeId, ushort segmentId) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}) called.");
            }
#endif

            if (nodeId != timedNode.NodeId) {
                Log.Warning(
                    $"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}): TTL " +
                    $"@ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
                return null;
            }

            if (CustomSegmentLights.TryGetValue(segmentId, out CustomSegmentLights customLights)) {
                return customLights;
            }

            Log.Info($"TimedTrafficLightsStep.GetSegmentLights({nodeId}, {segmentId}): TTL @ " +
                     $"node {timedNode.NodeId} does not know segment {segmentId}");
            return null;
        }

        public bool RelocateSegmentLights(ushort sourceSegmentId, ushort targetSegmentId) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug(
                    $"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, " +
                    $"{targetSegmentId}) called.");
            }
#endif

            if (!CustomSegmentLights.TryGetValue(
                    sourceSegmentId,
                    out CustomSegmentLights sourceLights)) {
                Log.Error(
                    $"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, " +
                    $"{targetSegmentId}): Timed traffic light does not know source segment {sourceSegmentId}. " +
                    $"Cannot relocate to {targetSegmentId}.");
                return false;
            }

            ref NetSegment targetSegment = ref targetSegmentId.ToSegment();

            if (!targetSegment.IsValid()) {
                Log.Error(
                    $"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): " +
                    $"Target segment {targetSegmentId} is invalid");
                return false;
            }

            bool? startNode = targetSegment.GetRelationToNode(timedNode.NodeId);

            if (!startNode.HasValue) {
                Log.Error(
                    $"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): " +
                    $"Node {timedNode.NodeId} is neither start nor end node of target segment {targetSegmentId}");
                return false;
            }

            CustomSegmentLights.Remove(sourceSegmentId);
            CustomSegmentLightsManager.Instance
                     .GetOrLiveSegmentLights(targetSegmentId, startNode.Value)
                     .Housekeeping(true, true);
            sourceLights.Relocate(targetSegmentId, startNode.Value, this);
            CustomSegmentLights[targetSegmentId] = sourceLights;

            Log._Debug(
                $"TimedTrafficLightsStep.RelocateSegmentLights({sourceSegmentId}, {targetSegmentId}): " +
                $"Relocated lights: {sourceSegmentId} -> {targetSegmentId} @ node {timedNode.NodeId}");
            return true;
        }

        /// <summary>
        /// Adds a new segment to this step. It is cloned from the live custom traffic light.
        /// </summary>
        /// <param name="segmentId"></param>
        internal bool AddSegment(ushort segmentId, bool startNode, bool makeRed) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}) " +
                           $"called @ node {timedNode.NodeId}.");
            }
#endif

            ref NetSegment netSegment = ref segmentId.ToSegment();
            ushort nodeId = startNode ? netSegment.m_startNode : netSegment.m_endNode;

            if (!netSegment.IsValid()) {
                Log.Error(
                    $"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}): " +
                    $"Segment {segmentId} is invalid");
                return false;
            }

            if (nodeId != timedNode.NodeId) {
                Log.Error(
                    $"TimedTrafficLightsStep.AddSegment({segmentId}, {startNode}, {makeRed}): " +
                    $"Segment {segmentId} is not connected to node {timedNode.NodeId} @ start {startNode}");
                return false;
            }

           CustomSegmentLightsManager customSegLightsMan = CustomSegmentLightsManager.Instance;
            CustomSegmentLights liveLights = customSegLightsMan.GetOrLiveSegmentLights(segmentId, startNode);

            liveLights.Housekeeping(true, true);

            CustomSegmentLights clonedLights = liveLights.Clone(this);

            CustomSegmentLights.Add(segmentId, clonedLights);
            if (makeRed) {
                CustomSegmentLights[segmentId].MakeRed();
            } else {
                CustomSegmentLights[segmentId].MakeRedOrGreen();
            }

            return customSegLightsMan.ApplyLightModes(segmentId, startNode, clonedLights);
        }

        public bool SetSegmentLights(ushort nodeId, ushort segmentId, CustomSegmentLights lights) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.SetSegmentLights({nodeId}, {segmentId}, {lights}) called.");
            }
#endif

            if (nodeId != timedNode.NodeId) {
                Log.Warning(
                    $"TimedTrafficLightsStep.SetSegmentLights({nodeId}, {segmentId}, {lights}): " +
                    $"TTL @ node {timedNode.NodeId} does not handle custom traffic lights for node {nodeId}");
                return false;
            }

            return SetSegmentLights(segmentId, lights);
        }

        public bool SetSegmentLights(ushort segmentId, CustomSegmentLights lights) {
#if DEBUG
            if (DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == timedNode.NodeId) {
                Log._Debug($"TimedTrafficLightsStep.SetSegmentLights({segmentId}, {lights}) called.");
            }
#endif
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                Log.Error($"TimedTrafficLightsStep.SetSegmentLights({segmentId}): Segment {segmentId} is invalid");
                return false;
            }

            bool? startNode = netSegment.GetRelationToNode(timedNode.NodeId);

            if (!startNode.HasValue) {
                Log.Error($"TimedTrafficLightsStep.SetSegmentLights: Segment {segmentId} is not " +
                          $"connected to node {timedNode.NodeId}");
                return false;
            }

            CustomSegmentLightsManager.Instance
                     .GetOrLiveSegmentLights(segmentId, startNode.Value)
                     .Housekeeping(true, true);
            lights.Relocate(segmentId, startNode.Value, this);
            CustomSegmentLights[segmentId] = lights;

            Log._Debug(
                $"TimedTrafficLightsStep.SetSegmentLights: Set lights @ seg. {segmentId}, " +
                $"node {timedNode.NodeId}");
            return true;
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public CustomSegmentLights GetSegmentLights(ushort segmentId,
                                                     bool startNode,
                                                     bool add = true,
                                                     RoadBaseAI.TrafficLightState lightState =
                                                         RoadBaseAI.TrafficLightState.Red)
        {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public CustomSegmentLights GetOrLiveSegmentLights(ushort segmentId, bool startNode) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public bool ApplyLightModes(ushort segmentId,
                                    bool startNode,
                                    CustomSegmentLights otherLights) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public void SetLightMode(ushort segmentId,
                                 bool startNode,
                                 ExtVehicleType vehicleType,
                                 LightMode mode) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public void AddNodeLights(ushort nodeId) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public void RemoveNodeLights(ushort nodeId) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public void RemoveSegmentLight(ushort segmentId, bool startNode) {
            throw new NotImplementedException();
        }

        // TODO IMPROVE THIS! Liskov substitution principle must hold.
        public bool IsSegmentLight(ushort segmentId, bool startNode) {
            throw new NotImplementedException();
        }
    }
}
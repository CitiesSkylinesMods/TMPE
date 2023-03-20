namespace TrafficManager.TrafficLight.Impl {
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.API.Traffic;
    using TrafficManager.Geometry.Impl;
    using TrafficManager.Manager.Impl;
    using TrafficManager.State.ConfigData;
    using TrafficManager.Traffic;
    using TrafficManager.Util;
    using UnityEngine;
    using TrafficManager.Util.Extensions;

    // TODO define TimedTrafficLights per node group, not per individual nodes
    public class TimedTrafficLights {
        public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup) {
            NodeId = nodeId;
            NodeGroup = new List<ushort>(nodeGroup);
            MasterNodeId = NodeGroup[0];

            ref NetNode node = ref nodeId.ToNode();
            UpdateDirections(ref node);
            UpdateSegmentEnds(ref node);

            started = false;
        }

        public ushort NodeId {
            get;
        }

        /// <summary>
        /// Gets or sets the master node.
        /// In case the traffic light is set for a group of nodes, the master node decides
        /// if all member steps are done.
        /// </summary>
        public ushort MasterNodeId {
            get; set; // TODO private set
        }

        private List<TimedTrafficLightsStep> Steps = new List<TimedTrafficLightsStep>();

        public int CurrentStep { get; set; }

        public IList<ushort> NodeGroup { get; set; } // TODO private set

        public bool TestMode { get; set; } // TODO private set

        private bool started;

        /// <summary>
        /// Indicates the total amount and direction of rotation that was applied to this timed traffic light
        /// </summary>
        public short RotationOffset { get; private set; }

        public IDictionary<ushort, IDictionary<ushort, ArrowDirection>> Directions { get; private set; }

        /// <summary>
        /// Segment ends that were set up for this timed traffic light
        /// </summary>
        private ICollection<ISegmentEndId> segmentEndIds = new HashSet<ISegmentEndId>();

        public override string ToString() {
            return string.Format(
                "[TimedTrafficLights\n\tNodeId = {0}\n\tmasterNodeId = {1}\n\tSteps = {2}\n" +
                "\tNodeGroup = {3}\n\ttestMode = {4}\n\tstarted = {5}\n\tDirections = {6}\n" +
                "\tsegmentEndIds = {7}\nTimedTrafficLights]",
                NodeId,
                MasterNodeId,
                Steps.CollectionToString(),
                NodeGroup.CollectionToString(),
                TestMode,
                started,
                Directions.DictionaryToString(),
                segmentEndIds.CollectionToString());
        }

        public void PasteSteps(TimedTrafficLights sourceTimedLight) {
            Stop();
            Steps.Clear();
            RotationOffset = 0;

            ExtNodeManager extNodeManager = ExtNodeManager.Instance;

            List<ushort> clockSortedSourceSegmentIds = extNodeManager.GetNodeSegmentIds(sourceTimedLight.NodeId, ClockDirection.Clockwise).ToList();
            List<ushort> clockSortedTargetSegmentIds = extNodeManager.GetNodeSegmentIds(NodeId, ClockDirection.Clockwise).ToList();

            if (clockSortedTargetSegmentIds.Count != clockSortedSourceSegmentIds.Count) {
                throw new Exception(
                    "TimedTrafficLights.PasteLight: Segment count mismatch -- source node " +
                    $"{sourceTimedLight.NodeId}: {clockSortedSourceSegmentIds.CollectionToString()} " +
                    $"vs. target node {NodeId}: {clockSortedTargetSegmentIds.CollectionToString()}");
            }

            for (int stepIndex = 0; stepIndex < sourceTimedLight.NumSteps(); ++stepIndex) {
                TimedTrafficLightsStep sourceStep = sourceTimedLight.GetStep(stepIndex);
                TimedTrafficLightsStep targetStep = new TimedTrafficLightsStep(
                    this,
                    sourceStep.MinTime,
                    sourceStep.MaxTime,
                    sourceStep.ChangeMetric,
                    sourceStep.WaitFlowBalance);

                for (int i = 0; i < clockSortedSourceSegmentIds.Count; ++i) {
                    ushort sourceSegmentId = clockSortedSourceSegmentIds[i];
                    ushort targetSegmentId = clockSortedTargetSegmentIds[i];

                    bool targetStartNode = targetSegmentId.ToSegment().IsStartNode(NodeId);

                    CustomSegmentLights sourceLights =
                        sourceStep.CustomSegmentLights[sourceSegmentId];
                    CustomSegmentLights targetLights = sourceLights.Clone(targetStep, false);

                    targetStep.SetSegmentLights(targetSegmentId, targetLights);
                    CustomSegmentLightsManager.Instance.ApplyLightModes(
                        targetSegmentId,
                        targetStartNode,
                        targetLights);
                }

                Steps.Add(targetStep);
            }

            if (sourceTimedLight.IsStarted()) {
                Start();
            }
        }

        private readonly object rotateLock_ = new object();

        private void Rotate(ArrowDirection dir) {
            if (!IsMasterNode() || NodeGroup.Count != 1 || Steps.Count <= 0) {
                return;
            }

            Stop();

            lock(rotateLock_) {

                Log._Debug($"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Rotating timed traffic light.");

                if (dir != ArrowDirection.Left && dir != ArrowDirection.Right) {
                    throw new NotSupportedException();
                }

                ExtNodeManager extNodeManager = ExtNodeManager.Instance;

                var clockDirection = dir == ArrowDirection.Right
                    ? ClockDirection.Clockwise
                    : ClockDirection.CounterClockwise;
                List<ushort> clockSortedSegmentIds = extNodeManager.GetNodeSegmentIds(NodeId, clockDirection).ToList();

                Log._Debug(
                    $"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Clock-sorted segment ids: " +
                    clockSortedSegmentIds.CollectionToString());

                if (clockSortedSegmentIds.Count <= 0) {
                    return;
                }

                int stepIndex = -1;

                foreach (TimedTrafficLightsStep step in Steps) {
                    ++stepIndex;
                    CustomSegmentLights bufferedLights = null;

                    for (int sourceIndex = 0;
                         sourceIndex < clockSortedSegmentIds.Count;
                         ++sourceIndex)
                    {
                        ushort sourceSegmentId = clockSortedSegmentIds[sourceIndex];
                        int targetIndex = (sourceIndex + 1) % clockSortedSegmentIds.Count;
                        ushort targetSegmentId = clockSortedSegmentIds[targetIndex];

                        Log._Debug(
                            $"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Moving light @ seg. " +
                            $"{sourceSegmentId} to seg. {targetSegmentId} @ step {stepIndex}");

                        CustomSegmentLights sourceLights =
                            sourceIndex == 0
                                ? step.RemoveSegmentLights(sourceSegmentId)
                                : bufferedLights;

                        if (sourceLights == null) {
                            throw new Exception(
                                $"TimedTrafficLights.Rotate({dir}): Error occurred while copying " +
                                $"custom lights from {sourceSegmentId} to {targetSegmentId} @ step {stepIndex}: " +
                                $"sourceLights is null @ sourceIndex={sourceIndex}, targetIndex={targetIndex}");
                        }

                        bufferedLights = step.RemoveSegmentLights(targetSegmentId);

                        sourceLights.Relocate(
                            targetSegmentId,
                            targetSegmentId.ToSegment().IsStartNode(NodeId));

                        if (!step.SetSegmentLights(targetSegmentId, sourceLights)) {
                            throw new Exception(
                                $"TimedTrafficLights.Rotate({dir}): Error occurred while copying " +
                                $"custom lights from {sourceSegmentId} to {targetSegmentId} @ step " +
                                $"{stepIndex}: could not set lights for target segment @ " +
                                $"sourceIndex={sourceIndex}, targetIndex={targetIndex}");
                        }
                    }
                }

                switch (dir) {
                    case ArrowDirection.Left: {
                        RotationOffset = (short)((RotationOffset + clockSortedSegmentIds.Count - 1) %
                                    clockSortedSegmentIds.Count);
                        break;
                    }

                    case ArrowDirection.Right: {
                        RotationOffset = (short)((RotationOffset + 1) % clockSortedSegmentIds.Count);
                        break;
                    }
                }

                CurrentStep = 0;
                SetLights(true);
            }
        }

        public void RotateLeft() {
            Rotate(ArrowDirection.Left);
        }

        public void RotateRight() {
            Rotate(ArrowDirection.Right);
        }

        private void UpdateDirections(ref NetNode node) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && (DebugSettings.NodeId == 0 || DebugSettings.NodeId == NodeId);
#else
            const bool logTrafficLights = false;
#endif
            Log._DebugIf(
                logTrafficLights,
                () => $">>>>> TimedTrafficLights.UpdateDirections: called for node {NodeId}");

            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            Directions = new Dictionary<ushort, IDictionary<ushort, ArrowDirection>>();

            for (int sourceSegmentIndex = 0; sourceSegmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++sourceSegmentIndex) {
                ushort sourceSegmentId = node.GetSegment(sourceSegmentIndex);

                if (sourceSegmentId == 0) {
                    continue;
                }

                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.UpdateDirections: Processing source segment {sourceSegmentId}");

                IDictionary<ushort, ArrowDirection> dirs = new Dictionary<ushort, ArrowDirection>();
                Directions.Add(sourceSegmentId, dirs);
                int endIndex = segEndMan.GetIndex(
                    sourceSegmentId,
                    sourceSegmentId.ToSegment().IsStartNode(NodeId));

                for (int targetSegmentIndex = 0; targetSegmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++targetSegmentIndex) {
                    ushort targetSegmentId = node.GetSegment(targetSegmentIndex);

                    if (targetSegmentId == 0) {
                        continue;
                    }

                    ArrowDirection dir = segEndMan.GetDirection(
                        ref segEndMan.ExtSegmentEnds[endIndex],
                        targetSegmentId);
                    dirs.Add(targetSegmentId, dir);

                    Log._DebugIf(
                        logTrafficLights,
                        () => "TimedTrafficLights.UpdateDirections: Processing source segment " +
                        $"{sourceSegmentId}, target segment {targetSegmentId}: adding dir {dir}");
                }
            }

            Log._DebugIf(
                logTrafficLights,
                () => $"<<<<< TimedTrafficLights.UpdateDirections: finished for node {NodeId}: " +
                $"{Directions.DictionaryToString()}");
        }

        public bool IsMasterNode() {
            return MasterNodeId == NodeId;
        }

        public TimedTrafficLightsStep AddStep(int minTime,
                                               int maxTime,
                                               StepChangeMetric changeMetric,
                                               float waitFlowBalance,
                                               bool makeRed = false) {
            // TODO currently, this method must be called for each node in the node group individually
            if (minTime < 0) {
                minTime = 0;
            }

            if (maxTime <= 0) {
                maxTime = 1;
            }

            if (maxTime < minTime) {
                maxTime = minTime;
            }

            var step = new TimedTrafficLightsStep(
                this,
                minTime,
                maxTime,
                changeMetric,
                waitFlowBalance,
                makeRed);

            Steps.Add(step);
            return step;
        }

        public void Start() {
            Start(0);
        }

        public void Start(int stepIndex) {
            // TODO currently, this method must be called for each node in the node group individually
            if (stepIndex < 0 || stepIndex >= Steps.Count) {
                stepIndex = 0;
            }

            /*if (!housekeeping())
                    return;*/

            TrafficLightManager.Instance.AddTrafficLight(NodeId, ref NodeId.ToNode());

            foreach (TimedTrafficLightsStep step in Steps) {
                foreach (CustomSegmentLights value in step.CustomSegmentLights.Values) {
                    value.Housekeeping(true, true);
                }
            }

            CheckInvalidPedestrianLights();

            CurrentStep = stepIndex;
            Steps[stepIndex].Start();
            Steps[stepIndex].UpdateLiveLights();

            started = true;
        }

        private void CheckInvalidPedestrianLights() {
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

            // Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");

            ref NetNode node = ref NodeId.ToNode();

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }

                bool startNode = segmentId.ToSegment().IsStartNode(NodeId);

                CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);
                if (lights == null) {
                    Log.Warning(
                        $"TimedTrafficLights.CheckInvalidPedestrianLights() @ node {NodeId}: " +
                        $"Could not retrieve segment lights for segment {segmentId} @ start {startNode}.");
                    continue;
                }

                // Log._Debug($"Checking seg. {segmentId} @ {NodeId}.");
                bool needsAlwaysGreenPedestrian = true;
                int s = 0;

                foreach (TimedTrafficLightsStep step in Steps) {
                    // Log._Debug($"Checking step {s}, seg. {segmentId} @ {NodeId}.");
                    if (!step.CustomSegmentLights.ContainsKey(segmentId)) {
                        // Log._Debug($"Step {s} @ {NodeId} does not contain a segment light for seg. {segmentId}.");
                        ++s;
                        continue;
                    }

                    // Log._Debug($"Checking step {s}, seg. {segmentId} @ {NodeId}:
                    //     {step.segmentLights[segmentId].PedestrianLightState} (pedestrianLightState
                    //     ={step.segmentLights[segmentId].pedestrianLightState}, ManualPedestrianMode
                    //     ={step.segmentLights[segmentId].ManualPedestrianMode}, AutoPedestrianLightState
                    //     ={step.segmentLights[segmentId].AutoPedestrianLightState}");
                    if (step.CustomSegmentLights[segmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
                        // Log._Debug($"Step {s} @ {NodeId} has a green ped. light @ seg. {segmentId}.");
                        needsAlwaysGreenPedestrian = false;
                        break;
                    }

                    ++s;
                }

                // Log._Debug($"Setting InvalidPedestrianLight of seg. {segmentId} @ {NodeId} to
                //     {needsAlwaysGreenPedestrian}.");
                lights.InvalidPedestrianLight = needsAlwaysGreenPedestrian;
            }
        }

        private void ClearInvalidPedestrianLights() {
            CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

            ref NetNode node = ref NodeId.ToNode();
            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;

            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);
                if (segmentId == 0) {
                    continue;
                }

                bool startNode = segmentId.ToSegment().IsStartNode(NodeId);

                CustomSegmentLights lights =
                    customTrafficLightsManager.GetSegmentLights(segmentId, startNode);

                if (lights == null) {
                    Log.Warning(
                        $"TimedTrafficLights.ClearInvalidPedestrianLights() @ node {NodeId}: " +
                        $"Could not retrieve segment lights for segment {segmentId} @ start {startNode}.");
                    continue;
                }

                lights.InvalidPedestrianLight = false;
            }
        }

        // TODO currently, this method must be called for each node in the node group individually
        public void RemoveNodeFromGroup(ushort otherNodeId) {
            NodeGroup.Remove(otherNodeId);
            if (NodeGroup.Count <= 0) {
                TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(
                    NodeId,
                    true,
                    false);
                return;
            }

            MasterNodeId = NodeGroup[0];
        }

        // TODO currently, this method must be called for each node in the node group individually
        // TODO improve & remove
        public bool Housekeeping() {
            // Log._Debug($"Housekeeping timed light @ {NodeId}");
            if (NodeGroup == null || NodeGroup.Count <= 0) {
                Stop();
                return false;
            }

            // Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
            MasterNodeId = NodeGroup[0];

            if (IsStarted()) {
                CheckInvalidPedestrianLights();
            }

            int i = 0;
            foreach (TimedTrafficLightsStep step in Steps) {
                foreach (CustomSegmentLights lights in step.CustomSegmentLights.Values) {
                    // Log._Debug($"----- Housekeeping timed light at step {i}, seg. {lights.SegmentId} @ {NodeId}");
                    CustomSegmentLightsManager.Instance
                             .GetOrLiveSegmentLights(lights.SegmentId, lights.StartNode)
                             .Housekeeping(true, true);
                    lights.Housekeeping(true, true);
                }

                ++i;
            }

            return true;
        }

        // TODO currently, this method must be called for each node in the node group individually
        public void MoveStep(int oldPos, int newPos) {
            var oldStep = Steps[oldPos];

            Steps.RemoveAt(oldPos);
            Steps.Insert(newPos, oldStep);
        }

        // TODO currently, this method must be called for each node in the node group individually
        public void Stop() {
            started = false;
            foreach (TimedTrafficLightsStep step in Steps) {
                step.Reset();
            }

            ClearInvalidPedestrianLights();
        }

        // TODO  currently, this method must be called for each node in the node group individually
        public void Destroy() {
            started = false;
            DestroySegmentEnds();
            Steps = null;
            NodeGroup = null;
        }

        // TODO currently, this method must be called for each node in the node group individually
        public bool IsStarted() {
            return started;
        }

        // TODO currently, this method must be called for each node in the node group individually
        public int NumSteps() {
            return Steps.Count;
        }

        // TODO currently, this method must be called for each node in the node group individually
        public TimedTrafficLightsStep GetStep(int stepId) {
            return Steps[stepId];
        }

        // TODO this method is currently called on each node, but should be called on the master node only
        public void SimulationStep() {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif

            if (!IsMasterNode() || !IsStarted()) {
                Log._DebugIf(
                    logTrafficLights,
                    () => "TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* " +
                    $"NodeId={NodeId} isMasterNode={IsMasterNode()} IsStarted={IsStarted()}");

                return;
            }

            // we are the master node
//            if (!housekeeping()) {
// #if DEBUGTTL
//                    Log.Warning($"TTL SimStep: *STOP* NodeId={NodeId} Housekeeping detected that
//                     this timed traffic light has become invalid: {NodeId}.");
// #endif
//                    Stop();
//                    return;
//            }

            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (1)");

            // using (var bm = Benchmark.MaybeCreateBenchmark(null, "SetLights.1")) {
            SetLights();
            // }

            // using (var bm = Benchmark.MaybeCreateBenchmark(null, "StepDone")) {
            if (!Steps[CurrentStep].StepDone(true)) {
                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} " +
                    $"current step ({CurrentStep}) is not done.");

                return;
            }
            // } // end benchmark

            //-------------------
            // step is done
            //-------------------
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (2)");

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (Steps[CurrentStep].NextStepRefIndex < 0) {
                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.SimulationStep(): Step {CurrentStep} is done at " +
                    $"timed light {NodeId}. Determining next step.");

                // next step has not yet identified yet. check for minTime=0 steps
                int nextStepIndex = (CurrentStep + 1) % NumSteps();

                if (Steps[nextStepIndex].MinTime == 0 && Steps[nextStepIndex].ChangeMetric ==
                    Steps[CurrentStep].ChangeMetric) {
                    // using (var bm = Benchmark.MaybeCreateBenchmark(null, "bestNextStepIndex")) {

                    // next step has minTime=0. calculate flow/wait ratios and compare.
                    int prevStepIndex = CurrentStep;

                    // Steps[CurrentStep].minFlow - Steps[CurrentStep].maxWait
                    float maxWaitFlowDiff = Steps[CurrentStep].GetMetric(
                        Steps[CurrentStep].CurrentFlow,
                        Steps[CurrentStep].CurrentWait);

                    if (float.IsNaN(maxWaitFlowDiff)) {
                        maxWaitFlowDiff = float.MinValue;
                    }

                    int bestNextStepIndex = prevStepIndex;

                    if (logTrafficLights) {
                        Log._DebugFormat(
                            "TimedTrafficLights.SimulationStep(): Next step {0} has minTime = 0 at " +
                            "timed light {1}. Old step {2} has waitFlowDiff={3} (flow={4}, wait={5}).",
                            nextStepIndex,
                            NodeId,
                            CurrentStep,
                            maxWaitFlowDiff,
                            Steps[CurrentStep].CurrentFlow,
                            Steps[CurrentStep].CurrentWait);
                    }

                    while (nextStepIndex != prevStepIndex) {
                        Steps[nextStepIndex].CalcWaitFlow(
                            false,
                            nextStepIndex,
                            out float wait,
                            out float flow);

                        // float flowWaitDiff = flow - wait;
                        float flowWaitDiff = Steps[nextStepIndex].GetMetric(flow, wait);

                        if (flowWaitDiff > maxWaitFlowDiff) {
                            maxWaitFlowDiff = flowWaitDiff;
                            bestNextStepIndex = nextStepIndex;
                        }

                        if (logTrafficLights) {
                            Log._DebugFormat(
                                "TimedTrafficLights.SimulationStep(): Checking upcoming step {0} " +
                                "@ node {1}: flow={2} wait={3} minTime={4}. bestWaitFlowDiff={5}, " +
                                "bestNextStepIndex={6}",
                                nextStepIndex,
                                NodeId,
                                flow,
                                wait,
                                Steps[nextStepIndex].MinTime,
                                bestNextStepIndex,
                                bestNextStepIndex);
                        }

                        if (Steps[nextStepIndex].MinTime != 0) {
                            int stepAfterPrev = (prevStepIndex + 1) % NumSteps();
                            if (nextStepIndex == stepAfterPrev) {
                                // always switch if the next step has a minimum time assigned
                                bestNextStepIndex = stepAfterPrev;
                            }

                            break;
                        }

                        nextStepIndex = (nextStepIndex + 1) % NumSteps();

                        if (Steps[nextStepIndex].ChangeMetric != Steps[CurrentStep].ChangeMetric) {
                            break;
                        }
                    }

                    if (bestNextStepIndex == CurrentStep) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => "TimedTrafficLights.SimulationStep(): Best next step " +
                            $"{bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) equals CurrentStep " +
                            $"@ node {NodeId}.");

                        // restart the current step
                        foreach (ushort slaveNodeId in NodeGroup) {
                            if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                                continue;
                            }

                            TimedTrafficLights slaveTtl1 =
                                tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;

                            slaveTtl1.GetStep(CurrentStep).Start(CurrentStep);
                            slaveTtl1.GetStep(CurrentStep).UpdateLiveLights();
                        }

                        return;
                    }

                    Log._DebugIf(
                        logTrafficLights,
                        () => "TimedTrafficLights.SimulationStep(): Best next step " +
                              $"{bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) does not equal " +
                              $"CurrentStep @ node {NodeId}.");

                    // set next step reference index for assuring a correct end transition
                    foreach (ushort slaveNodeId in NodeGroup) {
                        if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                            continue;
                        }

                        TimedTrafficLights slaveTtl2 =
                            tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                        slaveTtl2.GetStep(CurrentStep).NextStepRefIndex = bestNextStepIndex;
                    }

                    // } // end benchmark
                } else {
                    Steps[CurrentStep].NextStepRefIndex = nextStepIndex;
                }
            }

            // using (var bm = Benchmark.MaybeCreateBenchmark(null, "SetLights.2")) {
            SetLights(); // check if this is needed
            // }

            // using (var bm = Benchmark.MaybeCreateBenchmark(null, "IsEndTransitionDone")) {
            if (!Steps[CurrentStep].IsEndTransitionDone()) {
                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} " +
                    $"current step ({CurrentStep}): end transition is not done.");

                return;
            }

            // } // end benchmark

            //-----------------------------------
            // ending transition (yellow) finished
            if (logTrafficLights) {
                Log._DebugFormat(
                    "TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={0} ending transition " +
                    "done. NodeGroup={1}, nodeId={2}, NumSteps={3}",
                    NodeId,
                    string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray()),
                    NodeId,
                    NumSteps());
            }

            // using (var bm = Benchmark.MaybeCreateBenchmark(null, "ChangeStep")) {

            //---------------------------------
            // change step
            //---------------------------------
            int newStepIndex = Steps[CurrentStep].NextStepRefIndex;
            int oldStepIndex = CurrentStep;

            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights slaveTtl3 = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                slaveTtl3.CurrentStep = newStepIndex;

                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={slaveNodeId} " +
                    $"setting lights of next step: {CurrentStep}");

                slaveTtl3.GetStep(oldStepIndex).NextStepRefIndex = -1;
                slaveTtl3.GetStep(newStepIndex).Start(oldStepIndex);
                slaveTtl3.GetStep(newStepIndex).UpdateLiveLights();
            }

            // } // end benchmark
        }

        public void SetLights(bool noTransition = false) {
            if (Steps.Count <= 0) {
                return;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            // set lights
            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights slaveTtl = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                slaveTtl.GetStep(CurrentStep).UpdateLiveLights(noTransition);
            }
        }

        public void SkipStep(bool setLights = true, int prevStepRefIndex = -1) {
            if (!IsMasterNode()) {
                return;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            var newCurrentStep = (CurrentStep + 1) % NumSteps();
            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights slaveTtl = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;

                slaveTtl.GetStep(CurrentStep).SetStepDone();
                slaveTtl.CurrentStep = newCurrentStep;
                slaveTtl.GetStep(newCurrentStep).Start(prevStepRefIndex);

                if (setLights) {
                    slaveTtl.GetStep(newCurrentStep).UpdateLiveLights();
                }
            }
        }

        public long CheckNextChange(ushort segmentId,
                                    bool startNode,
                                    API.Traffic.Enums.ExtVehicleType vehicleType,
                                    int lightType)
        {
            int curStep = CurrentStep;
            int nextStep = (CurrentStep + 1) % NumSteps();
            long numFrames = Steps[CurrentStep].MaxTimeRemaining();

            RoadBaseAI.TrafficLightState currentState;
            CustomSegmentLights segmentLights =
                CustomSegmentLightsManager.Instance.GetSegmentLights(
                    segmentId,
                    startNode,
                    false);

            if (segmentLights == null) {
                Log._Debug($"CheckNextChange: No segment lights at node {NodeId}, segment {segmentId}");
                return 99;
            }

            CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
            if (segmentLight == null) {
                Log._Debug($"CheckNextChange: No segment light at node {NodeId}, segment {segmentId}");
                return 99;
            }

            switch (lightType) {
                case 0: {
                    currentState = segmentLight.LightMain;
                    break;
                }

                case 1: {
                    currentState = segmentLight.LightLeft;
                    break;
                }

                case 2: {
                    currentState = segmentLight.LightRight;
                    break;
                }

                default: {
                    currentState = segmentLights.PedestrianLightState ?? RoadBaseAI.TrafficLightState.Red;
                    break;
                }
            }

            while (true) {
                if (nextStep == curStep) {
                    numFrames = 99;
                    break;
                }

                RoadBaseAI.TrafficLightState light =
                    Steps[nextStep].GetLightState(segmentId, vehicleType, lightType);

                if (light != currentState) {
                    break;
                }

                numFrames += Steps[nextStep].MaxTime;
                nextStep = (nextStep + 1) % NumSteps();
            } // endless loop

            return numFrames;
        }

        public void ResetSteps() {
            Steps.Clear();
        }

        public void RemoveStep(int id) {
            Steps.RemoveAt(id);
        }

        public void OnGeometryUpdate() {
            Log._Trace(
                $"TimedTrafficLights.OnGeometryUpdate: called for timed traffic light @ {NodeId}.");

            ref NetNode node = ref NodeId.ToNode();

            UpdateDirections(ref node);
            UpdateSegmentEnds(ref node);

            if (NumSteps() <= 0) {
                Log._Debug($"TimedTrafficLights.OnGeometryUpdate: no steps @ {NodeId}");
                return;
            }

            BackUpInvalidStepSegments(ref node);
            HandleNewSegments(ref node);
        }

        /// <summary>
        /// Moves all custom segment lights that are associated with an invalid segment to a special
        /// container for later reuse
        /// </summary>
        private void BackUpInvalidStepSegments(ref NetNode node) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.BackUpInvalidStepSegments: called for timed traffic light @ {NodeId}");

            ICollection<ushort> validSegments = new HashSet<ushort>();
            for (int k = 0; k < 8; ++k) {
                ushort segmentId = node.GetSegment(k);

                if (segmentId == 0) {
                    continue;
                }

                // bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);
                validSegments.Add(segmentId);
            }

            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.BackUpInvalidStepSegments: valid segments @ {NodeId}: " +
                validSegments.CollectionToString());

            int i = 0;
            foreach (TimedTrafficLightsStep step in Steps) {
                ICollection<ushort> invalidSegmentIds = new HashSet<ushort>();

                foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.CustomSegmentLights) {
                    if (!validSegments.Contains(e.Key)) {
                        step.InvalidSegmentLights.AddLast(e.Value);
                        invalidSegmentIds.Add(e.Key);

                        Log._DebugIf(
                            logTrafficLights,
                            () => "TimedTrafficLights.BackUpInvalidStepSegments: Detected invalid " +
                            $"segment @ step {i}, node {NodeId}: {e.Key}");
                    }
                }

                foreach (ushort invalidSegmentId in invalidSegmentIds) {
                    Log._DebugIf(
                        logTrafficLights,
                        () => "TimedTrafficLights.BackUpInvalidStepSegments: Removing invalid segment " +
                        $"{invalidSegmentId} from step {i} @ node {NodeId}");

                    step.CustomSegmentLights.Remove(invalidSegmentId);
                }

                if (logTrafficLights) {
                    Log._DebugFormat(
                        "TimedTrafficLights.BackUpInvalidStepSegments finished for TTL step {0} @ " +
                        "node {1}: step.CustomSegmentLights={2} step.InvalidSegmentLights={3}",
                        i,
                        NodeId,
                        step.CustomSegmentLights.DictionaryToString(),
                        step.InvalidSegmentLights.CollectionToString());
                }

                ++i;
            }
        }

        /// <summary>
        /// Processes new segments and adds them to the steps. If steps contain a custom light
        /// for an old invalid segment, this light is being reused for the new segment.
        /// </summary>
        /// <param name="nodeGeo"></param>
        private void HandleNewSegments(ref NetNode node) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif
            // Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");

            ExtSegmentManager extSegmentManager = ExtSegmentManager.Instance;
            for (int segmentIndex = 0; segmentIndex < Constants.MAX_SEGMENTS_OF_NODE; ++segmentIndex) {
                ushort segmentId = node.GetSegment(segmentIndex);

                if (segmentId == 0) {
                    continue;
                }

                var startNode = segmentId.ToSegment().IsStartNode(NodeId);

                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.HandleNewSegments: handling existing seg. {segmentId} @ {NodeId}");

                if (Steps[0].CustomSegmentLights.ContainsKey(segmentId)) {
                    continue;
                }

                // segment was created
                RotationOffset = 0;
                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.HandleNewSegments: New segment detected: {segmentId} @ {NodeId}");

                int stepIndex = -1;

                foreach (TimedTrafficLightsStep step in Steps) {
                    ++stepIndex;

                    LinkedListNode<CustomSegmentLights> lightsToReuseNode = step.InvalidSegmentLights.First;

                    if (lightsToReuseNode == null) {
                        // no old segment found: create a fresh custom light
                        Log._DebugIf(
                            logTrafficLights,
                            () => "TimedTrafficLights.HandleNewSegments: Adding new segment " +
                            $"{segmentId} to node {NodeId} without reusing old segment");

                        if (!step.AddSegment(segmentId, startNode, true)) {
                            if (logTrafficLights) {
                                Log.Warning(
                                    "TimedTrafficLights.HandleNewSegments: Failed to add segment " +
                                    $"{segmentId} @ start {startNode} to node {NodeId}");
                            }
                        }
                    } else {
                        // reuse old lights
                        step.InvalidSegmentLights.RemoveFirst();
                        CustomSegmentLights lightsToReuse = lightsToReuseNode.Value;

                        Log._DebugIf(
                            logTrafficLights,
                            () => $"Replacing old segment @ {NodeId} with new segment {segmentId}");

                        lightsToReuse.Relocate(segmentId, startNode);
                        step.SetSegmentLights(segmentId, lightsToReuse);
                    }
                }
            } // for each segment 0..7
        }

        public TimedTrafficLights MasterLights() {
            return TrafficLightSimulationManager.Instance.TrafficLightSimulations[MasterNodeId].timedLight;
        }

        public void SetTestMode(bool testMode) {
            TestMode = false;

            if (!IsStarted()) {
                return;
            }

            TestMode = testMode;
        }

        public bool IsInTestMode() {
            if (!IsStarted()) {
                TestMode = false;
            }

            return TestMode;
        }

        public void ChangeLightMode(ushort segmentId,
                                    API.Traffic.Enums.ExtVehicleType vehicleType,
                                    LightMode mode) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get()
                                    && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif
            ref NetSegment netSegment = ref segmentId.ToSegment();

            if (!netSegment.IsValid()) {
                if (logTrafficLights) {
                    Log.Error($"TimedTrafficLights.ChangeLightMode: Segment {segmentId} is invalid");
                }

                return;
            }

            bool? startNode = netSegment.GetRelationToNode(NodeId);

            if (!startNode.HasValue) {
                return;
            }

            foreach (TimedTrafficLightsStep step in Steps) {
                step.ChangeLightMode(segmentId, vehicleType, mode);
            }

            CustomSegmentLightsManager.Instance.SetLightMode(
                segmentId,
                startNode.Value,
                vehicleType,
                mode);
        }

        public void Join(TimedTrafficLights otherTimedLight) {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (NumSteps() < otherTimedLight.NumSteps()) {
                // increase the number of steps at our timed lights
                for (int i = NumSteps(); i < otherTimedLight.NumSteps(); ++i) {
                    TimedTrafficLightsStep otherStep = otherTimedLight.GetStep(i);

                    foreach (ushort slaveNodeId in NodeGroup) {
                        if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                            continue;
                        }

                        TimedTrafficLights slaveTtl1 = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;

                        slaveTtl1.AddStep(
                            otherStep.MinTime,
                            otherStep.MaxTime,
                            otherStep.ChangeMetric,
                            otherStep.WaitFlowBalance,
                            true);
                    }
                }
            } else {
                // increase the number of steps at their timed lights
                for (int i = otherTimedLight.NumSteps(); i < NumSteps(); ++i) {
                    TimedTrafficLightsStep ourStep = GetStep(i);

                    foreach (ushort slaveNodeId in otherTimedLight.NodeGroup) {
                        if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                            continue;
                        }

                        TimedTrafficLights slaveTtl2 =
                            tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;

                        slaveTtl2.AddStep(
                            ourStep.MinTime,
                            ourStep.MaxTime,
                            ourStep.ChangeMetric,
                            ourStep.WaitFlowBalance,
                            true);
                    }
                }
            }

            // join groups and re-defined master node, determine mean min/max times & mean wait-flow-balances
            var newNodeGroupSet = new HashSet<ushort>();
            newNodeGroupSet.UnionWith(NodeGroup);
            newNodeGroupSet.UnionWith(otherTimedLight.NodeGroup);
            var newNodeGroup = new List<ushort>(newNodeGroupSet);
            ushort newMasterNodeId = newNodeGroup[0];

            var minTimes = new int[NumSteps()];
            var maxTimes = new int[NumSteps()];
            var waitFlowBalances = new float[NumSteps()];
            var stepChangeMetrics = new StepChangeMetric?[NumSteps()];

            foreach (ushort timedNodeId in newNodeGroup) {
                if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights ttl = tlsMan.TrafficLightSimulations[timedNodeId].timedLight;

                for (int i = 0; i < NumSteps(); ++i) {
                    minTimes[i] += ttl.GetStep(i).MinTime;
                    maxTimes[i] += ttl.GetStep(i).MaxTime;
                    waitFlowBalances[i] += ttl.GetStep(i).WaitFlowBalance;
                    StepChangeMetric metric = ttl.GetStep(i).ChangeMetric;

                    if (metric != StepChangeMetric.Default) {
                        if (stepChangeMetrics[i] == null) {
                            // remember first non-default setting
                            stepChangeMetrics[i] = metric;
                        } else if (stepChangeMetrics[i] != metric) {
                            // reset back to default on metric mismatch
                            stepChangeMetrics[i] = StepChangeMetric.Default;
                        }
                    }
                }

                ttl.NodeGroup = newNodeGroup;
                ttl.MasterNodeId = newMasterNodeId;
            }

            // build means
            if (NumSteps() > 0) {
                for (int i = 0; i < NumSteps(); ++i) {
                    minTimes[i] = Math.Max(0, minTimes[i] / newNodeGroup.Count);
                    maxTimes[i] = Math.Max(1, maxTimes[i] / newNodeGroup.Count);
                    waitFlowBalances[i] = Math.Max(
                        0.001f,
                        waitFlowBalances[i] / newNodeGroup.Count);
                }
            }

            // apply means & reset
            foreach (ushort timedNodeId in newNodeGroup) {
                if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                    continue;
                }

                TimedTrafficLights ttl = tlsMan.TrafficLightSimulations[timedNodeId].timedLight;

                ttl.Stop();
                ttl.TestMode = false;

                for (int i = 0; i < NumSteps(); ++i) {
                    ttl.GetStep(i).MinTime = minTimes[i];
                    ttl.GetStep(i).MaxTime = maxTimes[i];
                    ttl.GetStep(i).WaitFlowBalance = waitFlowBalances[i];
                    ttl.GetStep(i).ChangeMetric =
                        stepChangeMetrics[i] == null
                            ? StepChangeMetric.Default
                            : (StepChangeMetric)stepChangeMetrics[i];
                }
            }
        }

        private void UpdateSegmentEnds(ref NetNode node) {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.UpdateSegmentEnds: called for node {NodeId}");

            var segEndMan = Constants.ManagerFactory.SegmentEndManager;
            ICollection<ISegmentEndId> segmentEndsToDelete = new HashSet<ISegmentEndId>();

            // update currently set segment ends
            foreach (ISegmentEndId endId in segmentEndIds) {
                Log._DebugIf(
                    logTrafficLights,
                    () => "TimedTrafficLights.UpdateSegmentEnds: updating existing segment end " +
                    $"{endId} for node {NodeId}");

                if (!segEndMan.UpdateSegmentEnd(endId)) {
                    Log._DebugIf(
                        logTrafficLights,
                        () => $"TimedTrafficLights.UpdateSegmentEnds: segment end {endId} " +
                        $"@ node {NodeId} is invalid");

                    segmentEndsToDelete.Add(endId);
                } else {
                    ISegmentEnd end = segEndMan.GetSegmentEnd(endId);
                    if (end.NodeId != NodeId) {
                        Log._DebugIf(
                            logTrafficLights,
                            () => $"TimedTrafficLights.UpdateSegmentEnds: Segment end {end} is " +
                            $"valid and updated but does not belong to TTL node {NodeId} anymore.");

                        segmentEndsToDelete.Add(endId);
                    } else {
                        Log._DebugIf(
                            logTrafficLights,
                            () => $"TimedTrafficLights.UpdateSegmentEnds: segment end {endId} " +
                            $"@ node {NodeId} is valid");
                    }
                }
            }

            // remove all invalid segment ends
            foreach (ISegmentEndId endId in segmentEndsToDelete) {
                Log._DebugIf(
                    logTrafficLights,
                    () => "TimedTrafficLights.UpdateSegmentEnds: Removing invalid segment " +
                    $"end {endId} @ node {NodeId}");

                segmentEndIds.Remove(endId);
            }

            // set up new segment ends
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.UpdateSegmentEnds: Setting up new segment ends @ node {NodeId}.");

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);

                if (segmentId == 0) {
                    continue;
                }

                var startNode = segmentId.ToSegment().IsStartNode(NodeId);
                ISegmentEndId endId = new SegmentEndId(segmentId, startNode);

                if (segmentEndIds.Contains(endId)) {
                    Log._DebugIf(
                        logTrafficLights,
                        () => $"TimedTrafficLights.UpdateSegmentEnds: Node {NodeId} already knows " +
                        $"segment {segmentId}");

                    continue;
                }

                Log._DebugIf(
                    logTrafficLights,
                    () => $"TimedTrafficLights.UpdateSegmentEnds: Adding segment {segmentId} to node {NodeId}");

                ISegmentEnd end = segEndMan.GetOrAddSegmentEnd(segmentId, startNode);

                if (end != null) {
                    segmentEndIds.Add(end);
                } else {
                    if (logTrafficLights) {
                    Log.Warning(
                        "TimedTrafficLights.UpdateSegmentEnds: Failed to add segment end " +
                        $"{segmentId} @ {startNode} to node {NodeId}: GetOrAddSegmentEnd returned null.");
                    }
                }
            }

            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.UpdateSegmentEnds: finished for node {NodeId}: " +
                $"{segmentEndIds.CollectionToString()}");
        }

        private void DestroySegmentEnds() {
#if DEBUG
            bool logTrafficLights = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;
#else
            const bool logTrafficLights = false;
#endif
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.DestroySegmentEnds: Destroying segment ends @ node {NodeId}");

            foreach (ISegmentEndId endId in segmentEndIds) {
                Log._DebugIf(
                    logTrafficLights,
                    () => "TimedTrafficLights.DestroySegmentEnds: Destroying segment end " +
                    $"{endId} @ node {NodeId}");

                // TODO: only remove if no priority sign is located at the segment end
                // (although this is currently not possible)
                Constants.ManagerFactory.SegmentEndManager.RemoveSegmentEnd(endId);
            }

            segmentEndIds.Clear();
            Log._DebugIf(
                logTrafficLights,
                () => $"TimedTrafficLights.DestroySegmentEnds: finished for node {NodeId}");
        }
    }
}
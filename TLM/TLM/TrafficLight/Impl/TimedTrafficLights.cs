#define DEBUGTTLx

namespace TrafficManager.TrafficLight.Impl {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using API.Manager;
    using API.Traffic.Enums;
    using API.TrafficLight;
    using CSUtil.Commons;
    using GenericGameBridge.Service;
    using Geometry.Impl;
    using Manager;
    using Manager.Impl;
    using State;
    using State.ConfigData;
    using Traffic;
    using Util;

    // TODO define TimedTrafficLights per node group, not per individual nodes
    public class TimedTrafficLights : ITimedTrafficLights {
        public ushort NodeId {
            get; private set;
        }

        /// <summary>
        /// In case the traffic light is set for a group of nodes, the master node decides
        /// if all member steps are done.
        /// </summary>
        public ushort MasterNodeId {
            get; set; // TODO private set
        }

        public List<TimedTrafficLightsStep> Steps = new List<TimedTrafficLightsStep>();
        public int CurrentStep { get; set; } = 0;

        public IList<ushort> NodeGroup { get; set; } // TODO private set
        public bool TestMode { get; set; } = false; // TODO private set

        private bool started = false;

        /// <summary>
        /// Indicates the total amount and direction of rotation that was applied to this timed traffic light
        /// </summary>
        public short RotationOffset { get; private set; } = 0;

        public IDictionary<ushort, IDictionary<ushort, ArrowDirection>> Directions { get; private set; } = null;

        /// <summary>
        /// Segment ends that were set up for this timed traffic light
        /// </summary>
        private ICollection<ISegmentEndId> segmentEndIds = new HashSet<ISegmentEndId>();

        public override string ToString() {
            return $"[TimedTrafficLights\n" +
                   "\t" + $"NodeId = {NodeId}\n" +
                   "\t" + $"masterNodeId = {MasterNodeId}\n" +
                   "\t" + $"Steps = {Steps.CollectionToString()}\n" +
                   "\t" + $"NodeGroup = {NodeGroup.CollectionToString()}\n" +
                   "\t" + $"testMode = {TestMode}\n" +
                   "\t" + $"started = {started}\n" +
                   "\t" + $"Directions = {Directions.DictionaryToString()}\n" +
                   "\t" + $"segmentEndIds = {segmentEndIds.CollectionToString()}\n" +
                   "TimedTrafficLights]";
        }

        public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup) {
            this.NodeId = nodeId;
            NodeGroup = new List<ushort>(nodeGroup);
            MasterNodeId = NodeGroup[0];

            Constants.ServiceFactory.NetService.ProcessNode(nodeId, delegate (ushort nId, ref NetNode node) {
                UpdateDirections(ref node);
                UpdateSegmentEnds(ref node);
                return true;
            });

            started = false;
        }

        private TimedTrafficLights() {

        }

        public void PasteSteps(ITimedTrafficLights sourceTimedLight) {
            Stop();
            Steps.Clear();
            RotationOffset = 0;

            IList<ushort> clockSortedSourceSegmentIds = new List<ushort>();
            Constants.ServiceFactory.NetService.IterateNodeSegments(sourceTimedLight.NodeId, ClockDirection.Clockwise, delegate (ushort segmentId, ref NetSegment segment) {
                clockSortedSourceSegmentIds.Add(segmentId);
                return true;
            });

            IList<ushort> clockSortedTargetSegmentIds = new List<ushort>();
            Constants.ServiceFactory.NetService.IterateNodeSegments(NodeId, ClockDirection.Clockwise, delegate (ushort segmentId, ref NetSegment segment) {
                clockSortedTargetSegmentIds.Add(segmentId);
                return true;
            });

            if (clockSortedTargetSegmentIds.Count != clockSortedSourceSegmentIds.Count) {
                throw new Exception($"TimedTrafficLights.PasteLight: Segment count mismatch -- source node {sourceTimedLight.NodeId}: {clockSortedSourceSegmentIds.CollectionToString()} vs. target node {NodeId}: {clockSortedTargetSegmentIds.CollectionToString()}");
            }

            for (int stepIndex = 0; stepIndex < sourceTimedLight.NumSteps(); ++stepIndex) {
                ITimedTrafficLightsStep sourceStep = sourceTimedLight.GetStep(stepIndex);
                TimedTrafficLightsStep targetStep = new TimedTrafficLightsStep(this, sourceStep.MinTime, sourceStep.MaxTime, sourceStep.ChangeMetric, sourceStep.WaitFlowBalance);
                for (int i = 0; i < clockSortedSourceSegmentIds.Count; ++i) {
                    ushort sourceSegmentId = clockSortedSourceSegmentIds[i];
                    ushort targetSegmentId = clockSortedTargetSegmentIds[i];

                    bool targetStartNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(targetSegmentId, NodeId);

                    ICustomSegmentLights sourceLights = sourceStep.CustomSegmentLights[sourceSegmentId];
                    ICustomSegmentLights targetLights = sourceLights.Clone(targetStep, false);
                    targetStep.SetSegmentLights(targetSegmentId, targetLights);
                    Constants.ManagerFactory.CustomSegmentLightsManager.ApplyLightModes(targetSegmentId, targetStartNode, targetLights);
                }
                Steps.Add(targetStep);
            }

            if (sourceTimedLight.IsStarted()) {
                Start();
            }
        }

        private object rotateLock = new object();

        private void Rotate(ArrowDirection dir) {
            if (! IsMasterNode() || NodeGroup.Count != 1 || Steps.Count <= 0) {
                return;
            }

            Stop();

            try {
                Monitor.Enter(rotateLock);

                Log._Debug($"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Rotating timed traffic light.");

                if (dir != ArrowDirection.Left && dir != ArrowDirection.Right) {
                    throw new NotSupportedException();
                }

                IList<ushort> clockSortedSegmentIds = new List<ushort>();
                Constants.ServiceFactory.NetService.IterateNodeSegments(NodeId, dir == ArrowDirection.Right ? ClockDirection.Clockwise : ClockDirection.CounterClockwise, delegate (ushort segmentId, ref NetSegment segment) {
                    clockSortedSegmentIds.Add(segmentId);
                    return true;
                });

                Log._Debug($"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Clock-sorted segment ids: {clockSortedSegmentIds.CollectionToString()}");

                if (clockSortedSegmentIds.Count <= 0) {
                    return;
                }

                int stepIndex = -1;
                foreach (TimedTrafficLightsStep step in Steps) {
                    ++stepIndex;
                    ICustomSegmentLights bufferedLights = null;
                    for (int sourceIndex = 0; sourceIndex < clockSortedSegmentIds.Count; ++sourceIndex) {
                        ushort sourceSegmentId = clockSortedSegmentIds[sourceIndex];
                        int targetIndex = (sourceIndex + 1) % clockSortedSegmentIds.Count;
                        ushort targetSegmentId = clockSortedSegmentIds[targetIndex];

                        Log._Debug($"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Moving light @ seg. {sourceSegmentId} to seg. {targetSegmentId} @ step {stepIndex}");

                        ICustomSegmentLights sourceLights = sourceIndex == 0 ? step.RemoveSegmentLights(sourceSegmentId) : bufferedLights;
                        if (sourceLights == null) {
                            throw new Exception($"TimedTrafficLights.Rotate({dir}): Error occurred while copying custom lights from {sourceSegmentId} to {targetSegmentId} @ step {stepIndex}: sourceLights is null @ sourceIndex={sourceIndex}, targetIndex={targetIndex}");
                        }
                        bufferedLights = step.RemoveSegmentLights(targetSegmentId);
                        sourceLights.Relocate(targetSegmentId, (bool)Constants.ServiceFactory.NetService.IsStartNode(targetSegmentId, NodeId));
                        if (!step.SetSegmentLights(targetSegmentId, sourceLights)) {
                            throw new Exception($"TimedTrafficLights.Rotate({dir}): Error occurred while copying custom lights from {sourceSegmentId} to {targetSegmentId} @ step {stepIndex}: could not set lights for target segment @ sourceIndex={sourceIndex}, targetIndex={targetIndex}");
                        }
                    }
                }

                switch (dir) {
                    case ArrowDirection.Left:
                        RotationOffset = (short)((RotationOffset + clockSortedSegmentIds.Count - 1) % clockSortedSegmentIds.Count);
                        break;
                    case ArrowDirection.Right:
                        RotationOffset = (short)((RotationOffset + 1) % clockSortedSegmentIds.Count);
                        break;
                }

                CurrentStep = 0;
                SetLights(true);
            } finally {
                Monitor.Exit(rotateLock);
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
            bool debug = DebugSwitch.TimedTrafficLights.Get() && (DebugSettings.NodeId == 0 || DebugSettings.NodeId == NodeId);
            if (debug)
                Log._Debug($">>>>> TimedTrafficLights.UpdateDirections: called for node {NodeId}");
#endif
            IExtSegmentEndManager segEndMan = Constants.ManagerFactory.ExtSegmentEndManager;
            Directions = new TinyDictionary<ushort, IDictionary<ushort, ArrowDirection>>();
            for (int i = 0; i < 8; ++i) {
                ushort sourceSegmentId = node.GetSegment(i);

                if (sourceSegmentId == 0)
                    continue;
#if DEBUG
                if (debug)
                    Log._Debug($"TimedTrafficLights.UpdateDirections: Processing source segment {sourceSegmentId}");
#endif

                IDictionary<ushort, ArrowDirection> dirs = new TinyDictionary<ushort, ArrowDirection>();
                Directions.Add(sourceSegmentId, dirs);
                int endIndex = segEndMan.GetIndex(sourceSegmentId, (bool)Constants.ServiceFactory.NetService.IsStartNode(sourceSegmentId, NodeId));

                for (int k = 0; k < 8; ++k) {
                    ushort targetSegmentId = node.GetSegment(k);

                    if (targetSegmentId == 0)
                        continue;

                    ArrowDirection dir = segEndMan.GetDirection(ref segEndMan.ExtSegmentEnds[endIndex], targetSegmentId);
                    dirs.Add(targetSegmentId, dir);
#if DEBUG
                    if (debug)
                        Log._Debug($"TimedTrafficLights.UpdateDirections: Processing source segment {sourceSegmentId}, target segment {targetSegmentId}: adding dir {dir}");
#endif
                }
            }
#if DEBUG
            if (debug)
                Log._Debug($"<<<<< TimedTrafficLights.UpdateDirections: finished for node {NodeId}: {Directions.DictionaryToString()}");
#endif
        }

        public bool IsMasterNode() {
            return MasterNodeId == NodeId;
        }

        public ITimedTrafficLightsStep AddStep(int minTime, int maxTime, StepChangeMetric changeMetric, float waitFlowBalance, bool makeRed = false) {
            // TODO currently, this method must be called for each node in the node group individually

            if (minTime < 0)
                minTime = 0;
            if (maxTime <= 0)
                maxTime = 1;
            if (maxTime < minTime)
                maxTime = minTime;

            TimedTrafficLightsStep step = new TimedTrafficLightsStep(this, minTime, maxTime, changeMetric, waitFlowBalance, makeRed);
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

            Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nodeId, ref NetNode node) {
                Constants.ManagerFactory.TrafficLightManager.AddTrafficLight(NodeId, ref node);
                return true;
            });

            foreach (TimedTrafficLightsStep step in Steps) {
                foreach (KeyValuePair<ushort, ICustomSegmentLights> e in step.CustomSegmentLights) {
                    e.Value.Housekeeping(true, true);
                }
            }

            CheckInvalidPedestrianLights();

            CurrentStep = stepIndex;
            Steps[stepIndex].Start();
            Steps[stepIndex].UpdateLiveLights();

            started = true;
        }

        private void CheckInvalidPedestrianLights() {
            ICustomSegmentLightsManager customTrafficLightsManager = Constants.ManagerFactory.CustomSegmentLightsManager;

            //Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
            for (int i = 0; i < 8; ++i) {
                ushort segmentId = 0;
                Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                    segmentId = node.GetSegment(i);
                    return true;
                });

                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);

                ICustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);
                if (lights == null) {
                    Log.Warning($"TimedTrafficLights.CheckInvalidPedestrianLights() @ node {NodeId}: Could not retrieve segment lights for segment {segmentId} @ start {startNode}.");
                    continue;
                }

                //Log._Debug($"Checking seg. {segmentId} @ {NodeId}.");
                bool needsAlwaysGreenPedestrian = true;
                int s = 0;
                foreach (TimedTrafficLightsStep step in Steps) {
                    //Log._Debug($"Checking step {s}, seg. {segmentId} @ {NodeId}.");
                    if (!step.CustomSegmentLights.ContainsKey(segmentId)) {
                        //Log._Debug($"Step {s} @ {NodeId} does not contain a segment light for seg. {segmentId}.");
                        ++s;
                        continue;
                    }
                    //Log._Debug($"Checking step {s}, seg. {segmentId} @ {NodeId}: {step.segmentLights[segmentId].PedestrianLightState} (pedestrianLightState={step.segmentLights[segmentId].pedestrianLightState}, ManualPedestrianMode={step.segmentLights[segmentId].ManualPedestrianMode}, AutoPedestrianLightState={step.segmentLights[segmentId].AutoPedestrianLightState}");
                    if (step.CustomSegmentLights[segmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
                        //Log._Debug($"Step {s} @ {NodeId} has a green ped. light @ seg. {segmentId}.");
                        needsAlwaysGreenPedestrian = false;
                        break;
                    }
                    ++s;
                }
                //Log._Debug($"Setting InvalidPedestrianLight of seg. {segmentId} @ {NodeId} to {needsAlwaysGreenPedestrian}.");
                lights.InvalidPedestrianLight = needsAlwaysGreenPedestrian;
            }
        }

        private void ClearInvalidPedestrianLights() {
            ICustomSegmentLightsManager customTrafficLightsManager = Constants.ManagerFactory.CustomSegmentLightsManager;

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = 0;
                Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                    segmentId = node.GetSegment(i);
                    return true;
                });

                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);

                ICustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(segmentId, startNode);
                if (lights == null) {
                    Log.Warning($"TimedTrafficLights.ClearInvalidPedestrianLights() @ node {NodeId}: Could not retrieve segment lights for segment {segmentId} @ start {startNode}.");
                    continue;
                }
                lights.InvalidPedestrianLight = false;
            }
        }

        public void RemoveNodeFromGroup(ushort otherNodeId) {
            // TODO currently, this method must be called for each node in the node group individually

            NodeGroup.Remove(otherNodeId);
            if (NodeGroup.Count <= 0) {
                Constants.ManagerFactory.TrafficLightSimulationManager.RemoveNodeFromSimulation(NodeId, true, false);
                return;
            }
            MasterNodeId = NodeGroup[0];
        }

        // TODO improve & remove
        public bool Housekeeping() {
            // TODO currently, this method must be called for each node in the node group individually
            //Log._Debug($"Housekeeping timed light @ {NodeId}");

            if (NodeGroup == null || NodeGroup.Count <= 0) {
                Stop();
                return false;
            }

            //Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
            MasterNodeId = NodeGroup[0];

            if (IsStarted())
                CheckInvalidPedestrianLights();

            int i = 0;
            foreach (TimedTrafficLightsStep step in Steps) {
                foreach (CustomSegmentLights lights in step.CustomSegmentLights.Values) {
                    //Log._Debug($"----- Housekeeping timed light at step {i}, seg. {lights.SegmentId} @ {NodeId}");
                    Constants.ManagerFactory.CustomSegmentLightsManager.GetOrLiveSegmentLights(lights.SegmentId, lights.StartNode).Housekeeping(true, true);
                    lights.Housekeeping(true, true);
                }
                ++i;
            }

            return true;
        }

        public void MoveStep(int oldPos, int newPos) {
            // TODO currently, this method must be called for each node in the node group individually

            var oldStep = Steps[oldPos];

            Steps.RemoveAt(oldPos);
            Steps.Insert(newPos, oldStep);
        }

        public void Stop() {
            // TODO currently, this method must be called for each node in the node group individually

            started = false;
            foreach (TimedTrafficLightsStep step in Steps) {
                step.Reset();
            }
            ClearInvalidPedestrianLights();
        }

        ~TimedTrafficLights() {
            Destroy();
        }

        public void Destroy() {
            // TODO  currently, this method must be called for each node in the node group individually

            started = false;
            DestroySegmentEnds();
            Steps = null;
            NodeGroup = null;
        }

        public bool IsStarted() {
            // TODO currently, this method must be called for each node in the node group individually

            return started;
        }

        public int NumSteps() {
            // TODO currently, this method must be called for each node in the node group individually

            return Steps.Count;
        }

        public ITimedTrafficLightsStep GetStep(int stepId) {
            // TODO currently, this method must be called for each node in the node group individually

            return Steps[stepId];
        }

        public void SimulationStep() {
            // TODO this method is currently called on each node, but should be called on the master node only

#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;
#endif

            if (!IsMasterNode() || !IsStarted()) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} isMasterNode={IsMasterNode()} IsStarted={IsStarted()}");
#endif
                return;
            }
            // we are the master node

            /*if (!housekeeping()) {
#if DEBUGTTL
                    Log.Warning($"TTL SimStep: *STOP* NodeId={NodeId} Housekeeping detected that this timed traffic light has become invalid: {NodeId}.");
#endif
                    Stop();
                    return;
            }*/

#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (1)");
#endif
#if BENCHMARK
			//using (var bm = new Benchmark(null, "SetLights.1")) {
#endif
            SetLights();
#if BENCHMARK
			//}
#endif

#if BENCHMARK
			//using (var bm = new Benchmark(null, "StepDone")) {
#endif
            if (!Steps[CurrentStep].StepDone(true)) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}) is not done.");
#endif
                return;
            }
#if BENCHMARK
			//}
#endif

            // step is done

#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} Setting lights (2)");
#endif

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
            if (Steps[CurrentStep].NextStepRefIndex < 0) {
#if DEBUGTTL
                if (debug) {
                    Log._Debug($"TimedTrafficLights.SimulationStep(): Step {CurrentStep} is done at timed light {NodeId}. Determining next step.");
                }
#endif
                // next step has not yet identified yet. check for minTime=0 steps
                int nextStepIndex = (CurrentStep + 1) % NumSteps();
                if (Steps[nextStepIndex].MinTime == 0 && Steps[nextStepIndex].ChangeMetric == Steps[CurrentStep].ChangeMetric) {
#if BENCHMARK
					//using (var bm = new Benchmark(null, "bestNextStepIndex")) {
#endif

                    // next step has minTime=0. calculate flow/wait ratios and compare.
                    int prevStepIndex = CurrentStep;

                    // Steps[CurrentStep].minFlow - Steps[CurrentStep].maxWait
                    float maxWaitFlowDiff = Steps[CurrentStep].GetMetric(Steps[CurrentStep].CurrentFlow, Steps[CurrentStep].CurrentWait);
                    if (float.IsNaN(maxWaitFlowDiff))
                        maxWaitFlowDiff = float.MinValue;
                    int bestNextStepIndex = prevStepIndex;

#if DEBUGTTL
                    if (debug) {
                        Log._Debug($"TimedTrafficLights.SimulationStep(): Next step {nextStepIndex} has minTime = 0 at timed light {NodeId}. Old step {CurrentStep} has waitFlowDiff={maxWaitFlowDiff} (flow={Steps[CurrentStep].CurrentFlow}, wait={Steps[CurrentStep].CurrentWait}).");
                    }
#endif

                    while (nextStepIndex != prevStepIndex) {
                        float wait;
                        float flow;
                        Steps[nextStepIndex].CalcWaitFlow(false, nextStepIndex, out wait, out flow);

                        //float flowWaitDiff = flow - wait;
                        float flowWaitDiff = Steps[nextStepIndex].GetMetric(flow, wait);
                        if (flowWaitDiff > maxWaitFlowDiff) {
                            maxWaitFlowDiff = flowWaitDiff;
                            bestNextStepIndex = nextStepIndex;
                        }

#if DEBUGTTL
                        if (debug) {
                            Log._Debug($"TimedTrafficLights.SimulationStep(): Checking upcoming step {nextStepIndex} @ node {NodeId}: flow={flow} wait={wait} minTime={Steps[nextStepIndex].MinTime}. bestWaitFlowDiff={bestNextStepIndex}, bestNextStepIndex={bestNextStepIndex}");
                        }
#endif

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
#if DEBUGTTL
                        if (debug) {
                            Log._Debug($"TimedTrafficLights.SimulationStep(): Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) equals CurrentStep @ node {NodeId}.");
                        }
#endif

                        // restart the current step
                        foreach (ushort slaveNodeId in NodeGroup) {
                            if (! tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                                continue;
                            }

                            ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                            slaveTTL.GetStep(CurrentStep).Start(CurrentStep);
                            slaveTTL.GetStep(CurrentStep).UpdateLiveLights();
                        }
                        return;
                    } else {
#if DEBUGTTL
                        if (debug) {
                            Log._Debug($"TimedTrafficLights.SimulationStep(): Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) does not equal CurrentStep @ node {NodeId}.");
                        }
#endif

                        // set next step reference index for assuring a correct end transition
                        foreach (ushort slaveNodeId in NodeGroup) {
                            if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                                continue;
                            }

                            ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                            slaveTTL.GetStep(CurrentStep).NextStepRefIndex = bestNextStepIndex;
                        }
                    }
#if BENCHMARK
					//}
#endif
                } else {
                    Steps[CurrentStep].NextStepRefIndex = nextStepIndex;
                }
            }

#if BENCHMARK
			//using (var bm = new Benchmark(null, "SetLights.2")) {
#endif
            SetLights(); // check if this is needed
#if BENCHMARK
			//}
#endif

#if BENCHMARK
			//using (var bm = new Benchmark(null, "IsEndTransitionDone")) {
#endif
            if (!Steps[CurrentStep].IsEndTransitionDone()) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}): end transition is not done.");
#endif
                return;
            }
#if BENCHMARK
			//}
#endif

            // ending transition (yellow) finished

#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} ending transition done. NodeGroup={string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray())}, nodeId={NodeId}, NumSteps={NumSteps()}");
#endif

#if BENCHMARK
			//using (var bm = new Benchmark(null, "ChangeStep")) {
#endif
            // change step
            int newStepIndex = Steps[CurrentStep].NextStepRefIndex;
            int oldStepIndex = CurrentStep;

            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                slaveTTL.CurrentStep = newStepIndex;

#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={slaveNodeId} setting lights of next step: {CurrentStep}");
#endif

                slaveTTL.GetStep(oldStepIndex).NextStepRefIndex = -1;
                slaveTTL.GetStep(newStepIndex).Start(oldStepIndex);
                slaveTTL.GetStep(newStepIndex).UpdateLiveLights();
            }
#if BENCHMARK
			//}
#endif
        }

        public void SetLights(bool noTransition=false) {
            if (Steps.Count <= 0) {
                return;
            }

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            // set lights
            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                slaveTTL.GetStep(CurrentStep).UpdateLiveLights(noTransition);
            }
        }

        public void SkipStep(bool setLights=true, int prevStepRefIndex=-1) {
            if (!IsMasterNode())
                return;

            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            var newCurrentStep = (CurrentStep + 1) % NumSteps();
            foreach (ushort slaveNodeId in NodeGroup) {
                if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                    continue;
                }

                ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;

                slaveTTL.GetStep(CurrentStep).SetStepDone();
                slaveTTL.CurrentStep = newCurrentStep;
                slaveTTL.GetStep(newCurrentStep).Start(prevStepRefIndex);
                if (setLights) {
                    slaveTTL.GetStep(newCurrentStep).UpdateLiveLights();
                }
            }
        }

        public long CheckNextChange(ushort segmentId,
                                    bool startNode,
                                    API.Traffic.Enums.ExtVehicleType vehicleType,
                                    int lightType) {
            var curStep = CurrentStep;
            var nextStep = (CurrentStep + 1) % NumSteps();
            var numFrames = Steps[CurrentStep].MaxTimeRemaining();

            RoadBaseAI.TrafficLightState currentState;
            ICustomSegmentLights segmentLights = Constants.ManagerFactory.CustomSegmentLightsManager.GetSegmentLights(segmentId, startNode, false);
            if (segmentLights == null) {
                Log._Debug($"CheckNextChange: No segment lights at node {NodeId}, segment {segmentId}");
                return 99;
            }
            ICustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
            if (segmentLight == null) {
                Log._Debug($"CheckNextChange: No segment light at node {NodeId}, segment {segmentId}");
                return 99;
            }

            if (lightType == 0)
                currentState = segmentLight.LightMain;
            else if (lightType == 1)
                currentState = segmentLight.LightLeft;
            else if (lightType == 2)
                currentState = segmentLight.LightRight;
            else
                currentState = segmentLights.PedestrianLightState == null ? RoadBaseAI.TrafficLightState.Red : (RoadBaseAI.TrafficLightState)segmentLights.PedestrianLightState;


            while (true) {
                if (nextStep == curStep) {
                    numFrames = 99;
                    break;
                }

                var light = Steps[nextStep].GetLightState(segmentId, vehicleType, lightType);

                if (light != currentState) {
                    break;
                } else {
                    numFrames += Steps[nextStep].MaxTime;
                }

                nextStep = (nextStep + 1) % NumSteps();
            }

            return numFrames;
        }

        public void ResetSteps() {
            Steps.Clear();
        }

        public void RemoveStep(int id) {
            Steps.RemoveAt(id);
        }

        public void OnGeometryUpdate() {
            Log._Trace($"TimedTrafficLights.OnGeometryUpdate: called for timed traffic light @ {NodeId}.");

            Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                UpdateDirections(ref node);
                UpdateSegmentEnds(ref node);
                return true;
            });

            if (NumSteps() <= 0) {
                Log._Debug($"TimedTrafficLights.OnGeometryUpdate: no steps @ {NodeId}");
                return;
            }

            Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nId, ref NetNode node) {
                BackUpInvalidStepSegments(ref node);
                HandleNewSegments(ref node);
                return true;
            });
        }

        /// <summary>
        /// Moves all custom segment lights that are associated with an invalid segment to a special container for later reuse
        /// </summary>
        private void BackUpInvalidStepSegments(ref NetNode node) {
#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;

            if (debug)
                Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: called for timed traffic light @ {NodeId}");
#endif

            ICollection<ushort> validSegments = new HashSet<ushort>();
            for (int k = 0; k < 8; ++k) {
                ushort segmentId = node.GetSegment(k);

                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);

                validSegments.Add(segmentId);
            }

#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: valid segments @ {NodeId}: {validSegments.CollectionToString()}");
#endif

            int i = 0;
            foreach (TimedTrafficLightsStep step in Steps) {
                ICollection<ushort> invalidSegmentIds = new HashSet<ushort>();
                foreach (KeyValuePair<ushort, ICustomSegmentLights> e in step.CustomSegmentLights) {
                    if (! validSegments.Contains(e.Key)) {
                        step.InvalidSegmentLights.AddLast(e.Value);
                        invalidSegmentIds.Add(e.Key);
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: Detected invalid segment @ step {i}, node {NodeId}: {e.Key}");
#endif
                    }
                }

                foreach (ushort invalidSegmentId in invalidSegmentIds) {
#if DEBUGTTL
                    if (debug)
                        Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: Removing invalid segment {invalidSegmentId} from step {i} @ node {NodeId}");
#endif
                    step.CustomSegmentLights.Remove(invalidSegmentId);
                }

#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments finished for TTL step {i} @ node {NodeId}: step.CustomSegmentLights={step.CustomSegmentLights.DictionaryToString()} step.InvalidSegmentLights={step.InvalidSegmentLights.CollectionToString()}");
#endif

                ++i;
            }
        }

        /// <summary>
        /// Processes new segments and adds them to the steps. If steps contain a custom light
        /// for an old invalid segment, this light is being reused for the new segment.
        /// </summary>
        /// <param name="nodeGeo"></param>
        private void HandleNewSegments(ref NetNode node) {
#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;
#endif

            ICustomSegmentLightsManager customTrafficLightsManager = Constants.ManagerFactory.CustomSegmentLightsManager;
            ITrafficPriorityManager prioMan = Constants.ManagerFactory.TrafficPriorityManager;

            //Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
            for (int k = 0; k < 8; ++k) {
                ushort segmentId = node.GetSegment(k);

                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);

#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.HandleNewSegments: handling existing seg. {segmentId} @ {NodeId}");
#endif

                if (Steps[0].CustomSegmentLights.ContainsKey(segmentId)) {
                    continue;
                }

                // segment was created
                RotationOffset = 0;
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.HandleNewSegments: New segment detected: {segmentId} @ {NodeId}");
#endif

                int stepIndex = -1;
                foreach (TimedTrafficLightsStep step in Steps) {
                    ++stepIndex;

                    LinkedListNode<ICustomSegmentLights> lightsToReuseNode = step.InvalidSegmentLights.First;
                    if (lightsToReuseNode == null) {
                        // no old segment found: create a fresh custom light
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"TimedTrafficLights.HandleNewSegments: Adding new segment {segmentId} to node {NodeId} without reusing old segment");
#endif
                        if (! step.AddSegment(segmentId, startNode, true)) {
#if DEBUGTTL
                            if (debug)
                                Log.Warning($"TimedTrafficLights.HandleNewSegments: Failed to add segment {segmentId} @ start {startNode} to node {NodeId}");
#endif
                        }
                    } else {
                        // reuse old lights
                        step.InvalidSegmentLights.RemoveFirst();
                        ICustomSegmentLights lightsToReuse = lightsToReuseNode.Value;

#if DEBUGTTL
                        if (debug)
                            Log._Debug($"Replacing old segment @ {NodeId} with new segment {segmentId}");
#endif
                        lightsToReuse.Relocate(segmentId, startNode);
                        step.SetSegmentLights(segmentId, lightsToReuse);
                    }
                }
            }
        }

        public ITimedTrafficLights MasterLights() {
            return TrafficLightSimulationManager.Instance.TrafficLightSimulations[MasterNodeId].timedLight;
        }

        public void SetTestMode(bool testMode) {
            this.TestMode = false;
            if (!IsStarted())
                return;
            this.TestMode = testMode;
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
#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;
#endif

            if (! Constants.ServiceFactory.NetService.IsSegmentValid(segmentId)) {
#if DEBUGTTL
                if (debug)
                    Log.Error($"TimedTrafficLights.ChangeLightMode: Segment {segmentId} is invalid");
#endif
                return;
            }

            bool? startNode = Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);
            if (startNode == null) {
                return;
            }

            foreach (TimedTrafficLightsStep step in Steps) {
                step.ChangeLightMode(segmentId, vehicleType, mode);
            }
            Constants.ManagerFactory.CustomSegmentLightsManager.SetLightMode(segmentId, (bool)startNode, vehicleType, mode);
        }

        public void Join(ITimedTrafficLights otherTimedLight) {
            TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

            if (NumSteps() < otherTimedLight.NumSteps()) {
                // increase the number of steps at our timed lights
                for (int i = NumSteps(); i < otherTimedLight.NumSteps(); ++i) {
                    ITimedTrafficLightsStep otherStep = otherTimedLight.GetStep(i);
                    foreach (ushort slaveNodeId in NodeGroup) {
                        if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                            continue;
                        }

                        ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                        slaveTTL.AddStep(otherStep.MinTime, otherStep.MaxTime, otherStep.ChangeMetric, otherStep.WaitFlowBalance, true);
                    }
                }
            } else {
                // increase the number of steps at their timed lights
                for (int i = otherTimedLight.NumSteps(); i < NumSteps(); ++i) {
                    ITimedTrafficLightsStep ourStep = GetStep(i);
                    foreach (ushort slaveNodeId in otherTimedLight.NodeGroup) {
                        if (!tlsMan.TrafficLightSimulations[slaveNodeId].IsTimedLight()) {
                            continue;
                        }

                        ITimedTrafficLights slaveTTL = tlsMan.TrafficLightSimulations[slaveNodeId].timedLight;
                        slaveTTL.AddStep(ourStep.MinTime, ourStep.MaxTime, ourStep.ChangeMetric, ourStep.WaitFlowBalance, true);
                    }
                }
            }

            // join groups and re-defined master node, determine mean min/max times & mean wait-flow-balances
            HashSet<ushort> newNodeGroupSet = new HashSet<ushort>();
            newNodeGroupSet.UnionWith(NodeGroup);
            newNodeGroupSet.UnionWith(otherTimedLight.NodeGroup);
            List<ushort> newNodeGroup = new List<ushort>(newNodeGroupSet);
            ushort newMasterNodeId = newNodeGroup[0];

            int[] minTimes = new int[NumSteps()];
            int[] maxTimes = new int[NumSteps()];
            float[] waitFlowBalances = new float[NumSteps()];
            StepChangeMetric?[] stepChangeMetrics = new StepChangeMetric?[NumSteps()];

            foreach (ushort timedNodeId in newNodeGroup) {
                if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                    continue;
                }
                ITimedTrafficLights ttl = tlsMan.TrafficLightSimulations[timedNodeId].timedLight;

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
                    waitFlowBalances[i] = Math.Max(0.001f, waitFlowBalances[i] / (float)newNodeGroup.Count);
                }
            }

            // apply means & reset
            foreach (ushort timedNodeId in newNodeGroup) {
                if (!tlsMan.TrafficLightSimulations[timedNodeId].IsTimedLight()) {
                    continue;
                }

                ITimedTrafficLights ttl = tlsMan.TrafficLightSimulations[timedNodeId].timedLight;

                ttl.Stop();
                ttl.TestMode = false;
                for (int i = 0; i < NumSteps(); ++i) {
                    ttl.GetStep(i).MinTime = minTimes[i];
                    ttl.GetStep(i).MaxTime = maxTimes[i];
                    ttl.GetStep(i).WaitFlowBalance = waitFlowBalances[i];
                    ttl.GetStep(i).ChangeMetric = stepChangeMetrics[i] == null ? StepChangeMetric.Default : (StepChangeMetric)stepChangeMetrics[i];
                }
            }
        }

        private void UpdateSegmentEnds(ref NetNode node) {
#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;

            if (debug)
                Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: called for node {NodeId}");
#endif

            ISegmentEndManager segEndMan = Constants.ManagerFactory.SegmentEndManager;

            ICollection<ISegmentEndId> segmentEndsToDelete = new HashSet<ISegmentEndId>();
            // update currently set segment ends
            foreach (ISegmentEndId endId in segmentEndIds) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: updating existing segment end {endId} for node {NodeId}");
#endif
                if (!segEndMan.UpdateSegmentEnd(endId)) {
#if DEBUGTTL
                    if (debug)
                        Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: segment end {endId} @ node {NodeId} is invalid");
#endif
                    segmentEndsToDelete.Add(endId);
                } else {
                    ISegmentEnd end = segEndMan.GetSegmentEnd(endId);
                    if (end.NodeId != NodeId) {
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Segment end {end} is valid and updated but does not belong to TTL node {NodeId} anymore.");
#endif
                        segmentEndsToDelete.Add(endId);
                    } else {
#if DEBUGTTL
                        if (debug)
                            Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: segment end {endId} @ node {NodeId} is valid");
#endif
                    }
                }
            }

            // remove all invalid segment ends
            foreach (ISegmentEndId endId in segmentEndsToDelete) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Removing invalid segment end {endId} @ node {NodeId}");
#endif
                segmentEndIds.Remove(endId);
            }

            // set up new segment ends
#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Setting up new segment ends @ node {NodeId}.");
#endif

            for (int i = 0; i < 8; ++i) {
                ushort segmentId = node.GetSegment(i);

                if (segmentId == 0) {
                    continue;
                }

                bool startNode = (bool)Constants.ServiceFactory.NetService.IsStartNode(segmentId, NodeId);
                ISegmentEndId endId = new SegmentEndId(segmentId, startNode);

                if (segmentEndIds.Contains(endId)) {
#if DEBUGTTL
                    if (debug)
                        Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Node {NodeId} already knows segment {segmentId}");
#endif
                    continue;
                }

#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Adding segment {segmentId} to node {NodeId}");
#endif
                ISegmentEnd end = segEndMan.GetOrAddSegmentEnd(segmentId, startNode);
                if (end != null) {
                    segmentEndIds.Add(end);
                } else {
#if DEBUGTTL
                    if (debug)
                        Log.Warning($"TimedTrafficLights.UpdateSegmentEnds: Failed to add segment end {segmentId} @ {startNode} to node {NodeId}: GetOrAddSegmentEnd returned null.");
#endif
                }
            }
#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: finished for node {NodeId}: {segmentEndIds.CollectionToString()}");
#endif
        }

        private void DestroySegmentEnds() {
#if DEBUGTTL
            bool debug = DebugSwitch.TimedTrafficLights.Get() && DebugSettings.NodeId == NodeId;

            if (debug)
                Log._Debug($"TimedTrafficLights.DestroySegmentEnds: Destroying segment ends @ node {NodeId}");
#endif
            foreach (ISegmentEndId endId in segmentEndIds) {
#if DEBUGTTL
                if (debug)
                    Log._Debug($"TimedTrafficLights.DestroySegmentEnds: Destroying segment end {endId} @ node {NodeId}");
#endif
                // TODO only remove if no priority sign is located at the segment end (although this is currently not possible)
                Constants.ManagerFactory.SegmentEndManager.RemoveSegmentEnd(endId);
            }
            segmentEndIds.Clear();
#if DEBUGTTL
            if (debug)
                Log._Debug($"TimedTrafficLights.DestroySegmentEnds: finished for node {NodeId}");
#endif
        }
    }
}
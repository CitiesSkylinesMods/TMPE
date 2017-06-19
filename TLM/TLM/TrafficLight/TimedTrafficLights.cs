#define DEBUGTTLx

using System;
using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.Custom.AI;
using TrafficManager.Geometry;
using TrafficManager.Traffic;
using TrafficManager.Manager;
using System.Linq;
using TrafficManager.Util;
using System.Threading;
using TrafficManager.State;
using GenericGameBridge.Service;
using CSUtil.Commons;

namespace TrafficManager.TrafficLight {
	// TODO [version 1.10] define TimedTrafficLights per node group, not per individual nodes
	// TODO class marked for complete rework in version 1.10
	public class TimedTrafficLights: IObserver<NodeGeometry> {
		public enum FlowWaitCalcMode {
			/// <summary>
			/// traffic measurements are averaged
			/// </summary>
			Mean,
			/// <summary>
			/// traffic measurements are summed up
			/// </summary>
			Total
		}

		public ushort NodeId {
			get; private set;
		}

		/// <summary>
		/// In case the traffic light is set for a group of nodes, the master node decides
		/// if all member steps are done.
		/// </summary>
		internal ushort masterNodeId;

		public List<TimedTrafficLightsStep> Steps = new List<TimedTrafficLightsStep>();
		public int CurrentStep = 0;

		public List<ushort> NodeGroup;
		private bool testMode = false;

		private bool started = false;

		/// <summary>
		/// Indicates the total amount and direction of rotation that was applied to this timed traffic light
		/// </summary>
		public short RotationOffset { get; private set; } = 0;

		private IDisposable nodeGeometryUnsubscriber = null;
		private object geoLock = new object();

		public IDictionary<ushort, IDictionary<ushort, ArrowDirection>> Directions { get; private set; } = null;

		/// <summary>
		/// Segment ends that were set up for this timed traffic light
		/// </summary>
		private ICollection<SegmentEndId> segmentEndIds = new HashSet<SegmentEndId>();

		public override string ToString() {
			return $"[TimedTrafficLights\n" +
				"\t" + $"NodeId = {NodeId}\n" +
				"\t" + $"masterNodeId = {masterNodeId}\n" +
				"\t" + $"Steps = {Steps.CollectionToString()}\n" +
				"\t" + $"NodeGroup = {NodeGroup.CollectionToString()}\n" +
				"\t" + $"testMode = {testMode}\n" +
				"\t" + $"started = {started}\n" +
				"\t" + $"Directions = {Directions.DictionaryToString()}\n" +
				"\t" + $"segmentEndIds = {segmentEndIds.CollectionToString()}\n" +
				"TimedTrafficLights]";
		}

		public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup) {
			this.NodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];

			UpdateDirections(NodeGeometry.Get(nodeId));
			UpdateSegmentEnds();
			SubscribeToNodeGeometry();

			started = false;
		}

		private TimedTrafficLights() {

		}

		internal void PasteSteps(TimedTrafficLights sourceTimedLight) {
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

			for (int stepIndex = 0; stepIndex < sourceTimedLight.Steps.Count; ++stepIndex) {
				TimedTrafficLightsStep sourceStep = sourceTimedLight.Steps[stepIndex];
				TimedTrafficLightsStep targetStep = new TimedTrafficLightsStep(this, sourceStep.minTime, sourceStep.maxTime, sourceStep.waitFlowBalance);
				for (int i = 0; i < clockSortedSourceSegmentIds.Count; ++i) {
					ushort sourceSegmentId = clockSortedSourceSegmentIds[i];
					ushort targetSegmentId = clockSortedTargetSegmentIds[i];

					SegmentGeometry segGeo = SegmentGeometry.Get(targetSegmentId);
					if (segGeo == null) {
						throw new Exception($"TimedTrafficLights.PasteSteps: No geometry information available for segment {targetSegmentId}");
					}

					bool targetStartNode = segGeo.StartNodeId() == NodeId;

					CustomSegmentLights sourceLights = sourceStep.CustomSegmentLights[sourceSegmentId];
					CustomSegmentLights targetLights = (CustomSegmentLights)sourceLights.Clone(targetStep, false);
					targetStep.SetSegmentLights(targetSegmentId, targetLights);
					CustomSegmentLightsManager.Instance.ApplyLightModes(targetSegmentId, targetStartNode, targetLights);
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
					CustomSegmentLights bufferedLights = null;
					for (int sourceIndex = 0; sourceIndex < clockSortedSegmentIds.Count; ++sourceIndex) {
						ushort sourceSegmentId = clockSortedSegmentIds[sourceIndex];
						int targetIndex = (sourceIndex + 1) % clockSortedSegmentIds.Count;
						ushort targetSegmentId = clockSortedSegmentIds[targetIndex];

						SegmentGeometry targetSegGeo = SegmentGeometry.Get(targetSegmentId); // should never fail here

						Log._Debug($"TimedTrafficLights.Rotate({dir}) @ node {NodeId}: Moving light @ seg. {sourceSegmentId} to seg. {targetSegmentId} @ step {stepIndex}");

						CustomSegmentLights sourceLights = sourceIndex == 0 ? step.RemoveSegmentLights(sourceSegmentId) : bufferedLights;
						if (sourceLights == null) {
							throw new Exception($"TimedTrafficLights.Rotate({dir}): Error occurred while copying custom lights from {sourceSegmentId} to {targetSegmentId} @ step {stepIndex}: sourceLights is null @ sourceIndex={sourceIndex}, targetIndex={targetIndex}");
						}
						bufferedLights = step.RemoveSegmentLights(targetSegmentId);
						sourceLights.Relocate(targetSegmentId, targetSegGeo.StartNodeId() == NodeId);
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

		private void UpdateDirections(NodeGeometry nodeGeo) {
			Log._Debug($">>>>> TimedTrafficLights.UpdateDirections: called for node {NodeId}");
			Directions = new TinyDictionary<ushort, IDictionary<ushort, ArrowDirection>>();
			foreach (SegmentEndGeometry srcSegEndGeo in nodeGeo.SegmentEndGeometries) {
				if (srcSegEndGeo == null)
					continue;
				Log._Debug($"TimedTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}");

				SegmentGeometry srcSegGeo = srcSegEndGeo.GetSegmentGeometry();
				if (srcSegGeo == null) {
					continue;
				}
				IDictionary<ushort, ArrowDirection> dirs = new TinyDictionary<ushort, ArrowDirection>();
				Directions.Add(srcSegEndGeo.SegmentId, dirs);
				foreach (SegmentEndGeometry trgSegEndGeo in nodeGeo.SegmentEndGeometries) {
					if (trgSegEndGeo == null)
						continue;

					ArrowDirection dir = srcSegGeo.GetDirection(trgSegEndGeo.SegmentId, srcSegEndGeo.StartNode);
					if (dir == ArrowDirection.None) {
						Log.Error($"TimedTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}, target segment {trgSegEndGeo.SegmentId}: Invalid direction {dir}");
						continue;
					}
					dirs.Add(trgSegEndGeo.SegmentId, dir);
					Log._Debug($"TimedTrafficLights.UpdateDirections: Processing source segment {srcSegEndGeo.SegmentId}, target segment {trgSegEndGeo.SegmentId}: adding dir {dir}");
				}
			}
			Log._Debug($"<<<<< TimedTrafficLights.UpdateDirections: finished for node {NodeId}: {Directions.DictionaryToString()}");
		}

		private void UnsubscribeFromNodeGeometry() {
			if (nodeGeometryUnsubscriber != null) {
				try {
					Monitor.Enter(geoLock);

					nodeGeometryUnsubscriber.Dispose();
					nodeGeometryUnsubscriber = null;
				} finally {
					Monitor.Exit(geoLock);
				}
			}
		}

		private void SubscribeToNodeGeometry() {
			if (nodeGeometryUnsubscriber != null) {
				return;
			}

			try {
				Monitor.Enter(geoLock);

				nodeGeometryUnsubscriber = NodeGeometry.Get(NodeId).Subscribe(this);
			} finally {
				Monitor.Exit(geoLock);
			}
		}

		public void OnUpdate(NodeGeometry geometry) {
			// not required since TrafficLightSimulation handles this for us: OnGeometryUpdate() is being called.
			// TODO improve
		}

		public bool IsMasterNode() {
			return masterNodeId == NodeId;
		}

		public TimedTrafficLightsStep AddStep(int minTime, int maxTime, float waitFlowBalance, bool makeRed = false) {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			if (minTime < 0)
				minTime = 0;
			if (maxTime <= 0)
				maxTime = 1;
			if (maxTime < minTime)
				maxTime = minTime;

			TimedTrafficLightsStep step = new TimedTrafficLightsStep(this, minTime, maxTime, waitFlowBalance, makeRed);
			Steps.Add(step);
			return step;
		}

		public void Start() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			/*if (!housekeeping())
				return;*/

			Constants.ServiceFactory.NetService.ProcessNode(NodeId, delegate (ushort nodeId, ref NetNode node) {
				TrafficLightManager.Instance.AddTrafficLight(NodeId, ref node);
				return true;
			});

			foreach (TimedTrafficLightsStep step in Steps) {
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.CustomSegmentLights) {
					e.Value.housekeeping(true, true);
				}
			}

			CheckInvalidPedestrianLights();

			CurrentStep = 0;
			Steps[0].Start();
			Steps[0].UpdateLiveLights();

			started = true;
		}

		private void CheckInvalidPedestrianLights() {
			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
			NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

			//Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null) {
					continue;
				}

				CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);
				if (lights == null) {
					Log.Warning($"TimedTrafficLights.CheckInvalidPedestrianLights() @ node {NodeId}: Could not retrieve segment lights for segment {end.SegmentId} @ start {end.StartNode}.");
					continue;
				}

				//Log._Debug($"Checking seg. {segmentId} @ {NodeId}.");
				bool needsAlwaysGreenPedestrian = true;
				int i = 0;
				foreach (TimedTrafficLightsStep step in Steps) {
					//Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}.");
					if (!step.CustomSegmentLights.ContainsKey(end.SegmentId)) {
						//Log._Debug($"Step {i} @ {NodeId} does not contain a segment light for seg. {segmentId}.");
						++i;
						continue;
					}
					//Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}: {step.segmentLights[segmentId].PedestrianLightState} (pedestrianLightState={step.segmentLights[segmentId].pedestrianLightState}, ManualPedestrianMode={step.segmentLights[segmentId].ManualPedestrianMode}, AutoPedestrianLightState={step.segmentLights[segmentId].AutoPedestrianLightState}");
					if (step.CustomSegmentLights[end.SegmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
						//Log._Debug($"Step {i} @ {NodeId} has a green ped. light @ seg. {segmentId}.");
						needsAlwaysGreenPedestrian = false;
						break;
					}
					++i;
				}
				//Log._Debug($"Setting InvalidPedestrianLight of seg. {segmentId} @ {NodeId} to {needsAlwaysGreenPedestrian}.");
				lights.InvalidPedestrianLight = needsAlwaysGreenPedestrian;
			}
		}

		private void ClearInvalidPedestrianLights() {
			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

			NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

			//Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null) {
					continue;
				}

				CustomSegmentLights lights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);
				if (lights == null) {
					Log.Warning($"TimedTrafficLights.ClearInvalidPedestrianLights() @ node {NodeId}: Could not retrieve segment lights for segment {end.SegmentId} @ start {end.StartNode}.");
					continue;
				}
				lights.InvalidPedestrianLight = false;
			}
		}

		internal void RemoveNodeFromGroup(ushort otherNodeId) {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			NodeGroup.Remove(otherNodeId);
			if (NodeGroup.Count <= 0) {
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, true, false);
				return;
			}
			masterNodeId = NodeGroup[0];
		}

		internal bool housekeeping() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually
			//Log._Debug($"Housekeeping timed light @ {NodeId}");

			if (NodeGroup == null || NodeGroup.Count <= 0) {
				Stop();
				return false;
			}

			//Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
			masterNodeId = NodeGroup[0];

			if (IsStarted())
				CheckInvalidPedestrianLights();

			int i = 0;
			foreach (TimedTrafficLightsStep step in Steps) {
				foreach (CustomSegmentLights lights in step.CustomSegmentLights.Values) {
					//Log._Debug($"----- Housekeeping timed light at step {i}, seg. {lights.SegmentId} @ {NodeId}");
					lights.housekeeping(true, true);
				}
				++i;
			}

			return true;
		}

		public void MoveStep(int oldPos, int newPos) {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			var oldStep = Steps[oldPos];

			Steps.RemoveAt(oldPos);
			Steps.Insert(newPos, oldStep);
		}

		public void Stop() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			started = false;
			foreach (TimedTrafficLightsStep step in Steps) {
				step.Reset();
			}
			ClearInvalidPedestrianLights();
		}

		~TimedTrafficLights() {
			Destroy();
		}

		internal void Destroy() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			started = false;
			DestroySegmentEnds();
			Steps = null;
			NodeGroup = null;
			UnsubscribeFromNodeGeometry();
		}

		public bool IsStarted() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			return started;
		}

		public int NumSteps() {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			return Steps.Count;
		}

		public TimedTrafficLightsStep GetStep(int stepId) {
			// TODO [version 1.10] currently, this method must be called for each node in the node group individually

			return Steps[stepId];
		}

		public void SimulationStep() {
			// TODO [version 1.10] this method is currently called on each node, but should be called on the master node only

#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;
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
			SetLights();

			if (!Steps[CurrentStep].StepDone(true)) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}) is not done.");
#endif
				return;
			}
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
				if (Steps[nextStepIndex].minTime == 0) {
					// next step has minTime=0. calculate flow/wait ratios and compare.
					int prevStepIndex = CurrentStep;

					float maxWaitFlowDiff = Steps[CurrentStep].minFlow - Steps[CurrentStep].maxWait;
					if (float.IsNaN(maxWaitFlowDiff))
						maxWaitFlowDiff = float.MinValue;
					int bestNextStepIndex = prevStepIndex;

#if DEBUGTTL
					if (debug) {
						Log._Debug($"TimedTrafficLights.SimulationStep(): Next step {nextStepIndex} has minTime = 0 at timed light {NodeId}. Old step {CurrentStep} has waitFlowDiff={maxWaitFlowDiff} (flow={Steps[CurrentStep].minFlow}, wait={Steps[CurrentStep].maxWait}).");
					}
#endif

					while (nextStepIndex != prevStepIndex) {
						float wait;
						float flow;
						Steps[nextStepIndex].calcWaitFlow(false, nextStepIndex, out wait, out flow);

						float flowWaitDiff = flow - wait;
						if (flowWaitDiff > maxWaitFlowDiff) {
							maxWaitFlowDiff = flowWaitDiff;
							bestNextStepIndex = nextStepIndex;
						}

#if DEBUGTTL
						if (debug) {
							Log._Debug($"TimedTrafficLights.SimulationStep(): Checking upcoming step {nextStepIndex} @ node {NodeId}: flow={flow} wait={wait} minTime={Steps[nextStepIndex].minTime}. bestWaitFlowDiff={bestNextStepIndex}, bestNextStepIndex={bestNextStepIndex}");
						}
#endif

						if (Steps[nextStepIndex].minTime != 0) {
							int stepAfterPrev = (prevStepIndex + 1) % NumSteps();
							if (nextStepIndex == stepAfterPrev) {
								// always switch if the next step has a minimum time assigned
								bestNextStepIndex = stepAfterPrev;
							}
							break;
						}

						nextStepIndex = (nextStepIndex + 1) % NumSteps();
					}

					
					if (bestNextStepIndex == CurrentStep) {
#if DEBUGTTL
						if (debug) {
							Log._Debug($"TimedTrafficLights.SimulationStep(): Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) equals CurrentStep @ node {NodeId}.");
						}
#endif

						// restart the current step
						foreach (ushort slaveNodeId in NodeGroup) {
							TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
							if (slaveSim == null || !slaveSim.IsTimedLight()) {
								continue;
							}

							slaveSim.TimedLight.Steps[CurrentStep].Start(CurrentStep);
							slaveSim.TimedLight.Steps[CurrentStep].UpdateLiveLights();
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
							TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
							if (slaveSim == null || !slaveSim.IsTimedLight()) {
								continue;
							}
							TimedTrafficLights timedLights = slaveSim.TimedLight;
							timedLights.Steps[CurrentStep].NextStepRefIndex = bestNextStepIndex;
						}
					}
				} else {
					Steps[CurrentStep].NextStepRefIndex = nextStepIndex;
				}
			}

			SetLights(); // check if this is needed

			if (!Steps[CurrentStep].IsEndTransitionDone()) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}): end transition is not done.");
#endif
				return;
			}
			// ending transition (yellow) finished

#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={NodeId} ending transition done. NodeGroup={string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray())}, nodeId={NodeId}, NumSteps={NumSteps()}");
#endif

			// change step
			int newStepIndex = Steps[CurrentStep].NextStepRefIndex;
			int oldStepIndex = CurrentStep;

			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}
				TimedTrafficLights timedLights = slaveSim.TimedLight;
				timedLights.CurrentStep = newStepIndex;

#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.SimulationStep(): TTL SimStep: NodeId={slaveNodeId} setting lights of next step: {CurrentStep}");
#endif

				timedLights.Steps[oldStepIndex].NextStepRefIndex = -1;
				timedLights.Steps[newStepIndex].Start(oldStepIndex);
				timedLights.Steps[newStepIndex].UpdateLiveLights();
			}
		}

		public void SetLights(bool noTransition=false) {
			if (Steps.Count <= 0) {
				return;
			}

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			// set lights
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					//TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
					continue;
				}
				slaveSim.TimedLight.Steps[CurrentStep].UpdateLiveLights(noTransition);
			}
		}

		public void SkipStep(bool setLights=true, int prevStepRefIndex=-1) {
			if (!IsMasterNode())
				return;

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			var newCurrentStep = (CurrentStep + 1) % NumSteps();
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					continue;
				}

				slaveSim.TimedLight.Steps[CurrentStep].SetStepDone();
				slaveSim.TimedLight.CurrentStep = newCurrentStep;
				slaveSim.TimedLight.Steps[newCurrentStep].Start(prevStepRefIndex);
				if (setLights)
					slaveSim.TimedLight.Steps[newCurrentStep].UpdateLiveLights();
			}
		}

		public long CheckNextChange(ushort segmentId, bool startNode, ExtVehicleType vehicleType, int lightType) {
			var curStep = CurrentStep;
			var nextStep = (CurrentStep + 1) % NumSteps();
			var numFrames = Steps[CurrentStep].MaxTimeRemaining();

			RoadBaseAI.TrafficLightState currentState;
			CustomSegmentLights segmentLights = CustomSegmentLightsManager.Instance.GetSegmentLights(segmentId, startNode, false);
			if (segmentLights == null) {
				Log._Debug($"CheckNextChange: No segment lights at node {NodeId}, segment {segmentId}");
                return 99;
			}
			CustomSegmentLight segmentLight = segmentLights.GetCustomLight(vehicleType);
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

				var light = Steps[nextStep].GetLight(segmentId, vehicleType, lightType);

				if (light != currentState) {
					break;
				} else {
					numFrames += Steps[nextStep].maxTime;
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

		internal void OnGeometryUpdate() {
			NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);
			Log._Debug($"TimedTrafficLights.OnGeometryUpdate: called for timed traffic light @ {NodeId}. nodeGeometry={nodeGeometry}");

			UpdateDirections(nodeGeometry);
			UpdateSegmentEnds();

			if (NumSteps() <= 0) {
				Log._Debug($"TimedTrafficLights.OnGeometryUpdate: no steps @ {NodeId}");
				return;
			}

			BackUpInvalidStepSegments(nodeGeometry);
			HandleNewSegments(nodeGeometry);
		}

		/// <summary>
		/// Moves all custom segment lights that are associated with an invalid segment to a special container for later reuse
		/// </summary>
		private void BackUpInvalidStepSegments(NodeGeometry nodeGeo) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;

			if (debug)
				Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: called for timed traffic light @ {NodeId}");
#endif

			ICollection<ushort> validSegments = new HashSet<ushort>();
			foreach (SegmentEndGeometry end in nodeGeo.SegmentEndGeometries) {
				if (end == null) {
					continue;
				}

				validSegments.Add(end.SegmentId);
			}

#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLights.BackUpInvalidStepSegments: valid segments @ {NodeId}: {validSegments.CollectionToString()}");
#endif

			int i = 0;
			foreach (TimedTrafficLightsStep step in Steps) {
				ICollection<ushort> invalidSegmentIds = new HashSet<ushort>();
				foreach (KeyValuePair<ushort, CustomSegmentLights> e in step.CustomSegmentLights) {
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
		private void HandleNewSegments(NodeGeometry nodeGeo) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;
#endif

			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			//Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
			foreach (SegmentEndGeometry end in nodeGeo.SegmentEndGeometries) {
				if (end == null) {
					continue;
				}

#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.HandleNewSegments: handling existing seg. {end.SegmentId} @ {NodeId}");
#endif

				if (Steps[0].CustomSegmentLights.ContainsKey(end.SegmentId)) {
					continue;
				}

				// segment was created
				RotationOffset = 0;
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.HandleNewSegments: New segment detected: {end.SegmentId} @ {NodeId}");
#endif

				int stepIndex = -1;
				foreach (TimedTrafficLightsStep step in Steps) {
					++stepIndex;

					LinkedListNode<CustomSegmentLights> lightsToReuseNode = step.InvalidSegmentLights.First;
					if (lightsToReuseNode == null) {
						// no old segment found: create a fresh custom light
#if DEBUGTTL
						if (debug)
							Log._Debug($"TimedTrafficLights.HandleNewSegments: Adding new segment {end.SegmentId} to node {NodeId} without reusing old segment");
#endif
						if (! step.AddSegment(end.SegmentId, end.StartNode, true)) {
#if DEBUGTTL
							if (debug)
								Log.Warning($"TimedTrafficLights.HandleNewSegments: Failed to add segment {end.SegmentId} @ start {end.StartNode} to node {NodeId}");
#endif
						}
					} else {
						// reuse old lights
						step.InvalidSegmentLights.RemoveFirst();
						CustomSegmentLights lightsToReuse = lightsToReuseNode.Value;

#if DEBUGTTL
						if (debug)
							Log._Debug($"Replacing old segment @ {NodeId} with new segment {end.SegmentId}");
#endif
						lightsToReuse.Relocate(end.SegmentId, end.StartNode);
						step.SetSegmentLights(end.SegmentId, lightsToReuse);
					}
				}
			}
		}

		internal TimedTrafficLights MasterLights() {
			TrafficLightSimulation masterSim = TrafficLightSimulationManager.Instance.GetNodeSimulation(masterNodeId);
			if (masterSim == null || !masterSim.IsTimedLight())
				return null;
			return masterSim.TimedLight;
		}

		internal void SetTestMode(bool testMode) {
			this.testMode = false;
			if (!IsStarted())
				return;
			this.testMode = testMode;
		}

		internal bool IsInTestMode() {
			if (!IsStarted())
				testMode = false;
			return testMode;
		}

		internal void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, CustomSegmentLight.Mode mode) {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;
#endif

			SegmentGeometry segGeo = SegmentGeometry.Get(segmentId);
			if (segGeo == null) {
#if DEBUGTTL
				if (debug)
					Log.Error($"TimedTrafficLights.ChangeLightMode: No geometry information available for segment {segmentId}");
#endif
				return;
			}

			foreach (TimedTrafficLightsStep step in Steps) {
				step.ChangeLightMode(segmentId, vehicleType, mode);
			}
			CustomSegmentLightsManager.Instance.SetLightMode(segmentId, segGeo.StartNodeId() == NodeId, vehicleType, mode);
		}

		internal void Join(TimedTrafficLights otherTimedLight) {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			if (NumSteps() < otherTimedLight.NumSteps()) {
				// increase the number of steps at our timed lights
				for (int i = NumSteps(); i < otherTimedLight.NumSteps(); ++i) {
					TimedTrafficLightsStep otherStep = otherTimedLight.GetStep(i);
					foreach (ushort slaveNodeId in NodeGroup) {
						TrafficLightSimulation ourSim = tlsMan.GetNodeSimulation(slaveNodeId);
						if (ourSim == null || !ourSim.IsTimedLight())
							continue;
						TimedTrafficLights ourTimedLight = ourSim.TimedLight;
						ourTimedLight.AddStep(otherStep.minTime, otherStep.maxTime, otherStep.waitFlowBalance, true);
					}
				}
			} else {
				// increase the number of steps at their timed lights
				for (int i = otherTimedLight.NumSteps(); i < NumSteps(); ++i) {
					TimedTrafficLightsStep ourStep = GetStep(i);
					foreach (ushort slaveNodeId in otherTimedLight.NodeGroup) {
						TrafficLightSimulation theirSim = tlsMan.GetNodeSimulation(slaveNodeId);
						if (theirSim == null || !theirSim.IsTimedLight())
							continue;
						TimedTrafficLights theirTimedLight = theirSim.TimedLight;
						theirTimedLight.AddStep(ourStep.minTime, ourStep.maxTime, ourStep.waitFlowBalance, true);
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

			foreach (ushort timedNodeId in newNodeGroup) {
				TrafficLightSimulation timedSim = tlsMan.GetNodeSimulation(timedNodeId);
				if (timedSim == null || !timedSim.IsTimedLight())
					continue;
				TimedTrafficLights timedLight = timedSim.TimedLight;
				for (int i = 0; i < NumSteps(); ++i) {
					minTimes[i] += timedLight.GetStep(i).minTime;
					maxTimes[i] += timedLight.GetStep(i).maxTime;
					waitFlowBalances[i] += timedLight.GetStep(i).waitFlowBalance;
				}

				timedLight.NodeGroup = newNodeGroup;
				timedLight.masterNodeId = newMasterNodeId;
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
				TrafficLightSimulation timedSim = tlsMan.GetNodeSimulation(timedNodeId);
				if (timedSim == null || !timedSim.IsTimedLight())
					continue;
				TimedTrafficLights timedLight = timedSim.TimedLight;
				timedLight.Stop();
				timedLight.testMode = false;
				for (int i = 0; i < NumSteps(); ++i) {
					timedLight.GetStep(i).minTime = minTimes[i];
					timedLight.GetStep(i).maxTime = maxTimes[i];
					timedLight.GetStep(i).waitFlowBalance = waitFlowBalances[i];
				}
			}
		}

		private void UpdateSegmentEnds() {
#if DEBUGTTL
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;

			if (debug)
				Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: called for node {NodeId}");
#endif

			ICollection<SegmentEndId> segmentEndsToDelete = new HashSet<SegmentEndId>();
			// update currently set segment ends
			foreach (SegmentEndId endId in segmentEndIds) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: updating existing segment end {endId} for node {NodeId}");
#endif
				if (! SegmentEndManager.Instance.UpdateSegmentEnd(endId)) {
#if DEBUGTTL
					if (debug)
						Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: segment end {endId} @ node {NodeId} is invalid");
#endif
					segmentEndsToDelete.Add(endId);
				} else {
					SegmentEnd end = SegmentEndManager.Instance.GetSegmentEnd(endId);
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
			foreach (SegmentEndId endId in segmentEndsToDelete) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Removing invalid segment end {endId} @ node {NodeId}");
#endif
				segmentEndIds.Remove(endId);
			}

			// set up new segment ends
			NodeGeometry nodeGeo = NodeGeometry.Get(NodeId);
#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Setting up new segment ends @ node {NodeId}. nodeGeo={nodeGeo}");
#endif

			foreach (SegmentEndGeometry endGeo in nodeGeo.SegmentEndGeometries) {
				if (endGeo == null) {
					continue;
				}

				if (segmentEndIds.Contains(endGeo)) {
#if DEBUGTTL
					if (debug)
						Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Node {NodeId} already knows segment {endGeo.SegmentId}");
#endif
					continue;
				}

#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.UpdateSegmentEnds: Adding segment {endGeo.SegmentId} to node {NodeId}");
#endif
				SegmentEnd end = SegmentEndManager.Instance.GetOrAddSegmentEnd(endGeo.SegmentId, endGeo.StartNode);
				if (end != null) {
					segmentEndIds.Add(end);
				} else {
#if DEBUGTTL
					if (debug)
						Log.Warning($"TimedTrafficLights.UpdateSegmentEnds: Failed to add segment end {endGeo.SegmentId} @ {endGeo.StartNode} to node {NodeId}: GetOrAddSegmentEnd returned null.");
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
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;

			if (debug)
				Log._Debug($"TimedTrafficLights.DestroySegmentEnds: Destroying segment ends @ node {NodeId}");
#endif
			foreach (SegmentEndId endId in segmentEndIds) {
#if DEBUGTTL
				if (debug)
					Log._Debug($"TimedTrafficLights.DestroySegmentEnds: Destroying segment end {endId} @ node {NodeId}");
#endif
				// TODO only remove if no priority sign is located at the segment end (although this is currently not possible)
				SegmentEndManager.Instance.RemoveSegmentEnd(endId);
			}
			segmentEndIds.Clear();
#if DEBUGTTL
			if (debug)
				Log._Debug($"TimedTrafficLights.DestroySegmentEnds: finished for node {NodeId}");
#endif
		}
	}
}

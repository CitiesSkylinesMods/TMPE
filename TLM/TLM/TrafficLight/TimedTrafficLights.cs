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

namespace TrafficManager.TrafficLight {
	// TODO [version 1.9] define TimedTrafficLights per node group, not per individual nodes
	// TODO class marked for complete rework in version 1.9
	public class TimedTrafficLights: IObserver<NodeGeometry> {
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

		private IDisposable nodeGeometryUnsubscriber = null;
		private object geoLock = new object();

		public Dictionary<ushort, Dictionary<ushort, ArrowDirection>> Directions { get; private set; } = null;

		public TimedTrafficLights(ushort nodeId, IEnumerable<ushort> nodeGroup) {
			this.NodeId = nodeId;
			NodeGroup = new List<ushort>(nodeGroup);
			masterNodeId = NodeGroup[0];

			UpdateDirections(NodeGeometry.Get(nodeId));
			SubscribeToNodeGeometry();
			SetupSegmentEnds();

			started = false;
		}

		private void UpdateDirections(NodeGeometry nodeGeo) {
			Directions = new Dictionary<ushort, Dictionary<ushort, ArrowDirection>>();
			foreach (SegmentEndGeometry srcSegEndGeo in nodeGeo.SegmentEndGeometries) {
				if (srcSegEndGeo == null)
					continue;

				SegmentGeometry srcSegGeo = srcSegEndGeo.GetSegmentGeometry();
				Dictionary<ushort, ArrowDirection> dirs = new Dictionary<ushort, ArrowDirection>();
				Directions.Add(srcSegEndGeo.SegmentId, dirs);
				foreach (SegmentEndGeometry trgSegEndGeo in nodeGeo.SegmentEndGeometries) {
					if (trgSegEndGeo == null)
						continue;

					dirs.Add(trgSegEndGeo.SegmentId, srcSegGeo.GetDirection(trgSegEndGeo.SegmentId, srcSegEndGeo.StartNode));
				}
			}
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
			UpdateDirections(geometry);
		}

		public bool IsMasterNode() {
			return masterNodeId == NodeId;
		}

		public TimedTrafficLightsStep AddStep(int minTime, int maxTime, float waitFlowBalance, bool makeRed = false) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

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
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			/*if (!housekeeping())
				return;*/
			
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
				if (end == null)
					continue;

				//Log._Debug($"Checking seg. {segmentId} @ {NodeId}.");
				bool needsAlwaysGreenPedestrian = true;
				int i = 0;
				foreach (TimedTrafficLightsStep step in Steps) {
					//Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}.");
					if (!step.segmentLights.ContainsKey(end.SegmentId)) {
						//Log._Debug($"Step {i} @ {NodeId} does not contain a segment light for seg. {segmentId}.");
						++i;
						continue;
					}
					//Log._Debug($"Checking step {i}, seg. {segmentId} @ {NodeId}: {step.segmentLights[segmentId].PedestrianLightState} (pedestrianLightState={step.segmentLights[segmentId].pedestrianLightState}, ManualPedestrianMode={step.segmentLights[segmentId].ManualPedestrianMode}, AutoPedestrianLightState={step.segmentLights[segmentId].AutoPedestrianLightState}");
					if (step.segmentLights[end.SegmentId].PedestrianLightState == RoadBaseAI.TrafficLightState.Green) {
						//Log._Debug($"Step {i} @ {NodeId} has a green ped. light @ seg. {segmentId}.");
						needsAlwaysGreenPedestrian = false;
						break;
					}
					++i;
				}
				//Log._Debug($"Setting InvalidPedestrianLight of seg. {segmentId} @ {NodeId} to {needsAlwaysGreenPedestrian}.");
				customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode).InvalidPedestrianLight = needsAlwaysGreenPedestrian;
			}
		}

		private void ClearInvalidPedestrianLights() {
			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;

			NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

			//Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;
				
				customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode).InvalidPedestrianLight = false;
			}
		}

		internal void RemoveNodeFromGroup(ushort otherNodeId) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			NodeGroup.Remove(otherNodeId);
			if (NodeGroup.Count <= 0) {
				TrafficLightSimulationManager.Instance.RemoveNodeFromSimulation(NodeId, true, false);
				return;
			}
			masterNodeId = NodeGroup[0];
		}

		internal bool housekeeping() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually
			//Log._Debug($"Housekeeping timed light @ {NodeId}");

			if (NodeGroup == null || NodeGroup.Count <= 0) {
				Stop();
				return false;
			}

			// check that live lights exist (TODO refactor?)
			SetupSegmentEnds();

			//Log.Warning($"Timed housekeeping: Setting master node to {NodeGroup[0]}");
			masterNodeId = NodeGroup[0];

			if (IsStarted())
				CheckInvalidPedestrianLights();

			int i = 0;
			foreach (TimedTrafficLightsStep step in Steps) {
				foreach (CustomSegmentLights lights in step.segmentLights.Values) {
					//Log._Debug($"----- Housekeeping timed light at step {i}, seg. {lights.SegmentId} @ {NodeId}");
					lights.housekeeping(true, true);
				}
				++i;
			}

			return true;
		}

		public void MoveStep(int oldPos, int newPos) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			var oldStep = Steps[oldPos];

			Steps.RemoveAt(oldPos);
			Steps.Insert(newPos, oldStep);
		}

		public void Stop() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			started = false;
			ClearInvalidPedestrianLights();
		}

		internal void Destroy() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			Stop();
			DestroySegmentEnds();
			Steps = null;
			NodeGroup = null;
			UnsubscribeFromNodeGeometry();
		}

		public bool IsStarted() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return started;
		}

		public int NumSteps() {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return Steps.Count;
		}

		public TimedTrafficLightsStep GetStep(int stepId) {
			// TODO [version 1.9] currently, this method must be called for each node in the node group individually

			return Steps[stepId];
		}

		public void SimulationStep() {
			// TODO [version 1.9] this method is currently called on each node, but should be called on the master node only

			if (!IsMasterNode() || !IsStarted()) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} isMasterNode={isMasterNode()} IsStarted={IsStarted()}");
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
			Log._Debug($"TTL SimStep: NodeId={NodeId} Setting lights (1)");
#endif
			SetLights();

			if (!Steps[CurrentStep].StepDone(true)) {
#if DEBUGTTL
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}) is not done.");
#endif
				return;
			}
			// step is done

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={NodeId} Setting lights (2)");
#endif

#if DEBUG
			bool debug = GlobalConfig.Instance.DebugSwitches[7] && GlobalConfig.Instance.TTLDebugNodeId == NodeId;
#endif

			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;
			if (Steps[CurrentStep].NextStepRefIndex < 0) {
#if DEBUG
				if (debug) {
					Log._Debug($"Step {CurrentStep} is done at timed light {NodeId}. Determining next step.");
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

#if DEBUG
					if (debug) {
						Log._Debug($"Next step {nextStepIndex} has minTime = 0 at timed light {NodeId}. Old step {CurrentStep} has waitFlowDiff={maxWaitFlowDiff} (flow={Steps[CurrentStep].minFlow}, wait={Steps[CurrentStep].maxWait}).");
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

#if DEBUG
						if (debug) {
							Log._Debug($"Checking upcoming step {nextStepIndex} @ node {NodeId}: flow={flow} wait={wait} minTime={Steps[nextStepIndex].minTime}. bestWaitFlowDiff={bestNextStepIndex}, bestNextStepIndex={bestNextStepIndex}");
						}
#endif

						if (Steps[nextStepIndex].minTime != 0) {
							break;
						}

						nextStepIndex = (nextStepIndex + 1) % NumSteps();
					}

					if (bestNextStepIndex == CurrentStep) {
#if DEBUG
						if (debug) {
							Log._Debug($"Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) equals CurrentStep @ node {NodeId}.");
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
#if DEBUG
						if (debug) {
							Log._Debug($"Best next step {bestNextStepIndex} (wait/flow diff = {maxWaitFlowDiff}) does not equal CurrentStep @ node {NodeId}.");
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
				Log._Debug($"TTL SimStep: *STOP* NodeId={NodeId} current step ({CurrentStep}): end transition is not done.");
#endif
				return;
			}
			// ending transition (yellow) finished

#if DEBUGTTL
			Log._Debug($"TTL SimStep: NodeId={NodeId} ending transition done. NodeGroup={string.Join(", ", NodeGroup.Select(x => x.ToString()).ToArray())}, nodeId={NodeId}, NumSteps={NumSteps()}");
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
			Log._Debug($"TTL SimStep: NodeId={slaveNodeId} setting lgihts of next step: {CurrentStep}");
#endif

				timedLights.Steps[oldStepIndex].NextStepRefIndex = -1;
				timedLights.Steps[newStepIndex].Start(oldStepIndex);
				timedLights.Steps[newStepIndex].UpdateLiveLights();
			}
		}

		public void SetLights() {
			TrafficLightSimulationManager tlsMan = TrafficLightSimulationManager.Instance;

			// set lights
			foreach (ushort slaveNodeId in NodeGroup) {
				TrafficLightSimulation slaveSim = tlsMan.GetNodeSimulation(slaveNodeId);
				if (slaveSim == null || !slaveSim.IsTimedLight()) {
					//TrafficLightSimulation.RemoveNodeFromSimulation(slaveNodeId, false); // we iterate over NodeGroup!!
					continue;
				}
				slaveSim.TimedLight.Steps[CurrentStep].UpdateLiveLights();
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

		internal void handleNewSegments() {
			Log._Debug($"TimedTrafficLights.handleNewSegments: called for timed traffic light @ {NodeId}");
			if (NumSteps() <= 0) {
				// no steps defined, just create live traffic lights
				/*for (int s = 0; s < 8; ++s) {
					ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
					if (segmentId <= 0)
						continue;
					if (! CustomTrafficLights.IsSegmentLight(NodeId, segmentId))
						CustomTrafficLights.AddSegmentLights(NodeId, segmentId);
				}*/

				Log._Debug($"TimedTrafficLights.handleNewSegments: no steps @ {NodeId}");
				return;
			}

			CustomSegmentLightsManager customTrafficLightsManager = CustomSegmentLightsManager.Instance;
			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			NodeGeometry nodeGeometry = NodeGeometry.Get(NodeId);

			//Log._Debug($"Checking for invalid pedestrian lights @ {NodeId}.");
			foreach (SegmentEndGeometry end in nodeGeometry.SegmentEndGeometries) {
				if (end == null)
					continue;
				Log._Debug($"TimedTrafficLights.handleNewSegments: handling existing seg. {end.SegmentId} @ {NodeId}");

				List<ushort> invalidSegmentIds = new List<ushort>();
				bool isNewSegment = !Steps[0].segmentLights.ContainsKey(end.SegmentId);

				if (isNewSegment) {
					// segment was created
					Log._Debug($"TimedTrafficLights.handleNewSegments: New segment detected: {end.SegmentId} @ {NodeId}");

					foreach (KeyValuePair<ushort, CustomSegmentLights> e in Steps[0].segmentLights) {
						var fromSegmentId = e.Key;

						if (!NetUtil.IsSegmentValid(fromSegmentId)) {
							Log._Debug($"Identified old segment {fromSegmentId} @ {NodeId}");
							invalidSegmentIds.Add(fromSegmentId);
						}
					}

					Log._Debug($"Setting up segment end for new segment {end.SegmentId} @ {NodeId}");
					SetupSegmentEnd(end.SegmentId);

					if (invalidSegmentIds.Count > 0) {
						var oldSegmentId = invalidSegmentIds[0];
						prioMan.RemovePrioritySegment(NodeId, oldSegmentId);
						Log._Debug($"Replacing old segment {oldSegmentId} @ {NodeId} with new segment {end.SegmentId}");

						// replace the old segment with the newly created one
						for (int i = 0; i < NumSteps(); ++i) {
							if (!Steps[i].segmentLights.ContainsKey(oldSegmentId)) {
								Log.Error($"Step {i} at node {NodeId} does not contain step lights for old segment {oldSegmentId}");
								Steps[i].addSegment(end.SegmentId, end.StartNode, true);
								Steps[i].calcMaxSegmentLength();
								continue;
							}

							CustomSegmentLights customLights = Steps[i].segmentLights[oldSegmentId];
							Log._Debug($"Removing old segment {oldSegmentId} @ {NodeId} from step {i}");
							Steps[i].segmentLights.Remove(oldSegmentId);
							Log._Debug($"Setting new segment id {end.SegmentId} at custom light from step {i}");
							customLights.Relocate(end.SegmentId, end.StartNode);
							Steps[i].segmentLights.Add(end.SegmentId, customLights);
							Steps[i].calcMaxSegmentLength();
							Log._Debug($"Getting live segment lights of new segment {end.SegmentId} @ {NodeId} and applying mode @ step {i}");
							CustomSegmentLights liveSegLights = customTrafficLightsManager.GetSegmentLights(end.SegmentId, end.StartNode);

							foreach (KeyValuePair<ExtVehicleType, CustomSegmentLight> e in customLights.CustomLights) {
								CustomSegmentLight liveSegLight = liveSegLights.GetCustomLight(e.Key);
								if (liveSegLight == null)
									continue;
								Log._Debug($"Updating live segment light mode of new segment {end.SegmentId} @ {NodeId} for vehicle type {e.Key} @ step {i}");
								liveSegLight.CurrentMode = e.Value.CurrentMode;
							}
							Log._Debug($"Finished applying new segment {end.SegmentId} @ {NodeId} @ step {i}");
						}
					} else {
						Log._Debug($"Adding new segment {end.SegmentId} to node {NodeId}");

						// create a new manual light
						for (int i = 0; i < NumSteps(); ++i) {
							Steps[i].addSegment(end.SegmentId, end.StartNode, true);
							Steps[i].calcMaxSegmentLength();
						}
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
			foreach (TimedTrafficLightsStep step in Steps) {
				step.ChangeLightMode(segmentId, vehicleType, mode);
			}
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

		private void SetupSegmentEnds() {
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId == 0)
					continue;
				SetupSegmentEnd(segmentId);
			}
		}

		private void DestroySegmentEnds() {
			for (int s = 0; s < 8; ++s) {
				ushort segmentId = Singleton<NetManager>.instance.m_nodes.m_buffer[NodeId].GetSegment(s);
				if (segmentId == 0)
					continue;
				DestroySegmentEnd(segmentId);
			}
		}

		private void SetupSegmentEnd(ushort segmentId) {
			if (segmentId <= 0)
				return;

			TrafficPriorityManager prioMan = TrafficPriorityManager.Instance;

			//if (!prioMan.IsPrioritySegment(NodeId, segmentId))
				prioMan.AddPrioritySegment(NodeId, segmentId, SegmentEnd.PriorityType.None);
		}

		private void DestroySegmentEnd(ushort segmentId) {
			if (segmentId <= 0)
				return;
			TrafficPriorityManager.Instance.RemovePrioritySegment(NodeId, segmentId);
		}
	}
}
